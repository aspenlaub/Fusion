using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Entities;

public class PackageUpdateOpportunity : IPackageUpdateOpportunity {
    public bool YesNo { get; set; } = false;
    public string PotentialMigrationId { get; set; } = "";
}