using System;
using System.IO;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Extensions;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion {
    public class JsonDepsDifferencer : IJsonDepsDifferencer {
        public bool AreJsonDependenciesIdenticalExceptForNamespaceVersion(string oldJson, string newJson, string mainNamespace, out string updateReason) {
            updateReason = "";
            if (string.IsNullOrWhiteSpace(mainNamespace)) { return oldJson == newJson; }

            var tag = '"' + mainNamespace + '/';
            oldJson = ReplaceNamespaceWithVersion(oldJson, tag);
            newJson = ReplaceNamespaceWithVersion(newJson, tag);

            var solutionId = mainNamespace.Substring(mainNamespace.LastIndexOf('.') + 1);
            tag = '"' + solutionId + '/';
            oldJson = ReplaceNamespaceWithVersion(oldJson, tag);
            newJson = ReplaceNamespaceWithVersion(newJson, tag);
            updateReason = Properties.Resources.JsonFilesHaveEqualLengthThatCannotBeIgnored;
            if (oldJson == newJson) {
                return true;
            }

            var tempFolder = new Folder(Path.GetTempPath()).SubFolder("AspenlaubTemp").SubFolder(nameof(JsonDepsDifferencer));
            tempFolder.CreateIfNecessary();
            var guid = Guid.NewGuid().ToString();
            File.WriteAllText(tempFolder.FullName + '\\' + guid + "_old.json", oldJson);
            File.WriteAllText(tempFolder.FullName + '\\' + guid + "_new.json", newJson);
            return false;
        }

        private static string ReplaceNamespaceWithVersion(string json, string tag) {
            var pos = -1;
            while (0 <= (pos = json.IndexOf(tag, pos + 1, StringComparison.InvariantCultureIgnoreCase))) {
                var pos2 = json.IndexOf('"', pos + tag.Length);
                if (pos2 > pos + tag.Length + 20) {
                    continue;
                }

                json = json.Substring(0, pos) + json.Substring(pos2);
            }

            return json;
        }
    }
}
