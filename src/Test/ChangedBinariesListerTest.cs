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

    private readonly IContainer _Container;

    public ChangedBinariesListerTest() {
        _Container = new ContainerBuilder().UseFusionNuclideProtchAndGitty("Fusion", new DummyCsArgumentPrompter()).Build();
    }

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
}