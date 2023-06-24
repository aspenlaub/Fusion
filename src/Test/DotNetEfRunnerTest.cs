using System.Collections.Generic;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Extensions;
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
    public void CanDropAndUpdateDatabase() {
        var projectFolder = DotNetEfToyTarget.Folder().SubFolder("src");

        DropDatabase(Sut, projectFolder);
        VerifyMigrationIds(Sut, projectFolder, new List<string>());

        UpdateDatabase(Sut, projectFolder, DotNetEfToy702MigrationId);
        VerifyMigrationIds(Sut, projectFolder, new List<string> { DotNetEfToy702MigrationId });

        UpdateDatabase(Sut, projectFolder, "");
        VerifyMigrationIds(Sut, projectFolder, new List<string> { DotNetEfToy702MigrationId });
    }

    [TestMethod]
    public void CanAddDummyMigration() {
        var projectFolder = DotNetEfToyTarget.Folder().SubFolder("src");

        DropDatabase(Sut, projectFolder);
        UpdateDatabase(Sut, projectFolder, DotNetEfToy702MigrationId);
        VerifyMigrationIds(Sut, projectFolder, new List<string> { DotNetEfToy702MigrationId });

        AddMigration(Sut, projectFolder, DotNetEfToyDummyMigrationId);

        UpdateDatabase(Sut, projectFolder, DotNetEfToyDummyMigrationId);
        VerifyMigrationIds(Sut, projectFolder,
                           new List<string> { DotNetEfToy702MigrationId, DotNetEfToyDummyMigrationId });
    }
}