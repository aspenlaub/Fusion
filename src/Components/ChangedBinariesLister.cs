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

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Components;

public class ChangedBinariesLister(IBinariesHelper binariesHelper, ICakeBuilder cakeBuilder,
        IFolderDeleter folderDeleter, IGitUtilities gitUtilities, INugetPackageRestorer nugetPackageRestorer)
    : IChangedBinariesLister {

    public IList<BinaryToUpdate> ListChangedBinaries(string repositoryId, string branchId, string previousHeadTipIdSha, string currentHeadTipIdSha, IErrorsAndInfos errorsAndInfos) {
        IList<BinaryToUpdate> changedBinaries = new List<BinaryToUpdate>();

        var workFolder = new Folder(Path.GetTempPath()).SubFolder("AspenlaubTemp").SubFolder(nameof(ChangedBinariesLister)).SubFolder(repositoryId);
        try {
            CleanUpFolder(workFolder, errorsAndInfos);
            if (!errorsAndInfos.AnyErrors()) {
                workFolder.CreateIfNecessary();
                changedBinaries = ListChangedBinaries(repositoryId, branchId, previousHeadTipIdSha, currentHeadTipIdSha, workFolder, errorsAndInfos, true);
                if (changedBinaries.Any()) {
                    changedBinaries = ListChangedBinaries(repositoryId, branchId, previousHeadTipIdSha, currentHeadTipIdSha, workFolder, errorsAndInfos, false);
                }
            }
        } catch (Exception e) {
            errorsAndInfos.Errors.Add(e.Message);
            changedBinaries = new List<BinaryToUpdate>();
        } finally {
            CleanUpFolder(workFolder, errorsAndInfos);
        }

        return changedBinaries;
    }

    private void CleanUpFolder(IFolder folder, IErrorsAndInfos errorsAndInfos) {
        if (!folder.Exists()) {
            return;
        }

        if (!folderDeleter.CanDeleteFolder(folder)) {
            errorsAndInfos.Errors.Add($"Folder deleter refuses to delete {folder.FullName}");
            return;
        }

        try {
            foreach (var file in Directory.GetFiles(folder.FullName, "*.*", SearchOption.AllDirectories)) {
                File.SetAttributes(file, FileAttributes.Normal);
            }
            folderDeleter.DeleteFolder(folder);
        } catch (Exception e) {
            errorsAndInfos.Errors.Add($"Could not delete {folder.FullName}");
            errorsAndInfos.Errors.Add(e.Message);
        }
    }

    private IList<BinaryToUpdate> ListChangedBinaries(string repositoryId, string branchId, string previousHeadTipIdSha, string currentHeadTipIdSha, IFolder workFolder, IErrorsAndInfos errorsAndInfos, bool doNotListFilesOfEqualLengthThatCanBeTreatedAsEqual) {
        var changedBinaries = new List<BinaryToUpdate>();
        var compileFolder = workFolder.SubFolder("Compile");
        var previousTargetFolder = workFolder.SubFolder("Previous");
        var currentTargetFolder = workFolder.SubFolder("Current");

        foreach (var previous in new[] { true, false}) {
            CleanUpFolder(compileFolder, errorsAndInfos);
            if (errorsAndInfos.AnyErrors()) { return changedBinaries; }
            compileFolder.CreateIfNecessary();

            var url = "https://github.com/aspenlaub/" + repositoryId + ".git";
            gitUtilities.Clone(url, branchId, compileFolder, new CloneOptions { BranchName = branchId }, false, errorsAndInfos);
            if (errorsAndInfos.AnyErrors()) { return changedBinaries; }

            var headTipIdSha = previous ? previousHeadTipIdSha : currentHeadTipIdSha;
            gitUtilities.Reset(compileFolder, headTipIdSha, errorsAndInfos);
            if (errorsAndInfos.AnyErrors()) { return changedBinaries; }

            var folderCleanUpErrorsAndInfos = new ErrorsAndInfos();
            var csProjFiles = Directory.GetFiles(workFolder.FullName, "*.csproj", SearchOption.AllDirectories).ToList();
            var linksBuildCake = false;
            foreach (var csProjFile in csProjFiles) {
                var contents = File.ReadAllLines(csProjFile).ToList();
                contents = contents.Select(AdjustLineIfVersioningRelated).Select(MakeDeterministic).ToList();
                File.WriteAllLines(csProjFile, contents);
                linksBuildCake = linksBuildCake
                    || contents.Any(l => l.Contains("Link=\"build.cake\"")
                            || l.Contains("Include=\"..\\build.cake\"")
                            || l.Contains("Include=\"..\\..\\build.cake\"")
                       );
            }

            RemoveNonSourceCodeFiles(compileFolder, linksBuildCake, folderCleanUpErrorsAndInfos);
            if (folderCleanUpErrorsAndInfos.AnyErrors()) {
                errorsAndInfos.Infos.AddRange(folderCleanUpErrorsAndInfos.Infos);
                errorsAndInfos.Errors.AddRange(folderCleanUpErrorsAndInfos.Errors);
                return changedBinaries;
            }

            var solutionFileName = compileFolder.SubFolder("src").FullName + @"\" + repositoryId + ".sln";
            errorsAndInfos.Infos.Add(string.Format(Properties.Resources.Restoring, repositoryId, headTipIdSha));
            var restoreErrorsAndInfos = new ErrorsAndInfos();
            nugetPackageRestorer.RestoreNugetPackages(solutionFileName, restoreErrorsAndInfos);
            if (restoreErrorsAndInfos.AnyErrors()) {
                errorsAndInfos.Errors.Add(string.Format(Properties.Resources.FailedToRestore, repositoryId, headTipIdSha));
                errorsAndInfos.Errors.AddRange(restoreErrorsAndInfos.Errors);
                return changedBinaries;
            }

            errorsAndInfos.Infos.Add(string.Format(Properties.Resources.Building, repositoryId, headTipIdSha));
            var buildErrorsAndInfos = new ErrorsAndInfos();
            cakeBuilder.Build(solutionFileName, false, "", buildErrorsAndInfos);
            if (buildErrorsAndInfos.AnyErrors()) {
                errorsAndInfos.Errors.Add(string.Format(Properties.Resources.FailedToBuild, repositoryId, headTipIdSha));
                errorsAndInfos.Errors.AddRange(buildErrorsAndInfos.Errors);
                return changedBinaries;
            }

            var binFolder = compileFolder.SubFolder("src").SubFolder("bin").SubFolder("Release");
            var targetFolder = previous ? previousTargetFolder : currentTargetFolder;
            CleanUpFolder(targetFolder, folderCleanUpErrorsAndInfos);
            if (folderCleanUpErrorsAndInfos.AnyErrors()) {
                errorsAndInfos.Errors.AddRange(folderCleanUpErrorsAndInfos.Errors);
                return changedBinaries;
            }
            targetFolder.CreateIfNecessary();
            var shortFileNames = Directory.GetFiles(binFolder.FullName, "*.*", SearchOption.AllDirectories)
                .Where(f => !f.StartsWith(binFolder.FullName + @"\ref\"))
                .Select(f => f.Substring(binFolder.FullName.Length + 1))
                .ToList();
            foreach (var shortFileName in shortFileNames) {
                var sourceFileName = binFolder.FullName + '\\' + shortFileName;
                var destinationFileName = targetFolder.FullName + '\\' + shortFileName;
                try {
                    var destinationFolder = new Folder(destinationFileName.Substring(0, destinationFileName.LastIndexOf('\\')));
                    destinationFolder.CreateIfNecessary();
                    File.Copy(sourceFileName, destinationFileName, true);
                } catch {
                    errorsAndInfos.Errors.Add(string.Format(Properties.Resources.FailedToCopy, sourceFileName, destinationFileName));
                }
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

            if (binariesHelper.CanFilesOfEqualLengthBeTreatedEqual(FolderUpdateMethod.AssembliesButNotIfOnlySlightlyChanged, "",
                    previousContents, currentContents, previousFileInfo,
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

    private void RemoveNonSourceCodeFiles(IFolder compileFolder, bool linksBuildCake, IErrorsAndInfos errorsAndInfos) {
        CleanUpFolder(compileFolder.SubFolder(".git"), errorsAndInfos);
        if (errorsAndInfos.AnyErrors()) { return; }

        var files = Directory.GetFiles(compileFolder.FullName, ".git*", SearchOption.TopDirectoryOnly).ToList();
        if (!linksBuildCake) {
            files.AddRange(Directory.GetFiles(compileFolder.FullName, "build.*", SearchOption.TopDirectoryOnly).ToList());
        }
        foreach (var file in files) {
            if (!File.Exists(file)) {
                errorsAndInfos.Infos.Add($"File no longer exists: {file}");
                continue;
            }

            try {
                File.Delete(file);
            } catch (Exception e) {
                errorsAndInfos.Errors.Add($"Could not delete file: {file}");
                errorsAndInfos.Errors.Add(e.Message);
                return;
            }
        }
    }

    private string AdjustLineIfVersioningRelated(string s) {
        if (s.Contains("<Version>")) { return "    <Version>2.0.24.7</Version>"; }
        if (s.Contains("<VersionDays>")) { return "    <VersionDays>24</VersionDays>"; }
        return s.Contains("<VersionMinutes") ? "    <VersionMinutes>7</VersionMinutes>" : s;
    }

    private string MakeDeterministic(string s) {
        return s.Contains("<Deterministic") ? "    <Deterministic>true</Deterministic>" : s;
    }
}