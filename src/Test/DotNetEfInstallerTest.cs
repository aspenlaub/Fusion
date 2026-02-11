using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using Autofac;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Test;

[TestClass]
public class DotNetEfInstallerTest {
    private readonly IContainer _Container = new ContainerBuilder().UseFusionNuclideProtchAndGitty("Fusion").Build();

    protected IDotNetEfInstaller Sut;

    [TestInitialize]
    public void Initialize() {
        IContainer container = new ContainerBuilder().UseFusionNuclideProtchAndGitty("Fusion").Build();
        Sut = container.Resolve<IDotNetEfInstaller>();
    }

    [TestMethod]
    public void CanInstallGlobalDotNetEfIfNecessary() {
        ISimpleLogger simpleLogger = _Container.Resolve<ISimpleLogger>();
        using (simpleLogger.BeginScope(SimpleLoggingScopeId.Create(nameof(CanInstallGlobalDotNetEfIfNecessary)))) {
            var errorsAndInfos = new ErrorsAndInfos();
            Sut.InstallOrUpdateGlobalDotNetEfIfNecessary(errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsToString());
        }
    }

    [TestMethod]
    public void GlobalDotNetEfIsInstalled() {
        ISimpleLogger simpleLogger = _Container.Resolve<ISimpleLogger>();
        using (simpleLogger.BeginScope(SimpleLoggingScopeId.Create(nameof(GlobalDotNetEfIsInstalled)))) {
            var errorsAndInfos = new ErrorsAndInfos();
            bool isInstalled = Sut.IsCurrentGlobalDotNetEfInstalled(errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsToString());
            Assert.IsTrue(isInstalled);
        }
    }
}