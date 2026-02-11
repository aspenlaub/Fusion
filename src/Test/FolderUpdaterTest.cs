using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Entities;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using Autofac;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Test;

[TestClass]
public class FolderUpdaterTest {
    private const string _peghRepositoryId = "Pegh";

    private const string _previousDummyServiceHeadTipIdSha = "4e131bd4e80c73ca037ec57994041cae0d48b2c9";
    private const string _currentDummyServiceHeadTipIdSha = "8d5bfbe50fb55fdcd3d18a87e9057eaad6b8e075";
    private const string _dummyServiceRepositoryId = "DummyService";

    private readonly IContainer _Container
        = new ContainerBuilder().UseFusionNuclideProtchAndGitty("Fusion").Build();
    private readonly IFolder _WorkFolder
        = new Folder(Path.GetTempPath()).SubFolder("AspenlaubTemp").SubFolder(nameof(FolderUpdaterTest));

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
        ISimpleLogger simpleLogger = _Container.Resolve<ISimpleLogger>();
        using (simpleLogger.BeginScope(SimpleLoggingScopeId.Create(nameof(CanListAndCopyChangedPeghBinaries)))) {
            IChangedBinariesLister lister = _Container.Resolve<IChangedBinariesLister>();
            var errorsAndInfos = new ErrorsAndInfos();
            IList<BinaryToUpdate> changedBinaries = await lister.ListChangedBinariesAsync(_peghRepositoryId, "master",
               ChangedBinariesListerTest.BeforeMajorPeghChangeHeadTipSha,
               ChangedBinariesListerTest.AfterMajorPeghChangeHeadTipIdSha, errorsAndInfos);
            Assert.HasCount(3, changedBinaries);
            IFolder sourceFolder = _WorkFolder.SubFolder("Source");
            sourceFolder.CreateIfNecessary();
            IFolder destinationFolder = _WorkFolder.SubFolder("Destination");
            destinationFolder.CreateIfNecessary();
            foreach (BinaryToUpdate changedBinary in changedBinaries) {
                await File.WriteAllTextAsync(sourceFolder.FullName + '\\' + changedBinary.FileName, changedBinary.FileName);
                await File.WriteAllTextAsync(destinationFolder.FullName + '\\' + changedBinary.FileName, "Old " + changedBinary.FileName);
                await File.WriteAllTextAsync(destinationFolder.FullName + @"\Unchanged" + changedBinary.FileName, "Unchanged " + changedBinary.FileName);
            }

            await File.WriteAllTextAsync(sourceFolder.FullName + @"\SomeNewFile.txt", "SomeNewFile");
            IFolderUpdater sut = _Container.Resolve<IFolderUpdater>();
            await sut.UpdateFolderAsync(_peghRepositoryId, "master",
                                        ChangedBinariesListerTest.BeforeMajorPeghChangeHeadTipSha,
                                        sourceFolder, ChangedBinariesListerTest.AfterMajorPeghChangeHeadTipIdSha,
                                        destinationFolder, true, true, "aspenlaub.local", errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsPlusRelevantInfos());
            foreach (BinaryToUpdate changedBinary in changedBinaries) {
                Assert.AreEqual(changedBinary.FileName, await File.ReadAllTextAsync(sourceFolder.FullName + '\\' + changedBinary.FileName));
                Assert.AreEqual(changedBinary.FileName, await File.ReadAllTextAsync(destinationFolder.FullName + '\\' + changedBinary.FileName));
                Assert.AreEqual("Unchanged " + changedBinary.FileName, await File.ReadAllTextAsync(destinationFolder.FullName + @"\Unchanged" + changedBinary.FileName));
            }

            Assert.IsTrue(File.Exists(destinationFolder.FullName + @"\SomeNewFile.txt"));
            Assert.AreEqual("SomeNewFile", await File.ReadAllTextAsync(destinationFolder.FullName + @"\SomeNewFile.txt"));
        }
    }

    [TestMethod]
    public async Task CanListAndCopyMissingDummyServiceBinaries() {
        ISimpleLogger simpleLogger = _Container.Resolve<ISimpleLogger>();
        using (simpleLogger.BeginScope(SimpleLoggingScopeId.Create(nameof(CanListAndCopyMissingDummyServiceBinaries)))) {
            IChangedBinariesLister lister = _Container.Resolve<IChangedBinariesLister>();
            var errorsAndInfos = new ErrorsAndInfos();
            IList<BinaryToUpdate> changedBinaries = await lister.ListChangedBinariesAsync(_dummyServiceRepositoryId, "master",
                _previousDummyServiceHeadTipIdSha, _currentDummyServiceHeadTipIdSha, errorsAndInfos);
            Assert.HasCount(11, changedBinaries);
            IFolder sourceFolder = _WorkFolder.SubFolder("Source");
            sourceFolder.CreateIfNecessary();
            foreach (FileInfo fileInfo in changedBinaries.Select(changedBinary => sourceFolder.FullName + "\\" + changedBinary.FileName).Select(f => new FileInfo(f))) {
                Assert.IsNotNull(fileInfo.DirectoryName);
                Directory.CreateDirectory(fileInfo.DirectoryName);
                await File.WriteAllTextAsync(fileInfo.FullName, fileInfo.FullName);
                await File.WriteAllTextAsync(fileInfo.FullName + ".bak", fileInfo.FullName);
            }

            IFolder destinationFolder = _WorkFolder.SubFolder("Destination");
            destinationFolder.CreateIfNecessary();
            IFolderUpdater sut = _Container.Resolve<IFolderUpdater>();
            await sut.UpdateFolderAsync(_dummyServiceRepositoryId, "master", _previousDummyServiceHeadTipIdSha, sourceFolder, _currentDummyServiceHeadTipIdSha, destinationFolder,
                                        true, true, "aspenlaub.local", errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsPlusRelevantInfos());
            foreach (string fileName in changedBinaries.Select(changedBinary => changedBinary.FileName)) {
                string sourceFileName = sourceFolder.FullName + "\\" + fileName;
                string destinationFileName = destinationFolder.FullName + "\\" + fileName;
                Assert.IsTrue(File.Exists(destinationFileName));
                Assert.AreEqual(sourceFileName, await File.ReadAllTextAsync(destinationFileName));
                destinationFileName = destinationFolder.FullName + "\\" + fileName + ".bak";
                Assert.IsTrue(File.Exists(destinationFileName));
                Assert.AreEqual(sourceFileName, await File.ReadAllTextAsync(destinationFileName));
            }
        }
    }

    private void CleanUpFolder(IFolder folder) {
        if (folder.Exists()) {
            _Container.Resolve<IFolderDeleter>().DeleteFolder(folder);
        }
    }
}