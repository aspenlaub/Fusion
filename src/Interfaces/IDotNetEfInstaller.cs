using Aspenlaub.Net.GitHub.CSharp.Skladasu.Interfaces;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;

public interface IDotNetEfInstaller {
    bool IsCurrentGlobalDotNetEfInstalled(IErrorsAndInfos errorsAndInfos);
    void InstallOrUpdateGlobalDotNetEfIfNecessary(IErrorsAndInfos errorsAndInfos);
}