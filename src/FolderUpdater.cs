﻿using System;
using System.IO;
using System.Linq;
using System.Threading;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Nuclide.Entities;
using Aspenlaub.Net.GitHub.CSharp.Nuclide.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion {
    public class FolderUpdater : IFolderUpdater {
        private readonly IBinariesHelper vBinariesHelper;
        private readonly IChangedBinariesLister vChangedBinariesLister;
        private readonly IPushedHeadTipShaRepository vPushedHeadTipShaRepository;
        private readonly ISecretRepository vSecretRepository;

        public FolderUpdater(IBinariesHelper binariesHelper, IChangedBinariesLister changedBinariesLister, IPushedHeadTipShaRepository  pushedHeadTipShaRepository, ISecretRepository secretRepository) {
            vBinariesHelper = binariesHelper;
            vChangedBinariesLister = changedBinariesLister;
            vPushedHeadTipShaRepository = pushedHeadTipShaRepository;
            vSecretRepository = secretRepository;
        }

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

            var hasSomethingBeenUpdated = false;
            foreach (var sourceFileInfo in Directory.GetFiles(sourceFolder.FullName, "*.*", SearchOption.AllDirectories).Select(f => new FileInfo(f))) {
                var destinationFileInfo = new FileInfo(destinationFolder.FullName + '\\' + sourceFileInfo.FullName.Substring(sourceFolder.FullName.Length));
                string updateReason;
                if (File.Exists(destinationFileInfo.FullName)) {
                    if (sourceFileInfo.Length == 0 && destinationFileInfo.Length == 0) { continue; }

                    if (sourceFileInfo.Length == destinationFileInfo.Length) {
                        var sourceContents = File.ReadAllBytes(sourceFileInfo.FullName);
                        var destinationContents = File.ReadAllBytes(destinationFileInfo.FullName);
                        if (sourceContents.Length == destinationContents.Length) {
                            if (vBinariesHelper.CanFilesOfEqualLengthBeTreatedEqual(folderUpdateMethod, mainNamespace, sourceContents, destinationContents, sourceFileInfo, hasSomethingBeenUpdated, destinationFileInfo, out updateReason)) {
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
                    var newNameForFileToBeOverwritten = NewNameForFileToBeOverwritten(destinationFileInfo.DirectoryName, destinationFileInfo.Name);
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

        public void UpdateFolder(string repositoryId, string sourceHeadTipIdSha, IFolder sourceFolder, string destinationHeadTipIdSha, IFolder destinationFolder,
                bool forRelease, bool createAndPushPackages, string nugetFeedId, IErrorsAndInfos errorsAndInfos) {
            var changedBinaries = vChangedBinariesLister.ListChangedBinaries(repositoryId, sourceHeadTipIdSha, destinationHeadTipIdSha, errorsAndInfos);
            if (errorsAndInfos.AnyErrors()) { return; }

            var anyCopies = false;
            foreach (var changedBinary in changedBinaries) {
                var sourceFileInfo = new FileInfo(sourceFolder.FullName + '\\' + changedBinary.FileName);
                var destinationFileInfo = new FileInfo(destinationFolder.FullName + '\\' + changedBinary.FileName);

                errorsAndInfos.Infos.Add(string.Format(Properties.Resources.UpdatingFile, sourceFileInfo.Name) + ", " + changedBinary.UpdateReason);

                CopyFileReturnSuccess(sourceFileInfo, destinationFileInfo, errorsAndInfos);
                anyCopies = true;
            }

            foreach (var sourceFileInfo in Directory.GetFiles(sourceFolder.FullName, "*.*").Select(f => new FileInfo(f))) {
                var destinationFileInfo = new FileInfo(destinationFolder.FullName + '\\' + sourceFileInfo.Name);
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

            var pushedHeadTipShas = vPushedHeadTipShaRepository.Get(nugetFeedId, errorsAndInfos);
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

            vPushedHeadTipShaRepository.Add(nugetFeedId, destinationHeadTipIdSha, repositoryId, "", errorsAndInfos);
            if (errorsAndInfos.Errors.Any()) { return; }
        }
    }
}
