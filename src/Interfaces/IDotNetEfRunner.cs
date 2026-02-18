using System.Collections.Generic;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Skladasu.Interfaces;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;

public interface IDotNetEfRunner {
    /// <summary>
    /// Drop database for the specified project, return errors
    /// </summary>
    /// <param name="projectFolder"></param>
    /// <param name="errorsAndInfos"></param>
    void DropDatabase(IFolder projectFolder, IErrorsAndInfos errorsAndInfos);

    /// <summary>
    /// Update database for the specified project, return errors
    /// </summary>
    /// <param name="projectFolder"></param>
    /// <param name="errorsAndInfos"></param>
    void UpdateDatabase(IFolder projectFolder, IErrorsAndInfos errorsAndInfos);

    /// <summary>
    /// List applied migrations for the specified project, return errors
    /// </summary>
    /// <param name="projectFolder"></param>
    /// <param name="errorsAndInfos"></param>
    /// <returns>List of migration IDs</returns>
    IList<string> ListAppliedMigrationIds(IFolder projectFolder, IErrorsAndInfos errorsAndInfos);

    /// <summary>
    /// Add migration to the specified project, return errors
    /// </summary>
    /// <param name="projectFolder"></param>
    /// <param name="migrationId"></param>
    /// <param name="errorsAndInfos"></param>
    void AddMigration(IFolder projectFolder, string migrationId, IErrorsAndInfos errorsAndInfos);
}