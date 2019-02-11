using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces {
    public interface INugetPackageUpdater {
        void UpdateNugetPackagesInRepository(IFolder repositoryFolder, out bool yesNo, out bool inconclusive, IErrorsAndInfos errorsAndInfos);
    }
}
