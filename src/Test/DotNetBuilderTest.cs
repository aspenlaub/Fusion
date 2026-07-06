using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Components;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Skladasu.Entities;
using Autofac;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Test;

[TestClass]
public class DotNetBuilderTest {
    private IDotNetBuilder _Sut;
    private IContainer _Container;
    private AutomationTestHelper _AutomationTestHelper;

    [TestInitialize]
    public void Initialize() {
        _AutomationTestHelper = new AutomationTestHelper(nameof(DotNetBuilderTest));
        _Container = new ContainerBuilder().UseFusionNuclideProtchAndGitty("Fusion").Build();
        _Sut = _Container.Resolve<IDotNetBuilder>();
    }

    [TestMethod]
    public async Task CanDebugBuildSolutionThatCompilesInDebug() {
        await CanOrCannotBuildAsync("Compiles", true, true);
    }

    [TestMethod]
    public async Task CanReleaseBuildSolutionThatCompilesInRelease() {
        await CanOrCannotBuildAsync("Compiles", false, true);
    }

    [TestMethod]
    public async Task CanDebugBuildSolutionThatCompilesInDebugOnly() {
        await CanOrCannotBuildAsync("CompilesInDebug", true, true);
    }

    [TestMethod]
    public async Task CannotReleaseBuildSolutionThatCompilesInDebugOnly() {
        await CanOrCannotBuildAsync("CompilesInDebug", false, false);
    }

    [TestMethod]
    public async Task CannotDebugBuildSolutionThatCompilesInReleaseOnly() {
        await CanOrCannotBuildAsync("CompilesInRelease", true, false);
    }

    [TestMethod]
    public async Task CanReleaseBuildSolutionThatCompilesInReleaseOnly() {
        await CanOrCannotBuildAsync("CompilesInRelease", false, true);
    }

    [TestMethod]
    public async Task CannotDebugBuildSolutionThatDoesNotCompile() {
        await CanOrCannotBuildAsync("DoesNotCompile", true, false);
    }

    [TestMethod]
    public async Task CannotReleaseBuildSolutionThatDoesNotCompile() {
        await CanOrCannotBuildAsync("DoesNotCompile", false, false);
    }

    protected async Task CanOrCannotBuildAsync(string solutionId, bool debug, bool buildExpected) {
        ISimpleLogger simpleLogger = _Container.Resolve<ISimpleLogger>();
        string id = nameof(CanOrCannotBuildAsync) + solutionId
             + (debug ? "Debug" : "Release")
             + (buildExpected ? "Build" : "NoBuild");
        using (simpleLogger.BeginScope(SimpleLoggingScopeId.Create(id))) {
            string solutionFileName = _AutomationTestHelper.AutomationTestProjectsFolder.SubFolder(solutionId).FullName + $"\\{solutionId}.slnx";
            Assert.IsTrue(File.Exists(solutionFileName));

            string finalFolderName = _AutomationTestHelper.FinalFolder.FullName + '\\' + solutionId + @"Bin\" + (debug ? "Debug" : "Release") + @"\";
            if (Directory.Exists(finalFolderName)) {
                var deleter = new FolderDeleter();
                bool canDelete = deleter.CanDeleteFolder(new Folder(finalFolderName), out _);
                Assert.IsTrue(canDelete);
                deleter.DeleteFolder(new Folder(finalFolderName));
            }

            Directory.CreateDirectory(finalFolderName);
            var errorsAndInfos = new ErrorsAndInfos();
            bool buildSucceeded = await _Sut.BuildAsync(solutionFileName, debug, finalFolderName, errorsAndInfos, CancellationToken.None);
            Assert.AreEqual(buildExpected, buildSucceeded);
            if (!buildSucceeded) {
                return;
            }

            Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsPlusRelevantInfos());
        }
    }
}