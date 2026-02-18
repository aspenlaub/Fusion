using Aspenlaub.Net.GitHub.CSharp.Skladasu.Interfaces;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;

public interface IDotNetBuilder {
    bool Build(string solutionFileName, bool debug, string tempFolderName, IErrorsAndInfos errorsAndInfos);
}