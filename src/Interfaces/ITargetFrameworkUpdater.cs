using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Skladasu.Interfaces;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;

public interface ITargetFrameworkUpdater {
    Task<List<string>> InstalledTargetFrameworksAsync(IErrorsAndInfos errorsAndInfos);
    Task<YesNoInconclusive> IsThereATargetFrameworkUpdateOpportunityAsync(IFolder repositoryFolder, IErrorsAndInfos errorsAndInfos);
    Task UpdateTargetFrameworkInRepositoryAsync(IFolder repositoryFolder, IErrorsAndInfos errorsAndInfos,
        CancellationToken cancellationToken);
    Task UpdateTargetFrameworkInRepositoryAsync(IFolder repositoryFolder, string targetFramework, IErrorsAndInfos errorsAndInfos,
        CancellationToken cancellationToken);
}
