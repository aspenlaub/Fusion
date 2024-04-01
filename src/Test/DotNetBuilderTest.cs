using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Components;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Extensions;
using Autofac;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Test;

[TestClass]
public class DotNetBuilderTest {
    private IDotNetBuilder _Sut;
    private IContainer _Container;
    private AutomationTestHelper _AutomationTestHelper;

    [TestInitialize]
    public void Initialize() {
        _AutomationTestHelper = new AutomationTestHelper(nameof(DotNetBuilderTest));
        _Container = new ContainerBuilder().UseFusionNuclideProtchAndGitty("Fusion", new DummyCsArgumentPrompter()).Build();
        _Sut = _Container.Resolve<IDotNetBuilder>();
    }

    [TestMethod]
    public void CanDebugBuildSolutionThatCompilesInDebug() {
        CanOrCannotBuild("Compiles", true, true);
    }

    [TestMethod]
    public void CanReleaseBuildSolutionThatCompilesInRelease() {
        CanOrCannotBuild("Compiles", false, true);
    }

    [TestMethod]
    public void CanDebugBuildSolutionThatCompilesInDebugOnly() {
        CanOrCannotBuild("CompilesInDebug", true, true);
    }

    [TestMethod]
    public void CannotReleaseBuildSolutionThatCompilesInDebugOnly() {
        CanOrCannotBuild("CompilesInDebug", false, false);
    }

    [TestMethod]
    public void CannotDebugBuildSolutionThatCompilesInReleaseOnly() {
        CanOrCannotBuild("CompilesInRelease", true, false);
    }

    [TestMethod]
    public void CanReleaseBuildSolutionThatCompilesInReleaseOnly() {
        CanOrCannotBuild("CompilesInRelease", false, true);
    }

    [TestMethod]
    public void CannotDebugBuildSolutionThatDoesNotCompile() {
        CanOrCannotBuild("DoesNotCompile", true, false);
    }

    [TestMethod]
    public void CannotReleaseBuildSolutionThatDoesNotCompile() {
        CanOrCannotBuild("DoesNotCompile", false, false);
    }

    protected void CanOrCannotBuild(string solutionId, bool debug, bool buildExpected) {
        var simpleLogger = _Container.Resolve<ISimpleLogger>();
        var id = nameof(CanOrCannotBuild) + solutionId
            + (debug ? "Debug" : "Release")
            + (buildExpected ? "Build" : "NoBuild");
        using (simpleLogger.BeginScope(SimpleLoggingScopeId.Create(id))) {
            var solutionFileName = _AutomationTestHelper.AutomationTestProjectsFolder.SubFolder(solutionId).FullName + $"\\{solutionId}.sln";
            Assert.IsTrue(File.Exists(solutionFileName));

            var finalFolderName = _AutomationTestHelper.FinalFolder.FullName + '\\' + solutionId + @"Bin\" + (debug ? "Debug" : "Release") + @"\";
            if (Directory.Exists(finalFolderName)) {
                var deleter = new FolderDeleter();
                var canDelete = deleter.CanDeleteFolder(new Folder(finalFolderName), out _);
                Assert.IsTrue(canDelete);
                deleter.DeleteFolder(new Folder(finalFolderName));
            }

            Directory.CreateDirectory(finalFolderName);
            var errorsAndInfos = new ErrorsAndInfos();
            var buildSucceeded = _Sut.Build(solutionFileName, debug, finalFolderName, errorsAndInfos);
            Assert.AreEqual(buildExpected, buildSucceeded);
            if (!buildSucceeded) {
                return;
            }

            Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsPlusRelevantInfos());
        }
    }
}