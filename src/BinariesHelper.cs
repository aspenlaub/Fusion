using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Extensions;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion {
    public class BinariesHelper : IBinariesHelper {
        private const int MinimumBinaryFileSizeInBytes = 4000;

        private readonly IJsonDepsDifferencer vJsonDepsDifferencer;

        public BinariesHelper(IJsonDepsDifferencer jsonDepsDifferencer) {
            vJsonDepsDifferencer = jsonDepsDifferencer;
        }

        public bool CanFilesOfEqualLengthBeTreatedEqual(FolderUpdateMethod folderUpdateMethod, string mainNamespace, IReadOnlyList<byte> sourceContents, IReadOnlyList<byte> destinationContents,
            FileInfo sourceFileInfo, bool hasSomethingBeenUpdated, FileSystemInfo destinationFileInfo, out string updateReason) {
            updateReason = Properties.Resources.FilesHaveEqualLengthThatCannotBeIgnored;
            var differences = sourceContents.Where((t, i) => t != destinationContents[i]).Count();
            if (differences == 0) {
                return true;
            }

            if (folderUpdateMethod == FolderUpdateMethod.AssembliesButNotIfOnlySlightlyChanged && IsBinary(sourceFileInfo.Name) && differences < 100 && sourceFileInfo.Length >= MinimumBinaryFileSizeInBytes) {
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

        public bool IsBinary(string fileName) {
            return fileName.EndsWith(@".exe") || fileName.EndsWith(@".dll") || fileName.EndsWith(@".pdb");
        }

        public bool IsJsonDependencyFile(string fileName) {
            return fileName.EndsWith(@".deps.json");
        }

        public bool IsPdbFile(string fileName) {
            return fileName.EndsWith(@".pdb");
        }
    }
}
