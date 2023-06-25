using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Gitty;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Gitty.TestUtilities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Components;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using Autofac;
using LibGit2Sharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Test;

public class DotNetEfTestBase {
    protected static readonly TestTargetFolder DotNetEfToyTarget
        = new(nameof(DotNetEfInstallerTest), "DotNetEfToy");
    protected const string DotNetEfToyHeadTipSha = "3dfb03d7b8747a29545b505085c03127a9179989";
    protected const string DotNetEfToyDatabaseName = "Aspenlaub.Net.GitHub.CSharp.DotNetEfToy.UnitTest";
    protected const string DotNetEfToy702MigrationId = "20230425060415_EntityFrameworkCore702";
    protected const string DotNetEfToyDummyMigrationId = "DummyEntityFrameworkCore";

    protected static readonly TestTargetFolder DotNetEfToyTarget2
        = new(nameof(DotNetEfInstallerTest) + "2", "DotNetEfToy");
    protected const string DotNetEfToyHeadTipSha2 = "254cc7ae90cc2c37b306d9d541187ae28d0f06e7";
    protected const string DotNetEfToy705MigrationId = "20230428080002_MicrosoftEntityFrameworkCoreTools705";

    private static IContainer PrivateContainer;
    public static IContainer Container
        => PrivateContainer ??= new ContainerBuilder().UseGittyTestUtilities().UseFusionNuclideProtchAndGitty("Fusion", new DummyCsArgumentPrompter()).Build();

    public void InitializeTarget() {
        DotNetEfToyTarget.Delete();
        var gitUtilities = Container.Resolve<IGitUtilities>();
        var url = "https://github.com/aspenlaub/" + DotNetEfToyTarget.SolutionId + ".git";
        var errorsAndInfos = new ErrorsAndInfos();
        gitUtilities.Clone(url, "master", DotNetEfToyTarget.Folder(), new CloneOptions { BranchName = "master" }, true, errorsAndInfos);
        Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
        gitUtilities.Reset(DotNetEfToyTarget.Folder(), DotNetEfToyHeadTipSha, errorsAndInfos);
        Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());

        DotNetEfToyTarget2.Delete();
        url = "https://github.com/aspenlaub/" + DotNetEfToyTarget2.SolutionId + ".git";
        errorsAndInfos = new ErrorsAndInfos();
        gitUtilities.Clone(url, "master", DotNetEfToyTarget2.Folder(), new CloneOptions { BranchName = "master" }, true, errorsAndInfos);
        Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
        gitUtilities.Reset(DotNetEfToyTarget2.Folder(), DotNetEfToyHeadTipSha2, errorsAndInfos);
        Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
    }

    protected void AddMigration(IDotNetEfRunner dotNetEfRunner, IFolder projectFolder, string migrationId) {
        var errorsAndInfos = new ErrorsAndInfos();
        dotNetEfRunner.AddMigration(projectFolder, migrationId, errorsAndInfos);
        const string expectedInfo = "Done.";
        Assert.IsTrue(errorsAndInfos.Infos.Any(i => i.StartsWith(expectedInfo)));
    }

    protected void VerifyMigrationIds(IDotNetEfRunner dotNetEfRunner, IFolder projectFolder, IList<string> expectedMigrationIds) {
        var actualMigrationIds = ListAppliedMigrationIds(dotNetEfRunner, projectFolder);
        VerifyMigrationIds(expectedMigrationIds, actualMigrationIds);
    }

    protected void VerifyMigrationIds(IList<string> expectedMigrationIds, IList<string> actualMigrationIds) {
        Assert.AreEqual(expectedMigrationIds.Count, actualMigrationIds.Count);
        for (var i = 0; i < expectedMigrationIds.Count; i++) {
            Assert.IsTrue(actualMigrationIds[i].EndsWith(expectedMigrationIds[i]));
        }
    }

    protected IList<string> ListAppliedMigrationIds(IDotNetEfRunner dotNetEfRunner, IFolder projectFolder) {
        var errorsAndInfos = new ErrorsAndInfos();
        var migrationIds = dotNetEfRunner.ListAppliedMigrationIds(projectFolder, errorsAndInfos);
        Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
        return migrationIds;
    }

    protected void DropDatabase(IDotNetEfRunner dotNetEfRunner, IFolder projectFolder) {
        var errorsAndInfos = new ErrorsAndInfos();
        dotNetEfRunner.DropDatabase(projectFolder, errorsAndInfos);
        Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
        const string expectedInfo = $"Dropping database '{DotNetEfToyDatabaseName}'";
        Assert.IsTrue(errorsAndInfos.Infos.Any(i => i.StartsWith(expectedInfo)));
    }

    protected void UpdateDatabase(IDotNetEfRunner dotNetEfRunner, IFolder projectFolder, string expectedMigration) {
        var errorsAndInfos = new ErrorsAndInfos();
        dotNetEfRunner.UpdateDatabase(projectFolder, errorsAndInfos);
        Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
        var expectedInfoStart = string.IsNullOrEmpty(expectedMigration)
            ? "No migrations were applied" : "Applying migration '";
        var expectedInfo = string.IsNullOrEmpty(expectedMigration)
            ? "database is already up to date" : $"{expectedMigration}'";
        Assert.IsTrue(errorsAndInfos.Infos.Any(i
            => i.StartsWith(expectedInfoStart) && i.Contains(expectedInfo)));
    }

    protected async Task<IPackageUpdateOpportunity> EntityFrameworkNugetUpdateOpportunitiesAsync(TestTargetFolder testTargetFolder, IErrorsAndInfos errorsAndInfos) {
        var updater = Container.Resolve<INugetPackageUpdater>();
        var packageUpdateOpportunity = await updater.AreThereEntityFrameworkNugetUpdateOpportunitiesAsync(
            testTargetFolder.Folder(), errorsAndInfos);
        Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
        return packageUpdateOpportunity;
    }
}