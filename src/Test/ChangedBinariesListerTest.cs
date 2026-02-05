using System.Collections.Generic;
using System.Linq;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Entities;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Components;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Autofac;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Test;

[TestClass]
public class ChangedBinariesListerTest {
    internal const string BeforeMajorPeghChangeHeadTipSha = "78d200008e60a19c2ce5ee79d944bc8de16d3ea1";
    private const string _majorPeghChangeHeadTipIdSha = "c64e00c219dfb5a3ac378eeae0f8297a98790514";
    internal const string AfterMajorPeghChangeHeadTipIdSha = "694dadf8f51db5cd5ae4705bb7687ecaa17596a6";

    private const string _previousShatilayaHeadTipIdSha = "e8f2ae2cf737e1886ab4a0da30cd5dc0a4509ca1";
    private const string _currentShatilayaHeadTipIdSha = "bdba763bf1a70defd567b4b5b7b75767c2c23873";

    private const string _previousNuclideHeadTipIdSha = "6c0f886a0eda3bc55a00849c64164d5dae3acd52";
    private const string _currentNuclideHeadTipIdSha = "a9bec6ccf1f929e9a44f51197c40fdf5fd19eaa1";

    private readonly IContainer _Container
        = new ContainerBuilder().UseFusionNuclideProtchAndGitty("Fusion").Build();

    [TestMethod]
    public void UnchangedPeghBinariesAreNotListed() {
        ISimpleLogger simpleLogger = _Container.Resolve<ISimpleLogger>();
        using (simpleLogger.BeginScope(SimpleLoggingScopeId.Create(nameof(UnchangedPeghBinariesAreNotListed)))) {
            IChangedBinariesLister sut = _Container.Resolve<IChangedBinariesLister>();
            Assert.IsNotNull(sut);
            var errorsAndInfos = new ErrorsAndInfos();
            IList<BinaryToUpdate> changedBinaries = sut.ListChangedBinaries("Pegh", "master", _majorPeghChangeHeadTipIdSha, AfterMajorPeghChangeHeadTipIdSha, errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsPlusRelevantInfos());
            Assert.IsFalse(changedBinaries.Any());
        }
    }

    [TestMethod]
    public void CanListChangedPeghBinaries() {
        ISimpleLogger simpleLogger = _Container.Resolve<ISimpleLogger>();
        using (simpleLogger.BeginScope(SimpleLoggingScopeId.Create(nameof(CanListChangedPeghBinaries)))) {
            IChangedBinariesLister sut = _Container.Resolve<IChangedBinariesLister>();
            Assert.IsNotNull(sut);
            var errorsAndInfos = new ErrorsAndInfos();
            IList<BinaryToUpdate> changedBinaries = sut.ListChangedBinaries("Pegh", "master", BeforeMajorPeghChangeHeadTipSha, _majorPeghChangeHeadTipIdSha, errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsPlusRelevantInfos());
            Assert.HasCount(3, changedBinaries);
            Assert.IsTrue(changedBinaries.Any(c => c.FileName == "Aspenlaub.Net.GitHub.CSharp.Pegh.dll"));
            Assert.IsTrue(changedBinaries.Any(c => c.FileName == "Aspenlaub.Net.GitHub.CSharp.Pegh.pdb"));
            Assert.IsTrue(changedBinaries.Any(c => c.FileName == "Aspenlaub.Net.GitHub.CSharp.Pegh.deps.json"));
        }
    }

    [TestMethod]
    public void CanHandleLinkedBuildCake() {
        ISimpleLogger simpleLogger = _Container.Resolve<ISimpleLogger>();
        using (simpleLogger.BeginScope(SimpleLoggingScopeId.Create(nameof(CanHandleLinkedBuildCake)))) {
            IChangedBinariesLister sut = _Container.Resolve<IChangedBinariesLister>();
            Assert.IsNotNull(sut);
            var errorsAndInfos = new ErrorsAndInfos();
            IList<BinaryToUpdate> changedBinaries = sut.ListChangedBinaries("Shatilaya", "master", _previousShatilayaHeadTipIdSha,
                                                                            _currentShatilayaHeadTipIdSha, errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsPlusRelevantInfos());
            Assert.IsFalse(changedBinaries.Any());

            changedBinaries = sut.ListChangedBinaries("Nuclide", "master", _previousNuclideHeadTipIdSha,
                _currentNuclideHeadTipIdSha, errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsPlusRelevantInfos());
            Assert.IsFalse(changedBinaries.Any());
        }
    }
}