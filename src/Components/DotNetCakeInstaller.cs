using System.IO;
using System.Linq;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using IDotNetCakeInstaller = Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces.IDotNetCakeInstaller;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Components;

public class DotNetCakeInstaller : IDotNetCakeInstaller {
    private const string _cakeToolId = "cake.tool";
#if NET10_0_OR_GREATER
    private const string _veryOldPinnedCakeToolVersion = "4.0.0";
    private const string _oldPinnedCakeToolVersion = "5.0.0";
    private const string _pinnedCakeToolVersion = "6.0.0";
    private const string _upcomingPinnedCakeToolVersion = "7.0.0";
#else
    private const string _veryOldPinnedCakeToolVersion = "3.1.0";
    private const string _oldPinnedCakeToolVersion = "4.0.0";
    private const string _pinnedCakeToolVersion = "5.0.0";
    private const string _upcomingPinnedCakeToolVersion = "6.0.0";
#endif
    private const string _currentPinnedCakeToolVersion = "5.0.0";
    private const string _dotNetExecutableFileName = "dotnet";
    private const string _dotNetToolListArguments = "tool list --global";
    private const string _dotNetInstallCakeToolArguments = "tool install Cake.Tool --version "
        + _pinnedCakeToolVersion + " --global";
    private const string _dotNetUpdateCakeToolArguments = "tool update Cake.Tool --version "
        + _pinnedCakeToolVersion + " --global";
    private const string _dotNetUninstallCakeToolArguments = "tool uninstall Cake.Tool --global";
    private const string _dotNetInstallCurrentCakeToolArguments = "tool install Cake.Tool --version "
        + _currentPinnedCakeToolVersion + " --global";

    private readonly IProcessRunner _ProcessRunner;
    private readonly IFolder _WorkingFolder;

    public DotNetCakeInstaller(IProcessRunner processRunner) {
        _ProcessRunner = processRunner;
        _WorkingFolder = new Folder(Path.GetTempPath()).SubFolder(nameof(DotNetCakeInstaller));
        _WorkingFolder.CreateIfNecessary();
    }

    public bool IsCurrentGlobalDotNetCakeInstalled(IErrorsAndInfos errorsAndInfos) {
        return IsGlobalDotNetCakeInstalled(_currentPinnedCakeToolVersion, errorsAndInfos);
    }

    public bool IsGlobalDotNetCakeInstalled(string version, IErrorsAndInfos errorsAndInfos) {
        _ProcessRunner.RunProcess(_dotNetExecutableFileName, _dotNetToolListArguments, _WorkingFolder, errorsAndInfos);
        if (errorsAndInfos.AnyErrors()) { return false; }

        string line = errorsAndInfos.Infos.LastOrDefault(l => l.StartsWith(_cakeToolId));
        return line?.Substring(_cakeToolId.Length).TrimStart().StartsWith(version) == true;
    }

    public void InstallOrUpdateGlobalDotNetCakeIfNecessary(IErrorsAndInfos errorsAndInfos) {
        if (IsGlobalDotNetCakeInstalled(_pinnedCakeToolVersion, errorsAndInfos)) {
            RestoreCurrentPinnedCakeVersion(errorsAndInfos);
            return;
        }
        if (errorsAndInfos.AnyErrors()) { return; }

        bool oldPinnedCakeToolVersionInstalled =
            IsGlobalDotNetCakeInstalled(_veryOldPinnedCakeToolVersion, errorsAndInfos)
            || IsGlobalDotNetCakeInstalled(_oldPinnedCakeToolVersion, errorsAndInfos);
        if (errorsAndInfos.AnyErrors()) { return; }

        if (IsGlobalDotNetCakeInstalled(_upcomingPinnedCakeToolVersion, errorsAndInfos)) {
            if (errorsAndInfos.AnyErrors()) { return; }
            _ProcessRunner.RunProcess(_dotNetExecutableFileName, _dotNetUninstallCakeToolArguments,
                _WorkingFolder, errorsAndInfos);
            if (errorsAndInfos.AnyErrors()) {
                var keptErrors = errorsAndInfos.Errors.Where(x => !x.Contains("Access to the path")).ToList();
                errorsAndInfos.Errors.Clear();
                keptErrors.ForEach(x => errorsAndInfos.Errors.Add(x));
                return;
            }
        }

        _ProcessRunner.RunProcess(_dotNetExecutableFileName,
              oldPinnedCakeToolVersionInstalled
                  ? _dotNetUpdateCakeToolArguments
                  : _dotNetInstallCakeToolArguments,
              _WorkingFolder, errorsAndInfos);
        if (errorsAndInfos.AnyErrors()) { return; }

        if (!IsGlobalDotNetCakeInstalled(_pinnedCakeToolVersion, errorsAndInfos)) {
            errorsAndInfos.Errors.Add(Properties.Resources.CouldNotInstallCakeTool);
        }

        RestoreCurrentPinnedCakeVersion(errorsAndInfos);
    }

    private void RestoreCurrentPinnedCakeVersion(IErrorsAndInfos errorsAndInfos) {
        if (IsGlobalDotNetCakeInstalled(_currentPinnedCakeToolVersion, errorsAndInfos)) {
            return;
        }

        _ProcessRunner.RunProcess(_dotNetExecutableFileName, _dotNetUninstallCakeToolArguments,
                                  _WorkingFolder, errorsAndInfos);
        _ProcessRunner.RunProcess(_dotNetExecutableFileName, _dotNetInstallCurrentCakeToolArguments,
                                  _WorkingFolder, errorsAndInfos);
    }
}