using System.Linq;
using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Extensions;
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
    }

    [TestInitialize]
    public void Initialize() {
        InitializeTarget();
    }

    [TestMethod]
    public async Task EntityFrameworkUpdateIsAvailable() {
        var errorsAndInfos = new ErrorsAndInfos();
        var packageUpdateOpportunity = await EntityFrameworkNugetUpdateOpportunitiesAsync(errorsAndInfos);
        Assert.IsTrue(packageUpdateOpportunity.YesNo);
        var potentialMigrationId = packageUpdateOpportunity.PotentialMigrationId;
        Assert.IsTrue(potentialMigrationId.StartsWith("MicrosoftEntityFrameworkCoreTools"));
        Assert.AreNotEqual("MicrosoftEntityFrameworkCoreTools7050", potentialMigrationId);
        const string expectedInfo = "Could update package Microsoft.EntityFrameworkCore.Tools from 7.0.2";
        Assert.IsTrue(errorsAndInfos.Infos.Any(i => i.StartsWith(expectedInfo)));
    }

    [TestMethod]
    public async Task CanUpdateEntityFramework() {
        var packageConfigsScanner = Container.Resolve<IPackageConfigsScanner>();
        var dependencyErrorsAndInfos = new ErrorsAndInfos();
        var projectFolder = DotNetEfToyTarget.Folder().SubFolder("src");
        var dependencyIdsAndVersions = await packageConfigsScanner.DependencyIdsAndVersionsAsync(projectFolder.FullName, true, false, dependencyErrorsAndInfos);

        var dotNetEfRunner = Container.Resolve<IDotNetEfRunner>();

        var migrationIdsBeforeUpdate = ListAppliedMigrationIds(dotNetEfRunner, projectFolder);

        var yesNoInconclusive = await UpdateEntityFrameworkPackagesAsync();
        Assert.IsTrue(yesNoInconclusive.YesNo);
        Assert.IsFalse(yesNoInconclusive.Inconclusive);

        var errorsAndInfos = new ErrorsAndInfos();
        var packageUpdateOpportunity = await EntityFrameworkNugetUpdateOpportunitiesAsync(errorsAndInfos);
        Assert.IsFalse(packageUpdateOpportunity.YesNo);
        Assert.IsTrue(string.IsNullOrEmpty(packageUpdateOpportunity.PotentialMigrationId));
        Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsToString());

        var migrationIdsAfterUpdate = ListAppliedMigrationIds(dotNetEfRunner, projectFolder);

        Assert.AreEqual(migrationIdsBeforeUpdate.Count + 1, migrationIdsAfterUpdate.Count,
            "One added and applied migration was expected");

        VerifyMigrationIds(migrationIdsBeforeUpdate,
            migrationIdsAfterUpdate.Take(migrationIdsBeforeUpdate.Count).ToList());
        Assert.IsTrue(migrationIdsAfterUpdate.Last().EndsWith(DotNetEfToyDummyMigrationId));

        var dependencyIdsAndVersionsAfterUpdate = await packageConfigsScanner.DependencyIdsAndVersionsAsync(projectFolder.FullName, true, false, dependencyErrorsAndInfos);
        Assert.AreEqual(dependencyIdsAndVersions.Count, dependencyIdsAndVersionsAfterUpdate.Count,
                        $"Project had {dependencyIdsAndVersions.Count} package/-s before update, {dependencyIdsAndVersionsAfterUpdate.Count} afterwards");
        Assert.IsTrue(dependencyIdsAndVersions.All(i => dependencyIdsAndVersionsAfterUpdate.ContainsKey(i.Key)), "Package id/-s have changed");
        Assert.IsTrue(dependencyIdsAndVersions.Any(i => dependencyIdsAndVersionsAfterUpdate[i.Key].ToString() != i.Value.ToString()), "No package update was made");
    }

    private async Task<YesNoInconclusive> UpdateEntityFrameworkPackagesAsync() {
        var sut = Container.Resolve<INugetPackageUpdater>();
        var errorsAndInfos = new ErrorsAndInfos();
        var yesNoInconclusive = await sut.UpdateEntityFrameworkNugetPackagesInRepositoryAsync(DotNetEfToyTarget.Folder(),
            DotNetEfToyDummyMigrationId, errorsAndInfos);
        Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
        return yesNoInconclusive;
    }
}