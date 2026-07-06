using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Skladasu.Entities;
using Aspenlaub.Net.GitHub.CSharp.Skladasu.Interfaces;
using NuGet.Packaging;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Components;

public class DotNetEfRunner(IProcessRunner processRunner) : IDotNetEfRunner {
    private const string _dotNetExecutableFileName = "dotnet";

    public async Task DropDatabaseAsync(IFolder projectFolder, IErrorsAndInfos errorsAndInfos, CancellationToken cancellationToken) {
        if (!projectFolder.Exists()) {
            errorsAndInfos.Errors.Add(string.Format(Properties.Resources.FolderNotFound, projectFolder.FullName));
            return;
        }

        const string arguments = "ef database drop -f";
        await processRunner.RunProcessAsync(_dotNetExecutableFileName, arguments, projectFolder, errorsAndInfos, cancellationToken);
    }

    public async Task UpdateDatabaseAsync(IFolder projectFolder, IErrorsAndInfos errorsAndInfos, CancellationToken cancellationToken) {
        if (!projectFolder.Exists()) {
            errorsAndInfos.Errors.Add(string.Format(Properties.Resources.FolderNotFound, projectFolder.FullName));
            return;
        }

        const string arguments = "ef database update";
        await processRunner.RunProcessAsync(_dotNetExecutableFileName, arguments, projectFolder, errorsAndInfos, cancellationToken);
    }

    public async Task<IList<string>> ListAppliedMigrationIdsAsync(IFolder projectFolder, IErrorsAndInfos errorsAndInfos, CancellationToken cancellationToken) {
        if (!projectFolder.Exists()) {
            errorsAndInfos.Errors.Add(string.Format(Properties.Resources.FolderNotFound, projectFolder.FullName));
            return new List<string>();
        }

        const string arguments = "ef migrations list";
        var runnerErrorsAndInfos = new ErrorsAndInfos();
        await processRunner.RunProcessAsync(_dotNetExecutableFileName, arguments, projectFolder, runnerErrorsAndInfos, cancellationToken);
        if (runnerErrorsAndInfos.AnyErrors()) {
            errorsAndInfos.Errors.AddRange(runnerErrorsAndInfos.Errors);
            return new List<string>();
        }

        var migrationIds = runnerErrorsAndInfos.Infos
            .Where(i => i.StartsWith("20") && !i.Contains('('))
            .ToList();
        return migrationIds;
    }

    public async Task AddMigrationAsync(IFolder projectFolder, string migrationId, IErrorsAndInfos errorsAndInfos, CancellationToken cancellationToken) {
        if (!projectFolder.Exists()) {
            errorsAndInfos.Errors.Add(string.Format(Properties.Resources.FolderNotFound, projectFolder.FullName));
            return;
        }

        string arguments = "ef migrations add " + migrationId;
        await processRunner.RunProcessAsync(_dotNetExecutableFileName, arguments, projectFolder, errorsAndInfos, cancellationToken);
    }
}