using Aspenlaub.Net.GitHub.CSharp.Fusion.Components;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
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
        IContainer container = new ContainerBuilder().UseFusionNuclideProtchAndGitty("Gitty").Build();
        Sut = container.Resolve<IDotNetCakeInstaller>();
    }

    [TestMethod]
    public void CanInstallGlobalDotNetCakeIfNecessary() {
        var errorsAndInfos = new ErrorsAndInfos();
        Sut.InstallOrUpdateGlobalDotNetCakeIfNecessary(errorsAndInfos, out bool inconclusive);
        if (inconclusive) {
            Assert.Inconclusive();
        } else {
            Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsToString());
        }
    }

    [TestMethod]
    public void ProvenGlobalDotNetCakeIsInstalled() {
        var errorsAndInfos = new ErrorsAndInfos();
        bool isInstalled = Sut.IsProvenGlobalDotNetCakeInstalled(errorsAndInfos);
        Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsToString());
        Assert.IsTrue(isInstalled);
    }

    [TestMethod]
    public void CanCheckIfGlobalCakeToolVersionMatchesTargetFramework() {
        var errorsAndInfos = new ErrorsAndInfos();
        bool matches = Sut.DoesGlobalCakeToolVersionMatchTargetFramework(false, errorsAndInfos);
        if (matches) {
            Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsToString());
        }
        Assert.AreEqual(DotNetCakeInstaller.CakeToolVersionMatchingCompiledTargetFramework ==
            DotNetCakeInstaller.ProvenCakeToolVersion, matches);
    }
}