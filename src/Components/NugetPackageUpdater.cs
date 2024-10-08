﻿using System;
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
using NuGet.Packaging;
using NuGet.Protocol.Core.Types;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Components;

public class NugetPackageUpdater : INugetPackageUpdater {
    private const string MicrosoftEntityFrameworkPrefix = "Microsoft.EntityFramework";
    private readonly IGitUtilities _GitUtilities;
    private readonly IProcessRunner _ProcessRunner;
    private readonly INugetFeedLister _NugetFeedLister;
    private readonly ISecretRepository _SecretRepository;
    private readonly IPackageReferencesScanner _PackageReferencesScanner;
    private readonly ISimpleLogger _SimpleLogger;
    private readonly IMethodNamesFromStackFramesExtractor _MethodNamesFromStackFramesExtractor;
    private readonly IDotNetEfRunner _DotNetEfRunner;

    private readonly IList<string> _EndingsThatAllowReset = new List<string> { "csproj", "config" };

    public NugetPackageUpdater(IGitUtilities gitUtilities, IProcessRunner processRunner,
            INugetFeedLister nugetFeedLister, ISecretRepository secretRepository,
            IPackageReferencesScanner packageReferencesScanner, ISimpleLogger simpleLogger,
            IMethodNamesFromStackFramesExtractor methodNamesFromStackFramesExtractor,
            IDotNetEfRunner dotNetEfRunner) {
        _GitUtilities = gitUtilities;
        _ProcessRunner = processRunner;
        _NugetFeedLister = nugetFeedLister;
        _SecretRepository = secretRepository;
        _PackageReferencesScanner = packageReferencesScanner;
        _SimpleLogger = simpleLogger;
        _MethodNamesFromStackFramesExtractor = methodNamesFromStackFramesExtractor;
        _DotNetEfRunner = dotNetEfRunner;
    }

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
        using (_SimpleLogger.BeginScope(SimpleLoggingScopeId.Create(nameof(UpdateNugetPackagesInRepositoryAsync)))) {
            var methodNamesFromStack = _MethodNamesFromStackFramesExtractor.ExtractMethodNamesFromStackFrames();
            _SimpleLogger.LogInformationWithCallStack("Determining files with uncommitted changes", methodNamesFromStack);
            var yesNoInconclusive = new YesNoInconclusive();
            var files = _GitUtilities.FilesWithUncommittedChanges(repositoryFolder);
            yesNoInconclusive.Inconclusive = files.Any(f => _EndingsThatAllowReset.All(e => !f.EndsWith("." + e, StringComparison.InvariantCultureIgnoreCase)));
            yesNoInconclusive.YesNo = false;
            if (yesNoInconclusive.Inconclusive) {
                errorsAndInfos.Infos.Add("Not all files allow a reset");
                _SimpleLogger.LogInformationWithCallStack($"Returning {yesNoInconclusive}", methodNamesFromStack);
                return yesNoInconclusive;
            }
            var nugetFeedsSecret = new SecretNugetFeeds();
            var nugetFeeds = await _SecretRepository.GetAsync(nugetFeedsSecret, errorsAndInfos);
            if (errorsAndInfos.Errors.Any()) {
                _SimpleLogger.LogInformationWithCallStack($"Returning {yesNoInconclusive}", methodNamesFromStack);
                return yesNoInconclusive;
            }

            var nugetFeedIds = nugetFeeds.Select(f => f.Id).ToList();

            _SimpleLogger.LogInformationWithCallStack("Resetting repository", methodNamesFromStack);
            _GitUtilities.Reset(repositoryFolder, _GitUtilities.HeadTipIdSha(repositoryFolder), errorsAndInfos);
            if (errorsAndInfos.AnyErrors()) {
                errorsAndInfos.Infos.Add("Could not reset");
                _SimpleLogger.LogInformationWithCallStack($"Returning {yesNoInconclusive}", methodNamesFromStack);
                return yesNoInconclusive;
            }

            _SimpleLogger.LogInformationWithCallStack("Searching for project files", methodNamesFromStack);
            var projectFileFullNames = Directory.GetFiles(repositoryFolder.SubFolder("src").FullName, "*.csproj", SearchOption.AllDirectories).ToList();
            if (!projectFileFullNames.Any()) {
                errorsAndInfos.Infos.Add("No project files found");
                _SimpleLogger.LogInformationWithCallStack($"Returning {yesNoInconclusive}", methodNamesFromStack);
                return yesNoInconclusive;
            }

            foreach (var projectFileFullName in projectFileFullNames) {
                _SimpleLogger.LogInformationWithCallStack($"Analyzing project file {projectFileFullName}", methodNamesFromStack);
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
                _SimpleLogger.LogInformationWithCallStack($"Returning {yesNoInconclusive}", methodNamesFromStack);
                return yesNoInconclusive;
            }

            errorsAndInfos.Infos.Add("No project was updated");
            _SimpleLogger.LogInformationWithCallStack("Resetting repository", methodNamesFromStack);
            _GitUtilities.Reset(repositoryFolder, _GitUtilities.HeadTipIdSha(repositoryFolder), errorsAndInfos);
            _SimpleLogger.LogInformationWithCallStack($"Returning {yesNoInconclusive}", methodNamesFromStack);
            return yesNoInconclusive;
        }
    }

    private async Task<bool> UpdateNugetPackagesForProjectAsync(string projectFileFullName,
            IList<string> nugetFeedIds, bool yesNo, bool entityFrameworkOnly, string migrationId,
            string checkedOutBranch, IErrorsAndInfos projectErrorsAndInfos, IErrorsAndInfos projectReducedErrorsAndInfos) {
        using (_SimpleLogger.BeginScope(SimpleLoggingScopeId.Create(nameof(UpdateNugetPackagesForProjectAsync)))) {
            var methodNamesFromStack = _MethodNamesFromStackFramesExtractor.ExtractMethodNamesFromStackFrames();
            var projectFileFolder = new Folder(projectFileFullName.Substring(0, projectFileFullName.LastIndexOf('\\')));

            LogProjectPackageUpdateMessage("Checking if project has at least one migration",
                projectErrorsAndInfos, projectReducedErrorsAndInfos, methodNamesFromStack);
            var projectHasMigrations = entityFrameworkOnly
               && _DotNetEfRunner.ListAppliedMigrationIds(projectFileFolder, projectErrorsAndInfos)
                                 .Any();

            if (entityFrameworkOnly && projectHasMigrations) {
                LogProjectPackageUpdateMessage( "Updating database",
                    projectErrorsAndInfos, projectReducedErrorsAndInfos, methodNamesFromStack);
                _DotNetEfRunner.UpdateDatabase(projectFileFolder, projectErrorsAndInfos);
                if (projectErrorsAndInfos.AnyErrors()) {
                    _SimpleLogger.LogInformationWithCallStack("Returning false", methodNamesFromStack);
                    return false;
                }
            }

            LogProjectPackageUpdateMessage( "Retrieving dependency ids and versions",
                    projectErrorsAndInfos, projectReducedErrorsAndInfos, methodNamesFromStack);
            var dependencyErrorsAndInfos = new ErrorsAndInfos();
            var dependencyIdsAndVersions =
                await _PackageReferencesScanner.DependencyIdsAndVersionsAsync(projectFileFullName.Substring(0, projectFileFullName.LastIndexOf('\\')), true, true, dependencyErrorsAndInfos);

            LogProjectPackageUpdateMessage( "Retrieving manually updated packages",
                    projectErrorsAndInfos, projectReducedErrorsAndInfos, methodNamesFromStack);
            var secret = new SecretManuallyUpdatedPackages();
            var manuallyUpdatedPackages = await _SecretRepository.GetAsync(secret, projectErrorsAndInfos);
            if (projectErrorsAndInfos.AnyErrors()) {
                _SimpleLogger.LogInformationWithCallStack("Returning false", methodNamesFromStack);
                return false;
            }

            var ids = dependencyIdsAndVersions
                .Select(dependencyIdsAndVersion => dependencyIdsAndVersion.Key)
                .Where(id => entityFrameworkOnly
                       ? id.StartsWith(MicrosoftEntityFrameworkPrefix)
                       : !id.StartsWith(MicrosoftEntityFrameworkPrefix)
                        && manuallyUpdatedPackages.All(p => !p.Matches(id, checkedOutBranch, projectFileFullName)))
                .ToList();
            foreach (var id in ids) {
                LogProjectPackageUpdateMessage( $"Updating dependency {id}",
                        projectErrorsAndInfos, projectReducedErrorsAndInfos, methodNamesFromStack);
                var dependencyIdAndVersion = dependencyIdsAndVersions.First(d => d.Key == id);
                var version = System.Version.Parse(dependencyIdAndVersion.Value);
                var latestRemotePackageVersion = await IdentifyLatestRemotePackageVersion(nugetFeedIds, entityFrameworkOnly, id, version);
                if (latestRemotePackageVersion == null) {
                    continue;
                }

                var arguments = "remove " + projectFileFullName + " package " + id;
                _ProcessRunner.RunProcess("dotnet", arguments, projectFileFolder, projectErrorsAndInfos);
                projectErrorsAndInfos.Infos.Add(arguments);
                projectReducedErrorsAndInfos.Infos.Add(arguments);
                if (entityFrameworkOnly) {
                    arguments = "add " + projectFileFullName + " package " + id + " -v " + latestRemotePackageVersion;
                } else {
                    arguments = "add " + projectFileFullName + " package " + id;
                }
                _ProcessRunner.RunProcess("dotnet", arguments, projectFileFolder, projectErrorsAndInfos);
                projectErrorsAndInfos.Infos.Add(arguments);
                projectReducedErrorsAndInfos.Infos.Add(arguments);
            }

            if (entityFrameworkOnly && projectHasMigrations) {
                LogProjectPackageUpdateMessage( "Adding migration",
                        projectErrorsAndInfos, projectReducedErrorsAndInfos, methodNamesFromStack);
                if (string.IsNullOrEmpty(migrationId)) {
                    migrationId = ids[0].Replace(".", "");
                }
                _DotNetEfRunner.AddMigration(projectFileFolder, migrationId, projectErrorsAndInfos);
                if (projectErrorsAndInfos.AnyErrors()) {
                    _SimpleLogger.LogInformationWithCallStack("Returning false", methodNamesFromStack);
                    return false;
                }

                LogProjectPackageUpdateMessage( "Updating database",
                        projectErrorsAndInfos, projectReducedErrorsAndInfos, methodNamesFromStack);
                _DotNetEfRunner.UpdateDatabase(projectFileFolder, projectErrorsAndInfos);
                if (projectErrorsAndInfos.AnyErrors()) {
                    _SimpleLogger.LogInformationWithCallStack("Returning false", methodNamesFromStack);
                    return false;
                }
            }

            LogProjectPackageUpdateMessage( "Retrieving dependency ids and versions once more",
                    projectErrorsAndInfos, projectReducedErrorsAndInfos, methodNamesFromStack);
            var dependencyIdsAndVersionsAfterUpdate =
                await _PackageReferencesScanner.DependencyIdsAndVersionsAsync(projectFileFullName.Substring(0, projectFileFullName.LastIndexOf('\\')), true, true, dependencyErrorsAndInfos);

            LogProjectPackageUpdateMessage( "Determining differences",
                    projectErrorsAndInfos, projectReducedErrorsAndInfos, methodNamesFromStack);
            foreach (var dependencyIdsAndVersion in dependencyIdsAndVersionsAfterUpdate) {
                var id = dependencyIdsAndVersion.Key;
                var version = dependencyIdsAndVersion.Value;
                yesNo = yesNo || !dependencyIdsAndVersions.ContainsKey(id) || version != dependencyIdsAndVersions[id];
            }

            _SimpleLogger.LogInformationWithCallStack($"Returning {yesNo}", methodNamesFromStack);
            return yesNo;
        }
    }

    private void LogProjectPackageUpdateMessage(string message,
            IErrorsAndInfos projectErrorsAndInfos, IErrorsAndInfos projectReducedErrorsAndInfos,
            IList<string> methodNamesFromStack) {
        _SimpleLogger.LogInformationWithCallStack(message, methodNamesFromStack);
        projectErrorsAndInfos.Infos.Add(message);
        projectReducedErrorsAndInfos.Infos.Add(message);
    }

    public async Task<bool> AreThereNugetUpdateOpportunitiesAsync(IFolder repositoryFolder,
                                                                  string checkedOutBranch, IErrorsAndInfos errorsAndInfos) {
        var packageUpdateOpportunity = await AreThereNugetUpdateOpportunitiesForSolutionAsync(
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
        var packageUpdateOpportunity = await AreThereNugetUpdateOpportunitiesForSolutionAsync(solutionFolder,
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
        var nugetFeeds = await _SecretRepository.GetAsync(nugetFeedsSecret, errorsAndInfos);
        if (errorsAndInfos.Errors.Any()) { return packageUpdateOpportunity; }

        var nugetFeedIds = nugetFeeds.Select(f => f.Id).ToList();
        foreach (var projectFileFullName in projectFileFullNames) {
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
        var dependencyIdsAndVersions = await _PackageReferencesScanner.DependencyIdsAndVersionsAsync(projectFileFullName.Substring(0, projectFileFullName.LastIndexOf('\\')), true, true, dependencyErrorsAndInfos);
        var packageUpdateOpportunity = new PackageUpdateOpportunity();

        var secret = new SecretManuallyUpdatedPackages();
        var manuallyUpdatedPackages = await _SecretRepository.GetAsync(secret, errorsAndInfos);
        if (errorsAndInfos.AnyErrors()) { return packageUpdateOpportunity; }

        foreach (var dependencyIdsAndVersion in dependencyIdsAndVersions) {
            var id = dependencyIdsAndVersion.Key;
            if (entityFrameworkUpdatesOnly) {
                if (!id.StartsWith(MicrosoftEntityFrameworkPrefix)) { continue; }
            } else {
                if (id.StartsWith(MicrosoftEntityFrameworkPrefix)) { continue; }
                if (manuallyUpdatedPackages.Any(p => p.Matches(id, checkedOutBranch, projectFileFullName))) { continue; }
            }

            if (!System.Version.TryParse(dependencyIdsAndVersion.Value, out var version)) {
                continue;
            }

            var latestRemotePackageVersion = await IdentifyLatestRemotePackageVersion(nugetFeedIds, entityFrameworkUpdatesOnly, id, version);
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

    private async Task<System.Version> IdentifyLatestRemotePackageVersion(IEnumerable<string> nugetFeedIds,
        bool entityFrameworkUpdatesOnly, string id, System.Version version) {
        IList<IPackageSearchMetadata> remotePackages = null;
        foreach (var nugetFeedId in nugetFeedIds) {
            var listingErrorsAndInfos = new ErrorsAndInfos();
            remotePackages = await _NugetFeedLister.ListReleasedPackagesAsync(nugetFeedId, id, listingErrorsAndInfos);
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

        var latestRemotePackageVersion = remotePackages.Max(p => p.Identity.Version.Version);
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