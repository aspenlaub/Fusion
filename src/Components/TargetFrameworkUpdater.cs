using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Protch.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Skladasu.Interfaces;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Components;

public class TargetFrameworkUpdater(IGitUtilities gitUtilities, IProjectFactory projectFactory) : ITargetFrameworkUpdater {
    public async Task<List<string>> InstalledTargetFrameworksAsync(IErrorsAndInfos errorsAndInfos) {
        var installedTargetFrameworks = new List<string>();

        var startInfo = new ProcessStartInfo {
            FileName = "dotnet",
            Arguments = "--list-sdks",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try {
            using var process = Process.Start(startInfo);
            if (process == null) {
                return installedTargetFrameworks;
            }

            string output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            string[] lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            installedTargetFrameworks.AddRange((
                from line in lines let bracketIndex = line.IndexOf('[')
                    where bracketIndex > 0
                        select TargetFrameworkMoniker(line.Substring(0, bracketIndex).Trim())

            ).Where(x => !string.IsNullOrEmpty(x)));
        } catch (Exception ex) {
            errorsAndInfos.Errors.Add($"Could not determine installed target frameworks: {ex.Message}");
        }

        return [.. installedTargetFrameworks.OrderByDescending(ExtractMajorVersionNumber)];
    }


    public static int ExtractMajorVersionNumber(string s) {
        if (!s.Contains('.')) {
            return -1;
        }

        s = s.Substring(0, s.IndexOf('.'));
        if (!s.StartsWith("net")) {
            return -1;
        }

        s = s.Substring(3);
        return int.TryParse(s, out int majorVersionNumber) ? majorVersionNumber : -1;
    }

    private static string TargetFrameworkMoniker(string version) {
        var parts = version.Split('.').ToList();
        return parts.Count != 3 ? "" : $"net{parts[0]}.{parts[1]}";
    }

    public async Task<YesNoInconclusive> IsThereATargetFrameworkUpdateOpportunityAsync(IFolder repositoryFolder,
            IErrorsAndInfos errorsAndInfos) {
        string checkedOutBranch = gitUtilities.CheckedOutBranch(repositoryFolder);
        if (checkedOutBranch != "master" && checkedOutBranch != "main") {
            return new YesNoInconclusive { Inconclusive = true };
        }

        List<string> targetFrameworks = await InstalledTargetFrameworksAsync(errorsAndInfos);
        if (errorsAndInfos.AnyErrors()) {
            return new YesNoInconclusive { Inconclusive = true };
        }

        List<IProject> projects = await GetProjectsInRepository(repositoryFolder, errorsAndInfos);

        string targetFramework = "";
        foreach (IProject project in projects) {
            if (!targetFrameworks.Any(x => project.TargetFramework.StartsWith(x))) {
                return new YesNoInconclusive { Inconclusive = true };
            }

            string projectTargetFramework = targetFrameworks.First(x => project.TargetFramework.StartsWith(x));
            if (targetFramework == "") {
                targetFramework = projectTargetFramework;
            } else if (targetFramework != projectTargetFramework) {
                return new YesNoInconclusive { Inconclusive = false, YesNo = false };
            }
        }

        return new YesNoInconclusive { Inconclusive = false, YesNo = targetFrameworks.IndexOf(targetFramework) > 0 };
    }

    private async Task<List<IProject>> GetProjectsInRepository(IFolder repositoryFolder, IErrorsAndInfos errorsAndInfos) {
        var solutionFileFullNames = Directory.GetFiles(repositoryFolder.FullName, "*.slnx", SearchOption.AllDirectories).ToList();
        if (solutionFileFullNames.Count != 1) {
            return [];
        }

        string solutionFileFullName = solutionFileFullNames[0];
        var projectFileFullNames = Directory.GetFiles(repositoryFolder.FullName, "*.csproj", SearchOption.AllDirectories).ToList();
        List<string> targetFrameworks = await InstalledTargetFrameworksAsync(errorsAndInfos);
        if (errorsAndInfos.AnyErrors()) {
            return [];
        }

        var projects = new List<IProject>();
        foreach (IProject project in projectFileFullNames
                .Select(projectFileFullName => projectFactory.Load(solutionFileFullName, projectFileFullName, errorsAndInfos))) {
            if (!targetFrameworks.Any(x => project.TargetFramework.StartsWith(x))) {
                return [];
            }

            projects.Add(project);
        }

        return projects;
    }

    public async Task UpdateTargetFrameworkInRepositoryAsync(IFolder repositoryFolder,
            IErrorsAndInfos errorsAndInfos, CancellationToken cancellationToken) {
        List<string> targetFrameworks = await InstalledTargetFrameworksAsync(errorsAndInfos);
        if (errorsAndInfos.AnyErrors()) {
            return;
        }

        await UpdateTargetFrameworkInRepositoryAsync(repositoryFolder, targetFrameworks[0], errorsAndInfos, cancellationToken);
    }

    public async Task UpdateTargetFrameworkInRepositoryAsync(IFolder repositoryFolder, string targetFramework,
            IErrorsAndInfos errorsAndInfos, CancellationToken cancellationToken) {
        string checkedOutBranch = gitUtilities.CheckedOutBranch(repositoryFolder);
        if (checkedOutBranch != "master" && checkedOutBranch != "main") {
            errorsAndInfos.Errors.Add($"Checked out branch is {checkedOutBranch}");
        }

        List<string> targetFrameworks = await InstalledTargetFrameworksAsync(errorsAndInfos);
        if (errorsAndInfos.AnyErrors()) {
            return;
        }

        List<IProject> projects = await GetProjectsInRepository(repositoryFolder, errorsAndInfos);
        if (errorsAndInfos.AnyErrors()) {
            return;
        }

        foreach (IProject project in projects) {
            string projectTargetFramework = targetFrameworks.First(x => project.TargetFramework.StartsWith(x));
            string projectFileFullName = project.ProjectFileFullName;
            string contents = await File.ReadAllTextAsync(projectFileFullName, cancellationToken);
            contents = contents.Replace(
                project.TargetFramework,
                project.TargetFramework.Replace(projectTargetFramework, targetFramework)
            );
            await File.WriteAllTextAsync(projectFileFullName, contents, cancellationToken);
        }
    }
}
