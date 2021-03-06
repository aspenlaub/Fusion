﻿using System.Linq;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Components;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Autofac;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Test {
    [TestClass]
    public class ChangedBinariesListerTest {
        private const string BeforeMajorChangeHeadTipSha = "932cb235841ce7ab5afc80fcbc3220c4ae54933e";
        private const string PreviousHeadTipIdSha = "6e314114c347c17776bdd8367cc5d0f1687a7775";
        private const string CurrentHeadTipIdSha = "b09bf637ae6eb84e098c81da6281034ea685f307";

        private readonly IContainer vContainer;

        public ChangedBinariesListerTest() {
            vContainer = new ContainerBuilder().UseFusionNuclideProtchAndGitty(new DummyCsArgumentPrompter()).Build();
        }

        [TestMethod]
        public void UnchangedBinariesAreNotListed() {
            var sut = vContainer.Resolve<IChangedBinariesLister>();
            Assert.IsNotNull(sut);
            var errorsAndInfos = new ErrorsAndInfos();
            var changedBinaries = sut.ListChangedBinaries("Pegh", PreviousHeadTipIdSha, CurrentHeadTipIdSha, errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsPlusRelevantInfos());
            Assert.IsFalse(changedBinaries.Any());
        }

        [TestMethod]
        public void CanListChangedBinaries() {
            var sut = vContainer.Resolve<IChangedBinariesLister>();
            Assert.IsNotNull(sut);
            var errorsAndInfos = new ErrorsAndInfos();
            var changedBinaries = sut.ListChangedBinaries("Pegh", BeforeMajorChangeHeadTipSha, CurrentHeadTipIdSha, errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsPlusRelevantInfos());
            Assert.AreEqual(3, changedBinaries.Count);
            Assert.IsTrue(changedBinaries.Any(c => c.FileName == "Aspenlaub.Net.GitHub.CSharp.Pegh.dll"));
            Assert.IsTrue(changedBinaries.Any(c => c.FileName == "Aspenlaub.Net.GitHub.CSharp.Pegh.pdb"));
            Assert.IsTrue(changedBinaries.Any(c => c.FileName == "Aspenlaub.Net.GitHub.CSharp.Pegh.deps.json"));
        }
    }
}
