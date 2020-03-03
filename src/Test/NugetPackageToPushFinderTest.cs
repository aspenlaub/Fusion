using System.Linq;
using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Gitty;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Gitty.TestUtilities;
using Aspenlaub.Net.GitHub.CSharp.Nuclide;
using Aspenlaub.Net.GitHub.CSharp.Nuclide.Entities;
using Aspenlaub.Net.GitHub.CSharp.Nuclide.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Nuclide.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Components;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Protch;
using Aspenlaub.Net.GitHub.CSharp.Protch.Interfaces;
using Autofac;
using LibGit2Sharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using IContainer = Autofac.IContainer;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Test {
    [TestClass]
    public class NugetPackageToPushFinderTest {
        protected static TestTargetFolder PakledCoreTarget = new TestTargetFolder(nameof(NugetPackageToPushFinderTest), "PakledCore");
        protected static TestTargetFolder ChabStandardTarget = new TestTargetFolder(nameof(NugetPackageToPushFinderTest), "ChabStandard");
        private static IContainer vContainer;
        protected static TestTargetInstaller TargetInstaller;
        protected static TestTargetRunner TargetRunner;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context) {
            vContainer = new ContainerBuilder().UseGittyTestUtilities().UseProtch().UseFusionNuclideProtchAndGitty(new DummyCsArgumentPrompter()).Build();
            TargetInstaller = vContainer.Resolve<TestTargetInstaller>();
            TargetRunner = vContainer.Resolve<TestTargetRunner>();
            TargetInstaller.DeleteCakeFolder(PakledCoreTarget);
            TargetInstaller.CreateCakeFolder(PakledCoreTarget, out var errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsPlusRelevantInfos());
            TargetInstaller.DeleteCakeFolder(ChabStandardTarget);
            TargetInstaller.CreateCakeFolder(ChabStandardTarget, out errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsPlusRelevantInfos());
        }

        [ClassCleanup]
        public static void ClassCleanup() {
            TargetInstaller.DeleteCakeFolder(PakledCoreTarget);
            TargetInstaller.DeleteCakeFolder(ChabStandardTarget);
        }

        [TestInitialize]
        public void Initialize() {
            PakledCoreTarget.Delete();
            ChabStandardTarget.Delete();
        }

        [TestCleanup]
        public void TestCleanup() {
            PakledCoreTarget.Delete();
            ChabStandardTarget.Delete();
        }

        [TestMethod]
        public async Task CanFindNugetPackagesToPushForPakled() {
            var errorsAndInfos = new ErrorsAndInfos();

            var nugetFeed = await GetNugetFeedAsync(errorsAndInfos);

            CloneTarget(PakledCoreTarget, errorsAndInfos);

            RunCakeScript(PakledCoreTarget, true, errorsAndInfos);

            errorsAndInfos = new ErrorsAndInfos();
            var sut = vContainer.Resolve<INugetPackageToPushFinder>();
            var packageToPush = await sut.FindPackageToPushAsync(NugetFeed.AspenlaubLocalFeed, PakledCoreTarget.Folder().ParentFolder().SubFolder(PakledCoreTarget.SolutionId + @"Bin\Release"), PakledCoreTarget.Folder(), PakledCoreTarget.Folder().SubFolder("src").FullName + @"\" + PakledCoreTarget.SolutionId + ".sln", errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
            var source = nugetFeed.UrlOrResolvedFolder(vContainer.Resolve<IFolderResolver>(), errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
            Assert.AreEqual(source, packageToPush.FeedUrl);
        }

        private static async Task<NugetFeed> GetNugetFeedAsync(IErrorsAndInfos errorsAndInfos) {
            var nugetFeedsSecret = new SecretNugetFeeds();
            var nugetFeeds = await vContainer.Resolve<ISecretRepository>().GetAsync(nugetFeedsSecret, errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
            var nugetFeed = nugetFeeds.FirstOrDefault(f => f.Id == NugetFeed.AspenlaubLocalFeed);
            Assert.IsNotNull(nugetFeed);
            return nugetFeed;
        }

        [TestMethod]
        public async Task PackageForTheSameCommitIsNotPushed() {
            var errorsAndInfos = new ErrorsAndInfos();

            CloneTarget(PakledCoreTarget, errorsAndInfos);

            var packages = await vContainer.Resolve<INugetFeedLister>().ListReleasedPackagesAsync(NugetFeed.AspenlaubLocalFeed, @"Aspenlaub.Net.GitHub.CSharp." + PakledCoreTarget.SolutionId, errorsAndInfos);
            if (errorsAndInfos.AnyErrors()) { return; }
            if (!packages.Any()) { return; }

            var latestPackageVersion = packages.Max(p => p.Identity.Version.Version);
            var latestPackage = packages.First(p => p.Identity.Version.Version == latestPackageVersion);

            var headTipIdSha = vContainer.Resolve<IGitUtilities>().HeadTipIdSha(PakledCoreTarget.Folder());
            if (!latestPackage.Tags.Contains(headTipIdSha)) {
                return; // $"No package has been pushed for {headTipIdSha} and {PakledCoreTarget.SolutionId}, please run build.cake for this solution"
            }

            RunCakeScript(PakledCoreTarget, false, errorsAndInfos);

            packages = await vContainer.Resolve<INugetFeedLister>().ListReleasedPackagesAsync(NugetFeed.AspenlaubLocalFeed, @"Aspenlaub.Net.GitHub.CSharp." + PakledCoreTarget.SolutionId, errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
            Assert.AreEqual(latestPackageVersion, packages.Max(p => p.Identity.Version.Version));
        }

        private static void CloneTarget(ITestTargetFolder testTargetFolder, IErrorsAndInfos errorsAndInfos) {
            var gitUtilities = new GitUtilities();
            var url = "https://github.com/aspenlaub/" + testTargetFolder.SolutionId + ".git";
            gitUtilities.Clone(url, "master", testTargetFolder.Folder(), new CloneOptions { BranchName = "master" }, true, errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
        }

        private void RunCakeScript(ITestTargetFolder testTargetFolder, bool disableNugetPush, IErrorsAndInfos errorsAndInfos) {
            var projectLogic = vContainer.Resolve<IProjectLogic>();
            var projectFactory = vContainer.Resolve<IProjectFactory>();
            var solutionFileFullName = testTargetFolder.Folder().SubFolder("src").FullName + '\\' + testTargetFolder.SolutionId + ".sln";
            var projectErrorsAndInfos = new ErrorsAndInfos();
            Assert.IsTrue(projectLogic.DoAllNetStandardOrCoreConfigurationsHaveNuspecs(projectFactory.Load(solutionFileFullName, solutionFileFullName.Replace(".sln", ".csproj"), projectErrorsAndInfos)));

            var target = disableNugetPush ? "IgnoreOutdatedBuildCakePendingChangesAndDoNotPush" : "IgnoreOutdatedBuildCakePendingChanges";
            TargetRunner.RunBuildCakeScript(BuildCake.Standard, testTargetFolder, target, errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
        }

        [TestMethod]
        public async Task CanFindNugetPackagesToPushForChabStandard() {
            var errorsAndInfos = new ErrorsAndInfos();
            var nugetFeed = await GetNugetFeedAsync(errorsAndInfos);

            CloneTarget(ChabStandardTarget, errorsAndInfos);

            RunCakeScript(ChabStandardTarget, true, errorsAndInfos);

            Assert.IsFalse(errorsAndInfos.Infos.Any(i => i.Contains("No test")));

            errorsAndInfos = new ErrorsAndInfos();
            var sut = vContainer.Resolve<INugetPackageToPushFinder>();
            var packageToPush = await sut.FindPackageToPushAsync(NugetFeed.AspenlaubLocalFeed, ChabStandardTarget.Folder().ParentFolder().SubFolder(ChabStandardTarget.SolutionId + @"Bin\Release"), ChabStandardTarget.Folder(), ChabStandardTarget.Folder().SubFolder("src").FullName + @"\" + ChabStandardTarget.SolutionId + ".sln", errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
            var source = nugetFeed.UrlOrResolvedFolder(vContainer.Resolve<IFolderResolver>(), errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
            Assert.AreEqual(source, packageToPush.FeedUrl);
        }
    }
}
