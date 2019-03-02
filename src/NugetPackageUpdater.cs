using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Entities;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Nuclide.Entities;
using Aspenlaub.Net.GitHub.CSharp.Nuclide.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using NuGet.Protocol.Core.Types;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion {
    public class NugetPackageUpdater : INugetPackageUpdater {
        private readonly IGitUtilities vGitUtilities;
        private readonly IProcessRunner vProcessRunner;
        private readonly INugetFeedLister vNugetFeedLister;
        private readonly ISecretRepository vSecretRepository;

        private readonly IList<string> vEndingsThatAllowReset = new List<string> { "csproj", "config" };

        public NugetPackageUpdater(IGitUtilities gitUtilities, IProcessRunner processRunner, INugetFeedLister nugetFeedLister, ISecretRepository secretRepository) {
            vGitUtilities = gitUtilities;
            vProcessRunner = processRunner;
            vNugetFeedLister = nugetFeedLister;
            vSecretRepository = secretRepository;
        }

        public async Task<IYesNoInconclusive> UpdateNugetPackagesInRepositoryAsync(IFolder repositoryFolder, IErrorsAndInfos errorsAndInfos) {
            var yesNoInconclusive = new YesNoInconclusive();
            var files = vGitUtilities.FilesWithUncommittedChanges(repositoryFolder);
            yesNoInconclusive.Inconclusive = files.Any(f => vEndingsThatAllowReset.All(e => !f.EndsWith("." + e, StringComparison.InvariantCultureIgnoreCase)));
            yesNoInconclusive.YesNo = false;
            if (yesNoInconclusive.Inconclusive) { return yesNoInconclusive; }

            vGitUtilities.Reset(repositoryFolder, vGitUtilities.HeadTipIdSha(repositoryFolder), errorsAndInfos);
            if (errorsAndInfos.AnyErrors()) { return yesNoInconclusive; }

            var projectFileFullNames = Directory.GetFiles(repositoryFolder.SubFolder("src").FullName, "*.csproj", SearchOption.AllDirectories).ToList();
            if (!projectFileFullNames.Any()) {
                return yesNoInconclusive;
            }

            foreach (var projectFileFullName in projectFileFullNames) {
                yesNoInconclusive.YesNo = await UpdateNugetPackagesForProjectAsync(projectFileFullName, yesNoInconclusive.YesNo, errorsAndInfos);
            }

            if (yesNoInconclusive.YesNo) { return yesNoInconclusive; }

            vGitUtilities.Reset(repositoryFolder, vGitUtilities.HeadTipIdSha(repositoryFolder), errorsAndInfos);
            return yesNoInconclusive;
        }

        public async Task<IYesNoInconclusive> UpdateNugetPackagesInSolutionAsync(IFolder solutionFolder, IErrorsAndInfos errorsAndInfos) {
            var yesNoInconclusive = new YesNoInconclusive();
            var projectFileFullNames = Directory.GetFiles(solutionFolder.FullName, "*.csproj", SearchOption.AllDirectories).ToList();
            if (!projectFileFullNames.Any()) {
                return yesNoInconclusive;
            }

            foreach (var projectFileFullName in projectFileFullNames) {
                yesNoInconclusive.YesNo = await UpdateNugetPackagesForProjectAsync(projectFileFullName, yesNoInconclusive.YesNo, errorsAndInfos);
            }

            return yesNoInconclusive;
        }

        private async Task<bool> UpdateNugetPackagesForProjectAsync(string projectFileFullName, bool yesNo, IErrorsAndInfos errorsAndInfos) {
            var namespaceManager = new XmlNamespaceManager(new NameTable());
            XDocument document;
            try {
                document = XDocument.Load(projectFileFullName);
            } catch {
                errorsAndInfos.Errors.Add(string.Format(Properties.Resources.CouldNotLoadProject, projectFileFullName));
                return yesNo;
            }

            var secret = new SecretManuallyUpdatedPackages();
            var manuallyUpdatedPackages = await vSecretRepository.GetAsync(secret, errorsAndInfos);
            if (errorsAndInfos.AnyErrors()) { return false; }

            var packageToVersion = new Dictionary<string, string>();
            foreach (var element in document.XPathSelectElements("/Project/ItemGroup/PackageReference", namespaceManager)) {
                var id = element.Attribute("Include")?.Value;
                if (string.IsNullOrEmpty(id)) { continue; }

                if (manuallyUpdatedPackages.Any(p => p.Id == id)) { continue; }

                var version = element.Attribute("Version")?.Value;
                if (string.IsNullOrEmpty(version)) { continue; }

                packageToVersion[id] = version;
                var projectFileFolder = projectFileFullName.Substring(0, projectFileFullName.LastIndexOf('\\'));
                vProcessRunner.RunProcess("dotnet", "add " + projectFileFullName + " package " + id, projectFileFolder, errorsAndInfos);
            }

            try {
                document = XDocument.Load(projectFileFullName);
            } catch {
                errorsAndInfos.Errors.Add(string.Format(Properties.Resources.CouldNotLoadProject, projectFileFullName));
                return yesNo;
            }

            foreach (var element in document.XPathSelectElements("/Project/ItemGroup/PackageReference", namespaceManager)) {
                var id = element.Attribute("Include")?.Value;
                if (string.IsNullOrEmpty(id)) { continue; }

                var version = element.Attribute("Version")?.Value;
                if (string.IsNullOrEmpty(version)) { continue; }

                yesNo = yesNo || !packageToVersion.ContainsKey(id) || version != packageToVersion[id];
            }

            return yesNo;
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
            var namespaceManager = new XmlNamespaceManager(new NameTable());
            XDocument document;
            try {
                document = XDocument.Load(projectFileFullName);
            } catch {
                errorsAndInfos.Errors.Add(string.Format(Properties.Resources.CouldNotLoadProject, projectFileFullName));
                return false;
            }

            var secret = new SecretManuallyUpdatedPackages();
            var manuallyUpdatedPackages = await vSecretRepository.GetAsync(secret, errorsAndInfos);
            if (errorsAndInfos.AnyErrors()) { return false; }

            var yesNo = false;
            foreach (var element in document.XPathSelectElements("/Project/ItemGroup/PackageReference", namespaceManager)) {
                var id = element.Attribute("Include")?.Value;
                if (string.IsNullOrEmpty(id)) {
                    continue;
                }

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

                var version = element.Attribute("Version")?.Value;
                if (string.IsNullOrEmpty(version)) {
                    continue;
                }

                var latestRemotePackageVersion = remotePackages.Max(p => p.Identity.Version.Version);
                if (!latestRemotePackageVersion.ToString().StartsWith(version)) {
                    errorsAndInfos.Infos.Add(string.Format(Properties.Resources.CanUpdatePackageFromTo, id, version, latestRemotePackageVersion));
                    yesNo = true;
                }
            }

            return yesNo;
        }
    }
}
