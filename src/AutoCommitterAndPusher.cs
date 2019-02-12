using System;
using System.Linq;
using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Entities;
using Aspenlaub.Net.GitHub.CSharp.Gitty.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using LibGit2Sharp;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion {
    public class AutoCommitterAndPusher : IAutoCommitterAndPusher {
        private readonly IGitUtilities vGitUtilities;
        private readonly ISecretRepository vSecretRepository;

        public AutoCommitterAndPusher(IGitUtilities gitUtilities, ISecretRepository secretRepository) {
            vGitUtilities = gitUtilities;
            vSecretRepository = secretRepository;
        }

        public async Task AutoCommitAndPushSingleCakeFileAsync(IFolder repositoryFolder) {
            var files = vGitUtilities.FilesWithUncommittedChanges(repositoryFolder);
            if (files.Count != 1) { return; }

            var file = files[0];
            if (!file.EndsWith(".cake", StringComparison.InvariantCultureIgnoreCase)) { return; }

            var shortName = file.Substring(file.LastIndexOf('\\') + 1);

            var branchName = vGitUtilities.CheckedOutBranch(repositoryFolder);
            if (branchName != "master") { return; }

            var errorsAndInfos = new ErrorsAndInfos();
            vGitUtilities.IdentifyOwnerAndName(repositoryFolder, out var owner, out _, errorsAndInfos);
            if (errorsAndInfos.AnyErrors()) { return; }

            var personalAccessTokensSecret = new PersonalAccessTokensSecret();
            var personalAccessTokens = await vSecretRepository.GetAsync(personalAccessTokensSecret, errorsAndInfos);
            var personalAccessToken = personalAccessTokens.FirstOrDefault(t => t.Owner == owner && t.Purpose == "AutoCommitPush");
            if (personalAccessToken == null) { return; }

            using (var repo = new Repository(repositoryFolder.FullName)) {
                var remotes = repo.Network.Remotes.ToList();
                if (remotes.Count != 1) { return; }

                var remote = remotes[0];

                Commands.Stage(repo, files[0]);

                files = vGitUtilities.FilesWithUncommittedChanges(repositoryFolder);
                if (files.Count != 1) { return; }

                var author = new Signature(personalAccessToken.TokenName, personalAccessToken.Email, DateTime.Now);
                var committer = author;
                var message = string.Format(Properties.Resources.AutoUpdateOfCakeFile, shortName);
                repo.Commit(message, author, committer);

                var options = new PushOptions {
                    CredentialsProvider = (aUrl, aUserNameFromUrl, someTypes) => new UsernamePasswordCredentials {
                        Username = owner,
                        Password = personalAccessToken.Token
                    }
                };

                repo.Network.Push(remote, @"refs/heads/" + branchName, options);
            }
        }
    }
}
