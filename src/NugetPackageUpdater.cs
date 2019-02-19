using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Nuclide.Entities;
using Aspenlaub.Net.GitHub.CSharp.Nuclide.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using NuGet.Protocol.Core.Types;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion {
    public class NugetPackageUpdater : INugetPackageUpdater {
        private readonly IGitUtilities vGitUtilities;
        private readonly IProcessRunner vProcessRunner;
        private readonly INugetFeedLister vNugetFeedLister;
        private readonly ISecretRepository vSecretRepository;

        private readonly IList<string> vEndingsThatAllowReset = new List<string> { "csproj", "config" };

        public NugetPackageUpdater(IGitUtilities gitUtilities, IProcessRunner processRunner, INugetFeedLister nugetFeedLister, ISecretRepository secretRepository) {
            vGitUtilities = gitUtilities;
            vProcessRunner = processRunner;
            vNugetFeedLister = nugetFeedLister;
            vSecretRepository = secretRepository;
        }

        public void UpdateNugetPackagesInRepository(IFolder repositoryFolder, out bool yesNo, out bool inconclusive, IErrorsAndInfos errorsAndInfos) {
            var files = vGitUtilities.FilesWithUncommittedChanges(repositoryFolder);
            inconclusive = files.Any(f => vEndingsThatAllowReset.All(e => !f.EndsWith("." + e, StringComparison.InvariantCultureIgnoreCase)));
            yesNo = false;
            if (inconclusive) { return; }

            vGitUtilities.Reset(repositoryFolder, vGitUtilities.HeadTipIdSha(repositoryFolder), errorsAndInfos);
            if (errorsAndInfos.AnyErrors()) { return; }

            var projectFileFullNames = Directory.GetFiles(repositoryFolder.SubFolder("src").FullName, "*.csproj", SearchOption.AllDirectories).ToList();
            if (!projectFileFullNames.Any()) {
                return;
            }

            foreach (var projectFileFullName in projectFileFullNames) {
                UpdateNugetPackagesForProject(projectFileFullName, ref yesNo, errorsAndInfos);
            }

            if (yesNo) { return; }

            vGitUtilities.Reset(repositoryFolder, vGitUtilities.HeadTipIdSha(repositoryFolder), errorsAndInfos);
        }

        private void UpdateNugetPackagesForProject(string projectFileFullName, ref bool yesNo, IErrorsAndInfos errorsAndInfos) {
            var namespaceManager = new XmlNamespaceManager(new NameTable());
            XDocument document;
            try {
                document = XDocument.Load(projectFileFullName);
            } catch {
                errorsAndInfos.Errors.Add(string.Format(Properties.Resources.CouldNotLoadProject, projectFileFullName));
                return;
            }

            var packageToVersion = new Dictionary<string, string>();
            foreach (var element in document.XPathSelectElements("/Project/ItemGroup/PackageReference", namespaceManager)) {
                var id = element.Attribute("Include")?.Value;
                if (string.IsNullOrEmpty(id)) { continue; }

                var version = element.Attribute("Version")?.Value;
                if (string.IsNullOrEmpty(version)) { continue; }

                packageToVersion[id] = version;
                var projectFileFolder = projectFileFullName.Substring(0, projectFileFullName.LastIndexOf('\\'));
                vProcessRunner.RunProcess("dotnet", "add " + projectFileFullName + " package " + id, projectFileFolder, errorsAndInfos);
            }

            try {
                document = XDocument.Load(projectFileFullName);
            } catch {
                errorsAndInfos.Errors.Add(string.Format(Properties.Resources.CouldNotLoadProject, projectFileFullName));
                return;
            }

            foreach (var element in document.XPathSelectElements("/Project/ItemGroup/PackageReference", namespaceManager)) {
                var id = element.Attribute("Include")?.Value;
                if (string.IsNullOrEmpty(id)) { continue; }

                var version = element.Attribute("Version")?.Value;
                if (string.IsNullOrEmpty(version)) { continue; }

                yesNo = yesNo || !packageToVersion.ContainsKey(id) || version != packageToVersion[id];
            }
        }

        public async Task<bool> AreThereNugetUpdateOpportunitiesAsync(IFolder repositoryFolder, IErrorsAndInfos errorsAndInfos) {
            var projectFileFullNames = Directory.GetFiles(repositoryFolder.SubFolder("src").FullName, "*.csproj", SearchOption.AllDirectories).ToList();
            if (!projectFileFullNames.Any()) {
                return false;
            }

            var developerSettingsSecret = new DeveloperSettingsSecret();
            var developerSettings = await vSecretRepository.GetAsync(developerSettingsSecret, errorsAndInfos);
            if (errorsAndInfos.Errors.Any()) { return false; }

            var feedUrls = new[] { developerSettings.NugetFeedUrl, "https://packages.nuget.org/api/v2" };
            foreach (var projectFileFullName in projectFileFullNames) {
                if (await AreThereNugetUpdateOpportunitiesForProjectAsync(projectFileFullName, feedUrls, errorsAndInfos)) {
                    return !errorsAndInfos.AnyErrors();
                }
            }

            return false;
        }

        private async Task<bool> AreThereNugetUpdateOpportunitiesForProjectAsync(string projectFileFullName, IList<string> feedUrls, IErrorsAndInfos errorsAndInfos) {
            var namespaceManager = new XmlNamespaceManager(new NameTable());
            XDocument document;
            try {
                document = XDocument.Load(projectFileFullName);
            } catch {
                errorsAndInfos.Errors.Add(string.Format(Properties.Resources.CouldNotLoadProject, projectFileFullName));
                return false;
            }

            foreach (var element in document.XPathSelectElements("/Project/ItemGroup/PackageReference", namespaceManager)) {
                var id = element.Attribute("Include")?.Value;
                if (string.IsNullOrEmpty(id)) {
                    continue;
                }

                IList<IPackageSearchMetadata> remotePackages = null;
                foreach (var feedUrl in feedUrls) {
                    remotePackages = await vNugetFeedLister.ListReleasedPackagesAsync(feedUrl, id);
                    if (remotePackages.Any()) {
                        break;
                    }
                }

                if (remotePackages?.Any() != true) {
                    continue;
                }

                var version = element.Attribute("Version")?.Value;
                if (string.IsNullOrEmpty(version)) {
                    continue;
                }

                var latestRemotePackageVersion = remotePackages.Max(p => p.Identity.Version.Version);
                if (!latestRemotePackageVersion.ToString().StartsWith(version)) {
                    return true;
                }
            }

            return false;
        }
    }
}
