using System.ComponentModel.DataAnnotations;
using System.Xml.Serialization;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Entities;

public class ManuallyUpdatedPackage : IManuallyUpdatedPackage {
    [Key, XmlAttribute("id")]
    public string Id { get; set; }

    [XmlAttribute("branch")]
    public string Branch { get; set; }

    [XmlAttribute("projectfileinfix")]
    public string ProjectFileInfix { get; set; }
}