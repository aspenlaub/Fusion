using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Gitty.TestUtilities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Autofac;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Test {
    [TestClass]
    public class AutoCommitterAndPusherTest {

        [TestMethod]
        public void CanConstructAutoCommitterAndPusher() {
            var container = new ContainerBuilder().UseGittyTestUtilities().UseFusionNuclideProtchAndGitty().Build();
            Assert.IsNotNull(container.Resolve<IAutoCommitterAndPusher>());
        }

        [TestMethod]
        public async Task CanAutoCommitAndPushCake() {
            var container = new ContainerBuilder().UseGittyTestUtilities().UseFusionNuclideProtchAndGitty().Build();
            var autoCommitterAndPusher = container.Resolve<IAutoCommitterAndPusher>();
            await autoCommitterAndPusher.AutoCommitAndPushSingleCakeFileAsync(new Folder(@"D:\Users\Wolfgang\GitHub\ChabStandard")); // ToDo: remove this if it works
        }
    }
}
