using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Skladasu.Interfaces;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Components;

public class DotNetBuilder(IProcessRunner processRunner) : IDotNetBuilder {
    private const string _dotNetExecutableFileName = "dotnet";

    public async Task<bool> BuildAsync(string solutionFileName, bool debug, string tempFolderName, IErrorsAndInfos errorsAndInfos,
            CancellationToken cancellationToken) {
        if (!solutionFileName.Contains(".sln")) {
            throw new ArgumentException(nameof(solutionFileName));
        }
        string arguments = "build " + solutionFileName + " -v m -c " + (debug ? "Debug" : "Release");
        if (!string.IsNullOrEmpty(tempFolderName)) {
            arguments = arguments + " -o \"" + tempFolderName + "\"";
        }

        var solutionFolder = new Folder(
            solutionFileName.Substring(0, solutionFileName.LastIndexOf("\\", StringComparison.Ordinal))
        );
        await processRunner.RunProcessAsync(_dotNetExecutableFileName, arguments, solutionFolder, errorsAndInfos, cancellationToken);
        InfosToErrors(errorsAndInfos, "the file is locked", "");
        InfosToErrors(errorsAndInfos, "failed", "");
        InfosToErrors(errorsAndInfos, "error", "0 error");
        return !errorsAndInfos.Errors.Any();
    }

    private void InfosToErrors(IErrorsAndInfos errorsAndInfos, string cue, string ignore) {
        if (errorsAndInfos.Errors.Any(e =>
                e.Contains(cue, StringComparison.InvariantCultureIgnoreCase)
                && (string.IsNullOrEmpty(ignore)
                    || !e.Contains(ignore, StringComparison.InvariantCultureIgnoreCase)))) {
            return;
        }

        errorsAndInfos.Infos.Where(e =>
                e.Contains(cue, StringComparison.InvariantCultureIgnoreCase)
                && (string.IsNullOrEmpty(ignore)
                    || !e.Contains(ignore, StringComparison.InvariantCultureIgnoreCase)))
            .ToList()
            .ForEach(errorsAndInfos.Errors.Add);
    }
}