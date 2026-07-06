using System.Collections.Generic;
using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using Autofac;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Test;

[TestClass]
public class DotNetEfRunnerTest : DotNetEfTestBase {
    protected IDotNetEfRunner Sut;

    [TestCleanup]
    public void TestCleanup() {
        DotNetEfToyTarget.Delete();
        DotNetEfToyTarget2.Delete();
    }

    [TestInitialize]
    public void Initialize() {
        InitializeTarget();

        Sut = Container.Resolve<IDotNetEfRunner>();
    }

    [TestMethod]
    public async Task CanDropAndUpdateDatabase() {
        ISimpleLogger simpleLogger = Container.Resolve<ISimpleLogger>();
        using (simpleLogger.BeginScope(SimpleLoggingScopeId.Create(nameof(CanDropAndUpdateDatabase)))) {
            IFolder projectFolder = DotNetEfToyTarget.Folder().SubFolder("src");

            await DropDatabaseAsync(Sut, projectFolder);
            await VerifyMigrationIdsAsync(Sut, projectFolder, new List<string>());

            await UpdateDatabaseAsync(Sut, projectFolder, DotNetEfToy702MigrationId);
            await VerifyMigrationIdsAsync(Sut, projectFolder, new List<string> { DotNetEfToy702MigrationId });

            await UpdateDatabaseAsync(Sut, projectFolder, "");
            await VerifyMigrationIdsAsync(Sut, projectFolder, new List<string> { DotNetEfToy702MigrationId });
        }
    }

    [TestMethod]
    public async Task CanAddDummyMigration() {
        ISimpleLogger simpleLogger = Container.Resolve<ISimpleLogger>();
        using (simpleLogger.BeginScope(SimpleLoggingScopeId.Create(nameof(CanAddDummyMigration)))) {
            IFolder projectFolder = DotNetEfToyTarget.Folder().SubFolder("src");

            await DropDatabaseAsync(Sut, projectFolder);
            await UpdateDatabaseAsync(Sut, projectFolder, DotNetEfToy702MigrationId);
            await VerifyMigrationIdsAsync(Sut, projectFolder, new List<string> { DotNetEfToy702MigrationId });

            await AddMigrationAsync(Sut, projectFolder, DotNetEfToyDummyMigrationId);

            await UpdateDatabaseAsync(Sut, projectFolder, DotNetEfToyDummyMigrationId);
            await VerifyMigrationIdsAsync(Sut, projectFolder,
                new List<string> { DotNetEfToy702MigrationId, DotNetEfToyDummyMigrationId });
        }
    }
}