using System.IO;
using System.Linq;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Skladasu.Interfaces;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Components;

public class DotNetEfInstaller : IDotNetEfInstaller {
    private const string _efToolId = "dotnet-ef";
    private const string _oldPinnedEfToolVersion = "7.0.5";
    private const string _pinnedEfToolVersion = "7.0.5";
    private const string _dotNetExecutableFileName = "dotnet";
    private const string _dotNetToolListArguments = "tool list --global";
    private const string _dotNetInstallEfToolArguments = "tool install dotnet-ef --version 7.0.5 --global";
    private const string _dotNetUpdateEfToolArguments = "tool update dotnet-ef --version 7.0.5 --global";

    private readonly IProcessRunner _ProcessRunner;
    private readonly IFolder _WorkingFolder;

    public DotNetEfInstaller(IProcessRunner processRunner) {
        _ProcessRunner = processRunner;
        _WorkingFolder = new Folder(Path.GetTempPath()).SubFolder(nameof(DotNetEfInstaller));
        _WorkingFolder.CreateIfNecessary();
    }

    public bool IsCurrentGlobalDotNetEfInstalled(IErrorsAndInfos errorsAndInfos) {
        return IsGlobalDotNetEfInstalled(_pinnedEfToolVersion, errorsAndInfos);
    }

    public bool IsGlobalDotNetEfInstalled(string version, IErrorsAndInfos errorsAndInfos) {
        _ProcessRunner.RunProcess(_dotNetExecutableFileName, _dotNetToolListArguments, _WorkingFolder, errorsAndInfos);
        if (errorsAndInfos.AnyErrors()) { return false; }

        string line = errorsAndInfos.Infos.FirstOrDefault(l => l.StartsWith(_efToolId));
        return line?.Substring(_efToolId.Length).TrimStart().StartsWith(version) == true;
    }

    public void InstallOrUpdateGlobalDotNetEfIfNecessary(IErrorsAndInfos errorsAndInfos) {
        if (IsGlobalDotNetEfInstalled(_pinnedEfToolVersion, errorsAndInfos)) { return; }
        if (errorsAndInfos.AnyErrors()) { return; }

        bool oldPinnedEfToolVersionInstalled = IsGlobalDotNetEfInstalled(_oldPinnedEfToolVersion, errorsAndInfos);
        if (errorsAndInfos.AnyErrors()) { return; }

        _ProcessRunner.RunProcess(_dotNetExecutableFileName, oldPinnedEfToolVersionInstalled ? _dotNetUpdateEfToolArguments : _dotNetInstallEfToolArguments, _WorkingFolder, errorsAndInfos);
        if (errorsAndInfos.AnyErrors()) { return; }

        if (IsGlobalDotNetEfInstalled(_pinnedEfToolVersion, errorsAndInfos)) { return; }
        errorsAndInfos.Errors.Add(Properties.Resources.CouldNotInstallEfTool);
    }
}