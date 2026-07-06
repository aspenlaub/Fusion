using System.Threading;
using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Seoa.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Skladasu.Entities;
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
    public async Task CanInstallGlobalDotNetEfIfNecessary() {
        ISimpleLogger simpleLogger = _Container.Resolve<ISimpleLogger>();
        using (simpleLogger.BeginScope(SimpleLoggingScopeId.Create(nameof(CanInstallGlobalDotNetEfIfNecessary)))) {
            var errorsAndInfos = new ErrorsAndInfos();
            await Sut.InstallOrUpdateGlobalDotNetEfIfNecessaryAsync(errorsAndInfos, CancellationToken.None);
            Assert.That.ThereWereNoErrors(errorsAndInfos);
        }
    }

    [TestMethod]
    public async Task GlobalDotNetEfIsInstalled() {
        ISimpleLogger simpleLogger = _Container.Resolve<ISimpleLogger>();
        using (simpleLogger.BeginScope(SimpleLoggingScopeId.Create(nameof(GlobalDotNetEfIsInstalled)))) {
            var errorsAndInfos = new ErrorsAndInfos();
            bool isInstalled = await Sut.IsCurrentGlobalDotNetEfInstalledAsync(errorsAndInfos, CancellationToken.None);
            Assert.That.ThereWereNoErrors(errorsAndInfos);
            Assert.IsTrue(isInstalled);
        }
    }
}