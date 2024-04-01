using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Components;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using Autofac;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Test;

[TestClass]
public class DotNetEfInstallerTest {
    private readonly IContainer _Container = new ContainerBuilder().UseFusionNuclideProtchAndGitty("Fusion", new DummyCsArgumentPrompter()).Build();

    protected IDotNetEfInstaller Sut;

    [TestInitialize]
    public void Initialize() {
        var container = new ContainerBuilder().UseFusionNuclideProtchAndGitty("Fusion", new DummyCsArgumentPrompter()).Build();
        Sut = container.Resolve<IDotNetEfInstaller>();
    }

    [TestMethod]
    public void CanInstallGlobalDotNetEfIfNecessary() {
        var simpleLogger = _Container.Resolve<ISimpleLogger>();
        using (simpleLogger.BeginScope(SimpleLoggingScopeId.Create(nameof(CanInstallGlobalDotNetEfIfNecessary)))) {
            var errorsAndInfos = new ErrorsAndInfos();
            Sut.InstallOrUpdateGlobalDotNetEfIfNecessary(errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsToString());
        }
    }

    [TestMethod]
    public void GlobalDotNetEfIsInstalled() {
        var simpleLogger = _Container.Resolve<ISimpleLogger>();
        using (simpleLogger.BeginScope(SimpleLoggingScopeId.Create(nameof(GlobalDotNetEfIsInstalled)))) {
            var errorsAndInfos = new ErrorsAndInfos();
            var isInstalled = Sut.IsCurrentGlobalDotNetEfInstalled(errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsToString());
            Assert.IsTrue(isInstalled);
        }
    }
}