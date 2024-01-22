using System.IO;
using System.Linq;
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
    private const string PreviousPeghHeadTipIdSha = "932cb235841ce7ab5afc80fcbc3220c4ae54933e";
    private const string CurrentPeghHeadTipIdSha = "b09bf637ae6eb84e098c81da6281034ea685f307";
    private const string PeghRepositoryId = "Pegh";

    private const string PreviousDummyServiceHeadTipIdSha = "4e131bd4e80c73ca037ec57994041cae0d48b2c9";
    private const string CurrentDummyServiceHeadTipIdSha = "8d5bfbe50fb55fdcd3d18a87e9057eaad6b8e075";
    private const string DummyServiceRepositoryId = "DummyService";

    private readonly IContainer _Container;
    private readonly IFolder _WorkFolder;

    public FolderUpdaterTest() {
        _Container = new ContainerBuilder().UseFusionNuclideProtchAndGitty("Fusion", new DummyCsArgumentPrompter()).Build();
        _WorkFolder = new Folder(Path.GetTempPath()).SubFolder("AspenlaubTemp").SubFolder(nameof(FolderUpdaterTest));
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
    public async Task CanListAndCopyChangedPeghBinaries() {
        var lister = _Container.Resolve<IChangedBinariesLister>();
        var errorsAndInfos = new ErrorsAndInfos();
        var changedBinaries = lister.ListChangedBinaries(PeghRepositoryId, "master", PreviousPeghHeadTipIdSha, CurrentPeghHeadTipIdSha, errorsAndInfos);
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
        await sut.UpdateFolderAsync(PeghRepositoryId, "master", PreviousPeghHeadTipIdSha, sourceFolder, CurrentPeghHeadTipIdSha, destinationFolder, true, true, "aspenlaub.local", errorsAndInfos);
        Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsPlusRelevantInfos());
        foreach (var changedBinary in changedBinaries) {
            Assert.AreEqual(changedBinary.FileName, await File.ReadAllTextAsync(sourceFolder.FullName + '\\' + changedBinary.FileName));
            Assert.AreEqual(changedBinary.FileName, await File.ReadAllTextAsync(destinationFolder.FullName + '\\' + changedBinary.FileName));
            Assert.AreEqual("Unchanged " + changedBinary.FileName, await File.ReadAllTextAsync(destinationFolder.FullName + @"\Unchanged" + changedBinary.FileName));
        }
        Assert.IsTrue(File.Exists(destinationFolder.FullName + @"\SomeNewFile.txt"));
        Assert.AreEqual("SomeNewFile", await File.ReadAllTextAsync(destinationFolder.FullName + @"\SomeNewFile.txt"));
    }

    [TestMethod]
    public async Task CanListAndCopyMissingDummyServiceBinaries() {
        var lister = _Container.Resolve<IChangedBinariesLister>();
        var errorsAndInfos = new ErrorsAndInfos();
        var changedBinaries = lister.ListChangedBinaries(DummyServiceRepositoryId, "master", PreviousDummyServiceHeadTipIdSha, CurrentDummyServiceHeadTipIdSha, errorsAndInfos);
        Assert.AreEqual(11, changedBinaries.Count);
        var sourceFolder = _WorkFolder.SubFolder("Source");
        sourceFolder.CreateIfNecessary();
        foreach (var fileInfo in changedBinaries.Select(changedBinary => sourceFolder.FullName + "\\" + changedBinary.FileName).Select(f => new FileInfo(f))) {
            Assert.IsNotNull(fileInfo.DirectoryName);
            Directory.CreateDirectory(fileInfo.DirectoryName);
            await File.WriteAllTextAsync(fileInfo.FullName, fileInfo.FullName);
            await File.WriteAllTextAsync(fileInfo.FullName + ".bak", fileInfo.FullName);
        }

        var destinationFolder = _WorkFolder.SubFolder("Destination");
        destinationFolder.CreateIfNecessary();
        var sut = _Container.Resolve<IFolderUpdater>();
        await sut.UpdateFolderAsync(DummyServiceRepositoryId, "master", PreviousDummyServiceHeadTipIdSha, sourceFolder, CurrentDummyServiceHeadTipIdSha, destinationFolder, true, true, "aspenlaub.local", errorsAndInfos);
        Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsPlusRelevantInfos());
        foreach (var fileName in changedBinaries.Select(changedBinary => changedBinary.FileName)) {
            var sourceFileName = sourceFolder.FullName + "\\" + fileName;
            var destinationFileName = destinationFolder.FullName + "\\" + fileName;
            Assert.IsTrue(File.Exists(destinationFileName));
            Assert.AreEqual(sourceFileName, await File.ReadAllTextAsync(destinationFileName));
            destinationFileName = destinationFolder.FullName + "\\" + fileName + ".bak";
            Assert.IsTrue(File.Exists(destinationFileName));
            Assert.AreEqual(sourceFileName, await File.ReadAllTextAsync(destinationFileName));
        }
    }

    private void CleanUpFolder(IFolder folder) {
        if (folder.Exists()) {
            _Container.Resolve<IFolderDeleter>().DeleteFolder(folder);
        }
    }
}