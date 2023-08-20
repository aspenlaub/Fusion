using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Entities;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Nuclide.Entities;
using Aspenlaub.Net.GitHub.CSharp.Nuclide.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Nuclide.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Protch.Interfaces;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Protocol;
// ReSharper disable LoopCanBeConvertedToQuery

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Components;

public class NugetPackageToPushFinder : INugetPackageToPushFinder {
    private readonly IFolderResolver _FolderResolver;
    private readonly IGitUtilities _GitUtilities;
    private readonly INugetConfigReader _NugetConfigReader;
    private readonly INugetFeedLister _NugetFeedLister;
    private readonly IProjectFactory _ProjectFactory;
    private readonly ISecretRepository _SecretRepository;
    private readonly IPushedHeadTipShaRepository _PushedHeadTipShaRepository;
    private readonly IChangedBinariesLister _ChangedBinariesLister;
    private readonly IBranchesWithPackagesRepository _BranchesWithPackagesRepository;

    public NugetPackageToPushFinder(IFolderResolver folderResolver, IGitUtilities gitUtilities,
        INugetConfigReader nugetConfigReader, INugetFeedLister nugetFeedLister, IProjectFactory projectFactory,
        IPushedHeadTipShaRepository pushedHeadTipShaRepository, ISecretRepository secretRepository,
        IChangedBinariesLister changedBinariesLister, IBranchesWithPackagesRepository branchesWithPackagesRepository) {
        _FolderResolver = folderResolver;
        _GitUtilities = gitUtilities;
        _NugetConfigReader = nugetConfigReader;
        _NugetFeedLister = nugetFeedLister;
        _ProjectFactory = projectFactory;
        _PushedHeadTipShaRepository = pushedHeadTipShaRepository;
        _SecretRepository = secretRepository;
        _ChangedBinariesLister = changedBinariesLister;
        _BranchesWithPackagesRepository = branchesWithPackagesRepository;
    }

    public async Task<IPackageToPush> FindPackageToPushAsync(string nugetFeedId,
            IFolder packageFolderWithBinaries, IFolder repositoryFolder, string solutionFileFullName,
            string branchId, IErrorsAndInfos errorsAndInfos) {
        IPackageToPush packageToPush = new PackageToPush();
        errorsAndInfos.Infos.Add(Properties.Resources.CheckingProjectVsSolution);
        var projectFileFullName = solutionFileFullName.Replace(".sln", ".csproj");
        if (!File.Exists(projectFileFullName)) {
            errorsAndInfos.Errors.Add(string.Format(Properties.Resources.ProjectFileNotFound, projectFileFullName));
            return packageToPush;
        }

        errorsAndInfos.Infos.Add(Properties.Resources.LoadingProject);
        var project = _ProjectFactory.Load(solutionFileFullName, projectFileFullName, errorsAndInfos);
        if (errorsAndInfos.Errors.Any()) { return packageToPush; }

        errorsAndInfos.Infos.Add(Properties.Resources.LoadingNugetFeeds);
        var developerSettingsSecret = new DeveloperSettingsSecret();
        var developerSettings = await _SecretRepository.GetAsync(developerSettingsSecret, errorsAndInfos);
        if (errorsAndInfos.Errors.Any()) { return packageToPush; }

        if (developerSettings == null) {
            errorsAndInfos.Errors.Add(string.Format(Properties.Resources.MissingDeveloperSettings, developerSettingsSecret.Guid + ".xml"));
            return packageToPush;
        }

        var nugetFeedsSecret = new SecretNugetFeeds();
        var nugetFeeds = await _SecretRepository.GetAsync(nugetFeedsSecret, errorsAndInfos);
        if (errorsAndInfos.AnyErrors()) {
            return packageToPush;
        }

        errorsAndInfos.Infos.Add(Properties.Resources.IdentifyingNugetFeed);
        var nugetFeed = nugetFeeds.FirstOrDefault(f => f.Id == nugetFeedId);
        if (nugetFeed == null) {
            errorsAndInfos.Errors.Add(string.Format(Properties.Resources.UnknownNugetFeed, nugetFeedId, nugetFeedsSecret.Guid + ".xml"));
            return packageToPush;
        }

        if (!nugetFeed.IsAFolderToResolve()) {
            var nugetConfigFileFullName = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\NuGet\" + "nuget.config";
            packageToPush.ApiKey = _NugetConfigReader.GetApiKey(nugetConfigFileFullName, nugetFeed.Id, errorsAndInfos);
            if (errorsAndInfos.Errors.Any()) { return packageToPush; }
        }

        errorsAndInfos.Infos.Add(Properties.Resources.IdentifyingFeedUrl);
        var source = await nugetFeed.UrlOrResolvedFolderAsync(_FolderResolver, errorsAndInfos);
        if (errorsAndInfos.AnyErrors()) { return packageToPush; }

        packageToPush.FeedUrl = source;
        if (string.IsNullOrEmpty(packageToPush.FeedUrl)) {
            errorsAndInfos.Errors.Add(string.Format(Properties.Resources.IncompleteDeveloperSettings, developerSettingsSecret.Guid + ".xml"));
            return packageToPush;
        }

        errorsAndInfos.Infos.Add(Properties.Resources.SearchingLocalPackage);
        var localPackageRepository = new FindLocalPackagesResourceV2(packageFolderWithBinaries.FullName);
        var localPackages = new List<LocalPackageInfo>();
        foreach (var localPackage in localPackageRepository.GetPackages(new NullLogger(), CancellationToken.None)) {
            if (localPackage.Identity.Version.IsPrerelease) { continue; }

            localPackages.Add(localPackage);
        }
        if (!localPackages.Any()) {
            errorsAndInfos.Errors.Add(string.Format(Properties.Resources.NoPackageFilesFound, packageFolderWithBinaries.FullName));
            return packageToPush;
        }

        var latestLocalPackageVersion = localPackages.Max(p => p.Identity.Version.Version);
        errorsAndInfos.Infos.Add(string.Format(Properties.Resources.FoundLocalPackage, latestLocalPackageVersion));

        errorsAndInfos.Infos.Add(Properties.Resources.SearchingRemotePackage);
        var packageId = string.IsNullOrWhiteSpace(project.PackageId) ? project.RootNamespace : project.PackageId;
        packageId += _BranchesWithPackagesRepository.PackageInfix(branchId, true);
        var remotePackages = await _NugetFeedLister.ListReleasedPackagesAsync(nugetFeedId, packageId, errorsAndInfos);
        if (errorsAndInfos.Errors.Any()) { return packageToPush; }
        if (!remotePackages.Any()) {
            errorsAndInfos.Errors.Add(string.Format(Properties.Resources.NoRemotePackageFilesFound, packageToPush.FeedUrl, packageId));
            return packageToPush;
        }

        errorsAndInfos.Infos.Add(Properties.Resources.LoadingPushedHeadTipShas);
        var pushedHeadTipShas = await _PushedHeadTipShaRepository.GetAsync(nugetFeedId, errorsAndInfos);
        if (errorsAndInfos.AnyErrors()) { return packageToPush; }

        var headTipIdSha = repositoryFolder == null ? "" : _GitUtilities.HeadTipIdSha(repositoryFolder);
        if (!string.IsNullOrWhiteSpace(headTipIdSha) && pushedHeadTipShas.Contains(headTipIdSha)) {
            errorsAndInfos.Infos.Add(string.Format(Properties.Resources.HeadTipShaHasAlreadyBeenPushed, headTipIdSha, nugetFeedId));
            return packageToPush;
        }

        var latestRemotePackageVersion = remotePackages.Max(p => p.Identity.Version.Version);
        errorsAndInfos.Infos.Add(string.Format(Properties.Resources.FoundRemotePackage, latestRemotePackageVersion));
        if (latestRemotePackageVersion >= latestLocalPackageVersion) {
            errorsAndInfos.Infos.Add(string.Format(Properties.Resources.RemotePackageHasHigherOrEqualVersion, headTipIdSha));
            return packageToPush;
        }

        errorsAndInfos.Infos.Add(Properties.Resources.CheckingRemotePackageTag);
        var remotePackage = remotePackages.First(p => p.Identity.Version.Version == latestRemotePackageVersion);
        if (!string.IsNullOrEmpty(remotePackage.Tags) && !string.IsNullOrWhiteSpace(headTipIdSha)) {
            errorsAndInfos.Infos.Add(string.Format(Properties.Resources.TagsAre, remotePackage.Tags));
            var tags = remotePackage.Tags.Split(' ').ToList();
            if (tags.Contains(headTipIdSha)) {
                errorsAndInfos.Infos.Add(string.Format(Properties.Resources.PackageAlreadyTaggedWithHeadTipSha, headTipIdSha));
                return packageToPush;
            }

            if (tags.Count != 1) {
                errorsAndInfos.Errors.Add(string.Format(Properties.Resources.RemotePackageContainsSeveralTags, tags));
                return packageToPush;
            }

            var tag = tags[0];
            errorsAndInfos.Infos.Add(string.Format(Properties.Resources.CheckingIfThereAreChangedBinaries, headTipIdSha, tag));
            var listerErrorsAndInfos = new ErrorsAndInfos();
            var changedBinaries = _ChangedBinariesLister.ListChangedBinaries(project.PackageId, branchId, headTipIdSha, tag, listerErrorsAndInfos);
            if (listerErrorsAndInfos.AnyErrors()) {
                errorsAndInfos.Infos.AddRange(listerErrorsAndInfos.Infos);
                errorsAndInfos.Errors.AddRange(listerErrorsAndInfos.Errors);
                return packageToPush;
            }
            if (!changedBinaries.Any()) {
                errorsAndInfos.Infos.Add(string.Format(Properties.Resources.NoBinariesHaveChanged));
                return packageToPush;
            }
        }

        errorsAndInfos.Infos.Add(Properties.Resources.PackageNeedsToBePushed);
        packageToPush.PackageFileFullName = packageFolderWithBinaries.FullName + @"\" + packageId + "." + latestLocalPackageVersion + ".nupkg";
        packageToPush.Id = packageId;
        packageToPush.Version = latestLocalPackageVersion?.ToString();
        if (File.Exists(packageToPush.PackageFileFullName)) {
            return packageToPush;
        }

        errorsAndInfos.Errors.Add(string.Format(Properties.Resources.FileNotFound, packageToPush.PackageFileFullName));
        return packageToPush;
    }
}