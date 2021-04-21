﻿using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Entities;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Components;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Autofac;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Test {
    [TestClass]
    public class ManuallyUpdatedPackagesTest {
        [TestMethod]
        public async Task CanGetManuallyUpdatedPackages() {
            var errorsAndInfos = new ErrorsAndInfos();
            var secret = new SecretManuallyUpdatedPackages();
            var container = new ContainerBuilder().UsePegh(new DummyCsArgumentPrompter()).Build();
            var manuallyUpdatedPackages = await container.Resolve<ISecretRepository>().GetAsync(secret, errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsPlusRelevantInfos());
            Assert.IsNotNull(manuallyUpdatedPackages);
        }
    }
}
