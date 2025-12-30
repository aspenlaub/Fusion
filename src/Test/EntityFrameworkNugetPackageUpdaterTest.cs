using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Gitty.TestUtilities;
using Aspenlaub.Net.GitHub.CSharp.Gitty.TestUtilities.Aspenlaub.Net.GitHub.CSharp.Gitty.TestUtilities;
using Aspenlaub.Net.GitHub.CSharp.Nuclide.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
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
        ISimpleLogger simpleLogger = Container.Resolve<ISimpleLogger>();
        using (simpleLogger.BeginScope(SimpleLoggingScopeId.Create(nameof(EntityFrameworkUpdateIsAvailable)))) {
            var errorsAndInfos = new ErrorsAndInfos();
            IPackageUpdateOpportunity packageUpdateOpportunity = await EntityFrameworkNugetUpdateOpportunitiesAsync(DotNetEfToyTarget, errorsAndInfos);
            Assert.IsTrue(packageUpdateOpportunity.YesNo);
            string potentialMigrationId = packageUpdateOpportunity.PotentialMigrationId;
            Assert.StartsWith("MicrosoftEntityFrameworkCoreTools", potentialMigrationId);
            Assert.AreNotEqual("MicrosoftEntityFrameworkCoreTools7050", potentialMigrationId);
            const string expectedInfo = "Could update package Microsoft.EntityFrameworkCore.Tools from 7.0.2 to 7.";
            Assert.IsTrue(errorsAndInfos.Infos.Any(i => i.StartsWith(expectedInfo)));
        }
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
        ISimpleLogger simpleLogger = Container.Resolve<ISimpleLogger>();
        string id = nameof(CanUpdateEntityFramework) + testTargetFolder.SolutionId + lastMigrationIdBeforeUpdate;
        using (simpleLogger.BeginScope(SimpleLoggingScopeId.Create(id))) {
            IPackageReferencesScanner packageReferencesScanner = Container.Resolve<IPackageReferencesScanner>();
            var dependencyErrorsAndInfos = new ErrorsAndInfos();
            IFolder projectFolder = testTargetFolder.Folder().SubFolder("src");
            IDictionary<string, string> dependencyIdsAndVersions = await packageReferencesScanner.DependencyIdsAndVersionsAsync(projectFolder.FullName, true, false, dependencyErrorsAndInfos);

            IDotNetEfRunner dotNetEfRunner = Container.Resolve<IDotNetEfRunner>();

            IList<string> migrationIdsBeforeUpdate = ListAppliedMigrationIds(dotNetEfRunner, projectFolder);

            if (migrationIdsBeforeUpdate.Count > 0 && migrationIdsBeforeUpdate[^1] != lastMigrationIdBeforeUpdate) {
                if (migrationIdsBeforeUpdate.Contains(lastMigrationIdBeforeUpdate)) {
                    DropDatabase(dotNetEfRunner, projectFolder);
                }

                UpdateDatabase(dotNetEfRunner, projectFolder, lastMigrationIdBeforeUpdate);
            }

            migrationIdsBeforeUpdate = ListAppliedMigrationIds(dotNetEfRunner, projectFolder);

            YesNoInconclusive yesNoInconclusive = await UpdateEntityFrameworkPackagesAsync(testTargetFolder);
            Assert.IsTrue(yesNoInconclusive.YesNo);
            Assert.IsFalse(yesNoInconclusive.Inconclusive);

            var errorsAndInfos = new ErrorsAndInfos();
            IPackageUpdateOpportunity packageUpdateOpportunity = await EntityFrameworkNugetUpdateOpportunitiesAsync(testTargetFolder, errorsAndInfos);
            Assert.IsFalse(packageUpdateOpportunity.YesNo);
            Assert.IsTrue(string.IsNullOrEmpty(packageUpdateOpportunity.PotentialMigrationId));
            Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsToString());

            IList<string> migrationIdsAfterUpdate = ListAppliedMigrationIds(dotNetEfRunner, projectFolder);

            Assert.HasCount(migrationIdsBeforeUpdate.Count + 1, migrationIdsAfterUpdate,
                            "One added and applied migration was expected");

            VerifyMigrationIds(migrationIdsBeforeUpdate,
                               migrationIdsAfterUpdate.Take(migrationIdsBeforeUpdate.Count).ToList());
            Assert.EndsWith(DotNetEfToyDummyMigrationId, migrationIdsAfterUpdate.Last());

            IDictionary<string, string> dependencyIdsAndVersionsAfterUpdate = await packageReferencesScanner.DependencyIdsAndVersionsAsync(projectFolder.FullName, true, false, dependencyErrorsAndInfos);
            Assert.HasCount(dependencyIdsAndVersions.Count, dependencyIdsAndVersionsAfterUpdate,
                            $"Project had {dependencyIdsAndVersions.Count} package/-s before update, {dependencyIdsAndVersionsAfterUpdate.Count} afterwards");
            Assert.IsTrue(dependencyIdsAndVersions.All(i => dependencyIdsAndVersionsAfterUpdate.ContainsKey(i.Key)), "Package id/-s have changed");
            Assert.IsTrue(dependencyIdsAndVersions.Any(i => dependencyIdsAndVersionsAfterUpdate[i.Key].ToString() != i.Value.ToString()), "No package update was made");
        }
    }

    private async Task<YesNoInconclusive> UpdateEntityFrameworkPackagesAsync(ITestTargetFolder testTargetFolder) {
        INugetPackageUpdater sut = Container.Resolve<INugetPackageUpdater>();
        var errorsAndInfos = new ErrorsAndInfos();
        YesNoInconclusive yesNoInconclusive = await sut.UpdateEntityFrameworkNugetPackagesInRepositoryAsync(testTargetFolder.Folder(),
                                                                                                            DotNetEfToyDummyMigrationId, "master", errorsAndInfos);
        Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
        return yesNoInconclusive;
    }
}