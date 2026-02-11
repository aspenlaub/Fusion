using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Entities;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Extensions;
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

    private readonly IContainer _Container
        = new ContainerBuilder().UseFusionNuclideProtchAndGitty("Fusion").Build();

    [TestMethod]
    public async Task UnchangedPeghBinariesAreNotListed() {
        ISimpleLogger simpleLogger = _Container.Resolve<ISimpleLogger>();
        using (simpleLogger.BeginScope(SimpleLoggingScopeId.Create(nameof(UnchangedPeghBinariesAreNotListed)))) {
            IChangedBinariesLister sut = _Container.Resolve<IChangedBinariesLister>();
            Assert.IsNotNull(sut);
            var errorsAndInfos = new ErrorsAndInfos();
            IList<BinaryToUpdate> changedBinaries = await sut.ListChangedBinariesAsync("Pegh", "master", _majorPeghChangeHeadTipIdSha, AfterMajorPeghChangeHeadTipIdSha, errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsPlusRelevantInfos());
            Assert.IsFalse(changedBinaries.Any());
        }
    }

    [TestMethod]
    public async Task CanListChangedPeghBinaries() {
        ISimpleLogger simpleLogger = _Container.Resolve<ISimpleLogger>();
        using (simpleLogger.BeginScope(SimpleLoggingScopeId.Create(nameof(CanListChangedPeghBinaries)))) {
            IChangedBinariesLister sut = _Container.Resolve<IChangedBinariesLister>();
            Assert.IsNotNull(sut);
            var errorsAndInfos = new ErrorsAndInfos();
            IList<BinaryToUpdate> changedBinaries = await sut.ListChangedBinariesAsync("Pegh", "master", BeforeMajorPeghChangeHeadTipSha, _majorPeghChangeHeadTipIdSha, errorsAndInfos);
            Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsPlusRelevantInfos());
            Assert.HasCount(3, changedBinaries);
            Assert.Contains(c => c.FileName == "Aspenlaub.Net.GitHub.CSharp.Pegh.dll", changedBinaries);
            Assert.Contains(c => c.FileName == "Aspenlaub.Net.GitHub.CSharp.Pegh.pdb", changedBinaries);
            Assert.Contains(c => c.FileName == "Aspenlaub.Net.GitHub.CSharp.Pegh.deps.json", changedBinaries);
        }
    }
}