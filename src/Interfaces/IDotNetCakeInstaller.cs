using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;

// ReSharper disable UnusedMember.Global

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;

public interface IDotNetCakeInstaller {
    bool IsProvenGlobalDotNetCakeInstalled(IErrorsAndInfos errorsAndInfos);
    bool DoesGlobalCakeToolVersionMatchTargetFramework(bool doNotLogErrorMessage, IErrorsAndInfos errorsAndInfos);
    // ReSharper disable once UnusedMemberInSuper.Global
    bool IsGlobalDotNetCakeInstalled(string version, IErrorsAndInfos errorsAndInfos);
    void InstallOrUpdateGlobalDotNetCakeIfNecessary(IErrorsAndInfos errorsAndInfos, out bool inconclusive);
    void UpdateGlobalDotNetCakeToMatchTargetFrameworkIfNecessary(IErrorsAndInfos errorsAndInfos);
}