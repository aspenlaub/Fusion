using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using System.Collections.Generic;
using System.Linq;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using NuGet.Packaging;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Components;

public class DotNetEfRunner(IProcessRunner processRunner) : IDotNetEfRunner {
    private const string _dotNetExecutableFileName = "dotnet";

    public void DropDatabase(IFolder projectFolder, IErrorsAndInfos errorsAndInfos) {
        if (!projectFolder.Exists()) {
            errorsAndInfos.Errors.Add(string.Format(Properties.Resources.FolderNotFound, projectFolder.FullName));
            return;
        }

        const string arguments = "ef database drop -f";
        processRunner.RunProcess(_dotNetExecutableFileName, arguments, projectFolder, errorsAndInfos);
    }

    public void UpdateDatabase(IFolder projectFolder, IErrorsAndInfos errorsAndInfos) {
        if (!projectFolder.Exists()) {
            errorsAndInfos.Errors.Add(string.Format(Properties.Resources.FolderNotFound, projectFolder.FullName));
            return;
        }

        const string arguments = "ef database update";
        processRunner.RunProcess(_dotNetExecutableFileName, arguments, projectFolder, errorsAndInfos);
    }

    public IList<string> ListAppliedMigrationIds(IFolder projectFolder, IErrorsAndInfos errorsAndInfos) {
        if (!projectFolder.Exists()) {
            errorsAndInfos.Errors.Add(string.Format(Properties.Resources.FolderNotFound, projectFolder.FullName));
            return new List<string>();
        }

        const string arguments = "ef migrations list";
        var runnerErrorsAndInfos = new ErrorsAndInfos();
        processRunner.RunProcess(_dotNetExecutableFileName, arguments, projectFolder, runnerErrorsAndInfos);
        if (runnerErrorsAndInfos.AnyErrors()) {
            errorsAndInfos.Errors.AddRange(runnerErrorsAndInfos.Errors);
            return new List<string>();
        }

        var migrationIds = runnerErrorsAndInfos.Infos
            .Where(i => i.StartsWith("20") && !i.Contains('('))
            .ToList();
        return migrationIds;
    }

    public void AddMigration(IFolder projectFolder, string migrationId, IErrorsAndInfos errorsAndInfos) {
        if (!projectFolder.Exists()) {
            errorsAndInfos.Errors.Add(string.Format(Properties.Resources.FolderNotFound, projectFolder.FullName));
            return;
        }

        string arguments = "ef migrations add " + migrationId;
        processRunner.RunProcess(_dotNetExecutableFileName, arguments, projectFolder, errorsAndInfos);
    }
}