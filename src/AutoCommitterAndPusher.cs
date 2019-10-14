﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Entities;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Nuclide.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using LibGit2Sharp;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion {
    public class AutoCommitterAndPusher : IAutoCommitterAndPusher {
        private readonly IGitUtilities vGitUtilities;
        private readonly ISecretRepository vSecretRepository;
        private readonly IPushedHeadTipShaRepository vPushedHeadTipShaRepository;

        public AutoCommitterAndPusher(IGitUtilities gitUtilities, ISecretRepository secretRepository, IPushedHeadTipShaRepository pushedHeadTipShaRepository) {
            vGitUtilities = gitUtilities;
            vSecretRepository = secretRepository;
            vPushedHeadTipShaRepository = pushedHeadTipShaRepository;
        }

        public async Task AutoCommitAndPushSingleCakeFileIfNecessaryAsync(IFolder repositoryFolder, IErrorsAndInfos errorsAndInfos) {
            await AutoCommitAndPushSingleCakeFileAsync(repositoryFolder, true, errorsAndInfos);
        }

        public async Task AutoCommitAndPushSingleCakeFileAsync(IFolder repositoryFolder, IErrorsAndInfos errorsAndInfos) {
            await AutoCommitAndPushSingleCakeFileAsync(repositoryFolder, false, errorsAndInfos);
        }

        private async Task AutoCommitAndPushSingleCakeFileAsync(IFolder repositoryFolder, bool onlyIfNecessary, IErrorsAndInfos errorsAndInfos) {
            var files = vGitUtilities.FilesWithUncommittedChanges(repositoryFolder).ToList();
            if (files.Count == 0) {
                if (onlyIfNecessary) { return; }

                errorsAndInfos.Errors.Add(Properties.Resources.NoFileWithUncommittedChanges);
            } else if (files.Count != 1) {
                errorsAndInfos.Errors.Add(string.Format(Properties.Resources.ExactlyOneFileExpected, string.Join(", ", files)));
                return;
            }

            var file = files[0];
            if (!file.EndsWith(".cake", StringComparison.InvariantCultureIgnoreCase)) {
                errorsAndInfos.Errors.Add(Properties.Resources.OnlyCakeFilesExpected);
                return;
            }

            var shortName = file.Substring(file.LastIndexOf('\\') + 1);

            var message = string.Format(Properties.Resources.AutoUpdateOfCakeFile, shortName);
            await AutoCommitAndPushAsync(repositoryFolder, files, onlyIfNecessary, message, true, errorsAndInfos);
        }

        public async Task AutoCommitAndPushPackageUpdates(IFolder repositoryFolder, IErrorsAndInfos errorsAndInfos) {
            var files = vGitUtilities.FilesWithUncommittedChanges(repositoryFolder).ToList();
            if (files.Count == 0) {
                errorsAndInfos.Errors.Add(Properties.Resources.AtLeastOneFileExpected);
                return;
            }

            if (!files.All(f => f.EndsWith(".csproj", StringComparison.InvariantCultureIgnoreCase) || f.EndsWith(".config", StringComparison.InvariantCultureIgnoreCase))) {
                errorsAndInfos.Errors.Add(Properties.Resources.OnlyCsProjAndConfigFilesExpected);
                return;
            }

            await AutoCommitAndPushAsync(repositoryFolder, files, false, Properties.Resources.PackageUpdates, false, errorsAndInfos);
        }

        private async Task AutoCommitAndPushAsync(IFolder repositoryFolder, List<string> files, bool onlyIfNecessary, string commitMessage, bool noRebuildRequired, IErrorsAndInfos errorsAndInfos) {
            var branchName = vGitUtilities.CheckedOutBranch(repositoryFolder);
            if (branchName != "master") {
                errorsAndInfos.Errors.Add(Properties.Resources.CheckedOutBranchIsNotMaster);
                return;
            }

            var headTipShaBeforePush = vGitUtilities.HeadTipIdSha(repositoryFolder);

            vGitUtilities.IdentifyOwnerAndName(repositoryFolder, out var owner, out _, errorsAndInfos);
            if (errorsAndInfos.AnyErrors()) {
                errorsAndInfos.Errors.Add(Properties.Resources.OwnerAndNameNotFound);
                return;
            }

            var personalAccessTokensSecret = new PersonalAccessTokensSecret();
            var personalAccessTokens = await vSecretRepository.GetAsync(personalAccessTokensSecret, errorsAndInfos);
            var personalAccessToken = personalAccessTokens.FirstOrDefault(t => t.Owner == owner && t.Purpose == "AutoCommitPush");
            if (personalAccessToken == null) {
                errorsAndInfos.Errors.Add(Properties.Resources.AutoCommitPushAccessTokenNotFound);
                return;
            }

            using (var repo = new Repository(repositoryFolder.FullName)) {
                var remotes = repo.Network.Remotes.ToList();
                if (remotes.Count != 1) {
                    errorsAndInfos.Errors.Add(Properties.Resources.RemoteNotFoundOrNotUnique);
                    return;
                }

                var remote = remotes[0];

                files.ForEach(f => Commands.Stage(repo, f));

                var checkFiles = vGitUtilities.FilesWithUncommittedChanges(repositoryFolder);
                if (onlyIfNecessary && !checkFiles.Any()) { return; }

                if (checkFiles.Count != files.Count) {
                    errorsAndInfos.Errors.Add(string.Format(Properties.Resources.NumberOfFilesWithUncommittedChangesHasChanged,
                        string.Join(", ", files), string.Join(", ", checkFiles)));
                    return;
                }

                var author = new Signature(personalAccessToken.TokenName, personalAccessToken.Email, DateTime.Now);
                var committer = author;
                repo.Commit(commitMessage, author, committer);

                var options = new PushOptions {
                    CredentialsProvider = (aUrl, aUserNameFromUrl, someTypes) => new UsernamePasswordCredentials {
                        Username = owner,
                        Password = personalAccessToken.Token
                    }
                };

                repo.Network.Push(remote, @"refs/heads/" + branchName, options);

                if (!noRebuildRequired) { return; }

                var pushedHeadTipShaRepository = vPushedHeadTipShaRepository;
                if (!pushedHeadTipShaRepository.Get(errorsAndInfos).Contains(headTipShaBeforePush)) { return; }

                var headTipSha = vGitUtilities.HeadTipIdSha(repositoryFolder);
                pushedHeadTipShaRepository.Add(headTipSha, errorsAndInfos);
            }
        }
    }
}
