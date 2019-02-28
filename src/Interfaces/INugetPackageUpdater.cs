using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces {
    public interface INugetPackageUpdater {
        Task<bool> AreThereNugetUpdateOpportunitiesAsync(IFolder repositoryFolder, IErrorsAndInfos errorsAndInfos);
        Task<IYesNoInconclusive> UpdateNugetPackagesInRepositoryAsync(IFolder repositoryFolder, IErrorsAndInfos errorsAndInfos);
    }
}
