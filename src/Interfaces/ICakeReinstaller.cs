using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces {
    public interface ICakeReinstaller {
        string CakeFolderFullName { get; }
        void ReinstallCake(IErrorsAndInfos errorsAndInfos);
    }
}
