using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Gitty;
using Autofac;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Test;

[TestClass]
public class AutoCommitterAndPusherTest {

    [TestMethod]
    public void CanConstructAutoCommitterAndPusher() {
        IContainer container = new ContainerBuilder().UseGittyTestUtilities().UseFusionNuclideProtchAndGitty("Fusion").Build();
        Assert.IsNotNull(container.Resolve<IAutoCommitterAndPusher>());
    }
}