using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Gitty;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Gitty.TestUtilities;
using Aspenlaub.Net.GitHub.CSharp.Gitty.TestUtilities.Aspenlaub.Net.GitHub.CSharp.Gitty.TestUtilities;
using Aspenlaub.Net.GitHub.CSharp.Nuclide.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Components;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Autofac;
using LibGit2Sharp;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Test;

[TestClass]
public class NugetPackageUpdaterTest {
    private static readonly TestTargetFolder _pakledConsumerTarget
        = new(nameof(NugetPackageUpdaterTest), "PakledConsumer");
    private const string _pakledConsumerHeadTipSha = "2e3b17e227446bf20abb91cfc8c19bdd123fa2da"; // Before Pakled update
    private const string _pakledVersion = "2.4.1960.559"; // Before Pakled update
    private static IContainer _container;

    [ClassInitialize]
    public static void ClassInitialize(TestContext context) {
        _container = new ContainerBuilder().UseGittyTestUtilities().UseFusionNuclideProtchAndGitty("Fusion", new DummyCsArgumentPrompter()).Build();
    }

    [TestInitialize]
    public void Initialize() {
        _pakledConsumerTarget.Delete();
        var gitUtilities = _container.Resolve<IGitUtilities>();
        var url = "https://github.com/aspenlaub/" + _pakledConsumerTarget.SolutionId + ".git";
        var errorsAndInfos = new ErrorsAndInfos();
        gitUtilities.Clone(url, "master", _pakledConsumerTarget.Folder(), new CloneOptions { BranchName = "master" }, true, errorsAndInfos);
        Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
    }

    [TestCleanup]
    public void TestCleanup() {
        _pakledConsumerTarget.Delete();
    }

    [TestMethod]
    public async Task CanIdentifyNugetPackageOpportunity() {
        var simpleLogger = _container.Resolve<ISimpleLogger>();
        using (simpleLogger.BeginScope(SimpleLoggingScopeId.Create(nameof(CanIdentifyNugetPackageOpportunity)))) {
            var gitUtilities = _container.Resolve<IGitUtilities>();
            var errorsAndInfos = new ErrorsAndInfos();
            gitUtilities.Reset(_pakledConsumerTarget.Folder(), _pakledConsumerHeadTipSha, errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
            var yesNo = await NugetUpdateOpportunitiesAsync(_pakledConsumerTarget, errorsAndInfos);
            Assert.IsTrue(yesNo);
            Assert.IsTrue(errorsAndInfos.Infos.Any(i => i.Contains($"package Pakled from {_pakledVersion}")));
            var packageUpdateOpportunity = await EntityFrameworkNugetUpdateOpportunitiesAsync(errorsAndInfos);
            Assert.IsFalse(packageUpdateOpportunity.YesNo);
            Assert.IsTrue(string.IsNullOrEmpty(packageUpdateOpportunity.PotentialMigrationId));
        }
    }

    [TestMethod]
    public async Task CanUpdateNugetPackagesWithCsProjAndConfigChanges() {
        var simpleLogger = _container.Resolve<ISimpleLogger>();
        using (simpleLogger.BeginScope(SimpleLoggingScopeId.Create(nameof(CanUpdateNugetPackagesWithCsProjAndConfigChanges)))) {
            var gitUtilities = _container.Resolve<IGitUtilities>();
            var errorsAndInfos = new ErrorsAndInfos();
            gitUtilities.Reset(_pakledConsumerTarget.Folder(), _pakledConsumerHeadTipSha, errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
            var packageReferencesScanner = _container.Resolve<IPackageReferencesScanner>();
            var dependencyErrorsAndInfos = new ErrorsAndInfos();
            var dependencyIdsAndVersions =
                await packageReferencesScanner.DependencyIdsAndVersionsAsync(_pakledConsumerTarget.Folder().SubFolder("src").FullName, true, false, dependencyErrorsAndInfos);
            MakeCsProjAndConfigChange();
            errorsAndInfos = new ErrorsAndInfos();
            var yesNoInconclusive = await UpdateNugetPackagesAsync(_pakledConsumerTarget.Folder(), errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
            Assert.IsTrue(yesNoInconclusive.YesNo);
            Assert.IsFalse(yesNoInconclusive.Inconclusive);
            errorsAndInfos = new ErrorsAndInfos();
            yesNoInconclusive.YesNo = await NugetUpdateOpportunitiesAsync(_pakledConsumerTarget, errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
            Assert.IsFalse(yesNoInconclusive.YesNo);
            var dependencyIdsAndVersionsAfterUpdate =
                await packageReferencesScanner.DependencyIdsAndVersionsAsync(_pakledConsumerTarget.Folder().SubFolder("src").FullName, true, false, dependencyErrorsAndInfos);
            Assert.AreEqual(dependencyIdsAndVersions.Count, dependencyIdsAndVersionsAfterUpdate.Count,
                            $"Project had {dependencyIdsAndVersions.Count} package/-s before update, {dependencyIdsAndVersionsAfterUpdate.Count} afterwards");
            Assert.IsTrue(dependencyIdsAndVersions.All(i => dependencyIdsAndVersionsAfterUpdate.ContainsKey(i.Key)), "Package id/-s have changed");
            Assert.IsTrue(dependencyIdsAndVersions.Any(i => dependencyIdsAndVersionsAfterUpdate[i.Key].ToString() != i.Value.ToString()), "No package update was made");
        }
    }

    [TestMethod]
    public async Task CanDetermineThatThereIsNoNugetPackageToUpdateWithCsProjAndConfigChanges() {
        var simpleLogger = _container.Resolve<ISimpleLogger>();
        using (simpleLogger.BeginScope(SimpleLoggingScopeId.Create(nameof(CanDetermineThatThereIsNoNugetPackageToUpdateWithCsProjAndConfigChanges)))) {
            var errorsAndInfos = new ErrorsAndInfos();
            var yesNoInconclusive = await UpdateNugetPackagesAsync(_pakledConsumerTarget.Folder(), errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
            if (yesNoInconclusive.YesNo) {
                return;
            }

            MakeCsProjAndConfigChange();
            errorsAndInfos = new ErrorsAndInfos();
            yesNoInconclusive = await UpdateNugetPackagesAsync(_pakledConsumerTarget.Folder(), errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
            Assert.IsFalse(yesNoInconclusive.YesNo);
            Assert.IsFalse(yesNoInconclusive.Inconclusive);
        }
    }

    [TestMethod]
    public async Task ErrorWhenAskedToUpdateNugetPackagesWithCsChange() {
        var simpleLogger = _container.Resolve<ISimpleLogger>();
        using (simpleLogger.BeginScope(SimpleLoggingScopeId.Create(nameof(ErrorWhenAskedToUpdateNugetPackagesWithCsChange)))) {
            MakeCsChange();
            var errorsAndInfos = new ErrorsAndInfos();
            var yesNoInconclusive = await UpdateNugetPackagesAsync(_pakledConsumerTarget.Folder(), errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
            Assert.IsFalse(yesNoInconclusive.YesNo);
            Assert.IsTrue(yesNoInconclusive.Inconclusive);
        }
    }

    private async Task<bool> NugetUpdateOpportunitiesAsync(ITestTargetFolder target, IErrorsAndInfos errorsAndInfos) {
        var sut = _container.Resolve<INugetPackageUpdater>();
        var yesNo = await sut.AreThereNugetUpdateOpportunitiesAsync(
            target.Folder(), "master", errorsAndInfos);
        Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
        var yesNo2 = await sut.AreThereNugetUpdateOpportunitiesForSolutionAsync(
            target.Folder().SubFolder("src"), "master", errorsAndInfos);
        Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
        return yesNo && yesNo2;
    }

    private async Task<IPackageUpdateOpportunity> EntityFrameworkNugetUpdateOpportunitiesAsync(IErrorsAndInfos errorsAndInfos) {
        var sut = _container.Resolve<INugetPackageUpdater>();
        var packageUpdateOpportunity = await sut.AreThereEntityFrameworkNugetUpdateOpportunitiesAsync(
            _pakledConsumerTarget.Folder(), "master", errorsAndInfos);
        Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
        return packageUpdateOpportunity;
    }

    private async Task<YesNoInconclusive> UpdateNugetPackagesAsync(IFolder targetFolder, IErrorsAndInfos errorsAndInfos) {
        var sut = _container.Resolve<INugetPackageUpdater>();
        var yesNoInconclusive = await sut.UpdateNugetPackagesInRepositoryAsync(targetFolder, "master", errorsAndInfos);
        Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
        return yesNoInconclusive;
    }

    private void MakeCsChange() {
        File.WriteAllText(_pakledConsumerTarget.FullName() + @"\src\Cs.cs", "Cs.cs");
    }

    private void MakeCsProjAndConfigChange() {
        File.WriteAllText(_pakledConsumerTarget.FullName() + @"\src\CsProj.csproj", "CsProj.csproj");
        File.WriteAllText(_pakledConsumerTarget.FullName() + @"\src\Config.config", "Config.config");
    }
}