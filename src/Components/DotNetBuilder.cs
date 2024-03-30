using System;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using System.Linq;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Components;

public class DotNetBuilder(IProcessRunner processRunner) : IDotNetBuilder {
    private const string DotNetExecutableFileName = "dotnet";

    public bool Build(string solutionFileName, bool debug, string tempFolderName, IErrorsAndInfos errorsAndInfos) {
        var arguments = "build -v m -c " + (debug ? "Debug" : "Release");
        if (!string.IsNullOrEmpty(tempFolderName)) {
            arguments = arguments + " -o \"" + tempFolderName + "\"";
        }

        var solutionFolder = new Folder(
            solutionFileName.Substring(0, solutionFileName.LastIndexOf("\\", StringComparison.Ordinal))
        );
        processRunner.RunProcess(DotNetExecutableFileName, arguments, solutionFolder, errorsAndInfos);
        if (!errorsAndInfos.Errors.Any(e => e.Contains("The file is locked"))) {
            errorsAndInfos.Infos.Where(e => e.Contains("The file is locked")).ToList().ForEach(e => errorsAndInfos.Errors.Add(e));
        }
        return !errorsAndInfos.Errors.Any();
    }
}