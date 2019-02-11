using System.IO;
using System.Linq;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Gitty.TestUtilities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Autofac;
using LibGit2Sharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using IContainer = Autofac.IContainer;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Test {
    [TestClass]
    public class NugetPackageUpdaterTest {
        private static readonly TestTargetFolder PakledConsumerCoreTarget = new TestTargetFolder(nameof(NugetPackageUpdaterTest), "PakledConsumerCore");
        private const string HeadTipSha = "e2012ba3bfbaff0ab985cd2629b1a5b368410ace"; // Before PakledCoreUpdate
        private static IContainer vContainer;
        private static TestTargetInstaller vTargetInstaller;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context) {
            vContainer = new ContainerBuilder().UseGittyTestUtilities().UseFusionNuclideProtchAndGitty().Build();
            vTargetInstaller = vContainer.Resolve<TestTargetInstaller>();
            vTargetInstaller.DeleteCakeFolder(PakledConsumerCoreTarget);
            vTargetInstaller.CreateCakeFolder(PakledConsumerCoreTarget, out var errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
        }

        [TestInitialize]
        public void Initialize() {
            PakledConsumerCoreTarget.Delete();
            var gitUtilities = vContainer.Resolve<IGitUtilities>();
            var url = "https://github.com/aspenlaub/" + PakledConsumerCoreTarget.SolutionId + ".git";
            var errorsAndInfos = new ErrorsAndInfos();
            gitUtilities.Clone(url, PakledConsumerCoreTarget.Folder(), new CloneOptions { BranchName = "master" }, true, errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
        }

        [TestCleanup]
        public void TestCleanup() {
            PakledConsumerCoreTarget.Delete();
        }

        [TestMethod]
        public void CanUpdateNugetPackagesWithCsProjAndConfigChanges() {
            var gitUtilities = vContainer.Resolve<IGitUtilities>();
            var errorsAndInfos = new ErrorsAndInfos();
            gitUtilities.Reset(PakledConsumerCoreTarget.Folder(), HeadTipSha, errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
            MakeCsProjAndConfigChange();
            UpdateNugetPackages(out var yesNo, out var inconclusive);
            Assert.IsTrue(yesNo);
            Assert.IsFalse(inconclusive);
        }

        [TestMethod]
        public void CanDetermineThatThereIsNoNugetPackageToUpdateWithCsProjAndConfigChanges() {
            MakeCsProjAndConfigChange();
            UpdateNugetPackages(out var yesNo, out var inconclusive);
            Assert.IsFalse(yesNo);
            Assert.IsFalse(inconclusive);
        }

        [TestMethod]
        public void ErrorWhenAskedToUpdateNugetPackagesWithCsChange() {
            MakeCsChange();
            UpdateNugetPackages(out var yesNo, out var inconclusive);
            Assert.IsFalse(yesNo);
            Assert.IsTrue(inconclusive);
        }

        private void UpdateNugetPackages(out bool yesNo, out bool inconclusive) {
            var sut = vContainer.Resolve<INugetPackageUpdater>();
            var errorsAndInfos = new ErrorsAndInfos();
            sut.UpdateNugetPackagesInRepository(PakledConsumerCoreTarget.Folder(), out yesNo, out inconclusive, errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
        }

        private void MakeCsChange() {
            File.WriteAllText(PakledConsumerCoreTarget.FullName() + @"\src\Cs.cs", "Cs.cs");
        }

        private void MakeCsProjAndConfigChange() {
            File.WriteAllText(PakledConsumerCoreTarget.FullName() + @"\src\CsProj.csproj", "CsProj.csproj");
            File.WriteAllText(PakledConsumerCoreTarget.FullName() + @"\src\Config.config", "Config.config");
        }
    }
}
