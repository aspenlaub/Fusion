using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Entities;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Entities;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Nuclide.Entities;
using Aspenlaub.Net.GitHub.CSharp.Nuclide.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using Microsoft.Extensions.Logging;
using NuGet.Protocol.Core.Types;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion {
    public class NugetPackageUpdater : INugetPackageUpdater {
        private readonly IGitUtilities vGitUtilities;
        private readonly IProcessRunner vProcessRunner;
        private readonly INugetFeedLister vNugetFeedLister;
        private readonly ISecretRepository vSecretRepository;
        private readonly IPackageConfigsScanner vPackageConfigsScanner;
        private readonly ISimpleLogger vSimpleLogger;

        private readonly IList<string> vEndingsThatAllowReset = new List<string> { "csproj", "config" };

        public NugetPackageUpdater(IGitUtilities gitUtilities, IProcessRunner processRunner, INugetFeedLister nugetFeedLister, ISecretRepository secretRepository, IPackageConfigsScanner packageConfigsScanner, ISimpleLogger simpleLogger) {
            vGitUtilities = gitUtilities;
            vProcessRunner = processRunner;
            vNugetFeedLister = nugetFeedLister;
            vSecretRepository = secretRepository;
            vPackageConfigsScanner = packageConfigsScanner;
            vSimpleLogger = simpleLogger;
        }

        public async Task<IYesNoInconclusive> UpdateNugetPackagesInRepositoryAsync(IFolder repositoryFolder, IErrorsAndInfos errorsAndInfos) {
            using (vSimpleLogger.BeginScope(SimpleLoggingScopeId.Create(nameof(UpdateNugetPackagesInRepositoryAsync), Guid.NewGuid().ToString()))) {
                vSimpleLogger.LogInformation("Determining files with uncommitted changes");
                var yesNoInconclusive = new YesNoInconclusive();
                var files = vGitUtilities.FilesWithUncommittedChanges(repositoryFolder);
                yesNoInconclusive.Inconclusive = files.Any(f => vEndingsThatAllowReset.All(e => !f.EndsWith("." + e, StringComparison.InvariantCultureIgnoreCase)));
                yesNoInconclusive.YesNo = false;
                if (yesNoInconclusive.Inconclusive) {
                    vSimpleLogger.LogInformation($"Returning {yesNoInconclusive}");
                    return yesNoInconclusive;
                }

                vSimpleLogger.LogInformation("Resetting repository");
                vGitUtilities.Reset(repositoryFolder, vGitUtilities.HeadTipIdSha(repositoryFolder), errorsAndInfos);
                if (errorsAndInfos.AnyErrors()) {
                    vSimpleLogger.LogInformation($"Returning {yesNoInconclusive}");
                    return yesNoInconclusive;
                }

                vSimpleLogger.LogInformation("Searching for project files");
                var projectFileFullNames = Directory.GetFiles(repositoryFolder.SubFolder("src").FullName, "*.csproj", SearchOption.AllDirectories).ToList();
                if (!projectFileFullNames.Any()) {
                    vSimpleLogger.LogInformation($"Returning {yesNoInconclusive}");
                    return yesNoInconclusive;
                }

                foreach (var projectFileFullName in projectFileFullNames) {
                    vSimpleLogger.LogInformation($"Analyzing project file {projectFileFullName}");
                    var projectErrorsAndInfos = new ErrorsAndInfos();
                    if (!await UpdateNugetPackagesForProjectAsync(projectFileFullName, yesNoInconclusive.YesNo, projectErrorsAndInfos)) {
                        continue;
                    }

                    yesNoInconclusive.YesNo = true;
                }

                if (yesNoInconclusive.YesNo) {
                    vSimpleLogger.LogInformation($"Returning {yesNoInconclusive}");
                    return yesNoInconclusive;
                }

                vSimpleLogger.LogInformation("Resetting repository");
                vGitUtilities.Reset(repositoryFolder, vGitUtilities.HeadTipIdSha(repositoryFolder), errorsAndInfos);
                vSimpleLogger.LogInformation($"Returning {yesNoInconclusive}");
                return yesNoInconclusive;
            }
        }

        public async Task<IYesNoInconclusive> UpdateNugetPackagesInSolutionAsync(IFolder solutionFolder, IErrorsAndInfos errorsAndInfos) {
            using (vSimpleLogger.BeginScope(SimpleLoggingScopeId.Create(nameof(UpdateNugetPackagesInSolutionAsync), Guid.NewGuid().ToString()))) {
                vSimpleLogger.LogInformation("Searching for project files");
                var yesNoInconclusive = new YesNoInconclusive();
                var projectFileFullNames = Directory.GetFiles(solutionFolder.FullName, "*.csproj", SearchOption.AllDirectories).ToList();
                if (!projectFileFullNames.Any()) {
                    vSimpleLogger.LogInformation($"Returning {yesNoInconclusive}");
                    return yesNoInconclusive;
                }

                foreach (var projectFileFullName in projectFileFullNames) {
                    vSimpleLogger.LogInformation($"Analyzing project file {projectFileFullName}");
                    yesNoInconclusive.YesNo = await UpdateNugetPackagesForProjectAsync(projectFileFullName, yesNoInconclusive.YesNo, errorsAndInfos);
                }

                vSimpleLogger.LogInformation($"Returning {yesNoInconclusive}");
                return yesNoInconclusive;
            }
        }

        private async Task<bool> UpdateNugetPackagesForProjectAsync(string projectFileFullName, bool yesNo, IErrorsAndInfos errorsAndInfos) {
            using (vSimpleLogger.BeginScope(SimpleLoggingScopeId.Create(nameof(UpdateNugetPackagesForProjectAsync), Guid.NewGuid().ToString()))) {
                vSimpleLogger.LogInformation("Retrieving dependency ids and versions");
                var dependencyErrorsAndInfos = new ErrorsAndInfos();
                var dependencyIdsAndVersions =
                    await vPackageConfigsScanner.DependencyIdsAndVersionsAsync(projectFileFullName.Substring(0, projectFileFullName.LastIndexOf('\\')), true, true, dependencyErrorsAndInfos);

                vSimpleLogger.LogInformation("Retrieving manually updated packages");
                var secret = new SecretManuallyUpdatedPackages();
                var manuallyUpdatedPackages = await vSecretRepository.GetAsync(secret, errorsAndInfos);
                if (errorsAndInfos.AnyErrors()) {
                    vSimpleLogger.LogInformation($"Returning false");
                    return false;
                }

                foreach (var id in dependencyIdsAndVersions.Select(dependencyIdsAndVersion => dependencyIdsAndVersion.Key).Where(id => manuallyUpdatedPackages.All(p => p.Id != id))) {
                    vSimpleLogger.LogInformation($"Updating dependency {id}");
                    var projectFileFolder = projectFileFullName.Substring(0, projectFileFullName.LastIndexOf('\\'));
                    vProcessRunner.RunProcess("dotnet", "remove " + projectFileFullName + " package " + id, projectFileFolder, errorsAndInfos);
                    vProcessRunner.RunProcess("dotnet", "add " + projectFileFullName + " package " + id, projectFileFolder, errorsAndInfos);
                }

                vSimpleLogger.LogInformation("Retrieving dependency ids and versions once more");
                var dependencyIdsAndVersionsAfterUpdate =
                    await vPackageConfigsScanner.DependencyIdsAndVersionsAsync(projectFileFullName.Substring(0, projectFileFullName.LastIndexOf('\\')), true, true, dependencyErrorsAndInfos);

                vSimpleLogger.LogInformation("Determining differences");
                foreach (var dependencyIdsAndVersion in dependencyIdsAndVersionsAfterUpdate) {
                    var id = dependencyIdsAndVersion.Key;
                    var version = dependencyIdsAndVersion.Value;
                    yesNo = yesNo || !dependencyIdsAndVersions.ContainsKey(id) || version != dependencyIdsAndVersions[id];
                }

                vSimpleLogger.LogInformation($"Returning {yesNo}");
                return yesNo;
            }
        }

        public async Task<bool> AreThereNugetUpdateOpportunitiesAsync(IFolder repositoryFolder, IErrorsAndInfos errorsAndInfos) {
            return await AreThereNugetUpdateOpportunitiesForSolutionAsync(repositoryFolder.SubFolder("src"), errorsAndInfos);
        }

        public async Task<bool> AreThereNugetUpdateOpportunitiesForSolutionAsync(IFolder solutionFolder, IErrorsAndInfos errorsAndInfos) {
            var projectFileFullNames = Directory.GetFiles(solutionFolder.FullName, "*.csproj", SearchOption.AllDirectories).ToList();
            if (!projectFileFullNames.Any()) {
                return false;
            }

            var developerSettingsSecret = new DeveloperSettingsSecret();
            var developerSettings = await vSecretRepository.GetAsync(developerSettingsSecret, errorsAndInfos);
            if (errorsAndInfos.Errors.Any()) { return false; }

            var feedUrls = new[] { developerSettings.NugetFeedUrl, "https://packages.nuget.org/api/v2" };
            foreach (var projectFileFullName in projectFileFullNames) {
                if (await AreThereNugetUpdateOpportunitiesForProjectAsync(projectFileFullName, feedUrls, errorsAndInfos)) {
                    return !errorsAndInfos.AnyErrors();
                }
            }

            return false;
        }

        private async Task<bool> AreThereNugetUpdateOpportunitiesForProjectAsync(string projectFileFullName, IList<string> feedUrls, IErrorsAndInfos errorsAndInfos) {
            var dependencyErrorsAndInfos = new ErrorsAndInfos();
            var dependencyIdsAndVersions = await vPackageConfigsScanner.DependencyIdsAndVersionsAsync(projectFileFullName.Substring(0, projectFileFullName.LastIndexOf('\\')), true, true, dependencyErrorsAndInfos);

            var secret = new SecretManuallyUpdatedPackages();
            var manuallyUpdatedPackages = await vSecretRepository.GetAsync(secret, errorsAndInfos);
            if (errorsAndInfos.AnyErrors()) { return false; }

            var yesNo = false;
            foreach (var dependencyIdsAndVersion in dependencyIdsAndVersions) {
                var id = dependencyIdsAndVersion.Key;
                if (manuallyUpdatedPackages.Any(p => p.Id == id)) { continue; }

                IList<IPackageSearchMetadata> remotePackages = null;
                foreach (var feedUrl in feedUrls) {
                    remotePackages = await vNugetFeedLister.ListReleasedPackagesAsync(feedUrl, id);
                    if (remotePackages.Any()) {
                        break;
                    }
                }

                if (remotePackages?.Any() != true) {
                    continue;
                }

                var version = dependencyIdsAndVersion.Value;
                var latestRemotePackageVersion = remotePackages.Max(p => p.Identity.Version.Version);
                if (latestRemotePackageVersion.ToString().StartsWith(version)) {
                    continue;
                }

                errorsAndInfos.Infos.Add(string.Format(Properties.Resources.CanUpdatePackageFromTo, id, version, latestRemotePackageVersion));
                yesNo = true;
            }

            return yesNo;
        }
    }
}
