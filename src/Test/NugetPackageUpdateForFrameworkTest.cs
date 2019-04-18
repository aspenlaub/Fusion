using System;
using System.Linq;
using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Gitty;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Entities;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Gitty.TestUtilities;
using Aspenlaub.Net.GitHub.CSharp.Nuclide.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using Autofac;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Test {
    [TestClass]
    public class NugetPackageUpdateForFrameworkTest {
        private static readonly TestTargetFolder WakekTarget = new TestTargetFolder(nameof(NugetPackageUpdaterTest), "Wakek");
        private const string WakekHeadTipSha = "cd4a867dd28c11e341efabcf31fac4b22fa2ef51"; // Before a couple of package updates
        private static IContainer vContainer;
        private static TestTargetInstaller vTargetInstaller;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context) {
            vContainer = new ContainerBuilder().UseGittyTestUtilities().UseFusionNuclideProtchAndGitty().Build();
            vTargetInstaller = vContainer.Resolve<TestTargetInstaller>();
            vTargetInstaller.DeleteCakeFolder(WakekTarget);
            vTargetInstaller.CreateCakeFolder(WakekTarget, out var errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
        }

        [TestInitialize]
        public void Initialize() {
            WakekTarget.Delete();
            var gitUtilities = vContainer.Resolve<IGitUtilities>();
            var errorsAndInfos = new ErrorsAndInfos();
            var url = "https://github.com/aspenlaub/" + WakekTarget.SolutionId + ".git";
            gitUtilities.Clone(url, "master", WakekTarget.Folder(), new CloneOptions { BranchName = "master" }, true, errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
        }

        [TestCleanup]
        public void TestCleanup() {
            WakekTarget.Delete();
        }

        [TestMethod, Ignore]
        public async Task CanUpdateNugetPackagesForFrameworkProject() {
            var simpleLogger = new SimpleLogger(new SimpleLogFlusher());
            var id = Guid.NewGuid().ToString();
            using (simpleLogger.BeginScope(SimpleLoggingScopeId.Create(nameof(CanUpdateNugetPackagesForFrameworkProject), id))) {
                simpleLogger.LogInformation("Resetting Wakek target folder");
                var gitUtilities = vContainer.Resolve<IGitUtilities>();
                var errorsAndInfos = new ErrorsAndInfos();
                gitUtilities.Reset(WakekTarget.Folder(), WakekHeadTipSha, errorsAndInfos);
                Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
                simpleLogger.LogInformation("Retrieving dependency ids and versions");
                var packageConfigsScanner = vContainer.Resolve<IPackageConfigsScanner>();
                var dependencyErrorsAndInfos = new ErrorsAndInfos();
                var dependencyIdsAndVersions = await packageConfigsScanner.DependencyIdsAndVersionsAsync(WakekTarget.Folder().SubFolder("src").FullName, true, true, dependencyErrorsAndInfos);
                simpleLogger.LogInformation("Updating nuget packages");
                var yesNoInconclusive = await UpdateNugetPackagesAsync();
                Assert.IsTrue(yesNoInconclusive.YesNo);
                Assert.IsFalse(yesNoInconclusive.Inconclusive);
                simpleLogger.LogInformation("Looking for nuget update opportunities, none expected");
                yesNoInconclusive.YesNo = await NugetUpdateOpportunitiesAsync(errorsAndInfos);
                Assert.IsFalse(yesNoInconclusive.YesNo);
                simpleLogger.LogInformation("Retrieving dependency ids and versions once more");
                var dependencyIdsAndVersionsAfterUpdate =
                    await packageConfigsScanner.DependencyIdsAndVersionsAsync(WakekTarget.Folder().SubFolder("src").FullName, true, true, dependencyErrorsAndInfos);
                Assert.AreEqual(dependencyIdsAndVersions.Count, dependencyIdsAndVersionsAfterUpdate.Count,
                    $"Project had {dependencyIdsAndVersions.Count} package/-s before update, {dependencyIdsAndVersionsAfterUpdate.Count} afterwards");
                Assert.IsTrue(dependencyIdsAndVersions.All(i => dependencyIdsAndVersionsAfterUpdate.ContainsKey(i.Key)), "Package id/-s have changed");
                Assert.IsTrue(dependencyIdsAndVersions.Any(i => dependencyIdsAndVersionsAfterUpdate[i.Key].ToString() != i.Value.ToString()), "No package update was made");
                simpleLogger.LogInformation("Success");
            }
        }

        private async Task<bool> NugetUpdateOpportunitiesAsync(IErrorsAndInfos errorsAndInfos) {
            var sut = vContainer.Resolve<INugetPackageUpdater>();
            var yesNo = await sut.AreThereNugetUpdateOpportunitiesAsync(WakekTarget.Folder(), errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
            return yesNo;
        }

        private async Task<IYesNoInconclusive> UpdateNugetPackagesAsync() {
            var sut = vContainer.Resolve<INugetPackageUpdater>();
            var errorsAndInfos = new ErrorsAndInfos();
            var yesNoInconclusive = await sut.UpdateNugetPackagesInRepositoryAsync(WakekTarget.Folder(), errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
            return yesNoInconclusive;
        }
    }
}
