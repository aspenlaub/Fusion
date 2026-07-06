using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

    public async Task<bool> IsCurrentGlobalDotNetEfInstalledAsync(IErrorsAndInfos errorsAndInfos, CancellationToken cancellationToken) {
        return await IsGlobalDotNetEfInstalledAsync(_pinnedEfToolVersion, errorsAndInfos, cancellationToken);
    }

    public async Task<bool> IsGlobalDotNetEfInstalledAsync(string version, IErrorsAndInfos errorsAndInfos, CancellationToken cancellationToken) {
        await _ProcessRunner.RunProcessAsync(_dotNetExecutableFileName, _dotNetToolListArguments, _WorkingFolder, errorsAndInfos, cancellationToken);
        if (errorsAndInfos.AnyErrors()) { return false; }

        string line = errorsAndInfos.Infos.FirstOrDefault(l => l.StartsWith(_efToolId));
        return line?.Substring(_efToolId.Length).TrimStart().StartsWith(version) == true;
    }

    public async Task InstallOrUpdateGlobalDotNetEfIfNecessaryAsync(IErrorsAndInfos errorsAndInfos, CancellationToken cancellationToken) {
        if (await IsGlobalDotNetEfInstalledAsync(_pinnedEfToolVersion, errorsAndInfos, cancellationToken)) { return; }
        if (errorsAndInfos.AnyErrors()) { return; }

        bool oldPinnedEfToolVersionInstalled = await IsGlobalDotNetEfInstalledAsync(_oldPinnedEfToolVersion, errorsAndInfos, cancellationToken);
        if (errorsAndInfos.AnyErrors()) { return; }

        await _ProcessRunner.RunProcessAsync(_dotNetExecutableFileName,
            oldPinnedEfToolVersionInstalled ? _dotNetUpdateEfToolArguments : _dotNetInstallEfToolArguments, _WorkingFolder,
            errorsAndInfos, cancellationToken);
        if (errorsAndInfos.AnyErrors()) { return; }

        if (await IsGlobalDotNetEfInstalledAsync(_pinnedEfToolVersion, errorsAndInfos, cancellationToken)) { return; }
        errorsAndInfos.Errors.Add(Properties.Resources.CouldNotInstallEfTool);
    }
}