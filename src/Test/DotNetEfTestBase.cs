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
    }

    protected void AddMigration(IDotNetEfRunner runner, IFolder projectFolder, string migrationId) {
        var errorsAndInfos = new ErrorsAndInfos();
        runner.AddMigration(projectFolder, migrationId, errorsAndInfos);
        const string expectedInfo = "Done.";
        Assert.IsTrue(errorsAndInfos.Infos.Any(i => i.StartsWith(expectedInfo)));
    }

    protected void VerifyMigrationIds(IDotNetEfRunner runner, IFolder projectFolder, IList<string> expectedMigrationIds) {
        var errorsAndInfos = new ErrorsAndInfos();
        var migrationIds = runner.ListAppliedMigrationIds(projectFolder, errorsAndInfos);
        Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
        Assert.AreEqual(expectedMigrationIds.Count, migrationIds.Count);
        for (var i = 0; i < expectedMigrationIds.Count; i++) {
            Assert.IsTrue(migrationIds[i].EndsWith(expectedMigrationIds[i]));
        }
    }

    protected void DropDatabase(IDotNetEfRunner runner, IFolder projectFolder) {
        var errorsAndInfos = new ErrorsAndInfos();
        runner.DropDatabase(projectFolder, errorsAndInfos);
        Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
        const string expectedInfo = $"Dropping database '{DotNetEfToyDatabaseName}'";
        Assert.IsTrue(errorsAndInfos.Infos.Any(i => i.StartsWith(expectedInfo)));
    }

    protected void UpdateDatabase(IDotNetEfRunner runner, IFolder projectFolder, string expectedMigration) {
        var errorsAndInfos = new ErrorsAndInfos();
        runner.UpdateDatabase(projectFolder, errorsAndInfos);
        Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
        const string expectedInfoStart = "Applying migration '";
        var expectedInfo = $"{expectedMigration}'";
        Assert.IsTrue(errorsAndInfos.Infos.Any(i
                                                   => i.StartsWith(expectedInfoStart) && i.Contains(expectedInfo)));
    }

    protected async Task<bool> EntityFrameworkNugetUpdateOpportunitiesAsync(IErrorsAndInfos errorsAndInfos) {
        var updater = Container.Resolve<INugetPackageUpdater>();
        var yesNo = await updater.AreThereEntityFrameworkNugetUpdateOpportunitiesAsync(
            DotNetEfToyTarget.Folder(), errorsAndInfos);
        Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
        return yesNo;
    }
}