using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;

public interface IDotNetCakeInstaller {
    bool IsCurrentGlobalDotNetCakeInstalled(IErrorsAndInfos errorsAndInfos);
    // ReSharper disable once UnusedMemberInSuper.Global
    bool IsGlobalDotNetCakeInstalled(string version, IErrorsAndInfos errorsAndInfos);
    void InstallOrUpdateGlobalDotNetCakeIfNecessary(IErrorsAndInfos errorsAndInfos);
}