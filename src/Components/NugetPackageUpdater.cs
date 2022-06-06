using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Entities;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Nuclide.Entities;
using Aspenlaub.Net.GitHub.CSharp.Nuclide.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using NuGet.Protocol.Core.Types;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Components;

public class NugetPackageUpdater : INugetPackageUpdater {
    private readonly IGitUtilities _GitUtilities;
    private readonly IProcessRunner _ProcessRunner;
    private readonly INugetFeedLister _NugetFeedLister;
    private readonly ISecretRepository _SecretRepository;
    private readonly IPackageConfigsScanner _PackageConfigsScanner;
    private readonly ISimpleLogger _SimpleLogger;
    private readonly IMethodNamesFromStackFramesExtractor _MethodNamesFromStackFramesExtractor;

    private readonly IList<string> _EndingsThatAllowReset = new List<string> { "csproj", "config" };

    public NugetPackageUpdater(IGitUtilities gitUtilities, IProcessRunner processRunner, INugetFeedLister nugetFeedLister, ISecretRepository secretRepository, IPackageConfigsScanner packageConfigsScanner, ISimpleLogger simpleLogger, IMethodNamesFromStackFramesExtractor methodNamesFromStackFramesExtractor) {
        _GitUtilities = gitUtilities;
        _ProcessRunner = processRunner;
        _NugetFeedLister = nugetFeedLister;
        _SecretRepository = secretRepository;
        _PackageConfigsScanner = packageConfigsScanner;
        _SimpleLogger = simpleLogger;
        _MethodNamesFromStackFramesExtractor = methodNamesFromStackFramesExtractor;
    }

    public async Task<YesNoInconclusive> UpdateNugetPackagesInRepositoryAsync(IFolder repositoryFolder, IErrorsAndInfos errorsAndInfos) {
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
                if (!await UpdateNugetPackagesForProjectAsync(projectFileFullName, yesNoInconclusive.YesNo, projectErrorsAndInfos)) {
                    continue;
                }

                yesNoInconclusive.YesNo = true;
            }

            if (yesNoInconclusive.YesNo) {
                errorsAndInfos.Infos.Add("No project was updated");
                _SimpleLogger.LogInformationWithCallStack($"Returning {yesNoInconclusive}", methodNamesFromStack);
                return yesNoInconclusive;
            }

            _SimpleLogger.LogInformationWithCallStack("Resetting repository", methodNamesFromStack);
            _GitUtilities.Reset(repositoryFolder, _GitUtilities.HeadTipIdSha(repositoryFolder), errorsAndInfos);
            _SimpleLogger.LogInformationWithCallStack($"Returning {yesNoInconclusive}", methodNamesFromStack);
            return yesNoInconclusive;
        }
    }

    public async Task<YesNoInconclusive> UpdateNugetPackagesInSolutionAsync(IFolder solutionFolder, IErrorsAndInfos errorsAndInfos) {
        using (_SimpleLogger.BeginScope(SimpleLoggingScopeId.Create(nameof(UpdateNugetPackagesInSolutionAsync)))) {
            var methodNamesFromStack = _MethodNamesFromStackFramesExtractor.ExtractMethodNamesFromStackFrames();
            _SimpleLogger.LogInformationWithCallStack("Searching for project files", methodNamesFromStack);
            var yesNoInconclusive = new YesNoInconclusive();
            var projectFileFullNames = Directory.GetFiles(solutionFolder.FullName, "*.csproj", SearchOption.AllDirectories).ToList();
            if (!projectFileFullNames.Any()) {
                _SimpleLogger.LogInformationWithCallStack($"Returning {yesNoInconclusive}", methodNamesFromStack);
                return yesNoInconclusive;
            }

            foreach (var projectFileFullName in projectFileFullNames) {
                _SimpleLogger.LogInformationWithCallStack($"Analyzing project file {projectFileFullName}", methodNamesFromStack);
                yesNoInconclusive.YesNo = await UpdateNugetPackagesForProjectAsync(projectFileFullName, yesNoInconclusive.YesNo, errorsAndInfos);
            }

            _SimpleLogger.LogInformationWithCallStack($"Returning {yesNoInconclusive}", methodNamesFromStack);
            return yesNoInconclusive;
        }
    }

    private async Task<bool> UpdateNugetPackagesForProjectAsync(string projectFileFullName, bool yesNo, IErrorsAndInfos errorsAndInfos) {
        using (_SimpleLogger.BeginScope(SimpleLoggingScopeId.Create(nameof(UpdateNugetPackagesForProjectAsync)))) {
            var methodNamesFromStack = _MethodNamesFromStackFramesExtractor.ExtractMethodNamesFromStackFrames();
            _SimpleLogger.LogInformationWithCallStack("Retrieving dependency ids and versions", methodNamesFromStack);
            var dependencyErrorsAndInfos = new ErrorsAndInfos();
            var dependencyIdsAndVersions =
                await _PackageConfigsScanner.DependencyIdsAndVersionsAsync(projectFileFullName.Substring(0, projectFileFullName.LastIndexOf('\\')), true, true, dependencyErrorsAndInfos);

            _SimpleLogger.LogInformationWithCallStack("Retrieving manually updated packages", methodNamesFromStack);
            var secret = new SecretManuallyUpdatedPackages();
            var manuallyUpdatedPackages = await _SecretRepository.GetAsync(secret, errorsAndInfos);
            if (errorsAndInfos.AnyErrors()) {
                _SimpleLogger.LogInformationWithCallStack("Returning false", methodNamesFromStack);
                return false;
            }

            foreach (var id in dependencyIdsAndVersions.Select(dependencyIdsAndVersion => dependencyIdsAndVersion.Key).Where(id => manuallyUpdatedPackages.All(p => p.Id != id))) {
                _SimpleLogger.LogInformationWithCallStack($"Updating dependency {id}", methodNamesFromStack);
                var projectFileFolder = new Folder(projectFileFullName.Substring(0, projectFileFullName.LastIndexOf('\\')));
                _ProcessRunner.RunProcess("dotnet", "remove " + projectFileFullName + " package " + id, projectFileFolder, errorsAndInfos);
                _ProcessRunner.RunProcess("dotnet", "add " + projectFileFullName + " package " + id, projectFileFolder, errorsAndInfos);
            }

            _SimpleLogger.LogInformationWithCallStack("Retrieving dependency ids and versions once more", methodNamesFromStack);
            var dependencyIdsAndVersionsAfterUpdate =
                await _PackageConfigsScanner.DependencyIdsAndVersionsAsync(projectFileFullName.Substring(0, projectFileFullName.LastIndexOf('\\')), true, true, dependencyErrorsAndInfos);

            _SimpleLogger.LogInformationWithCallStack("Determining differences", methodNamesFromStack);
            foreach (var dependencyIdsAndVersion in dependencyIdsAndVersionsAfterUpdate) {
                var id = dependencyIdsAndVersion.Key;
                var version = dependencyIdsAndVersion.Value;
                yesNo = yesNo || !dependencyIdsAndVersions.ContainsKey(id) || version != dependencyIdsAndVersions[id];
            }

            _SimpleLogger.LogInformationWithCallStack($"Returning {yesNo}", methodNamesFromStack);
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

        var nugetFeedsSecret = new SecretNugetFeeds();
        var nugetFeeds = await _SecretRepository.GetAsync(nugetFeedsSecret, errorsAndInfos);
        if (errorsAndInfos.Errors.Any()) { return false; }

        var feedIds = nugetFeeds.Select(f => f.Id).ToList();
        foreach (var projectFileFullName in projectFileFullNames) {
            if (await AreThereNugetUpdateOpportunitiesForProjectAsync(projectFileFullName, feedIds, errorsAndInfos)) {
                return !errorsAndInfos.AnyErrors();
            }
        }

        return false;
    }

    private async Task<bool> AreThereNugetUpdateOpportunitiesForProjectAsync(string projectFileFullName, IList<string> nugetFeedIds, IErrorsAndInfos errorsAndInfos) {
        var dependencyErrorsAndInfos = new ErrorsAndInfos();
        var dependencyIdsAndVersions = await _PackageConfigsScanner.DependencyIdsAndVersionsAsync(projectFileFullName.Substring(0, projectFileFullName.LastIndexOf('\\')), true, true, dependencyErrorsAndInfos);

        var secret = new SecretManuallyUpdatedPackages();
        var manuallyUpdatedPackages = await _SecretRepository.GetAsync(secret, errorsAndInfos);
        if (errorsAndInfos.AnyErrors()) { return false; }

        var yesNo = false;
        foreach (var dependencyIdsAndVersion in dependencyIdsAndVersions) {
            var id = dependencyIdsAndVersion.Key;
            if (manuallyUpdatedPackages.Any(p => p.Id == id)) { continue; }

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
                continue;
            }

            if (!Version.TryParse(dependencyIdsAndVersion.Value, out var version)) {
                continue;
            }

            var latestRemotePackageVersion = remotePackages.Max(p => p.Identity.Version.Version);
            if (latestRemotePackageVersion <= version || latestRemotePackageVersion?.ToString().StartsWith(version.ToString()) == true) {
                continue;
            }

            errorsAndInfos.Infos.Add(string.Format(Properties.Resources.CanUpdatePackageFromTo, id, version, latestRemotePackageVersion));
            yesNo = true;
        }

        return yesNo;
    }
}