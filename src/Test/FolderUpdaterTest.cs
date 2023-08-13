using System.IO;
using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Components;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using Autofac;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Test;

[TestClass]
public class FolderUpdaterTest {
    private const string BeforeMajorChangeHeadTipSha = "932cb235841ce7ab5afc80fcbc3220c4ae54933e";
    private const string CurrentHeadTipIdSha = "b09bf637ae6eb84e098c81da6281034ea685f307";
    private const string RepositoryId = "Pegh";

    private readonly IContainer _Container;
    private readonly IFolder _WorkFolder;

    public FolderUpdaterTest() {
        _Container = new ContainerBuilder().UseFusionNuclideProtchAndGitty("Fusion", new DummyCsArgumentPrompter()).Build();
        _WorkFolder = new Folder(Path.GetTempPath()).SubFolder("AspenlaubTemp").SubFolder(nameof(FolderUpdaterTest)).SubFolder(RepositoryId);
    }

    [TestInitialize]
    public void Initialize() {
        CleanUpFolder(_WorkFolder);
    }

    [TestCleanup]
    public void Cleanup() {
        CleanUpFolder(_WorkFolder);
    }

    [TestMethod]
    public async Task CanListAndCopyChangedBinaries() {
        var lister = _Container.Resolve<IChangedBinariesLister>();
        var errorsAndInfos = new ErrorsAndInfos();
        var changedBinaries = lister.ListChangedBinaries(RepositoryId, "master", BeforeMajorChangeHeadTipSha, CurrentHeadTipIdSha, errorsAndInfos);
        Assert.AreEqual(3, changedBinaries.Count);
        var sourceFolder = _WorkFolder.SubFolder("Source");
        sourceFolder.CreateIfNecessary();
        var destinationFolder = _WorkFolder.SubFolder("Destination");
        destinationFolder.CreateIfNecessary();
        foreach (var changedBinary in changedBinaries) {
            await File.WriteAllTextAsync(sourceFolder.FullName + '\\' + changedBinary.FileName, changedBinary.FileName);
            await File.WriteAllTextAsync(destinationFolder.FullName + '\\' + changedBinary.FileName, "Old " + changedBinary.FileName);
            await File.WriteAllTextAsync(destinationFolder.FullName + @"\Unchanged" + changedBinary.FileName, "Unchanged " + changedBinary.FileName);
        }
        await File.WriteAllTextAsync(sourceFolder.FullName + @"\SomeNewFile.txt", "SomeNewFile");
        var sut = _Container.Resolve<IFolderUpdater>();
        await sut.UpdateFolderAsync(RepositoryId, "master", BeforeMajorChangeHeadTipSha, sourceFolder, CurrentHeadTipIdSha, destinationFolder, true, true, "aspenlaub.local", errorsAndInfos);
        Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsPlusRelevantInfos());
        foreach (var changedBinary in changedBinaries) {
            Assert.AreEqual(changedBinary.FileName, await File.ReadAllTextAsync(sourceFolder.FullName + '\\' + changedBinary.FileName));
            Assert.AreEqual(changedBinary.FileName, await File.ReadAllTextAsync(destinationFolder.FullName + '\\' + changedBinary.FileName));
            Assert.AreEqual("Unchanged " + changedBinary.FileName, await File.ReadAllTextAsync(destinationFolder.FullName + @"\Unchanged" + changedBinary.FileName));
        }
        Assert.IsTrue(File.Exists(destinationFolder.FullName + @"\SomeNewFile.txt"));
        Assert.AreEqual("SomeNewFile", await File.ReadAllTextAsync(destinationFolder.FullName + @"\SomeNewFile.txt"));
    }

    private void CleanUpFolder(IFolder folder) {
        if (folder.Exists()) {
            _Container.Resolve<IFolderDeleter>().DeleteFolder(folder);
        }
    }
}