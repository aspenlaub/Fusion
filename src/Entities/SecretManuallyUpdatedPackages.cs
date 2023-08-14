using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Entities;

public class SecretManuallyUpdatedPackages : ISecret<ManuallyUpdatedPackages> {
    private ManuallyUpdatedPackages _DefaultManuallyUpdatedPackages;
    public ManuallyUpdatedPackages DefaultValue => _DefaultManuallyUpdatedPackages ??= new ManuallyUpdatedPackages { new() { Id = "LibGit2Sharp" } };

    public string Guid => "{7185ADA1-2632-4B70-A76E-6CAB7B660DDD}";
}