using System;
using System.Linq;
using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Components;

public class MsBuilder(IShatilayaRunner shatilayaRunner) : IMsBuilder {
    public async Task<bool> BuildAsync(string solutionFileName, bool debug, IErrorsAndInfos errorsAndInfos) {
        if (!solutionFileName.Contains(".sln")) {
            throw new ArgumentException(nameof(solutionFileName));
        }
        string target = debug ? "DebugBuild" : "ReleaseBuild";
        if (!solutionFileName.EndsWith("slnx") || !solutionFileName.Contains(@"\src\")) {
            target = "Legacy" + target;
        }
        IFolder folder = new Folder(solutionFileName.Substring(0, solutionFileName.LastIndexOf('\\')));
        if (solutionFileName.Contains(@"\src\")) {
            folder = folder.ParentFolder();
        }
        await shatilayaRunner.RunShatilayaAsync(folder, target, errorsAndInfos);
        return !errorsAndInfos.Errors.Any();
    }

    public async Task<IFolder> BuildToTempAsync(string solutionFileName, bool debug, IErrorsAndInfos errorsAndInfos) {
        if (!solutionFileName.Contains(".sln")) {
            throw new ArgumentException(nameof(solutionFileName));
        }
        string target = debug ? "DebugBuildToTemp" : "ReleaseBuildToTemp";
        IFolder folder = new Folder(solutionFileName.Substring(0, solutionFileName.LastIndexOf('\\')));
        if (solutionFileName.Contains(@"\src\")) {
            folder = folder.ParentFolder();
        }
        await shatilayaRunner.RunShatilayaAsync(folder, target, errorsAndInfos);
        if (errorsAndInfos.AnyErrors()) {
            return null;
        }

        const string outputFolderTag = "Output folder is: ";
        string line = errorsAndInfos.Infos.SingleOrDefault(s => s.StartsWith(outputFolderTag));
        if (string.IsNullOrEmpty(line)) {
            errorsAndInfos.Errors.Add(Properties.Resources.OutputFolderCouldNotBeFound);
            return null;
        }

        var outputFolder = new Folder(line.Substring(outputFolderTag.Length));
        return outputFolder.Exists() ? outputFolder : null;
    }

    public async Task<IFolder> BuildSolutionOrCsProjToTempInReleaseAsync(string fileToBuildFullName, IErrorsAndInfos errorsAndInfos) {
        string target = fileToBuildFullName.Contains(".sln") ? "ReleaseBuildToTemp" : "ReleaseBuildCsProjToTemp";
        IFolder folder = new Folder(fileToBuildFullName.Substring(0, fileToBuildFullName.LastIndexOf('\\')));
        if (fileToBuildFullName.Contains(@"\src\")) {
            folder = folder.ParentFolder();
        }
        await shatilayaRunner.RunShatilayaAsync(folder, target, errorsAndInfos);
        if (errorsAndInfos.AnyErrors()) {
            return null;
        }

        const string outputFolderTag = "Output folder is: ";
        string line = errorsAndInfos.Infos.SingleOrDefault(s => s.StartsWith(outputFolderTag));
        if (string.IsNullOrEmpty(line)) {
            errorsAndInfos.Errors.Add(Properties.Resources.OutputFolderCouldNotBeFound);
            return null;
        }

        var outputFolder = new Folder(line.Substring(outputFolderTag.Length));
        return outputFolder.Exists() ? outputFolder : null;
    }
}