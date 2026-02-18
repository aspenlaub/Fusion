using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Gitty;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Components;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Gitty.TestUtilities;
using Aspenlaub.Net.GitHub.CSharp.Gitty.TestUtilities.Aspenlaub.Net.GitHub.CSharp.Gitty.TestUtilities;
using Aspenlaub.Net.GitHub.CSharp.Nuclide.Entities;
using Aspenlaub.Net.GitHub.CSharp.Nuclide.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Nuclide.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Protch;
using Aspenlaub.Net.GitHub.CSharp.Protch.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Skladasu.Entities;
using Aspenlaub.Net.GitHub.CSharp.Skladasu.Interfaces;
using LibGit2Sharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Autofac;
using NuGet.Protocol.Core.Types;
using Version = System.Version;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Test;

[TestClass]
public class NugetPackageToPushFinderTest {
    protected static TestTargetFolder PakledTarget = new(nameof(NugetPackageToPushFinderTest), "Pakled");
    protected static TestTargetFolder ChabTarget = new(nameof(NugetPackageToPushFinderTest), "Chab");
    protected static TestTargetFolder VishizhukelNetTarget = new(nameof(NugetPackageToPushFinderTest), "VishizhukelNet");
    private static IContainer _container, _containerWithMockedPushedHeadTipShaRepository;
    protected static ITestTargetRunner TargetRunner;

    [ClassInitialize]
    public static void ClassInitialize(TestContext context) {
        _container = new ContainerBuilder().UseGittyTestUtilities().UseProtch().UseFusionNuclideProtchAndGitty("Fusion").Build();
        ContainerBuilder containerBuilder = new ContainerBuilder().UseGittyTestUtilities().UseProtch().UseFusionNuclideProtchAndGitty("Fusion");
        var pushedHeadTipShaRepositoryMock = new Mock<IPushedHeadTipShaRepository>();
        pushedHeadTipShaRepositoryMock.Setup(r => r.GetAsync(It.IsAny<string>(), It.IsAny<IErrorsAndInfos>())).Returns(Task.FromResult(new List<string>()));
        containerBuilder.RegisterInstance(pushedHeadTipShaRepositoryMock.Object);
        _containerWithMockedPushedHeadTipShaRepository = containerBuilder.Build();

        TargetRunner = _container.Resolve<ITestTargetRunner>();
    }

    [TestInitialize]
    public void Initialize() {
        PakledTarget.Delete();
        ChabTarget.Delete();
    }

    [TestCleanup]
    public void TestCleanup() {
        PakledTarget.Delete();
        ChabTarget.Delete();
    }

    [TestMethod]
    public async Task CanFindNugetPackagesToPushForPakled() {
        ISimpleLogger simpleLogger = _container.Resolve<ISimpleLogger>();
        using (simpleLogger.BeginScope(SimpleLoggingScopeId.Create(nameof(CanFindNugetPackagesToPushForPakled)))) {
            var errorsAndInfos = new ErrorsAndInfos();

            NugetFeed nugetFeed = await GetNugetFeedAsync(errorsAndInfos);

            CloneTarget(PakledTarget, errorsAndInfos);

            await RunShatilayaAsync(PakledTarget, true, errorsAndInfos);

            errorsAndInfos = new ErrorsAndInfos();
            INugetPackageToPushFinder sut = _container.Resolve<INugetPackageToPushFinder>();
            IPackageToPush packageToPush = await sut.FindPackageToPushAsync(NugetFeed.AspenlaubLocalFeed,
                PakledTarget.Folder().ParentFolder().SubFolder(PakledTarget.SolutionId + @"Bin\Release"), PakledTarget.Folder(),
                PakledTarget.Folder().SubFolder("src").FullName + @"\" + PakledTarget.SolutionId + ".slnx", "master",
                errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
            string source = await nugetFeed.UrlOrResolvedFolderAsync(_container.Resolve<IFolderResolver>(), errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
            Assert.AreEqual(source, packageToPush.FeedUrl);
        }
    }

    private static async Task<NugetFeed> GetNugetFeedAsync(IErrorsAndInfos errorsAndInfos) {
        var nugetFeedsSecret = new SecretNugetFeeds();
        NugetFeeds nugetFeeds = await _container.Resolve<ISecretRepository>().GetAsync(nugetFeedsSecret, errorsAndInfos);
        Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
        NugetFeed nugetFeed = nugetFeeds.FirstOrDefault(f => f.Id == NugetFeed.AspenlaubLocalFeed);
        Assert.IsNotNull(nugetFeed);
        return nugetFeed;
    }

    [TestMethod]
    public async Task PackageForTheSameCommitIsNotPushed() {
        ISimpleLogger simpleLogger = _container.Resolve<ISimpleLogger>();
        using (simpleLogger.BeginScope(SimpleLoggingScopeId.Create(nameof(PackageForTheSameCommitIsNotPushed)))) {
            var errorsAndInfos = new ErrorsAndInfos();

            CloneTarget(PakledTarget, errorsAndInfos);

            IList<IPackageSearchMetadata> packages = await _container.Resolve<INugetFeedLister>()
                                                                     .ListReleasedPackagesAsync(NugetFeed.AspenlaubLocalFeed, @"Aspenlaub.Net.GitHub.CSharp." + PakledTarget.SolutionId, errorsAndInfos);
            if (errorsAndInfos.AnyErrors()) {
                return;
            }

            if (!packages.Any()) {
                return;
            }

            Version latestPackageVersion = packages.Max(p => p.Identity.Version.Version);
            IPackageSearchMetadata latestPackage = packages.First(p => p.Identity.Version.Version == latestPackageVersion);

            string headTipIdSha = _container.Resolve<IGitUtilities>().HeadTipIdSha(PakledTarget.Folder());
            if (!latestPackage.Tags.Contains(headTipIdSha)) {
                return;
            }

            await RunShatilayaAsync(PakledTarget, false, errorsAndInfos);

            packages = await _container.Resolve<INugetFeedLister>()
                                      .ListReleasedPackagesAsync(NugetFeed.AspenlaubLocalFeed, @"Aspenlaub.Net.GitHub.CSharp." + PakledTarget.SolutionId, errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
            Assert.AreEqual(latestPackageVersion, packages.Max(p => p.Identity.Version.Version));
        }
    }

    private static void CloneTarget(ITestTargetFolder testTargetFolder, IErrorsAndInfos errorsAndInfos) {
        var gitUtilities = new GitUtilities();
        string url = "https://github.com/aspenlaub/" + testTargetFolder.SolutionId + ".git";
        gitUtilities.Clone(url, "master", testTargetFolder.Folder(), new CloneOptions { BranchName = "master" }, true, errorsAndInfos);
        Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
    }

    private static async Task RunShatilayaAsync(ITestTargetFolder testTargetFolder, bool disableNugetPush, IErrorsAndInfos errorsAndInfos) {
        IProjectLogic projectLogic = _container.Resolve<IProjectLogic>();
        IProjectFactory projectFactory = _container.Resolve<IProjectFactory>();
        string solutionFileFullName = testTargetFolder.Folder().SubFolder("src").FullName + '\\' + testTargetFolder.SolutionId + ".slnx";
        var projectErrorsAndInfos = new ErrorsAndInfos();
        Assert.IsTrue(projectLogic.DoAllConfigurationsHaveNuspecs(projectFactory.Load(solutionFileFullName,
            solutionFileFullName.Replace(".slnx", ".csproj"), projectErrorsAndInfos)));

        string target = disableNugetPush ? "IgnorePendingChangesAndDoNotPush" : "IgnorePendingChanges";
        await TargetRunner.RunShatilayaAsync(testTargetFolder, target, errorsAndInfos);
        Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
    }

    [TestMethod]
    public async Task CanFindNugetPackagesToPushForChab() {
        ISimpleLogger simpleLogger = _container.Resolve<ISimpleLogger>();
        using (simpleLogger.BeginScope(SimpleLoggingScopeId.Create(nameof(CanFindNugetPackagesToPushForChab)))) {
            var errorsAndInfos = new ErrorsAndInfos();
            NugetFeed nugetFeed = await GetNugetFeedAsync(errorsAndInfos);

            CloneTarget(ChabTarget, errorsAndInfos);

            await RunShatilayaAsync(ChabTarget, true, errorsAndInfos);

            Assert.DoesNotContain(i => i.Contains("No test"), errorsAndInfos.Infos);

            errorsAndInfos = new ErrorsAndInfos();
            INugetPackageToPushFinder sut = _container.Resolve<INugetPackageToPushFinder>();
            IPackageToPush packageToPush = await sut.FindPackageToPushAsync(NugetFeed.AspenlaubLocalFeed, ChabTarget.Folder().ParentFolder().SubFolder(ChabTarget.SolutionId + @"Bin\Release"),
                ChabTarget.Folder(), ChabTarget.Folder().SubFolder("src").FullName + @"\" + ChabTarget.SolutionId + ".slnx",
                "master", errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
            string source = await nugetFeed.UrlOrResolvedFolderAsync(_container.Resolve<IFolderResolver>(), errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
            Assert.AreEqual(source, packageToPush.FeedUrl);

            sut = _containerWithMockedPushedHeadTipShaRepository.Resolve<INugetPackageToPushFinder>();
            packageToPush = await sut.FindPackageToPushAsync(NugetFeed.AspenlaubLocalFeed, ChabTarget.Folder().ParentFolder().SubFolder(ChabTarget.SolutionId + @"Bin\Release"),
                ChabTarget.Folder(), ChabTarget.Folder().SubFolder("src").FullName + @"\" + ChabTarget.SolutionId + ".slnx", "master",
                errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
            source = await nugetFeed.UrlOrResolvedFolderAsync(_container.Resolve<IFolderResolver>(), errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
            Assert.AreEqual(source, packageToPush.FeedUrl);
        }
    }

    [TestMethod]
    public async Task CanCheckForNugetPackagesToPushForVishizhukelNet() {
        ISimpleLogger simpleLogger = _container.Resolve<ISimpleLogger>();
        using (simpleLogger.BeginScope(SimpleLoggingScopeId.Create(nameof(CanCheckForNugetPackagesToPushForVishizhukelNet)))) {
            var errorsAndInfos = new ErrorsAndInfos();

            IFolderResolver folderResolver = _container.Resolve<IFolderResolver>();
            IFolder gitHubFolder = await folderResolver.ResolveAsync("$(GitHub)", errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());

            errorsAndInfos = new ErrorsAndInfos();
            INugetPackageToPushFinder sut = _container.Resolve<INugetPackageToPushFinder>();
            IFolder packageFolderWithBinaries = gitHubFolder.SubFolder(VishizhukelNetTarget.SolutionId + @"Bin\Release");
            if (!packageFolderWithBinaries.Exists()) {
                return;
            }

            IFolder repositoryFolder = gitHubFolder.SubFolder(VishizhukelNetTarget.SolutionId);
            Assert.IsTrue(repositoryFolder.Exists(), $"Folder {repositoryFolder.FullName} does not exist");
            string solutionFileFullName = repositoryFolder.SubFolder("src").FullName + @"\" + VishizhukelNetTarget.SolutionId + ".slnx";
            await sut.FindPackageToPushAsync(NugetFeed.AspenlaubLocalFeed, packageFolderWithBinaries, repositoryFolder, solutionFileFullName, "master", errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
        }
    }
}