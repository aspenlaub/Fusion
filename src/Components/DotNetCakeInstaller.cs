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
    private const string _pinnedCakeToolVersionMatchingCompiledTargetFramework = "6.0.0";
    private const string _runnerUpPinnedCakeToolVersion = "7.0.0";
#else
    private const string _veryOldPinnedCakeToolVersion = "3.1.0";
    private const string _oldPinnedCakeToolVersion = "4.0.0";
    private const string _pinnedCakeToolVersion = "5.0.0";
    private const string _runnerUpPinnedCakeToolVersion = "6.0.0";
#endif
    private const string _provenPinnedCakeToolVersion = "5.0.0";
    private const string _dotNetExecutableFileName = "dotnet";
    private const string _dotNetToolListArguments = "tool list --global";
    private const string _dotNetInstallCakeToolArguments = "tool install Cake.Tool --version "
        + _pinnedCakeToolVersionMatchingCompiledTargetFramework + " --global";
    private const string _dotNetUpdateCakeToolArguments = "tool update Cake.Tool --version "
        + _pinnedCakeToolVersionMatchingCompiledTargetFramework + " --global";
    private const string _dotNetUninstallCakeToolArguments = "tool uninstall Cake.Tool --global";
    private const string _dotNetInstallCurrentCakeToolArguments = "tool install Cake.Tool --version "
        + _provenPinnedCakeToolVersion + " --global";

    private readonly IProcessRunner _ProcessRunner;
    private readonly IFolder _WorkingFolder;

    public DotNetCakeInstaller(IProcessRunner processRunner) {
        _ProcessRunner = processRunner;
        _WorkingFolder = new Folder(Path.GetTempPath()).SubFolder(nameof(DotNetCakeInstaller));
        _WorkingFolder.CreateIfNecessary();
    }

    public bool IsProvenGlobalDotNetCakeInstalled(IErrorsAndInfos errorsAndInfos) {
        return IsGlobalDotNetCakeInstalled(_provenPinnedCakeToolVersion, errorsAndInfos);
    }

    public bool IsGlobalDotNetCakeInstalled(string version, IErrorsAndInfos errorsAndInfos) {
        _ProcessRunner.RunProcess(_dotNetExecutableFileName, _dotNetToolListArguments, _WorkingFolder, errorsAndInfos);
        if (errorsAndInfos.AnyErrors()) { return false; }

        string line = errorsAndInfos.Infos.LastOrDefault(l => l.StartsWith(_cakeToolId));
        return line?.Substring(_cakeToolId.Length).TrimStart().StartsWith(version) == true;
    }

    public void InstallOrUpdateGlobalDotNetCakeIfNecessary(IErrorsAndInfos errorsAndInfos) {
        if (IsGlobalDotNetCakeInstalled(_pinnedCakeToolVersionMatchingCompiledTargetFramework, errorsAndInfos)) {
            RestoreProvenPinnedCakeVersion(errorsAndInfos);
            return;
        }
        if (errorsAndInfos.AnyErrors()) { return; }

        bool oldPinnedCakeToolVersionInstalled =
            IsGlobalDotNetCakeInstalled(_veryOldPinnedCakeToolVersion, errorsAndInfos)
            || IsGlobalDotNetCakeInstalled(_oldPinnedCakeToolVersion, errorsAndInfos);
        if (errorsAndInfos.AnyErrors()) { return; }

        if (IsGlobalDotNetCakeInstalled(_runnerUpPinnedCakeToolVersion, errorsAndInfos)) {
            errorsAndInfos.Errors.Add("We are here, aren't we?");
            if (errorsAndInfos.AnyErrors()) { return; }

            bool skipTest;
            try {
                _ProcessRunner.RunProcess(_dotNetExecutableFileName, _dotNetUninstallCakeToolArguments,
                    _WorkingFolder, errorsAndInfos);
                skipTest = errorsAndInfos.AnyErrors();
            } catch {
                skipTest = true;
            }
            if (skipTest) {
                errorsAndInfos.Infos.Clear();
                errorsAndInfos.Errors.Clear();
                return;
            }
        }

        _ProcessRunner.RunProcess(_dotNetExecutableFileName,
              oldPinnedCakeToolVersionInstalled
                  ? _dotNetUpdateCakeToolArguments
                  : _dotNetInstallCakeToolArguments,
              _WorkingFolder, errorsAndInfos);
        if (errorsAndInfos.AnyErrors()) { return; }

        if (!IsGlobalDotNetCakeInstalled(_pinnedCakeToolVersionMatchingCompiledTargetFramework, errorsAndInfos)) {
            errorsAndInfos.Errors.Add(Properties.Resources.CouldNotInstallCakeTool);
        }

        RestoreProvenPinnedCakeVersion(errorsAndInfos);
    }

    private void RestoreProvenPinnedCakeVersion(IErrorsAndInfos errorsAndInfos) {
        if (IsGlobalDotNetCakeInstalled(_provenPinnedCakeToolVersion, errorsAndInfos)) {
            return;
        }

        _ProcessRunner.RunProcess(_dotNetExecutableFileName, _dotNetUninstallCakeToolArguments,
            _WorkingFolder, errorsAndInfos);
        _ProcessRunner.RunProcess(_dotNetExecutableFileName, _dotNetInstallCurrentCakeToolArguments,
            _WorkingFolder, errorsAndInfos);
    }
}