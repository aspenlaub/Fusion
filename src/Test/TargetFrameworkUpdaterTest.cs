using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Components;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Gitty;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Gitty.TestUtilities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Protch.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Skladasu.Entities;
using Autofac;
using LibGit2Sharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Test;

[TestClass]
public class TargetFrameworkUpdaterTest {
    private ITargetFrameworkUpdater _Sut;
    private static readonly TestTargetFolder _pakledTarget
        = new(nameof(TargetFrameworkUpdaterTest), "Pakled");
    private static IContainer _container;

    [ClassInitialize]
    public static void ClassInitialize(TestContext context) {
        _container = new ContainerBuilder().UseGittyTestUtilities().UseFusionNuclideProtchAndGitty("Fusion").Build();
    }

    [TestInitialize]
    public void Initialize() {
        _pakledTarget.Delete();
        IGitUtilities gitUtilities = _container.Resolve<IGitUtilities>();
        string url = "https://github.com/aspenlaub/" + _pakledTarget.SolutionId + ".git";
        var errorsAndInfos = new ErrorsAndInfos();
        gitUtilities.Clone(url, "master", _pakledTarget.Folder(), new CloneOptions { BranchName = "master" }, true, errorsAndInfos);
        Assert.IsFalse(errorsAndInfos.Errors.Any(), errorsAndInfos.ErrorsPlusRelevantInfos());
        _Sut = new TargetFrameworkUpdater(_container.Resolve<IGitUtilities>(), _container.Resolve<IProjectFactory>());
    }

    [TestMethod]
    public async Task CanGetInstalledTargetFrameworks() {
        var errorsAndInfos = new ErrorsAndInfos();
        List<string> installedTargetFrameworks = await _Sut.InstalledTargetFrameworksAsync(errorsAndInfos);
        Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsPlusRelevantInfos());
        Assert.IsGreaterThanOrEqualTo(2, installedTargetFrameworks.Count);
        var majorVersionNumbers = installedTargetFrameworks.Select(ExtractMajorVersionNumber).ToList();
        Assert.Contains(10, majorVersionNumbers);
        Assert.Contains(11, majorVersionNumbers);
        for (int i = 0; i < majorVersionNumbers.Count - 1; i++) {
            Assert.IsGreaterThan(majorVersionNumbers[i + 1], majorVersionNumbers[i]);
        }
    }

    [TestMethod]
    public async Task CanIdentifyTargetFrameworkUpdateOpportunity() {
        var errorsAndInfos = new ErrorsAndInfos();
        YesNoInconclusive targetFrameworkUpdateOpportunity
            = await _Sut.IsThereATargetFrameworkUpdateOpportunityAsync(_pakledTarget.Folder(), errorsAndInfos);
        Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsPlusRelevantInfos());
        if (targetFrameworkUpdateOpportunity.Inconclusive) {
            throw new Exception("Target framework update opportunity analysis was inconclusive");
        }
        if (targetFrameworkUpdateOpportunity.YesNo) {
            Assert.Inconclusive("Target framework update opportunity exists");
        }

        List<string> installedTargetFrameworks = await _Sut.InstalledTargetFrameworksAsync(errorsAndInfos);
        Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsPlusRelevantInfos());
        string oldTargetFramework = installedTargetFrameworks[1];
        await _Sut.UpdateTargetFrameworkInRepositoryAsync(_pakledTarget.Folder(), oldTargetFramework,
            errorsAndInfos, CancellationToken.None);
        Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsPlusRelevantInfos());
        targetFrameworkUpdateOpportunity
            = await _Sut.IsThereATargetFrameworkUpdateOpportunityAsync(_pakledTarget.Folder(), errorsAndInfos);

        Assert.IsTrue(!targetFrameworkUpdateOpportunity.Inconclusive && targetFrameworkUpdateOpportunity.YesNo,
            "A target framework update opportunity was expected");
    }

    private static int ExtractMajorVersionNumber(string s) {
        if (!s.Contains('.')) {
            return -1;
        }

        s = s.Substring(0, s.IndexOf('.'));
        if (!s.StartsWith("net")) {
            return -1;
        }

        s = s.Substring(3);
        return int.TryParse(s, out int majorVersionNumber) ? majorVersionNumber : -1;
    }
}
