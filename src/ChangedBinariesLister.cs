﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Entities;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Nuclide.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using LibGit2Sharp;
using NuGet.Packaging;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion {
    public class ChangedBinariesLister : IChangedBinariesLister {
        private readonly IBinariesHelper vBinariesHelper;
        private readonly ICakeBuilder vCakeBuilder;
        private readonly IFolderDeleter vFolderDeleter;
        private readonly IGitUtilities vGitUtilities;
        private readonly INugetPackageRestorer vNugetPackageRestorer;

        public ChangedBinariesLister(IBinariesHelper binariesHelper, ICakeBuilder cakeBuilder, IFolderDeleter folderDeleter, IGitUtilities gitUtilities, INugetPackageRestorer nugetPackageRestorer) {
            vBinariesHelper = binariesHelper;
            vCakeBuilder = cakeBuilder;
            vFolderDeleter = folderDeleter;
            vGitUtilities = gitUtilities;
            vNugetPackageRestorer = nugetPackageRestorer;
        }

        public IList<BinaryToUpdate> ListChangedBinaries(string repositoryId, string previousHeadTipIdSha, string currentHeadTipIdSha, IErrorsAndInfos errorsAndInfos) {
            IList<BinaryToUpdate> changedBinaries;

            var workFolder = new Folder(Path.GetTempPath()).SubFolder("AspenlaubTemp").SubFolder(nameof(ChangedBinariesLister)).SubFolder(repositoryId);
            try {
                CleanUpFolder(workFolder);
                workFolder.CreateIfNecessary();
                changedBinaries = ListChangedBinaries(repositoryId, previousHeadTipIdSha, currentHeadTipIdSha, workFolder, errorsAndInfos, true);
                if (changedBinaries.Any()) {
                    changedBinaries = ListChangedBinaries(repositoryId, previousHeadTipIdSha, currentHeadTipIdSha, workFolder, errorsAndInfos, false);
                }
            } catch (Exception e) {
                errorsAndInfos.Errors.Add(e.Message);
                changedBinaries = new List<BinaryToUpdate>();
            } finally {
                CleanUpFolder(workFolder);
            }

            return changedBinaries;
        }

        private void CleanUpFolder(IFolder folder) {
            if (folder.Exists()) {
                vFolderDeleter.DeleteFolder(folder);
            }
        }

        private IList<BinaryToUpdate> ListChangedBinaries(string repositoryId, string previousHeadTipIdSha, string currentHeadTipIdSha, IFolder workFolder, IErrorsAndInfos errorsAndInfos, bool doNotListFilesOfEqualLengthThatCanBeTreatedAsEqual) {
            var changedBinaries = new List<BinaryToUpdate>();
            var compileFolder = workFolder.SubFolder("Compile");
            var previousTargetFolder = workFolder.SubFolder("Previous");
            var currentTargetFolder = workFolder.SubFolder("Current");

            foreach (var previous in new[] { true, false}) {
                CleanUpFolder(compileFolder);
                compileFolder.CreateIfNecessary();

                var url = "https://github.com/aspenlaub/" + repositoryId + ".git";
                vGitUtilities.Clone(url, "master", compileFolder, new CloneOptions { BranchName = "master" }, true, errorsAndInfos);
                if (errorsAndInfos.AnyErrors()) { return changedBinaries; }

                var headTipIdSha = previous ? previousHeadTipIdSha : currentHeadTipIdSha;
                vGitUtilities.Reset(compileFolder, headTipIdSha, errorsAndInfos);
                if (errorsAndInfos.AnyErrors()) { return changedBinaries; }

                var csProjFiles = Directory.GetFiles(workFolder.FullName, "*.csproj", SearchOption.AllDirectories).ToList();
                foreach (var csProjFile in csProjFiles) {
                    var contents = File.ReadAllLines(csProjFile).ToList();
                    contents = contents.Select(AdjustLineIfVersioningRelated).ToList();
                    File.WriteAllLines(csProjFile, contents);
                }

                var solutionFileName = compileFolder.SubFolder("src").FullName + @"\" + repositoryId + ".sln";
                errorsAndInfos.Infos.Add(string.Format(Properties.Resources.Restoring, repositoryId, headTipIdSha));
                var restoreErrorsAndInfos = new ErrorsAndInfos();
                vNugetPackageRestorer.RestoreNugetPackages(solutionFileName, restoreErrorsAndInfos);
                if (restoreErrorsAndInfos.AnyErrors()) {
                    errorsAndInfos.Errors.Add(string.Format(Properties.Resources.FailedToRestore, repositoryId, headTipIdSha));
                    errorsAndInfos.Errors.AddRange(restoreErrorsAndInfos.Errors);
                    return changedBinaries;
                }

                errorsAndInfos.Infos.Add(string.Format(Properties.Resources.Building, repositoryId, headTipIdSha));
                var buildErrorsAndInfos = new ErrorsAndInfos();
                vCakeBuilder.Build(solutionFileName, false, "", buildErrorsAndInfos);
                if (buildErrorsAndInfos.AnyErrors()) {
                    errorsAndInfos.Errors.Add(string.Format(Properties.Resources.FailedToBuild, repositoryId, headTipIdSha));
                    errorsAndInfos.Errors.AddRange(buildErrorsAndInfos.Errors);
                    return changedBinaries;
                }

                var binFolder = compileFolder.SubFolder("src").SubFolder("bin").SubFolder("Release");
                var targetFolder = previous ? previousTargetFolder : currentTargetFolder;
                CleanUpFolder(targetFolder);
                targetFolder.CreateIfNecessary();
                foreach (var shortFileName in Directory.GetFiles(binFolder.FullName, "*.*", SearchOption.AllDirectories).Select(f => f.Substring(binFolder.FullName.Length + 1))) {
                    File.Copy(binFolder.FullName + '\\' + shortFileName, targetFolder.FullName + '\\' + shortFileName);
                }
            }

            foreach (var shortFileName in Directory.GetFiles(currentTargetFolder.FullName, "*.*", SearchOption.AllDirectories).Select(f => f.Substring(currentTargetFolder.FullName.Length + 1))) {
                var previousFileName = previousTargetFolder.FullName + '\\' + shortFileName;
                var currentFileName = currentTargetFolder.FullName + '\\' + shortFileName;
                if (!File.Exists(previousFileName)) {
                    changedBinaries.Add(new BinaryToUpdate { FileName = shortFileName, UpdateReason = Properties.Resources.FileIsNew });
                    continue;
                }

                var previousFileInfo = new FileInfo(previousFileName);
                var currentFileInfo = new FileInfo(currentFileName);

                var previousContents = File.ReadAllBytes(previousFileName);
                var currentContents = File.ReadAllBytes(currentFileName);
                if (previousContents.Length != currentContents.Length) {
                    changedBinaries.Add(new BinaryToUpdate { FileName = shortFileName, UpdateReason = string.Format(Properties.Resources.FilesDifferInLength, previousContents.Length, currentContents.Length) });
                    continue;
                }

                var differences = previousContents.Where((t, i) => t != currentContents[i]).Count();
                if (differences == 0) {
                    continue;
                }

                if (vBinariesHelper.CanFilesOfEqualLengthBeTreatedEqual(FolderUpdateMethod.AssembliesButNotIfOnlySlightlyChanged, "", previousContents, currentContents, previousFileInfo,
                    false, currentFileInfo, out var updateReason)) {
                    if (!doNotListFilesOfEqualLengthThatCanBeTreatedAsEqual) {
                        changedBinaries.Add(new BinaryToUpdate { FileName = shortFileName, UpdateReason = Properties.Resources.OtherFilesRequireUpdateAnyway });
                    }

                    continue;
                }

                changedBinaries.Add(new BinaryToUpdate { FileName = shortFileName, UpdateReason = updateReason });
            }

            return changedBinaries;
        }

        private string AdjustLineIfVersioningRelated(string s) {
            if (s.Contains("<VersionDays>")) { return "    <VersionDays>24</VersionDays>"; }
            return s.Contains("<VersionMinutes") ? "    <VersionMinutes>7</VersionMinutes>" : s;
        }
    }
}