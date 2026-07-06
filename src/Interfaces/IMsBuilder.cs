using System.Threading;
using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Skladasu.Interfaces;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;

public interface IMsBuilder {
    Task<bool> BuildAsync(string solutionFileName, bool debug, IErrorsAndInfos errorsAndInfos, CancellationToken cancellationToken);
#pragma warning disable IDE0051
    Task<IFolder> BuildToTempAsync(string solutionFileName, bool debug, IErrorsAndInfos errorsAndInfos, CancellationToken cancellationToken);
    Task<IFolder> BuildSolutionOrCsProjToTempInReleaseAsync(string fileToBuildFullName, IErrorsAndInfos errorsAndInfos, CancellationToken cancellationToken);
#pragma warning restore IDE0051
}