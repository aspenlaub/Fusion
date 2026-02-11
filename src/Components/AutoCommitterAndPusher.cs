using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Entities;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Nuclide.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using LibGit2Sharp;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion.Components;

public class AutoCommitterAndPusher(IGitUtilities gitUtilities, ISecretRepository secretRepository,
        IPushedHeadTipShaRepository headTipShaRepository, IBranchesWithPackagesRepository branchesWithPackagesRepository)
            : IAutoCommitterAndPusher {

    public async Task AutoCommitAndPushPackageUpdates(string nugetFeedId, IFolder repositoryFolder, IErrorsAndInfos errorsAndInfos) {
        var files = gitUtilities.FilesWithUncommittedChanges(repositoryFolder).ToList();
        if (files.Count == 0) {
            errorsAndInfos.Errors.Add(Properties.Resources.AtLeastOneFileExpected);
            return;
        }

        var unexpectedFiles = files
            .Where(f =>
                !f.EndsWith(".csproj", StringComparison.InvariantCultureIgnoreCase)
                && !f.Contains(@"Migrations\", StringComparison.InvariantCultureIgnoreCase)
                && !f.Contains("Migrations/", StringComparison.InvariantCultureIgnoreCase))
            .ToList();
        if (unexpectedFiles.Any()) {
            errorsAndInfos.Errors.Add(string.Format(
                Properties.Resources.OnlyCsProjFilesAndMigrationsExpected,
                string.Join(", ", unexpectedFiles))
            );
            return;
        }

        await AutoCommitAndPushAsync(nugetFeedId, repositoryFolder, files, false, Properties.Resources.PackageUpdates, false, errorsAndInfos);
    }

    private async Task AutoCommitAndPushAsync(string nugetFeedId, IFolder repositoryFolder, List<string> files, bool onlyIfNecessary, string commitMessage, bool noRebuildRequired, IErrorsAndInfos errorsAndInfos) {
        string branchName = gitUtilities.CheckedOutBranch(repositoryFolder);
        IList<string> branchesWithPackages = await branchesWithPackagesRepository.GetBranchIdsAsync(errorsAndInfos);
        if (!branchesWithPackages.Contains(branchName)) {
            errorsAndInfos.Errors.Add(Properties.Resources.CheckedOutBranchIsNotMasterMainOrBranchWithPackages);
            return;
        }

        string headTipShaBeforePush = gitUtilities.HeadTipIdSha(repositoryFolder);

        gitUtilities.IdentifyOwnerAndName(repositoryFolder, out string owner, out _, errorsAndInfos);
        if (errorsAndInfos.AnyErrors()) {
            errorsAndInfos.Errors.Add(Properties.Resources.OwnerAndNameNotFound);
            return;
        }

        var personalAccessTokensSecret = new PersonalAccessTokensSecret();
        PersonalAccessTokens personalAccessTokens = await secretRepository.GetAsync(personalAccessTokensSecret, errorsAndInfos);
        PersonalAccessToken personalAccessToken = personalAccessTokens.FirstOrDefault(t => t.Owner == owner && t.Purpose == "AutoCommitPush");
        if (personalAccessToken == null) {
            errorsAndInfos.Errors.Add(Properties.Resources.AutoCommitPushAccessTokenNotFound);
            return;
        }

        using var repo = new Repository(repositoryFolder.FullName);

        var remotes = repo.Network.Remotes.ToList();
        if (remotes.Count != 1) {
            errorsAndInfos.Errors.Add(Properties.Resources.RemoteNotFoundOrNotUnique);
            return;
        }

        Remote remote = remotes[0];

        files.ForEach(f => {
            // ReSharper disable once AccessToDisposedClosure
            Commands.Stage(repo, f);
        });

        IList<string> checkFiles = gitUtilities.FilesWithUncommittedChanges(repositoryFolder);
        if (onlyIfNecessary && !checkFiles.Any()) { return; }

        if (checkFiles.Count != files.Count) {
            errorsAndInfos.Errors.Add(string.Format(Properties.Resources.NumberOfFilesWithUncommittedChangesHasChanged,
                string.Join(", ", files), string.Join(", ", checkFiles)));
            return;
        }

        var author = new Signature(personalAccessToken.TokenName, personalAccessToken.Email, DateTime.Now);
        Signature committer = author;
        repo.Commit(commitMessage, author, committer);

        var options = new PushOptions {
            CredentialsProvider = (_, _, _) => new UsernamePasswordCredentials {
                Username = owner,
                Password = personalAccessToken.Token
            }
        };

        repo.Network.Push(remote, @"refs/heads/" + branchName, options);

        if (!noRebuildRequired) { return; }

        IPushedHeadTipShaRepository pushedHeadTipShaRepository = headTipShaRepository;
        if (!(await pushedHeadTipShaRepository.GetAsync(nugetFeedId, errorsAndInfos)).Contains(headTipShaBeforePush)) { return; }

        string headTipSha = gitUtilities.HeadTipIdSha(repositoryFolder);
        await pushedHeadTipShaRepository.AddAsync(nugetFeedId, headTipSha, errorsAndInfos);
    }
}