using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Extensions;

public static class ManuallyUpdatedPackageExtensions {
    public static bool Matches(this IManuallyUpdatedPackage manuallyUpdatedPackage, string id,
            string checkedOutBranch, string projectFileFullName) {
        return manuallyUpdatedPackage.Id == id
               && manuallyUpdatedPackage.Branch == checkedOutBranch
               && (string.IsNullOrEmpty(manuallyUpdatedPackage.ProjectFileInfix)
                    || projectFileFullName.Contains(manuallyUpdatedPackage.ProjectFileInfix));
    }
}