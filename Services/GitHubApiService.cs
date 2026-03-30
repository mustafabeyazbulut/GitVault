using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GitVault.Helpers;
using GitVault.Models;
using Octokit;

namespace GitVault.Services
{
    public class RepositoryInfo
    {
        public string Name { get; set; }
        public string CloneUrl { get; set; }
        public string Owner { get; set; }
        public string DefaultBranch { get; set; } = "main";
        public DateTime LastPush { get; set; }
    }

    public class GitHubApiService
    {
        private const string SRC = "GitHubApi";
        private readonly GitHubClient _client;

        public GitHubApiService()
        {
            _client = new GitHubClient(new ProductHeaderValue("GitVault"));

            if (!string.IsNullOrEmpty(AppSettings.GitHubToken))
            {
                _client.Credentials = new Credentials(AppSettings.GitHubToken);
                LogHelpers.Info("GitHub token ile kimlik dogrulama yapildi", LogCategory.GitHub, SRC);
            }
            else
            {
                LogHelpers.Warn("GitHub token belirtilmemis! Public repolar ile sinirli olacak", LogCategory.GitHub, SRC);
            }
        }

        public async Task<List<RepositoryInfo>> GetAllRepositoriesAsync()
        {
            var repos = new List<RepositoryInfo>();

            // Organizasyon repolari
            foreach (var org in AppSettings.Organizations)
            {
                try
                {
                    LogHelpers.Info($"Organizasyon repolari aliniyor: {org}", LogCategory.GitHub, SRC);
                    var orgRepos = await RetryHelper.ExecuteAsync(
                        () => _client.Repository.GetAllForOrg(org),
                        maxRetries: 3, delaySeconds: 10,
                        operationName: $"GitHub API - {org} org repolari");

                    foreach (var repo in orgRepos)
                    {
                        if (!IsIgnored(repo.Name))
                        {
                            repos.Add(new RepositoryInfo
                            {
                                Name = repo.Name,
                                CloneUrl = repo.CloneUrl,
                                Owner = org,
                                DefaultBranch = repo.DefaultBranch,
                                LastPush = repo.PushedAt?.UtcDateTime ?? DateTime.MinValue
                            });
                        }
                        else
                        {
                            LogHelpers.Debug($"Repo ignore edildi: {org}/{repo.Name}", LogCategory.GitHub, SRC);
                        }
                    }

                    LogHelpers.Info($"{org} organizasyonundan {orgRepos.Count} repo alindi, {repos.Count(r => r.Owner == org)} tanesi aktif", LogCategory.GitHub, SRC);
                }
                catch (Exception ex)
                {
                    LogHelpers.Error($"Organizasyon repolari alinamadi: {org}", ex, LogCategory.GitHub, SRC);
                }
            }

            // Kullanici repolari
            foreach (var user in AppSettings.Users)
            {
                try
                {
                    LogHelpers.Info($"Kullanici repolari aliniyor: {user}", LogCategory.GitHub, SRC);
                    var userRepos = await RetryHelper.ExecuteAsync(
                        () => _client.Repository.GetAllForUser(user),
                        maxRetries: 3, delaySeconds: 10,
                        operationName: $"GitHub API - {user} kullanici repolari");

                    foreach (var repo in userRepos)
                    {
                        if (!IsIgnored(repo.Name))
                        {
                            repos.Add(new RepositoryInfo
                            {
                                Name = repo.Name,
                                CloneUrl = repo.CloneUrl,
                                Owner = user,
                                DefaultBranch = repo.DefaultBranch,
                                LastPush = repo.PushedAt?.UtcDateTime ?? DateTime.MinValue
                            });
                        }
                        else
                        {
                            LogHelpers.Debug($"Repo ignore edildi: {user}/{repo.Name}", LogCategory.GitHub, SRC);
                        }
                    }

                    LogHelpers.Info($"{user} kullanicisinden {userRepos.Count} repo alindi, {repos.Count(r => r.Owner == user)} tanesi aktif", LogCategory.GitHub, SRC);
                }
                catch (Exception ex)
                {
                    LogHelpers.Error($"Kullanici repolari alinamadi: {user}", ex, LogCategory.GitHub, SRC);
                }
            }

            LogHelpers.Info($"Toplam {repos.Count} repo bulundu (ignore harici)", LogCategory.GitHub, SRC);
            return repos;
        }

        private bool IsIgnored(string repoName)
        {
            return AppSettings.IgnoreRepos
                .Any(ignored => string.Equals(ignored, repoName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
