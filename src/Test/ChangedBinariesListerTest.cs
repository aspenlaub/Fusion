using System.Linq;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Components;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Autofac;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Test;

[TestClass]
public class ChangedBinariesListerTest {
    private const string BeforeMajorPeghChangeHeadTipSha = "932cb235841ce7ab5afc80fcbc3220c4ae54933e";
    private const string PreviousPeghHeadTipIdSha = "6e314114c347c17776bdd8367cc5d0f1687a7775";
    private const string CurrentPeghHeadTipIdSha = "b09bf637ae6eb84e098c81da6281034ea685f307";

    private const string PreviousShatilayaHeadTipIdSha = "e8f2ae2cf737e1886ab4a0da30cd5dc0a4509ca1";
    private const string CurrentShatilayaHeadTipIdSha = "bdba763bf1a70defd567b4b5b7b75767c2c23873";

    private const string PreviousNuclideHeadTipIdSha = "6c0f886a0eda3bc55a00849c64164d5dae3acd52";
    private const string CurrentNuclideHeadTipIdSha = "a9bec6ccf1f929e9a44f51197c40fdf5fd19eaa1";

    private readonly IContainer _Container
        = new ContainerBuilder().UseFusionNuclideProtchAndGitty("Fusion", new DummyCsArgumentPrompter()).Build();

    [TestMethod]
    public void UnchangedPeghBinariesAreNotListed() {
        var sut = _Container.Resolve<IChangedBinariesLister>();
        Assert.IsNotNull(sut);
        var errorsAndInfos = new ErrorsAndInfos();
        var changedBinaries = sut.ListChangedBinaries("Pegh", "master", PreviousPeghHeadTipIdSha, CurrentPeghHeadTipIdSha, errorsAndInfos);
        Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsPlusRelevantInfos());
        Assert.IsFalse(changedBinaries.Any());
    }

    [TestMethod]
    public void CanListChangedPeghBinaries() {
        var sut = _Container.Resolve<IChangedBinariesLister>();
        Assert.IsNotNull(sut);
        var errorsAndInfos = new ErrorsAndInfos();
        var changedBinaries = sut.ListChangedBinaries("Pegh", "master", BeforeMajorPeghChangeHeadTipSha, CurrentPeghHeadTipIdSha, errorsAndInfos);
        Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsPlusRelevantInfos());
        Assert.AreEqual(3, changedBinaries.Count);
        Assert.IsTrue(changedBinaries.Any(c => c.FileName == "Aspenlaub.Net.GitHub.CSharp.Pegh.dll"));
        Assert.IsTrue(changedBinaries.Any(c => c.FileName == "Aspenlaub.Net.GitHub.CSharp.Pegh.pdb"));
        Assert.IsTrue(changedBinaries.Any(c => c.FileName == "Aspenlaub.Net.GitHub.CSharp.Pegh.deps.json"));
    }

    [TestMethod]
    public void CanHandleLinkedBuildCake() {
        var sut = _Container.Resolve<IChangedBinariesLister>();
        Assert.IsNotNull(sut);
        var errorsAndInfos = new ErrorsAndInfos();
        var changedBinaries = sut.ListChangedBinaries("Shatilaya", "master", PreviousShatilayaHeadTipIdSha,
            CurrentShatilayaHeadTipIdSha, errorsAndInfos);
        Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsPlusRelevantInfos());
        Assert.IsFalse(changedBinaries.Any());

        changedBinaries = sut.ListChangedBinaries("Nuclide", "master", PreviousNuclideHeadTipIdSha,
            CurrentNuclideHeadTipIdSha, errorsAndInfos);
        Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsPlusRelevantInfos());
        Assert.IsFalse(changedBinaries.Any());
    }
}