using System.IO;
using System.Linq;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Components;

public class DotNetEfInstaller : IDotNetEfInstaller {
    private const string EfToolId = "dotnet-ef";
    private const string OldPinnedEfToolVersion = "7.0.5";
    private const string PinnedEfToolVersion = "7.0.5";
    private const string DotNetExecutableFileName = "dotnet";
    private const string DotNetToolListArguments = "tool list --global";
    private const string DotNetInstallEfToolArguments = "tool install dotnet-ef --version 7.0.5 --global";
    private const string DotNetUpdateEfToolArguments = "tool update dotnet-ef --version 7.0.5 --global";

    private readonly IProcessRunner _ProcessRunner;
    private readonly IFolder _WorkingFolder;

    public DotNetEfInstaller(IProcessRunner processRunner) {
        _ProcessRunner = processRunner;
        _WorkingFolder = new Folder(Path.GetTempPath()).SubFolder(nameof(DotNetEfInstaller));
        _WorkingFolder.CreateIfNecessary();
    }

    public bool IsCurrentGlobalDotNetEfInstalled(IErrorsAndInfos errorsAndInfos) {
        return IsGlobalDotNetEfInstalled(PinnedEfToolVersion, errorsAndInfos);
    }

    public bool IsGlobalDotNetEfInstalled(string version, IErrorsAndInfos errorsAndInfos) {
        _ProcessRunner.RunProcess(DotNetExecutableFileName, DotNetToolListArguments, _WorkingFolder, errorsAndInfos);
        if (errorsAndInfos.AnyErrors()) { return false; }

        var line = errorsAndInfos.Infos.FirstOrDefault(l => l.StartsWith(EfToolId));
        return line?.Substring(EfToolId.Length).TrimStart().StartsWith(version) == true;
    }

    public void InstallOrUpdateGlobalDotNetEfIfNecessary(IErrorsAndInfos errorsAndInfos) {
        if (IsGlobalDotNetEfInstalled(PinnedEfToolVersion, errorsAndInfos)) { return; }
        if (errorsAndInfos.AnyErrors()) { return; }

        var oldPinnedEfToolVersionInstalled = IsGlobalDotNetEfInstalled(OldPinnedEfToolVersion, errorsAndInfos);
        if (errorsAndInfos.AnyErrors()) { return; }

        _ProcessRunner.RunProcess(DotNetExecutableFileName, oldPinnedEfToolVersionInstalled ? DotNetUpdateEfToolArguments : DotNetInstallEfToolArguments, _WorkingFolder, errorsAndInfos);
        if (errorsAndInfos.AnyErrors()) { return; }

        if (IsGlobalDotNetEfInstalled(PinnedEfToolVersion, errorsAndInfos)) { return; }
        errorsAndInfos.Errors.Add(Properties.Resources.CouldNotInstallEfTool);
    }
}