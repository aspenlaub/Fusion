﻿using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Gitty.TestUtilities;
using Aspenlaub.Net.GitHub.CSharp.Nuclide.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using Autofac;
using LibGit2Sharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using IContainer = Autofac.IContainer;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Test {
    [TestClass]
    public class NugetPackageUpdaterTest {
        private static readonly TestTargetFolder PakledConsumerCoreTarget = new TestTargetFolder(nameof(NugetPackageUpdaterTest), "PakledConsumerCore");
        private const string PakledConsumerCoreHeadTipSha = "e2012ba3bfbaff0ab985cd2629b1a5b368410ace"; // Before PakledCoreUpdate
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
            gitUtilities.Clone(url, "master", PakledConsumerCoreTarget.Folder(), new CloneOptions { BranchName = "master" }, true, errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
        }

        [TestCleanup]
        public void TestCleanup() {
            PakledConsumerCoreTarget.Delete();
        }

        [TestMethod]
        public async Task CanIdentifyNugetPackageOpportunity() {
            var gitUtilities = vContainer.Resolve<IGitUtilities>();
            var errorsAndInfos = new ErrorsAndInfos();
            gitUtilities.Reset(PakledConsumerCoreTarget.Folder(), PakledConsumerCoreHeadTipSha, errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
            var yesNo = await NugetUpdateOpportunitiesAsync(errorsAndInfos);
            Assert.IsTrue(yesNo);
            Assert.IsTrue(errorsAndInfos.Infos.Any(i => i.Contains("package Aspenlaub.Net.GitHub.CSharp.PakledCore from 1.0.6974.31928")));
        }

        [TestMethod]
        public async Task CanUpdateNugetPackagesWithCsProjAndConfigChanges() {
            var gitUtilities = vContainer.Resolve<IGitUtilities>();
            var errorsAndInfos = new ErrorsAndInfos();
            gitUtilities.Reset(PakledConsumerCoreTarget.Folder(), PakledConsumerCoreHeadTipSha, errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
            var packageConfigsScanner = vContainer.Resolve<IPackageConfigsScanner>();
            var dependencyErrorsAndInfos = new ErrorsAndInfos();
            var dependencyIdsAndVersions = await packageConfigsScanner.DependencyIdsAndVersionsAsync(PakledConsumerCoreTarget.Folder().SubFolder("src").FullName, true, false, dependencyErrorsAndInfos);
            MakeCsProjAndConfigChange();
            var yesNoInconclusive = await UpdateNugetPackagesAsync();
            Assert.IsTrue(yesNoInconclusive.YesNo);
            Assert.IsFalse(yesNoInconclusive.Inconclusive);
            yesNoInconclusive.YesNo = await NugetUpdateOpportunitiesAsync(errorsAndInfos);
            Assert.IsFalse(yesNoInconclusive.YesNo);
            var dependencyIdsAndVersionsAfterUpdate = await packageConfigsScanner.DependencyIdsAndVersionsAsync(PakledConsumerCoreTarget.Folder().SubFolder("src").FullName, true, false, dependencyErrorsAndInfos);
            Assert.AreEqual(dependencyIdsAndVersions.Count, dependencyIdsAndVersionsAfterUpdate.Count,
                $"Project had {dependencyIdsAndVersions.Count} package/-s before update, {dependencyIdsAndVersionsAfterUpdate.Count} afterwards");
            Assert.IsTrue(dependencyIdsAndVersions.All(i => dependencyIdsAndVersionsAfterUpdate.ContainsKey(i.Key)), "Package id/-s have changed");
            Assert.IsTrue(dependencyIdsAndVersions.Any(i => dependencyIdsAndVersionsAfterUpdate[i.Key].ToString() != i.Value.ToString()), "No package update was made");
        }

        [TestMethod]
        public async Task CanDetermineThatThereIsNoNugetPackageToUpdateWithCsProjAndConfigChanges() {
            var yesNoInconclusive = await UpdateNugetPackagesAsync();
            if (yesNoInconclusive.YesNo) { return; }

            MakeCsProjAndConfigChange();
            yesNoInconclusive = await UpdateNugetPackagesAsync();
            Assert.IsFalse(yesNoInconclusive.YesNo);
            Assert.IsFalse(yesNoInconclusive.Inconclusive);
        }

        [TestMethod]
        public async Task ErrorWhenAskedToUpdateNugetPackagesWithCsChange() {
            MakeCsChange();
            var yesNoInconclusive = await UpdateNugetPackagesAsync();
            Assert.IsFalse(yesNoInconclusive.YesNo);
            Assert.IsTrue(yesNoInconclusive.Inconclusive);
        }

        private async Task<bool> NugetUpdateOpportunitiesAsync(IErrorsAndInfos errorsAndInfos) {
            var sut = vContainer.Resolve<INugetPackageUpdater>();
            var yesNo = await sut.AreThereNugetUpdateOpportunitiesAsync(PakledConsumerCoreTarget.Folder(), errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
            return yesNo;
        }

        private async Task<IYesNoInconclusive> UpdateNugetPackagesAsync() {
            var sut = vContainer.Resolve<INugetPackageUpdater>();
            var errorsAndInfos = new ErrorsAndInfos();
            var yesNoInconclusive = await sut.UpdateNugetPackagesInRepositoryAsync(PakledConsumerCoreTarget.Folder(), errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
            return yesNoInconclusive;
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
