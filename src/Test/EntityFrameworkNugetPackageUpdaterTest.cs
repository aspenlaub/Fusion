using System.Linq;
using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Test;

[TestClass]
public class EntityFrameworkNugetPackageUpdaterTest : DotNetEfTestBase {
    [TestCleanup]
    public void TestCleanup() {
        DotNetEfToyTarget.Delete();
    }

    [TestInitialize]
    public void Initialize() {
        InitializeTarget();
    }

    [TestMethod]
    public async Task EntityFrameworkUpdateIsAvailable() {
        var errorsAndInfos = new ErrorsAndInfos();
        var yesNo = await EntityFrameworkNugetUpdateOpportunitiesAsync(errorsAndInfos);
        Assert.IsTrue(yesNo);
        const string expectedInfo = "Could update package Microsoft.EntityFrameworkCore.Tools from 7.0.2";
        Assert.IsTrue(errorsAndInfos.Infos.Any(i => i.StartsWith(expectedInfo)));
    }
}