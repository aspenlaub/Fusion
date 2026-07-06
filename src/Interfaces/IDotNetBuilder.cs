using System.Threading;
using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Skladasu.Interfaces;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;

public interface IDotNetBuilder {
    Task<bool> BuildAsync(string solutionFileName, bool debug, string tempFolderName, IErrorsAndInfos errorsAndInfos, CancellationToken cancellationToken);
}