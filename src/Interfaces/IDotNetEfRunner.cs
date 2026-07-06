using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Skladasu.Interfaces;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;

public interface IDotNetEfRunner {
    Task DropDatabaseAsync(IFolder projectFolder, IErrorsAndInfos errorsAndInfos, CancellationToken cancellationToken);
    Task UpdateDatabaseAsync(IFolder projectFolder, IErrorsAndInfos errorsAndInfos, CancellationToken cancellationToken);
    Task<IList<string>> ListAppliedMigrationIdsAsync(IFolder projectFolder, IErrorsAndInfos errorsAndInfos, CancellationToken cancellationToken);
    Task AddMigrationAsync(IFolder projectFolder, string migrationId, IErrorsAndInfos errorsAndInfos, CancellationToken cancellationToken);
}