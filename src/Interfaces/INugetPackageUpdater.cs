using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces {
    public interface INugetPackageUpdater {
        Task<bool> AreThereNugetUpdateOpportunitiesAsync(IFolder repositoryFolder, IErrorsAndInfos errorsAndInfos);
        void UpdateNugetPackagesInRepository(IFolder repositoryFolder, out bool yesNo, out bool inconclusive, IErrorsAndInfos errorsAndInfos);
    }
}
