﻿using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Gitty;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Components;
using Autofac;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Test;

[TestClass]
public class AutoCommitterAndPusherTest {

    [TestMethod]
    public void CanConstructAutoCommitterAndPusher() {
        var container = new ContainerBuilder().UseGittyTestUtilities().UseFusionNuclideProtchAndGitty("Fusion", new DummyCsArgumentPrompter()).Build();
        Assert.IsNotNull(container.Resolve<IAutoCommitterAndPusher>());
    }
}