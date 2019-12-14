using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using FolderUpdateMethod = Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces.FolderUpdateMethod;
using IFolderUpdater = Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces.IFolderUpdater;
using IJsonDepsDifferencer = Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces.IJsonDepsDifferencer;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion {
    public class FolderUpdater : IFolderUpdater {
        private const int MinimumBinaryFileSizeInBytes = 4000;

        private readonly IJsonDepsDifferencer vJsonDepsDifferencer;

        public FolderUpdater(IJsonDepsDifferencer jsonDepsDifferencer) {
            vJsonDepsDifferencer = jsonDepsDifferencer;
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
            foreach(var checkJsonDependencyFiles in new[] { false, true }) {
                foreach (var sourceFileInfo in Directory.GetFiles(sourceFolder.FullName, "*.*", SearchOption.AllDirectories).Select(f => new FileInfo(f))) {
                    if (checkJsonDependencyFiles && !IsJsonDependencyFile(sourceFileInfo.FullName)) { continue; }
                    if (!checkJsonDependencyFiles && IsJsonDependencyFile(sourceFileInfo.FullName)) { continue; }

                    var destinationFileInfo = new FileInfo(destinationFolder.FullName + '\\' + sourceFileInfo.FullName.Substring(sourceFolder.FullName.Length));
                    var updateReason = "";
                    if (File.Exists(destinationFileInfo.FullName)) {
                        if (sourceFileInfo.Length == 0 && destinationFileInfo.Length == 0) { continue; }

                        if (sourceFileInfo.Length == destinationFileInfo.Length) {
                            var sourceContents = File.ReadAllBytes(sourceFileInfo.FullName);
                            var destinationContents = File.ReadAllBytes(destinationFileInfo.FullName);
                            if (sourceContents.Length == destinationContents.Length) {
                                if (CanFilesOfEqualLengthBeTreatedEqual(folderUpdateMethod, mainNamespace, sourceContents, destinationContents, sourceFileInfo, hasSomethingBeenUpdated, destinationFileInfo, out updateReason)) {
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

                    try {
                        File.Copy(sourceFileInfo.FullName, destinationFileInfo.FullName, true);
                    } catch {
                        if (File.Exists(destinationFileInfo.FullName)) {
                            var newNameForFileToBeOverwritten = NewNameForFileToBeOverwritten(destinationFileInfo.DirectoryName, destinationFileInfo.Name);
                            try {
                                File.Move(destinationFileInfo.FullName, newNameForFileToBeOverwritten);
                            } catch {
                                errorsAndInfos.Errors.Add(string.Format(Properties.Resources.FailedToRename, destinationFileInfo.Name, newNameForFileToBeOverwritten.Substring(newNameForFileToBeOverwritten.LastIndexOf('\\') + 1)));
                                continue;
                            }
                        }

                        try {
                            File.Copy(sourceFileInfo.FullName, destinationFileInfo.FullName, true);
                        } catch {
                            errorsAndInfos.Errors.Add(string.Format(Properties.Resources.FailedToCopy, sourceFileInfo.FullName, destinationFileInfo.FullName));
                            continue;
                        }
                    }

                    File.SetLastWriteTime(destinationFileInfo.FullName, sourceFileInfo.LastWriteTime);
                    hasSomethingBeenUpdated = true;
                }
            }
        }

        private bool CanFilesOfEqualLengthBeTreatedEqual(FolderUpdateMethod folderUpdateMethod, string mainNamespace, IReadOnlyList<byte> sourceContents, IReadOnlyList<byte> destinationContents,
                FileInfo sourceFileInfo, bool hasSomethingBeenUpdated, FileSystemInfo destinationFileInfo, out string updateReason) {
            updateReason = Properties.Resources.FilesHaveEqualLengthThatCannotBeIgnored;
            var differences = sourceContents.Where((t, i) => t != destinationContents[i]).Count();
            if (differences == 0) {
                return true;
            }

            if (folderUpdateMethod == FolderUpdateMethod.AssembliesButNotIfOnlySlightlyChanged && IsBinary(sourceFileInfo.Name) && differences < 50 && sourceFileInfo.Length >= MinimumBinaryFileSizeInBytes) {
                return true;
            }

            var tempFolder = new Folder(Path.GetTempPath()).SubFolder("AspenlaubTemp").SubFolder(nameof(FolderUpdater));
            tempFolder.CreateIfNecessary();
            var guid = Guid.NewGuid().ToString();
            File.WriteAllBytes(tempFolder.FullName + '\\' + sourceFileInfo.Name + '_' + guid + "_diff" + differences + "_old.bin", sourceContents.ToArray());
            File.WriteAllBytes(tempFolder.FullName + '\\' + sourceFileInfo.Name + '_' + guid + "_diff" + differences + "_new.bin", destinationContents.ToArray());

            return folderUpdateMethod == FolderUpdateMethod.AssembliesButNotIfOnlySlightlyChanged
                        && !hasSomethingBeenUpdated
                        && IsJsonDependencyFile(sourceFileInfo.Name)
                        && vJsonDepsDifferencer.AreJsonDependenciesIdenticalExceptForNamespaceVersion(
                            File.ReadAllText(sourceFileInfo.FullName),
                            File.ReadAllText(destinationFileInfo.FullName),
                            mainNamespace, out updateReason);
        }

        protected static bool IsBinary(string fileName) {
            return fileName.EndsWith(@".exe") || fileName.EndsWith(@".dll") || fileName.EndsWith(@".pdb");
        }

        protected static bool IsJsonDependencyFile(string fileName) {
            return fileName.EndsWith(@".deps.json");
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
    }
}
