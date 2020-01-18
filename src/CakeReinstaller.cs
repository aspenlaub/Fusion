using System;
using System.IO;
using System.Threading;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Components;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using NuGet.Packaging;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion {
    public class CakeReinstaller : ICakeReinstaller {
        public string CakeFolderFullName => CakeFolder.FullName;

        protected IFolder CakeFolder => new Folder(Path.GetTempPath()).SubFolder("Cake");

        private readonly ICakeInstaller vCakeInstaller;

        public CakeReinstaller(ICakeInstaller cakeInstaller) {
            vCakeInstaller = cakeInstaller;
        }

        public void ReinstallCake(IErrorsAndInfos errorsAndInfos) {
            if (DeleteCakeFolder()) {
                foreach (var cakeFile in Directory.GetFiles(CakeFolder.FullName, "build-*.cake")) {
                    File.Delete(cakeFile);
                }

                var deleter = new FolderDeleter();
                deleter.DeleteFolder(CakeFolder);
            }

            if (CakeFolder.Exists()) {
                return;
            }

            Directory.CreateDirectory(CakeFolder.FullName);
            Thread.Sleep(TimeSpan.FromSeconds(2));
            vCakeInstaller.InstallCake(CakeFolder, out var cakeInstallerErrorsAndInfos);
            errorsAndInfos.Errors.AddRange(cakeInstallerErrorsAndInfos.Errors);
        }

        private bool DeleteCakeFolder() {
            if (!CakeFolder.Exists()) { return false; }
            return !File.Exists(CakeFolder.SubFolder(@"tools\Cake").FullName + @"\cake.exe")
                   || !File.Exists(CakeFolder.SubFolder(@"tools").FullName + @"\packages.config");
        }
    }
}
