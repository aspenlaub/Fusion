using Aspenlaub.Net.GitHub.CSharp.Gitty.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Components;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using Autofac;
using LibGit2Sharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Extensions;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Test;

public class AutomationTestHelper : IDisposable {
    public IFolder AutomationTestProjectsFolder { get; }
    public IFolder TempFolder => AutomationTestProjectsFolder.SubFolder("Temp");
    public IFolder FinalFolder => AutomationTestProjectsFolder.SubFolder("Final");

    public AutomationTestHelper(string subFolder) {
        IContainer container = new ContainerBuilder().UseFusionNuclideProtchAndGitty("Fusion").Build();
        var errorsAndInfos = new ErrorsAndInfos();
        const string url = "https://github.com/aspenlaub/AutomationTestProjects.git";
        AutomationTestProjectsFolder = new Folder(Path.GetTempPath()).SubFolder("AspenlaubTemp").SubFolder(subFolder);

        DeleteAllFolders();
        AutomationTestProjectsFolder.CreateIfNecessary();
        try {
            container.Resolve<IGitUtilities>().Clone(url, "master", AutomationTestProjectsFolder, new CloneOptions { BranchName = "master" }, true, errorsAndInfos);
        } catch {
            errorsAndInfos.Errors.Add($"Could not clone {url}");
        }
        Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());

        TempFolder.CreateIfNecessary();

        // ReSharper disable once LoopCanBePartlyConvertedToQuery
        foreach (string solutionFileFullName in Directory.GetFiles(AutomationTestProjectsFolder.FullName, "*.slnx", SearchOption.AllDirectories)) {
            var folder = new Folder(solutionFileFullName.Substring(0, solutionFileFullName.LastIndexOf('\\')));
            Assert.IsFalse(File.Exists(folder.FullName + @"\packages.config"));
        }
    }

    protected void DeleteAllFolders() {
        var deleter = new FolderDeleter();

        if (AutomationTestProjectsFolder.Exists()) {
            deleter.DeleteFolder(AutomationTestProjectsFolder);
        }
        if (TempFolder.Exists()) {
            deleter.DeleteFolder(TempFolder);
        }
    }

    public void Dispose() {
        DeleteAllFolders();
    }
}
