using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Components;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Extensions;
using Autofac;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Test;

[TestClass]
public class DotNetCakeInstallerTest {
    protected IDotNetCakeInstaller Sut;

    [TestInitialize]
    public void Initialize() {
        IContainer container = new ContainerBuilder().UseFusionNuclideProtchAndGitty("Gitty", new DummyCsArgumentPrompter()).Build();
        Sut = container.Resolve<IDotNetCakeInstaller>();
    }

    [TestMethod]
    public void CanInstallGlobalDotNetCakeIfNecessary() {
        var errorsAndInfos = new ErrorsAndInfos();
        Sut.InstallOrUpdateGlobalDotNetCakeIfNecessary(errorsAndInfos);
        Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsToString());
    }

    [TestMethod]
    public void GlobalDotNetCakeIsInstalled() {
        var errorsAndInfos = new ErrorsAndInfos();
        bool isInstalled = Sut.IsCurrentGlobalDotNetCakeInstalled(errorsAndInfos);
        Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsToString());
        Assert.IsTrue(isInstalled);
    }
}