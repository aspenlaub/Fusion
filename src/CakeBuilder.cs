using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion {
    public class CakeBuilder : ICakeBuilder {
        private readonly ICakeReinstaller vCakeReinstaller;
        private readonly ICakeRunner vCakeRunner;

        public CakeBuilder(ICakeReinstaller cakeReinstaller, ICakeRunner cakeRunner) {
            vCakeReinstaller = cakeReinstaller;
            vCakeRunner = cakeRunner;
        }

        public bool Build(string solutionFileName, bool debug, string tempFolderName, IErrorsAndInfos errorsAndInfos) {
            vCakeReinstaller.ReinstallCake(errorsAndInfos);
            if (errorsAndInfos.AnyErrors()) {
                return false;
            }

            var config = debug ? "Debug" : "Release";
            var cakeScript = new List<string> {
                "Task(\"Build\")",
                ".Does(() => {",
                $"MSBuild(@\"{solutionFileName}\", settings ",
                "=> settings",
                $".SetConfiguration(\"{config}\")",
                ".SetVerbosity(Verbosity.Minimal)",
                ".WithProperty(\"Platform\", \"Any CPU\")",
                tempFolderName == "" ? "" : $".WithProperty(\"OutDir\", @\"{tempFolderName}\")",
                ");",
                "});",
                "RunTarget(\"Build\");"
            };
            var cakeFolderFullName = vCakeReinstaller.CakeFolderFullName;
            var cakeFileName = cakeFolderFullName + @"\" + "build-" + Guid.NewGuid() + ".cake";
            File.WriteAllText(cakeFileName, string.Join("\r\n", cakeScript));
            vCakeRunner.CallCake(cakeFolderFullName + @"\" + @"tools\Cake\cake.exe", cakeFileName, errorsAndInfos);
            if (!errorsAndInfos.Errors.Any(e => e.Contains("The file is locked"))) {
                errorsAndInfos.Infos.Where(e => e.Contains("The file is locked")).ToList().ForEach(e => errorsAndInfos.Errors.Add(e));
            }
            return !errorsAndInfos.Errors.Any();
        }
    }
}
