using System.Linq;
using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Gitty.TestUtilities;
using Aspenlaub.Net.GitHub.CSharp.Gitty.TestUtilities.Aspenlaub.Net.GitHub.CSharp.Gitty.TestUtilities;
using Aspenlaub.Net.GitHub.CSharp.Nuclide.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Extensions;
using Autofac;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Test;

[TestClass]
public class EntityFrameworkNugetPackageUpdaterTest : DotNetEfTestBase {
    [TestCleanup]
    public void TestCleanup() {
        DotNetEfToyTarget.Delete();
        DotNetEfToyTarget2.Delete();
    }

    [TestInitialize]
    public void Initialize() {
        InitializeTarget();
    }

    [TestMethod]
    public async Task EntityFrameworkUpdateIsAvailable() {
        var errorsAndInfos = new ErrorsAndInfos();
        var packageUpdateOpportunity = await EntityFrameworkNugetUpdateOpportunitiesAsync(DotNetEfToyTarget, errorsAndInfos);
        Assert.IsTrue(packageUpdateOpportunity.YesNo);
        var potentialMigrationId = packageUpdateOpportunity.PotentialMigrationId;
        Assert.IsTrue(potentialMigrationId.StartsWith("MicrosoftEntityFrameworkCoreTools"));
        Assert.AreNotEqual("MicrosoftEntityFrameworkCoreTools7050", potentialMigrationId);
        const string expectedInfo = "Could update package Microsoft.EntityFrameworkCore.Tools from 7.0.2";
        Assert.IsTrue(errorsAndInfos.Infos.Any(i => i.StartsWith(expectedInfo)));
    }

    [TestMethod]
    public async Task CanUpdateEntityFramework() {
        await CanUpdateEntityFramework(DotNetEfToyTarget, DotNetEfToy702MigrationId);
    }

    [TestMethod]
    public async Task CanUpdateEntityFramework2() {
        await CanUpdateEntityFramework(DotNetEfToyTarget2, DotNetEfToy705MigrationId);
    }

    private async Task CanUpdateEntityFramework(TestTargetFolder testTargetFolder, string lastMigrationIdBeforeUpdate) {
        var packageReferencesScanner = Container.Resolve<IPackageReferencesScanner>();
        var dependencyErrorsAndInfos = new ErrorsAndInfos();
        var projectFolder = testTargetFolder.Folder().SubFolder("src");
        var dependencyIdsAndVersions = await packageReferencesScanner.DependencyIdsAndVersionsAsync(projectFolder.FullName, true, false, dependencyErrorsAndInfos);

        var dotNetEfRunner = Container.Resolve<IDotNetEfRunner>();

        var migrationIdsBeforeUpdate = ListAppliedMigrationIds(dotNetEfRunner, projectFolder);

        if (migrationIdsBeforeUpdate.Count > 0 && migrationIdsBeforeUpdate[^1] != lastMigrationIdBeforeUpdate) {
            if (migrationIdsBeforeUpdate.Contains(lastMigrationIdBeforeUpdate)) {
                DropDatabase(dotNetEfRunner, projectFolder);
            }
            UpdateDatabase(dotNetEfRunner, projectFolder, lastMigrationIdBeforeUpdate);
        }

        migrationIdsBeforeUpdate = ListAppliedMigrationIds(dotNetEfRunner, projectFolder);

        var yesNoInconclusive = await UpdateEntityFrameworkPackagesAsync(testTargetFolder);
        Assert.IsTrue(yesNoInconclusive.YesNo);
        Assert.IsFalse(yesNoInconclusive.Inconclusive);

        var errorsAndInfos = new ErrorsAndInfos();
        var packageUpdateOpportunity = await EntityFrameworkNugetUpdateOpportunitiesAsync(testTargetFolder, errorsAndInfos);
        Assert.IsFalse(packageUpdateOpportunity.YesNo);
        Assert.IsTrue(string.IsNullOrEmpty(packageUpdateOpportunity.PotentialMigrationId));
        Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsToString());

        var migrationIdsAfterUpdate = ListAppliedMigrationIds(dotNetEfRunner, projectFolder);

        Assert.AreEqual(migrationIdsBeforeUpdate.Count + 1, migrationIdsAfterUpdate.Count,
            "One added and applied migration was expected");

        VerifyMigrationIds(migrationIdsBeforeUpdate,
            migrationIdsAfterUpdate.Take(migrationIdsBeforeUpdate.Count).ToList());
        Assert.IsTrue(migrationIdsAfterUpdate.Last().EndsWith(DotNetEfToyDummyMigrationId));

        var dependencyIdsAndVersionsAfterUpdate = await packageReferencesScanner.DependencyIdsAndVersionsAsync(projectFolder.FullName, true, false, dependencyErrorsAndInfos);
        Assert.AreEqual(dependencyIdsAndVersions.Count, dependencyIdsAndVersionsAfterUpdate.Count,
                        $"Project had {dependencyIdsAndVersions.Count} package/-s before update, {dependencyIdsAndVersionsAfterUpdate.Count} afterwards");
        Assert.IsTrue(dependencyIdsAndVersions.All(i => dependencyIdsAndVersionsAfterUpdate.ContainsKey(i.Key)), "Package id/-s have changed");
        Assert.IsTrue(dependencyIdsAndVersions.Any(i => dependencyIdsAndVersionsAfterUpdate[i.Key].ToString() != i.Value.ToString()), "No package update was made");
    }

    private async Task<YesNoInconclusive> UpdateEntityFrameworkPackagesAsync(ITestTargetFolder testTargetFolder) {
        var sut = Container.Resolve<INugetPackageUpdater>();
        var errorsAndInfos = new ErrorsAndInfos();
        var yesNoInconclusive = await sut.UpdateEntityFrameworkNugetPackagesInRepositoryAsync(testTargetFolder.Folder(),
            DotNetEfToyDummyMigrationId, "master", errorsAndInfos);
        Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
        return yesNoInconclusive;
    }
}