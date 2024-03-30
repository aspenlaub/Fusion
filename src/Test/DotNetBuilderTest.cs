using System.Collections.Generic;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Components;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Extensions;
using Autofac;
using NuGet.Packaging;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Test;

[TestClass]
public class DotNetBuilderTest {
    protected IDotNetBuilder Sut;
    private IFolderResolver _FolderResolver;

    [TestInitialize]
    public void Initialize() {
        var container = new ContainerBuilder().UseFusionNuclideProtchAndGitty("Fusion", new DummyCsArgumentPrompter()).Build();
        Sut = container.Resolve<IDotNetBuilder>();
        _FolderResolver = container.Resolve<IFolderResolver>();
    }

    [TestMethod]
    public async Task CanBuildSolution() {
        var folder = new Folder(Path.GetTempPath()).SubFolder("AspenlaubTemp").SubFolder(nameof(CakeBuilderTest));
        folder.CreateIfNecessary();
        var files = GetAssemblyFileNames(folder);
        files.ForEach(File.Delete);
        var errorsAndInfos = new ErrorsAndInfos();
        var csharpFolder = await _FolderResolver.ResolveAsync("$(CSharp)", errorsAndInfos);
        Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsToString());
        var solutionFolder = csharpFolder.SubFolder("TheLittleThings");
        var solutionFileName = solutionFolder.FullName + @"\TheLittleThings.sln";
        Assert.IsTrue(File.Exists(solutionFileName));
        Sut.Build(solutionFileName, true, folder.FullName, errorsAndInfos);
        Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsToString());
        files = GetAssemblyFileNames(folder);
        Assert.AreEqual(5, files.Count);
    }

    private static List<string> GetAssemblyFileNames(IFolder folder) {
        var files = Directory.GetFiles(folder.FullName, "*TheLittle*.exe").ToList();
        files.AddRange(Directory.GetFiles(folder.FullName, "*TheLittle*.dll"));
        return files;
    }
}