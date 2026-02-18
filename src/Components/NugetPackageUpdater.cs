using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Entities;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Nuclide.Entities;
using Aspenlaub.Net.GitHub.CSharp.Nuclide.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Skladasu.Entities;
using Aspenlaub.Net.GitHub.CSharp.Skladasu.Interfaces;
using NuGet.Packaging;
using NuGet.Protocol.Core.Types;
using Version = System.Version;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Components;

public class NugetPackageUpdater(IGitUtilities gitUtilities, IProcessRunner processRunner,
        INugetFeedLister nugetFeedLister, ISecretRepository secretRepository,
        IPackageReferencesScanner packageReferencesScanner,
        ISimpleLogger simpleLogger,
        IMethodNamesFromStackFramesExtractor methodNamesFromStackFramesExtractor,
        IDotNetEfRunner dotNetEfRunner)
            : INugetPackageUpdater {

    private const string _microsoftEntityFrameworkPrefix = "Microsoft.EntityFramework";

    private readonly IList<string> _EndingsThatAllowReset = new List<string> { "csproj", "config" };

    public async Task<YesNoInconclusive> UpdateNugetPackagesInRepositoryAsync(IFolder repositoryFolder,
                                                                              string checkedOutBranch, IErrorsAndInfos errorsAndInfos) {
        return await UpdateNugetPackagesInRepositoryAsync(repositoryFolder, false, "", checkedOutBranch, errorsAndInfos);
    }

    public async Task<YesNoInconclusive> UpdateEntityFrameworkNugetPackagesInRepositoryAsync(IFolder repositoryFolder,
            string migrationId, string checkedOutBranch,
            IErrorsAndInfos errorsAndInfos) {
        return await UpdateNugetPackagesInRepositoryAsync(repositoryFolder, true, migrationId, checkedOutBranch, errorsAndInfos);
    }

    protected async Task<YesNoInconclusive> UpdateNugetPackagesInRepositoryAsync(IFolder repositoryFolder,
            bool entityFrameworkOnly, string migrationId, string checkedOutBranch,
            IErrorsAndInfos errorsAndInfos) {
        using (simpleLogger.BeginScope(SimpleLoggingScopeId.Create(nameof(UpdateNugetPackagesInRepositoryAsync)))) {
            IList<string> methodNamesFromStack = methodNamesFromStackFramesExtractor.ExtractMethodNamesFromStackFrames();
            simpleLogger.LogInformationWithCallStack("Determining files with uncommitted changes", methodNamesFromStack);
            var yesNoInconclusive = new YesNoInconclusive();
            IList<string> files = gitUtilities.FilesWithUncommittedChanges(repositoryFolder);
            yesNoInconclusive.Inconclusive = files.Any(f => _EndingsThatAllowReset.All(e => !f.EndsWith("." + e, StringComparison.InvariantCultureIgnoreCase)));
            yesNoInconclusive.YesNo = false;
            if (yesNoInconclusive.Inconclusive) {
                errorsAndInfos.Infos.Add("Not all files allow a reset");
                simpleLogger.LogInformationWithCallStack($"Returning {yesNoInconclusive}", methodNamesFromStack);
                return yesNoInconclusive;
            }
            var nugetFeedsSecret = new SecretNugetFeeds();
            NugetFeeds nugetFeeds = await secretRepository.GetAsync(nugetFeedsSecret, errorsAndInfos);
            if (errorsAndInfos.Errors.Any()) {
                simpleLogger.LogInformationWithCallStack($"Returning {yesNoInconclusive}", methodNamesFromStack);
                return yesNoInconclusive;
            }

            var nugetFeedIds = nugetFeeds.Select(f => f.Id).ToList();

            simpleLogger.LogInformationWithCallStack("Resetting repository", methodNamesFromStack);
            gitUtilities.Reset(repositoryFolder, gitUtilities.HeadTipIdSha(repositoryFolder), errorsAndInfos);
            if (errorsAndInfos.AnyErrors()) {
                errorsAndInfos.Infos.Add("Could not reset");
                simpleLogger.LogInformationWithCallStack($"Returning {yesNoInconclusive}", methodNamesFromStack);
                return yesNoInconclusive;
            }

            simpleLogger.LogInformationWithCallStack("Searching for project files", methodNamesFromStack);
            var projectFileFullNames = Directory.GetFiles(repositoryFolder.SubFolder("src").FullName, "*.csproj", SearchOption.AllDirectories).ToList();
            if (!projectFileFullNames.Any()) {
                errorsAndInfos.Infos.Add("No project files found");
                simpleLogger.LogInformationWithCallStack($"Returning {yesNoInconclusive}", methodNamesFromStack);
                return yesNoInconclusive;
            }

            foreach (string projectFileFullName in projectFileFullNames) {
                simpleLogger.LogInformationWithCallStack($"Analyzing project file {projectFileFullName}", methodNamesFromStack);
                var projectErrorsAndInfos = new ErrorsAndInfos();
                var projectReducedErrorsAndInfos = new ErrorsAndInfos();
                if (!await UpdateNugetPackagesForProjectAsync(projectFileFullName, nugetFeedIds,
                        yesNoInconclusive.YesNo, entityFrameworkOnly,
                        migrationId, checkedOutBranch, projectErrorsAndInfos, projectReducedErrorsAndInfos)) {
                    continue;
                }

                errorsAndInfos.Infos.AddRange(projectReducedErrorsAndInfos.Infos);
                yesNoInconclusive.YesNo = true;
            }

            if (yesNoInconclusive.YesNo) {
                errorsAndInfos.Infos.Add("One or more project/-s was/were updated");
                simpleLogger.LogInformationWithCallStack($"Returning {yesNoInconclusive}", methodNamesFromStack);
                return yesNoInconclusive;
            }

            errorsAndInfos.Infos.Add("No project was updated");
            simpleLogger.LogInformationWithCallStack("Resetting repository", methodNamesFromStack);
            gitUtilities.Reset(repositoryFolder, gitUtilities.HeadTipIdSha(repositoryFolder), errorsAndInfos);
            simpleLogger.LogInformationWithCallStack($"Returning {yesNoInconclusive}", methodNamesFromStack);
            return yesNoInconclusive;
        }
    }

    private async Task<bool> UpdateNugetPackagesForProjectAsync(string projectFileFullName,
            IList<string> nugetFeedIds, bool yesNo, bool entityFrameworkOnly, string migrationId,
            string checkedOutBranch, IErrorsAndInfos projectErrorsAndInfos, IErrorsAndInfos projectReducedErrorsAndInfos) {
        using (simpleLogger.BeginScope(SimpleLoggingScopeId.Create(nameof(UpdateNugetPackagesForProjectAsync)))) {
            IList<string> methodNamesFromStack = methodNamesFromStackFramesExtractor.ExtractMethodNamesFromStackFrames();
            var projectFileFolder = new Folder(projectFileFullName.Substring(0, projectFileFullName.LastIndexOf('\\')));

            LogProjectPackageUpdateMessage("Checking if project has at least one migration",
                projectErrorsAndInfos, projectReducedErrorsAndInfos, methodNamesFromStack);
            bool projectHasMigrations = entityFrameworkOnly
                                        && dotNetEfRunner.ListAppliedMigrationIds(projectFileFolder, projectErrorsAndInfos)
                                                         .Any();

            if (entityFrameworkOnly && projectHasMigrations) {
                LogProjectPackageUpdateMessage( "Updating database",
                    projectErrorsAndInfos, projectReducedErrorsAndInfos, methodNamesFromStack);
                dotNetEfRunner.UpdateDatabase(projectFileFolder, projectErrorsAndInfos);
                if (projectErrorsAndInfos.AnyErrors()) {
                    simpleLogger.LogInformationWithCallStack("Returning false", methodNamesFromStack);
                    return false;
                }
            }

            LogProjectPackageUpdateMessage( "Retrieving dependency ids and versions",
                    projectErrorsAndInfos, projectReducedErrorsAndInfos, methodNamesFromStack);
            var dependencyErrorsAndInfos = new ErrorsAndInfos();
            IDictionary<string, string> dependencyIdsAndVersions =
                await packageReferencesScanner.DependencyIdsAndVersionsAsync(projectFileFullName.Substring(0, projectFileFullName.LastIndexOf('\\')), true, true, dependencyErrorsAndInfos);

            LogProjectPackageUpdateMessage( "Retrieving manually updated packages",
                    projectErrorsAndInfos, projectReducedErrorsAndInfos, methodNamesFromStack);
            var secret = new SecretManuallyUpdatedPackages();
            ManuallyUpdatedPackages manuallyUpdatedPackages = await secretRepository.GetAsync(secret, projectErrorsAndInfos);
            if (projectErrorsAndInfos.AnyErrors()) {
                simpleLogger.LogInformationWithCallStack("Returning false", methodNamesFromStack);
                return false;
            }

            var ids = dependencyIdsAndVersions
                .Select(dependencyIdsAndVersion => dependencyIdsAndVersion.Key)
                .Where(id => entityFrameworkOnly
                       ? id.StartsWith(_microsoftEntityFrameworkPrefix)
                       : !id.StartsWith(_microsoftEntityFrameworkPrefix)
                        && manuallyUpdatedPackages.All(p => !p.Matches(id, checkedOutBranch, projectFileFullName)))
                .ToList();
            foreach (string id in ids) {
                LogProjectPackageUpdateMessage( $"Updating dependency {id}",
                        projectErrorsAndInfos, projectReducedErrorsAndInfos, methodNamesFromStack);
                KeyValuePair<string, string> dependencyIdAndVersion = dependencyIdsAndVersions.First(d => d.Key == id);
                var version = Version.Parse(dependencyIdAndVersion.Value);
                Version latestRemotePackageVersion = await IdentifyLatestRemotePackageVersion(nugetFeedIds, entityFrameworkOnly, id, version);
                if (latestRemotePackageVersion == null) {
                    continue;
                }

                string arguments = "remove " + projectFileFullName + " package " + id;
                processRunner.RunProcess("dotnet", arguments, projectFileFolder, projectErrorsAndInfos);
                projectErrorsAndInfos.Infos.Add(arguments);
                projectReducedErrorsAndInfos.Infos.Add(arguments);
                if (entityFrameworkOnly) {
                    arguments = "add " + projectFileFullName + " package " + id + " -v " + latestRemotePackageVersion;
                } else {
                    arguments = "add " + projectFileFullName + " package " + id;
                }
                processRunner.RunProcess("dotnet", arguments, projectFileFolder, projectErrorsAndInfos);
                projectErrorsAndInfos.Infos.Add(arguments);
                projectReducedErrorsAndInfos.Infos.Add(arguments);
            }

            if (entityFrameworkOnly && projectHasMigrations) {
                LogProjectPackageUpdateMessage( "Adding migration",
                        projectErrorsAndInfos, projectReducedErrorsAndInfos, methodNamesFromStack);
                if (string.IsNullOrEmpty(migrationId)) {
                    migrationId = ids[0].Replace(".", "");
                }
                dotNetEfRunner.AddMigration(projectFileFolder, migrationId, projectErrorsAndInfos);
                if (projectErrorsAndInfos.AnyErrors()) {
                    simpleLogger.LogInformationWithCallStack("Returning false", methodNamesFromStack);
                    return false;
                }

                LogProjectPackageUpdateMessage( "Updating database",
                        projectErrorsAndInfos, projectReducedErrorsAndInfos, methodNamesFromStack);
                dotNetEfRunner.UpdateDatabase(projectFileFolder, projectErrorsAndInfos);
                if (projectErrorsAndInfos.AnyErrors()) {
                    simpleLogger.LogInformationWithCallStack("Returning false", methodNamesFromStack);
                    return false;
                }
            }

            LogProjectPackageUpdateMessage( "Retrieving dependency ids and versions once more",
                    projectErrorsAndInfos, projectReducedErrorsAndInfos, methodNamesFromStack);
            IDictionary<string, string> dependencyIdsAndVersionsAfterUpdate =
                await packageReferencesScanner.DependencyIdsAndVersionsAsync(projectFileFullName.Substring(0, projectFileFullName.LastIndexOf('\\')), true, true, dependencyErrorsAndInfos);

            LogProjectPackageUpdateMessage( "Determining differences",
                    projectErrorsAndInfos, projectReducedErrorsAndInfos, methodNamesFromStack);
            foreach (KeyValuePair<string, string> dependencyIdsAndVersion in dependencyIdsAndVersionsAfterUpdate) {
                string id = dependencyIdsAndVersion.Key;
                string version = dependencyIdsAndVersion.Value;
                yesNo = yesNo || !dependencyIdsAndVersions.ContainsKey(id) || version != dependencyIdsAndVersions[id];
            }

            simpleLogger.LogInformationWithCallStack($"Returning {yesNo}", methodNamesFromStack);
            return yesNo;
        }
    }

    private void LogProjectPackageUpdateMessage(string message,
            IErrorsAndInfos projectErrorsAndInfos, IErrorsAndInfos projectReducedErrorsAndInfos,
            IList<string> methodNamesFromStack) {
        simpleLogger.LogInformationWithCallStack(message, methodNamesFromStack);
        projectErrorsAndInfos.Infos.Add(message);
        projectReducedErrorsAndInfos.Infos.Add(message);
    }

    public async Task<bool> AreThereNugetUpdateOpportunitiesAsync(IFolder repositoryFolder,
                                                                  string checkedOutBranch, IErrorsAndInfos errorsAndInfos) {
        IPackageUpdateOpportunity packageUpdateOpportunity = await AreThereNugetUpdateOpportunitiesForSolutionAsync(
            repositoryFolder.SubFolder("src"), false, checkedOutBranch, errorsAndInfos);
        return packageUpdateOpportunity.YesNo;
    }

    public async Task<IPackageUpdateOpportunity> AreThereEntityFrameworkNugetUpdateOpportunitiesAsync(
            IFolder repositoryFolder, string checkedOutBranch, IErrorsAndInfos errorsAndInfos) {
        return await AreThereNugetUpdateOpportunitiesForSolutionAsync(
            repositoryFolder.SubFolder("src"), true, checkedOutBranch, errorsAndInfos);
    }

    public async Task<bool> AreThereNugetUpdateOpportunitiesForSolutionAsync(IFolder solutionFolder,
            string checkedOutBranch, IErrorsAndInfos errorsAndInfos) {
        IPackageUpdateOpportunity packageUpdateOpportunity = await AreThereNugetUpdateOpportunitiesForSolutionAsync(solutionFolder,
                                                                                                                    false, checkedOutBranch, errorsAndInfos);
        return packageUpdateOpportunity.YesNo;
    }

    private async Task<IPackageUpdateOpportunity> AreThereNugetUpdateOpportunitiesForSolutionAsync(
            IFolder solutionFolder, bool entityFrameworkUpdatesOnly, string checkedOutBranch,
            IErrorsAndInfos errorsAndInfos) {
        IPackageUpdateOpportunity packageUpdateOpportunity = new PackageUpdateOpportunity();
        var projectFileFullNames = Directory.GetFiles(solutionFolder.FullName, "*.csproj", SearchOption.AllDirectories).ToList();
        if (!projectFileFullNames.Any()) {
            return packageUpdateOpportunity;
        }

        var nugetFeedsSecret = new SecretNugetFeeds();
        NugetFeeds nugetFeeds = await secretRepository.GetAsync(nugetFeedsSecret, errorsAndInfos);
        if (errorsAndInfos.Errors.Any()) { return packageUpdateOpportunity; }

        var nugetFeedIds = nugetFeeds.Select(f => f.Id).ToList();
        foreach (string projectFileFullName in projectFileFullNames) {
            packageUpdateOpportunity
                = await AreThereNugetUpdateOpportunitiesForProjectAsync(projectFileFullName,
                    nugetFeedIds, entityFrameworkUpdatesOnly, checkedOutBranch, errorsAndInfos);
            if (!packageUpdateOpportunity.YesNo) {
                continue;
            }

            packageUpdateOpportunity.YesNo = !errorsAndInfos.AnyErrors();
            return packageUpdateOpportunity;
        }

        return packageUpdateOpportunity;
    }

    private async Task<IPackageUpdateOpportunity> AreThereNugetUpdateOpportunitiesForProjectAsync(
            string projectFileFullName, IList<string> nugetFeedIds,
            bool entityFrameworkUpdatesOnly, string checkedOutBranch, IErrorsAndInfos errorsAndInfos) {
        var dependencyErrorsAndInfos = new ErrorsAndInfos();
        IDictionary<string, string> dependencyIdsAndVersions = await packageReferencesScanner.DependencyIdsAndVersionsAsync(projectFileFullName.Substring(0, projectFileFullName.LastIndexOf('\\')), true, true, dependencyErrorsAndInfos);
        var packageUpdateOpportunity = new PackageUpdateOpportunity();

        var secret = new SecretManuallyUpdatedPackages();
        ManuallyUpdatedPackages manuallyUpdatedPackages = await secretRepository.GetAsync(secret, errorsAndInfos);
        if (errorsAndInfos.AnyErrors()) { return packageUpdateOpportunity; }

        foreach (KeyValuePair<string, string> dependencyIdsAndVersion in dependencyIdsAndVersions) {
            string id = dependencyIdsAndVersion.Key;
            if (entityFrameworkUpdatesOnly) {
                if (!id.StartsWith(_microsoftEntityFrameworkPrefix)) { continue; }
            } else {
                if (id.StartsWith(_microsoftEntityFrameworkPrefix)) { continue; }
                if (manuallyUpdatedPackages.Any(p => p.Matches(id, checkedOutBranch, projectFileFullName))) { continue; }
            }

            if (!Version.TryParse(dependencyIdsAndVersion.Value, out Version version)) {
                continue;
            }

            Version latestRemotePackageVersion = await IdentifyLatestRemotePackageVersion(nugetFeedIds, entityFrameworkUpdatesOnly, id, version);
            if (latestRemotePackageVersion == null) {
                continue;
            }

            errorsAndInfos.Infos.Add(string.Format(Properties.Resources.CanUpdatePackageFromTo, id, version, latestRemotePackageVersion));
            packageUpdateOpportunity.YesNo = true;
            if (entityFrameworkUpdatesOnly) {
                packageUpdateOpportunity.PotentialMigrationId
                    = (id + latestRemotePackageVersion.ToString(3)).Replace(".", "");
            }
        }

        return packageUpdateOpportunity;
    }

    private async Task<Version> IdentifyLatestRemotePackageVersion(IEnumerable<string> nugetFeedIds,
        bool entityFrameworkUpdatesOnly, string id, Version version) {
        IList<IPackageSearchMetadata> remotePackages = null;
        foreach (string nugetFeedId in nugetFeedIds) {
            var listingErrorsAndInfos = new ErrorsAndInfos();
            remotePackages = await nugetFeedLister.ListReleasedPackagesAsync(nugetFeedId, id, listingErrorsAndInfos);
            if (listingErrorsAndInfos.AnyErrors()) {
                continue;
            }

            if (remotePackages.Any()) {
                break;
            }
        }

        if (remotePackages?.Any() != true) {
            return null;
        }

        if (entityFrameworkUpdatesOnly) {
            remotePackages = remotePackages.Where(p => p.Identity.Version.Major == version.Major).ToList();
        }

        Version latestRemotePackageVersion = remotePackages.Max(p => p.Identity.Version.Version);
        if (latestRemotePackageVersion <= version) {
            return null;
        }

        if (latestRemotePackageVersion?.ToString().StartsWith(version.ToString()) != true) {
            return latestRemotePackageVersion;
        }

        if (latestRemotePackageVersion.Major != version.Major) {
            throw new NotImplementedException("Investigation required");
        }
        if (latestRemotePackageVersion.Minor != version.Minor) {
            throw new NotImplementedException("Investigation required");
        }

        return latestRemotePackageVersion.Build <= version.Build ? null : latestRemotePackageVersion;
    }
}