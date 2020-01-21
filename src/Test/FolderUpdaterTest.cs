using System.IO;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Components;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using Autofac;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Test {
    [TestClass]
    public class FolderUpdaterTest {
        private const string BeforeMajorChangeHeadTipSha = "932cb235841ce7ab5afc80fcbc3220c4ae54933e";
        private const string CurrentHeadTipIdSha = "b09bf637ae6eb84e098c81da6281034ea685f307";
        private const string RepositoryId = "Pegh";

        private readonly IContainer vContainer;
        private readonly IFolder vWorkFolder;

        public FolderUpdaterTest() {
            vContainer = new ContainerBuilder().UseFusionNuclideProtchAndGitty(new DummyCsArgumentPrompter()).Build();
            vWorkFolder = new Folder(Path.GetTempPath()).SubFolder(nameof(FolderUpdaterTest)).SubFolder(RepositoryId);
        }

        [TestInitialize]
        public void Initialize() {
            CleanUpFolder(vWorkFolder);
        }

        [TestCleanup]
        public void Cleanup() {
            CleanUpFolder(vWorkFolder);
        }

        [TestMethod]
        public void CanListAndCopyChangedBinaries() {
            var lister = vContainer.Resolve<IChangedBinariesLister>();
            var errorsAndInfos = new ErrorsAndInfos();
            var changedBinaries = lister.ListChangedBinaries(RepositoryId, BeforeMajorChangeHeadTipSha, CurrentHeadTipIdSha, errorsAndInfos);
            Assert.AreEqual(2, changedBinaries.Count);
            var sourceFolder = vWorkFolder.SubFolder("Source");
            sourceFolder.CreateIfNecessary();
            var destinationFolder = vWorkFolder.SubFolder("Destination");
            destinationFolder.CreateIfNecessary();
            foreach (var changedBinary in changedBinaries) {
                File.WriteAllText(sourceFolder.FullName + '\\' + changedBinary.FileName, changedBinary.FileName);
                File.WriteAllText(destinationFolder.FullName + '\\' + changedBinary.FileName, "Old " + changedBinary.FileName);
                File.WriteAllText(destinationFolder.FullName + @"\Unchanged" + changedBinary.FileName, "Unchanged " + changedBinary.FileName);
            }
            File.WriteAllText(sourceFolder.FullName + @"\SomeNewFile.txt", "SomeNewFile");
            var sut = vContainer.Resolve<IFolderUpdater>();
            sut.UpdateFolder(RepositoryId, BeforeMajorChangeHeadTipSha, sourceFolder, CurrentHeadTipIdSha, destinationFolder, errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsPlusRelevantInfos());
            foreach (var changedBinary in changedBinaries) {
                Assert.AreEqual(changedBinary.FileName, File.ReadAllText(sourceFolder.FullName + '\\' + changedBinary.FileName));
                Assert.AreEqual(changedBinary.FileName, File.ReadAllText(destinationFolder.FullName + '\\' + changedBinary.FileName));
                Assert.AreEqual("Unchanged " + changedBinary.FileName, File.ReadAllText(destinationFolder.FullName + @"\Unchanged" + changedBinary.FileName));
            }
            Assert.IsTrue(File.Exists(destinationFolder.FullName + @"\SomeNewFile.txt"));
            Assert.AreEqual("SomeNewFile", File.ReadAllText(destinationFolder.FullName + @"\SomeNewFile.txt"));
        }

        private void CleanUpFolder(IFolder folder) {
            if (folder.Exists()) {
                vContainer.Resolve<IFolderDeleter>().DeleteFolder(folder);
            }
        }
    }
}
