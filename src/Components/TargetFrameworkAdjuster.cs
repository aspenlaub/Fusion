using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Components;

public class TargetFrameworkAdjuster {

    private static readonly List<string> _fromDotNetFrameworks = ["<TargetFramework>net7.0</TargetFramework>"];
    private static readonly string _toDotNetFrameworkTemplate = "<TargetFramework>net@@TARGET_FRAMEWORK@@</TargetFramework>";

    public static void UseCurrentDotNet(IFolder folder) {
        var assembly = Assembly.GetExecutingAssembly();
        string targetFramework = GetTargetFrameworkMajorDotMinor(assembly);
        string toDotNetFramework = _toDotNetFrameworkTemplate.Replace("@@TARGET_FRAMEWORK@@", targetFramework);

        string[] csprojFiles = Directory.GetFiles(folder.FullName, "*.csproj", SearchOption.AllDirectories);
        foreach (string csProjFile in csprojFiles) {
            string contents = File.ReadAllText(csProjFile);
            foreach (string newContents in _fromDotNetFrameworks
                                           .Where(fromDotNetFramework => contents.Contains(fromDotNetFramework, StringComparison.InvariantCultureIgnoreCase))
                                           .Select(fromDotNetFramework => contents.Replace(fromDotNetFramework, toDotNetFramework))
                                           .Where(newContents => contents != newContents)) {
                File.WriteAllText(csProjFile, newContents);
            }
        }
    }

    public static string GetTargetFrameworkMajorDotMinor(Assembly assembly) {
        CustomAttributeData targetFrameworkAttribute = assembly.CustomAttributes
            .SingleOrDefault(attribute => attribute.AttributeType.Name == nameof(TargetFrameworkAttribute));
        if (targetFrameworkAttribute == null) {
            return "";
        }
        string targetFramework = targetFrameworkAttribute.ConstructorArguments.FirstOrDefault().Value?.ToString();
        if (targetFramework == null) {
            return "";
        }
        const string tag = "Version=v";
        if (!targetFramework.Contains(tag)) {
            return "";
        }
        targetFramework = targetFramework.Substring(targetFramework.IndexOf(tag, StringComparison.InvariantCultureIgnoreCase) + tag.Length);
        return targetFramework;
    }
}
