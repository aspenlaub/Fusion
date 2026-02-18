using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Skladasu.Interfaces;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;

public interface INugetPackageUpdater {
    Task<bool> AreThereNugetUpdateOpportunitiesAsync(IFolder repositoryFolder, string checkedOutBranch, IErrorsAndInfos errorsAndInfos);
    Task<bool> AreThereNugetUpdateOpportunitiesForSolutionAsync(IFolder solutionFolder, string checkedOutBranch, IErrorsAndInfos errorsAndInfos);
    Task<IPackageUpdateOpportunity> AreThereEntityFrameworkNugetUpdateOpportunitiesAsync(IFolder repositoryFolder, string checkedOutBranch, IErrorsAndInfos errorsAndInfos);
    Task<YesNoInconclusive> UpdateNugetPackagesInRepositoryAsync(IFolder repositoryFolder, string checkedOutBranch, IErrorsAndInfos errorsAndInfos);
    Task<YesNoInconclusive> UpdateEntityFrameworkNugetPackagesInRepositoryAsync(IFolder repositoryFolder, string migrationId, string checkedOutBranch, IErrorsAndInfos errorsAndInfos);
}