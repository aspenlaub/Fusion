using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Entities;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Nuclide.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Components;

public class FolderUpdater(IBinariesHelper binariesHelper, IChangedBinariesLister changedBinariesLister,
        IPushedHeadTipShaRepository pushedHeadTipShaRepository)
            : IFolderUpdater {

    public void UpdateFolder(IFolder sourceFolder, IFolder destinationFolder, FolderUpdateMethod folderUpdateMethod, IErrorsAndInfos errorsAndInfos) {
        UpdateFolder(sourceFolder, destinationFolder, folderUpdateMethod, "", errorsAndInfos);
    }

    public void UpdateFolder(IFolder sourceFolder, IFolder destinationFolder, FolderUpdateMethod folderUpdateMethod, string mainNamespace, IErrorsAndInfos errorsAndInfos) {
        if (folderUpdateMethod != FolderUpdateMethod.AssembliesButNotIfOnlySlightlyChanged && folderUpdateMethod != FolderUpdateMethod.AssembliesEvenIfOnlySlightlyChanged) {
            throw new NotImplementedException("Update method is not implemented");
        }

        if (!destinationFolder.Exists()) {
            Directory.CreateDirectory(destinationFolder.FullName);
        }

        bool hasSomethingBeenUpdated = false;
        foreach (FileInfo sourceFileInfo in Directory.GetFiles(sourceFolder.FullName, "*.*", SearchOption.AllDirectories).Select(f => new FileInfo(f))) {
            var destinationFileInfo = new FileInfo(destinationFolder.FullName + '\\' + sourceFileInfo.FullName.Substring(sourceFolder.FullName.Length));
            string updateReason;
            if (File.Exists(destinationFileInfo.FullName)) {
                if (sourceFileInfo.Length == 0 && destinationFileInfo.Length == 0) { continue; }

                if (sourceFileInfo.Length == destinationFileInfo.Length) {
                    byte[] sourceContents = File.ReadAllBytes(sourceFileInfo.FullName);
                    byte[] destinationContents = File.ReadAllBytes(destinationFileInfo.FullName);
                    if (sourceContents.Length == destinationContents.Length) {
                        if (binariesHelper.CanFilesOfEqualLengthBeTreatedEqual(folderUpdateMethod, mainNamespace, sourceContents, destinationContents, sourceFileInfo, hasSomethingBeenUpdated, destinationFileInfo, out updateReason)) {
                            continue;
                        }
                    } else {
                        updateReason = string.Format(Properties.Resources.FilesDifferInLength, sourceContents.Length, destinationContents.Length);
                    }
                } else {
                    updateReason = string.Format(Properties.Resources.FilesDifferInLength, sourceFileInfo.Length, destinationFileInfo.Length);
                }
            } else {
                updateReason = string.Format(Properties.Resources.FileIsNew);
            }

            errorsAndInfos.Infos.Add(string.Format(Properties.Resources.UpdatingFile, sourceFileInfo.Name) + ", " + updateReason);
            if (!string.IsNullOrEmpty(destinationFileInfo.DirectoryName) && !Directory.Exists(destinationFileInfo.DirectoryName)) {
                Directory.CreateDirectory(destinationFileInfo.DirectoryName);
            }

            if (!CopyFileReturnSuccess(sourceFileInfo, destinationFileInfo, errorsAndInfos)) { continue; }

            hasSomethingBeenUpdated = true;
        }
    }

    private static string NewNameForFileToBeOverwritten(string folder, string name) {
        uint n = 0;
        string newOriginalFileName;
        do {
            n++;
            newOriginalFileName = folder + @"\~" + n + '~' + name;
        } while (File.Exists(newOriginalFileName));

        return newOriginalFileName;
    }

    private bool CopyFileReturnSuccess(FileSystemInfo sourceFileInfo, FileInfo destinationFileInfo, IErrorsAndInfos errorsAndInfos) {
        try {
            File.Copy(sourceFileInfo.FullName, destinationFileInfo.FullName, true);
        } catch {
            if (File.Exists(destinationFileInfo.FullName)) {
                string newNameForFileToBeOverwritten = NewNameForFileToBeOverwritten(destinationFileInfo.DirectoryName, destinationFileInfo.Name);
                try {
                    File.Move(destinationFileInfo.FullName, newNameForFileToBeOverwritten);
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                    if (File.Exists(destinationFileInfo.FullName)) {
                        errorsAndInfos.Errors.Add(string.Format(Properties.Resources.FileRenamedButOriginalStillExists, destinationFileInfo.Name, newNameForFileToBeOverwritten.Substring(newNameForFileToBeOverwritten.LastIndexOf('\\') + 1)));
                        return false;
                    }
                } catch {
                    errorsAndInfos.Errors.Add(string.Format(Properties.Resources.FailedToRename, destinationFileInfo.Name, newNameForFileToBeOverwritten.Substring(newNameForFileToBeOverwritten.LastIndexOf('\\') + 1)));
                    return false;
                }
            } else {
                var destinationFolder = new Folder(destinationFileInfo.FullName.Substring(0, destinationFileInfo.FullName.LastIndexOf('\\')));
                destinationFolder.CreateIfNecessary();
            }

            try {
                File.Copy(sourceFileInfo.FullName, destinationFileInfo.FullName, true);
            } catch {
                errorsAndInfos.Errors.Add(string.Format(Properties.Resources.FailedToCopy, sourceFileInfo.FullName, destinationFileInfo.FullName));
                return false;
            }

            File.SetLastWriteTime(destinationFileInfo.FullName, sourceFileInfo.LastWriteTime);
        }

        return true;
    }

    public async Task UpdateFolderAsync(string repositoryId, string branchId, string sourceHeadTipIdSha, IFolder sourceFolder, string destinationHeadTipIdSha, IFolder destinationFolder,
        bool forRelease, bool createAndPushPackages, string nugetFeedId, IErrorsAndInfos errorsAndInfos) {
        IList<BinaryToUpdate> changedBinaries = await changedBinariesLister.ListChangedBinariesAsync(repositoryId, branchId, sourceHeadTipIdSha, destinationHeadTipIdSha, errorsAndInfos);
        if (errorsAndInfos.AnyErrors()) { return; }

        bool anyCopies = false;
        foreach (BinaryToUpdate changedBinary in changedBinaries) {
            var sourceFileInfo = new FileInfo(sourceFolder.FullName + '\\' + changedBinary.FileName);
            if (!File.Exists(sourceFileInfo.FullName)) {
                continue;
            }

            var destinationFileInfo = new FileInfo(destinationFolder.FullName + '\\' + changedBinary.FileName);

            CopyFileReturnSuccess(sourceFileInfo, destinationFileInfo, errorsAndInfos);
            anyCopies = true;
        }

        var sourceFileInfos = Directory.GetFiles(sourceFolder.FullName, "*.*", SearchOption.AllDirectories)
           .Select(f => new FileInfo(f))
           .ToList();
        foreach (FileInfo sourceFileInfo in sourceFileInfos) {
            if (sourceFileInfo.DirectoryName == null) { continue; }

            var destinationFileInfo = new FileInfo(destinationFolder.FullName
                + sourceFileInfo.DirectoryName.Substring(sourceFolder.FullName.Length)
                + '\\' + sourceFileInfo.Name);
            if (destinationFileInfo.Exists) { continue; }

            CopyFileReturnSuccess(sourceFileInfo, destinationFileInfo, errorsAndInfos);
            anyCopies = true;
        }

        if (anyCopies) {
            errorsAndInfos.Infos.Add(string.Format(Properties.Resources.CannotMakeHeadTipShasEquivalentDueToCopies, sourceHeadTipIdSha, destinationHeadTipIdSha));
            return;
        }

        if (!forRelease) {
            errorsAndInfos.Infos.Add(string.Format(Properties.Resources.CannotMakeHeadTipShasEquivalentCauseThisIsNotRelease, sourceHeadTipIdSha, destinationHeadTipIdSha));
            return;
        }

        if (!createAndPushPackages) {
            errorsAndInfos.Infos.Add(string.Format(Properties.Resources.NoPackagesThereforeNoEquivalenceCheck, sourceHeadTipIdSha, destinationHeadTipIdSha));
            return;
        }

        if (string.IsNullOrEmpty(nugetFeedId)) {
            errorsAndInfos.Infos.Add(string.Format(Properties.Resources.NoNugetFeedIdThereforeNoEquivalenceCheck, sourceHeadTipIdSha, destinationHeadTipIdSha));
            return;
        }

        List<string> pushedHeadTipShas = await pushedHeadTipShaRepository.GetAsync(nugetFeedId, errorsAndInfos);
        if (errorsAndInfos.Errors.Any()) { return; }

        if (pushedHeadTipShas.Contains(destinationHeadTipIdSha)) {
            errorsAndInfos.Infos.Add(string.Format(Properties.Resources.HeadTipShaHasAlreadyBeenPushed, destinationHeadTipIdSha, nugetFeedId));
            return;
        }

        if (!pushedHeadTipShas.Contains(sourceHeadTipIdSha)) {
            errorsAndInfos.Infos.Add(string.Format(Properties.Resources.CannotMakeHeadTipShasEquivalentCauseSourceHasNotBeenPushed, sourceHeadTipIdSha, destinationHeadTipIdSha, nugetFeedId));
            return;
        }

        errorsAndInfos.Infos.Add(string.Format(Properties.Resources.AddingEquivalentHeadTipSha, sourceHeadTipIdSha, destinationHeadTipIdSha, nugetFeedId));

        await pushedHeadTipShaRepository.AddAsync(nugetFeedId, destinationHeadTipIdSha, repositoryId, "", errorsAndInfos);
    }
}