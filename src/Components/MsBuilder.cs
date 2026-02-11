using System.Linq;
using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Components;

public class MsBuilder(IShatilayaRunner shatilayaRunner) : IMsBuilder {
    public async Task<bool> BuildAsync(string solutionFileName, bool debug, IErrorsAndInfos errorsAndInfos) {
        string target = debug ? "PlainDebugBuild" : "PlainReleaseBuild";
        var folder = new Folder(solutionFileName.Substring(0, solutionFileName.LastIndexOf('\\')));
        await shatilayaRunner.RunShatilayaAsync(folder, target, errorsAndInfos);
        return !errorsAndInfos.Errors.Any();
    }
}