﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Components;

public class BinariesHelper : IBinariesHelper {
    private const int _minimumBinaryFileSizeInBytes = 4000;

    public bool CanFilesOfEqualLengthBeTreatedEqual(FolderUpdateMethod folderUpdateMethod, string mainNamespace, IReadOnlyList<byte> sourceContents, IReadOnlyList<byte> destinationContents,
        FileInfo sourceFileInfo, bool hasSomethingBeenUpdated, FileSystemInfo destinationFileInfo, out string updateReason) {
        updateReason = Properties.Resources.FilesHaveEqualLengthThatCannotBeIgnored;
        var differences = sourceContents.Where((t, i) => t != destinationContents[i]).Count();
        if (differences == 0) {
            return true;
        }

        updateReason = string.Format(Properties.Resources.FilesHaveEqualLengthButNDifferences, differences);
        return folderUpdateMethod == FolderUpdateMethod.AssembliesButNotIfOnlySlightlyChanged && IsBinary(sourceFileInfo.Name) && differences < 30 && sourceFileInfo.Length >= _minimumBinaryFileSizeInBytes;
    }

    public bool IsBinary(string fileName) {
        return fileName.EndsWith(@".exe") || fileName.EndsWith(@".dll") || fileName.EndsWith(@".pdb");
    }
}