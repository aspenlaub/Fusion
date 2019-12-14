namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces {
    public interface IJsonDepsDifferencer {
        bool AreJsonDependenciesIdenticalExceptForNamespaceVersion(string oldJson, string newJson, string mainNamespace, out string updateReason);
    }
}
