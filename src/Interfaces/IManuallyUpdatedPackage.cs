// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedMemberInSuper.Global

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;

public interface IManuallyUpdatedPackage {
    string Id { get; set; }
    string Branch { get; set; }
    string ProjectFileInfix { get; set; }
}