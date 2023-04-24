using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;

public interface IDotNetEfInstaller {
    bool IsCurrentGlobalDotNetEfInstalled(IErrorsAndInfos errorsAndInfos);
    void InstallOrUpdateGlobalDotNetEfIfNecessary(IErrorsAndInfos errorsAndInfos);
}