using System.Threading;
using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Skladasu.Interfaces;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;

public interface IDotNetEfInstaller {
    Task<bool> IsCurrentGlobalDotNetEfInstalledAsync(IErrorsAndInfos errorsAndInfos, CancellationToken cancellationToken);
    Task InstallOrUpdateGlobalDotNetEfIfNecessaryAsync(IErrorsAndInfos errorsAndInfos, CancellationToken cancellationToken);
}