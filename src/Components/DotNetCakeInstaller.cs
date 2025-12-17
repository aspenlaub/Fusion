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
    private const string _veryOldPinnedCakeToolVersion = "3.1.0";
    private const string _oldPinnedCakeToolVersion = "4.0.0";
    private const string _pinnedCakeToolVersion = "5.0.0";
    private const string _dotNetExecutableFileName = "dotnet";
    private const string _dotNetToolListArguments = "tool list --global";
    private const string _dotNetInstallCakeToolArguments = "tool install Cake.Tool --version "
        + _pinnedCakeToolVersion + " --global";
    private const string _dotNetUpdateCakeToolArguments = "tool update Cake.Tool --version "
        + _pinnedCakeToolVersion + " --global";

    private readonly IProcessRunner _ProcessRunner;
    private readonly IFolder _WorkingFolder;

    public DotNetCakeInstaller(IProcessRunner processRunner) {
        _ProcessRunner = processRunner;
        _WorkingFolder = new Folder(Path.GetTempPath()).SubFolder(nameof(DotNetCakeInstaller));
        _WorkingFolder.CreateIfNecessary();
    }

    public bool IsCurrentGlobalDotNetCakeInstalled(IErrorsAndInfos errorsAndInfos) {
        return IsGlobalDotNetCakeInstalled(_pinnedCakeToolVersion, errorsAndInfos);
    }

    public bool IsGlobalDotNetCakeInstalled(string version, IErrorsAndInfos errorsAndInfos) {
        _ProcessRunner.RunProcess(_dotNetExecutableFileName, _dotNetToolListArguments, _WorkingFolder, errorsAndInfos);
        if (errorsAndInfos.AnyErrors()) { return false; }

        string line = errorsAndInfos.Infos.FirstOrDefault(l => l.StartsWith(_cakeToolId));
        return line?.Substring(_cakeToolId.Length).TrimStart().StartsWith(version) == true;
    }

    public void InstallOrUpdateGlobalDotNetCakeIfNecessary(IErrorsAndInfos errorsAndInfos) {
        if (IsGlobalDotNetCakeInstalled(_pinnedCakeToolVersion, errorsAndInfos)) { return; }
        if (errorsAndInfos.AnyErrors()) { return; }

        bool oldPinnedCakeToolVersionInstalled =
            IsGlobalDotNetCakeInstalled(_veryOldPinnedCakeToolVersion, errorsAndInfos)
            || IsGlobalDotNetCakeInstalled(_oldPinnedCakeToolVersion, errorsAndInfos);
        if (errorsAndInfos.AnyErrors()) { return; }

        _ProcessRunner.RunProcess(_dotNetExecutableFileName,
            oldPinnedCakeToolVersionInstalled
                ? _dotNetUpdateCakeToolArguments
                : _dotNetInstallCakeToolArguments,
            _WorkingFolder, errorsAndInfos);
        if (errorsAndInfos.AnyErrors()) { return; }

        if (IsGlobalDotNetCakeInstalled(_pinnedCakeToolVersion, errorsAndInfos)) { return; }
        errorsAndInfos.Errors.Add(Properties.Resources.CouldNotInstallCakeTool);
    }
}