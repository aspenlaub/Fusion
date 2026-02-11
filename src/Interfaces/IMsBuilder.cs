using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;

public interface IMsBuilder {
    Task<bool> BuildAsync(string solutionFileName, bool debug, IErrorsAndInfos errorsAndInfos);
#pragma warning disable IDE0051
    Task<IFolder> BuildToTempAsync(string solutionFileName, bool debug, IErrorsAndInfos errorsAndInfos);
#pragma warning restore IDE0051
}