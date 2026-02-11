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
using NuGet.Protocol.Core.Types;
using Version = System.Version;

// ReSharper disable LoopCanBeConvertedToQuery

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Components;

public class NugetPackageToPushFinder(
        IFolderResolver folderResolver, IGitUtilities gitUtilities,
        INugetConfigReader nugetConfigReader, INugetFeedLister nugetFeedLister,
        IProjectFactory projectFactory, IPushedHeadTipShaRepository pushedHeadTipShaRepository,
        ISecretRepository secretRepository, IChangedBinariesLister changedBinariesLister,
        IBranchesWithPackagesRepository branchesWithPackagesRepository)
            : INugetPackageToPushFinder {

    public async Task<IPackageToPush> FindPackageToPushAsync(string nugetFeedId, IFolder packageFolderWithBinaries,
            IFolder repositoryFolder, string solutionFileFullName, string branchId, IErrorsAndInfos errorsAndInfos) {
        IPackageToPush packageToPush = new PackageToPush();
        errorsAndInfos.Infos.Add(Properties.Resources.CheckingProjectVsSolution);
        string projectFileFullName = solutionFileFullName
            .Replace(".slnx", ".csproj");
        if (!File.Exists(projectFileFullName)) {
            errorsAndInfos.Errors.Add(string.Format(Properties.Resources.ProjectFileNotFound, projectFileFullName));
            return packageToPush;
        }

        errorsAndInfos.Infos.Add(Properties.Resources.LoadingProject);
        IProject project = projectFactory.Load(solutionFileFullName, projectFileFullName, errorsAndInfos);
        if (errorsAndInfos.Errors.Any()) { return packageToPush; }

        errorsAndInfos.Infos.Add(Properties.Resources.LoadingNugetFeeds);
        var developerSettingsSecret = new DeveloperSettingsSecret();
        DeveloperSettings developerSettings = await secretRepository.GetAsync(developerSettingsSecret, errorsAndInfos);
        if (errorsAndInfos.Errors.Any()) { return packageToPush; }

        if (developerSettings == null) {
            errorsAndInfos.Errors.Add(string.Format(Properties.Resources.MissingDeveloperSettings, developerSettingsSecret.Guid + ".xml"));
            return packageToPush;
        }

        var nugetFeedsSecret = new SecretNugetFeeds();
        NugetFeeds nugetFeeds = await secretRepository.GetAsync(nugetFeedsSecret, errorsAndInfos);
        if (errorsAndInfos.AnyErrors()) {
            return packageToPush;
        }

        errorsAndInfos.Infos.Add(Properties.Resources.IdentifyingNugetFeed);
        NugetFeed nugetFeed = nugetFeeds.FirstOrDefault(f => f.Id == nugetFeedId);
        if (nugetFeed == null) {
            errorsAndInfos.Errors.Add(string.Format(Properties.Resources.UnknownNugetFeed, nugetFeedId, nugetFeedsSecret.Guid + ".xml"));
            return packageToPush;
        }

        if (!nugetFeed.IsAFolderToResolve()) {
            string nugetConfigFileFullName = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\NuGet\" + "nuget.config";
            packageToPush.ApiKey = nugetConfigReader.GetApiKey(nugetConfigFileFullName, nugetFeed.Id, errorsAndInfos);
            if (errorsAndInfos.Errors.Any()) { return packageToPush; }
        }

        errorsAndInfos.Infos.Add(Properties.Resources.IdentifyingFeedUrl);
        string source = await nugetFeed.UrlOrResolvedFolderAsync(folderResolver, errorsAndInfos);
        if (errorsAndInfos.AnyErrors()) { return packageToPush; }

        packageToPush.FeedUrl = source;
        if (string.IsNullOrEmpty(packageToPush.FeedUrl)) {
            errorsAndInfos.Errors.Add(string.Format(Properties.Resources.IncompleteDeveloperSettings, developerSettingsSecret.Guid + ".xml"));
            return packageToPush;
        }

        errorsAndInfos.Infos.Add(Properties.Resources.SearchingLocalPackage);
        var localPackageRepository = new FindLocalPackagesResourceV2(packageFolderWithBinaries.FullName);
        var localPackages = new List<LocalPackageInfo>();
        foreach (LocalPackageInfo localPackage in localPackageRepository.GetPackages(new NullLogger(), CancellationToken.None)) {
            if (localPackage.Identity.Version.IsPrerelease) { continue; }

            localPackages.Add(localPackage);
        }
        if (!localPackages.Any()) {
            errorsAndInfos.Errors.Add(string.Format(Properties.Resources.NoPackageFilesFound, packageFolderWithBinaries.FullName));
            return packageToPush;
        }

        Version latestLocalPackageVersion = localPackages.Max(p => p.Identity.Version.Version);
        errorsAndInfos.Infos.Add(string.Format(Properties.Resources.FoundLocalPackage, latestLocalPackageVersion));

        errorsAndInfos.Infos.Add(Properties.Resources.SearchingRemotePackage);
        string packageId = string.IsNullOrWhiteSpace(project.PackageId) ? project.RootNamespace : project.PackageId;
        packageId += branchesWithPackagesRepository.PackageInfix(branchId, true);
        IList<IPackageSearchMetadata> remotePackages = await nugetFeedLister.ListReleasedPackagesAsync(nugetFeedId, packageId, errorsAndInfos);
        if (errorsAndInfos.Errors.Any()) { return packageToPush; }
        if (!remotePackages.Any()) {
            errorsAndInfos.Errors.Add(string.Format(Properties.Resources.NoRemotePackageFilesFound, packageToPush.FeedUrl, packageId));
            return packageToPush;
        }

        errorsAndInfos.Infos.Add(Properties.Resources.LoadingPushedHeadTipShas);
        List<string> pushedHeadTipShas = await pushedHeadTipShaRepository.GetAsync(nugetFeedId, errorsAndInfos);
        if (errorsAndInfos.AnyErrors()) { return packageToPush; }

        string headTipIdSha = repositoryFolder == null ? "" : gitUtilities.HeadTipIdSha(repositoryFolder);
        if (!string.IsNullOrWhiteSpace(headTipIdSha) && pushedHeadTipShas.Contains(headTipIdSha)) {
            errorsAndInfos.Infos.Add(string.Format(Properties.Resources.HeadTipShaHasAlreadyBeenPushed, headTipIdSha, nugetFeedId));
            return packageToPush;
        }

        Version latestRemotePackageVersion = remotePackages.Max(p => p.Identity.Version.Version);
        errorsAndInfos.Infos.Add(string.Format(Properties.Resources.FoundRemotePackage, latestRemotePackageVersion));
        if (latestRemotePackageVersion >= latestLocalPackageVersion) {
            errorsAndInfos.Infos.Add(string.Format(Properties.Resources.RemotePackageHasHigherOrEqualVersion, headTipIdSha));
            return packageToPush;
        }

        errorsAndInfos.Infos.Add(Properties.Resources.CheckingRemotePackageTag);
        IPackageSearchMetadata remotePackage = remotePackages.First(p => p.Identity.Version.Version == latestRemotePackageVersion);
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

            string tag = tags[0];
            errorsAndInfos.Infos.Add(string.Format(Properties.Resources.CheckingIfThereAreChangedBinaries, headTipIdSha, tag));
            var listerErrorsAndInfos = new ErrorsAndInfos();
            IList<BinaryToUpdate> changedBinaries = await changedBinariesLister.ListChangedBinariesAsync(project.PackageId, branchId, headTipIdSha, tag, listerErrorsAndInfos);
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