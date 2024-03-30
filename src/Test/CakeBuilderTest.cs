using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Components;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Autofac;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using System.Threading.Tasks;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Test;

[TestClass]
public class CakeBuilderTest {
    protected ICakeBuilder Sut;
    private IFolderResolver _FolderResolver;

    [TestInitialize]
    public void Initialize() {
        var container = new ContainerBuilder().UseFusionNuclideProtchAndGitty("Fusion", new DummyCsArgumentPrompter()).Build();
        Sut = container.Resolve<ICakeBuilder>();
        _FolderResolver = container.Resolve<IFolderResolver>();
    }

    [TestMethod, Ignore]
    public async Task CanBuildSolution() {
        var folder = new Folder(Path.GetTempPath()).SubFolder("AspenlaubTemp").SubFolder(nameof(CakeBuilderTest));
        folder.CreateIfNecessary();
        var errorsAndInfos = new ErrorsAndInfos();
        var csharpFolder = await _FolderResolver.ResolveAsync("$(CSharp)", errorsAndInfos);
        Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsToString());
        var solutionFolder = csharpFolder.SubFolder("TheLittleThings");
        var solutionFileName = solutionFolder.FullName + @"\TheLittleThings.sln";
        Assert.IsTrue(File.Exists(solutionFileName));
        Sut.Build(solutionFileName, true, folder.FullName, errorsAndInfos);
        Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsToString());
    }
}