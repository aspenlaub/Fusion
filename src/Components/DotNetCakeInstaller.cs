using System.IO;
using System.Linq;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using IDotNetCakeInstaller = Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces.IDotNetCakeInstaller;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Components;

public class DotNetCakeInstaller : IDotNetCakeInstaller {
    private const string CakeToolId = "cake.tool";
    private const string VeryOldPinnedCakeToolVersion = "3.1.0";
    private const string OldPinnedCakeToolVersion = "4.0.0";
    private const string PinnedCakeToolVersion = "5.0.0";
    private const string DotNetExecutableFileName = "dotnet";
    private const string DotNetToolListArguments = "tool list --global";
    private const string DotNetInstallCakeToolArguments = "tool install Cake.Tool --version "
        + PinnedCakeToolVersion + " --global";
    private const string DotNetUpdateCakeToolArguments = "tool update Cake.Tool --version "
        + PinnedCakeToolVersion + " --global";

    private readonly IProcessRunner _ProcessRunner;
    private readonly IFolder _WorkingFolder;

    public DotNetCakeInstaller(IProcessRunner processRunner) {
        _ProcessRunner = processRunner;
        _WorkingFolder = new Folder(Path.GetTempPath()).SubFolder(nameof(DotNetCakeInstaller));
        _WorkingFolder.CreateIfNecessary();
    }

    public bool IsCurrentGlobalDotNetCakeInstalled(IErrorsAndInfos errorsAndInfos) {
        return IsGlobalDotNetCakeInstalled(PinnedCakeToolVersion, errorsAndInfos);
    }

    public bool IsGlobalDotNetCakeInstalled(string version, IErrorsAndInfos errorsAndInfos) {
        _ProcessRunner.RunProcess(DotNetExecutableFileName, DotNetToolListArguments, _WorkingFolder, errorsAndInfos);
        if (errorsAndInfos.AnyErrors()) { return false; }

        var line = errorsAndInfos.Infos.FirstOrDefault(l => l.StartsWith(CakeToolId));
        return line?.Substring(CakeToolId.Length).TrimStart().StartsWith(version) == true;
    }

    public void InstallOrUpdateGlobalDotNetCakeIfNecessary(IErrorsAndInfos errorsAndInfos) {
        if (IsGlobalDotNetCakeInstalled(PinnedCakeToolVersion, errorsAndInfos)) { return; }
        if (errorsAndInfos.AnyErrors()) { return; }

        var oldPinnedCakeToolVersionInstalled =
            IsGlobalDotNetCakeInstalled(VeryOldPinnedCakeToolVersion, errorsAndInfos)
            || IsGlobalDotNetCakeInstalled(OldPinnedCakeToolVersion, errorsAndInfos);
        if (errorsAndInfos.AnyErrors()) { return; }

        _ProcessRunner.RunProcess(DotNetExecutableFileName,
            oldPinnedCakeToolVersionInstalled
                ? DotNetUpdateCakeToolArguments
                : DotNetInstallCakeToolArguments,
            _WorkingFolder, errorsAndInfos);
        if (errorsAndInfos.AnyErrors()) { return; }

        if (IsGlobalDotNetCakeInstalled(PinnedCakeToolVersion, errorsAndInfos)) { return; }
        errorsAndInfos.Errors.Add(Properties.Resources.CouldNotInstallCakeTool);
    }
}