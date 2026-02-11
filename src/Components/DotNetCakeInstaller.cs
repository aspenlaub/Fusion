using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using IDotNetCakeInstaller = Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces.IDotNetCakeInstaller;

[assembly: InternalsVisibleTo("Aspenlaub.Net.GitHub.CSharp.Fusion.Test")]
namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Components;

public class DotNetCakeInstaller : IDotNetCakeInstaller {
    private const string _cakeToolId = "cake.tool";
    private const string _veryOldCakeToolVersion = "4.0.0";
    private const string _oldCakeToolVersion = "5.0.0";
    internal const string CakeToolVersionMatchingCompiledTargetFramework = "6.0.0";
    private const string _runnerUpCakeToolVersion = "7.0.0";
    internal const string ProvenCakeToolVersion = "6.0.0";
    private const string _dotNetExecutableFileName = "dotnet";
    private const string _dotNetToolListArguments = "tool list --global";
    private const string _dotNetInstallCakeToolArguments = "tool install Cake.Tool --version "
        + CakeToolVersionMatchingCompiledTargetFramework + " --global";
    private const string _dotNetUpdateCakeToolArguments = "tool update Cake.Tool --version "
        + CakeToolVersionMatchingCompiledTargetFramework + " --global";
    private const string _dotNetUninstallCakeToolArguments = "tool uninstall Cake.Tool --global";
    private const string _dotNetInstallProvenCakeToolArguments = "tool install Cake.Tool --version "
        + ProvenCakeToolVersion + " --global";
    private const string _dotNetInstallCakeToolMatchingTargetFrameworkArguments
        = "tool install Cake.Tool --version "
            + CakeToolVersionMatchingCompiledTargetFramework + " --global";

    private readonly IProcessRunner _ProcessRunner;
    private readonly IFolder _WorkingFolder;

    public DotNetCakeInstaller(IProcessRunner processRunner) {
        _ProcessRunner = processRunner;
        _WorkingFolder = new Folder(Path.GetTempPath()).SubFolder(nameof(DotNetCakeInstaller));
        _WorkingFolder.CreateIfNecessary();
    }

    public bool IsProvenGlobalDotNetCakeInstalled(IErrorsAndInfos errorsAndInfos) {
        return IsGlobalDotNetCakeInstalled(ProvenCakeToolVersion, errorsAndInfos);
    }

    public bool DoesGlobalCakeToolVersionMatchTargetFramework(bool doNotLogErrorMessage, IErrorsAndInfos errorsAndInfos) {
        if (IsGlobalDotNetCakeInstalled(CakeToolVersionMatchingCompiledTargetFramework, errorsAndInfos)) {
            return true;
        }
        if (doNotLogErrorMessage) { return false; }

        errorsAndInfos.Errors.Add(
            string.Format(Properties.Resources.GlobalCakeToolVersionDoesNotMatchTargetFramework,
                          CakeToolVersionMatchingCompiledTargetFramework)
        );
        return false;
    }

    public bool IsGlobalDotNetCakeInstalled(string version, IErrorsAndInfos errorsAndInfos) {
        _ProcessRunner.RunProcess(_dotNetExecutableFileName, _dotNetToolListArguments, _WorkingFolder, errorsAndInfos);
        if (errorsAndInfos.AnyErrors()) { return false; }

        string line = errorsAndInfos.Infos.LastOrDefault(l => l.StartsWith(_cakeToolId));
        return line?.Substring(_cakeToolId.Length).TrimStart().StartsWith(version) == true;
    }

    public void InstallOrUpdateGlobalDotNetCakeIfNecessary(IErrorsAndInfos errorsAndInfos, out bool inconclusive) {
        inconclusive = false;
        if (IsGlobalDotNetCakeInstalled(CakeToolVersionMatchingCompiledTargetFramework, errorsAndInfos)) {
            RestoreProvenCakeToolVersion(errorsAndInfos);
            return;
        }
        if (errorsAndInfos.AnyErrors()) { return; }

        // ReSharper disable once RedundantAssignment
        bool isOldCakeToolVersionInstalled =
            IsGlobalDotNetCakeInstalled(_veryOldCakeToolVersion, errorsAndInfos)
            || IsGlobalDotNetCakeInstalled(_oldCakeToolVersion, errorsAndInfos);
        if (errorsAndInfos.AnyErrors()) { return; }

        if (IsGlobalDotNetCakeInstalled(_runnerUpCakeToolVersion, errorsAndInfos)
            || CakeToolVersionMatchingCompiledTargetFramework != ProvenCakeToolVersion) {
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
                inconclusive = true;
                errorsAndInfos.Infos.Clear();
                errorsAndInfos.Errors.Clear();
                return;
            }
        }

        _ProcessRunner.RunProcess(_dotNetExecutableFileName,
              // ReSharper disable once ConditionIsAlwaysTrueOrFalse
              isOldCakeToolVersionInstalled
                  ? _dotNetUpdateCakeToolArguments
                  : _dotNetInstallCakeToolArguments,
              _WorkingFolder, errorsAndInfos);
        if (errorsAndInfos.AnyErrors()) { return; }

        if (!IsGlobalDotNetCakeInstalled(CakeToolVersionMatchingCompiledTargetFramework, errorsAndInfos)) {
            errorsAndInfos.Errors.Add(Properties.Resources.CouldNotInstallCakeTool);
        }

        RestoreProvenCakeToolVersion(errorsAndInfos);
    }

    public void UpdateGlobalDotNetCakeToMatchTargetFrameworkIfNecessary(IErrorsAndInfos errorsAndInfos) {
        if (DoesGlobalCakeToolVersionMatchTargetFramework(true, errorsAndInfos)) {
            return;
        }
        if (errorsAndInfos.AnyErrors()) { return; }

        _ProcessRunner.RunProcess(_dotNetExecutableFileName, _dotNetUninstallCakeToolArguments,
            _WorkingFolder, errorsAndInfos);
        _ProcessRunner.RunProcess(_dotNetExecutableFileName, _dotNetInstallCakeToolMatchingTargetFrameworkArguments,
            _WorkingFolder, errorsAndInfos);
    }

    private void RestoreProvenCakeToolVersion(IErrorsAndInfos errorsAndInfos) {
        if (IsGlobalDotNetCakeInstalled(ProvenCakeToolVersion, errorsAndInfos)) {
            return;
        }

        _ProcessRunner.RunProcess(_dotNetExecutableFileName, _dotNetUninstallCakeToolArguments,
            _WorkingFolder, errorsAndInfos);
        _ProcessRunner.RunProcess(_dotNetExecutableFileName, _dotNetInstallProvenCakeToolArguments,
            _WorkingFolder, errorsAndInfos);
    }
}