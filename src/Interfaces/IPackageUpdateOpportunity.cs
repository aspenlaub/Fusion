namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;

public interface IPackageUpdateOpportunity {
    bool YesNo { get; set; }
    string PotentialMigrationId { get; set; }
}