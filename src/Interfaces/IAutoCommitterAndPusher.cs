using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
// ReSharper disable UnusedMember.Global

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;

public interface IAutoCommitterAndPusher {
    Task AutoCommitAndPushPackageUpdates(string nugetFeedId, IFolder repositoryFolder, IErrorsAndInfos errorsAndInfos);
}