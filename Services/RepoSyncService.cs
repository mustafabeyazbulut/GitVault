using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GitVault.Helpers;
using GitVault.Models;
using Newtonsoft.Json;

namespace GitVault.Services
{
    public class RepoSyncService
    {
        private const string SRC = "RepoSync";
        private readonly string _trackingFile;

        public RepoSyncService()
        {
            _trackingFile = Path.Combine(AppSettings.DestinationPath, ".github-pull-tracking.json");
        }

        public bool CheckDestinationAccess()
        {
            try
            {
                var testPath = Path.Combine(AppSettings.DestinationPath, ".gitvault-access-test");
                Directory.CreateDirectory(AppSettings.DestinationPath);
                File.WriteAllText(testPath, DateTime.Now.ToString());
                File.Delete(testPath);
                return true;
            }
            catch (Exception ex)
            {
                LogHelpers.Error($"Hedef klasore erisilemedi: {AppSettings.DestinationPath}", ex, LogCategory.Sync, SRC);
                return false;
            }
        }

        // Donus degeri: true = repo guncellendi, false = degisiklik yoktu
        public async Task<bool> SyncRepositoryAsync(RepositoryInfo repo)
        {
            var ownerDir = Path.Combine(AppSettings.DestinationPath, repo.Owner);
            var repoDir = Path.Combine(ownerDir, repo.Name);
            var gitCacheDir = Path.Combine(AppSettings.DestinationPath, ".git-cache", repo.Owner, repo.Name);

            await RetryHelper.ExecuteAsync(() =>
            {
                Directory.CreateDirectory(ownerDir);
                Directory.CreateDirectory(Path.Combine(AppSettings.DestinationPath, ".git-cache", repo.Owner));
                return Task.CompletedTask;
            }, maxRetries: 3, delaySeconds: 5, operationName: $"Klasor olusturma: {repo.Owner}/{repo.Name}");

            string currentCommit;
            var cacheBaseDir = gitCacheDir;

            // Gecerli bir cache bul
            gitCacheDir = FindValidCache(cacheBaseDir);

            if (gitCacheDir != null)
            {
                // Mevcut gecerli cache var, fetch ile guncelle
                LogHelpers.Info($"Repo guncelleniyor: {repo.Owner}/{repo.Name} (cache: {Path.GetFileName(gitCacheDir)})", LogCategory.Git, SRC);

                var fetchResult = await RetryHelper.ExecuteAsync(async () =>
                {
                    var result = await RunGitAsync("fetch --all", gitCacheDir);
                    if (!result.Success) throw new Exception($"Git fetch hatasi: {result.Error}");
                    return result;
                }, maxRetries: 3, delaySeconds: 10, operationName: $"Git fetch: {repo.Owner}/{repo.Name}");

                var resetResult = await RunGitAsync($"reset --hard origin/{repo.DefaultBranch}", gitCacheDir);
                if (!resetResult.Success)
                {
                    LogHelpers.Error($"Git reset basarisiz: {repo.Owner}/{repo.Name} - {resetResult.Error}", LogCategory.Git, SRC);
                    return false;
                }

                var commitResult = await RunGitAsync("rev-parse HEAD", gitCacheDir);
                currentCommit = commitResult.Output.Trim();
            }
            else
            {
                // Gecerli cache yok, yeni tarihli klasore clone yap
                gitCacheDir = cacheBaseDir + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");

                LogHelpers.Info($"Repo klonlaniyor: {repo.Owner}/{repo.Name} -> {Path.GetFileName(gitCacheDir)}", LogCategory.Git, SRC);

                await RetryHelper.ExecuteAsync(async () =>
                {
                    var result = await RunGitAsync($"clone \"{repo.CloneUrl}\" \"{gitCacheDir}\"", null);
                    if (!result.Success)
                    {
                        try { if (Directory.Exists(gitCacheDir)) Directory.Delete(gitCacheDir, true); } catch { }
                        throw new Exception($"Git clone hatasi: {result.Error}");
                    }
                    return result;
                }, maxRetries: 3, delaySeconds: 10, operationName: $"Git clone: {repo.Owner}/{repo.Name}");

                var commitResult = await RunGitAsync("rev-parse HEAD", gitCacheDir);
                currentCommit = commitResult.Output.Trim();

                // Eski bozuk cache klasorlerini temizlemeye calis
                CleanOldCaches(cacheBaseDir, gitCacheDir);
            }

            // Son kaydedilen commit ile karsilastir
            var lastCommit = GetLastTrackedCommit(repo.Owner, repo.Name);
            if (string.Equals(currentCommit, lastCommit, StringComparison.OrdinalIgnoreCase))
            {
                LogHelpers.Info($"Degisiklik yok: {repo.Owner}/{repo.Name} (commit: {SafeSubstring(currentCommit, 7)})", LogCategory.Sync, SRC);
                return false;
            }

            var shortCommit = SafeSubstring(currentCommit, 7);
            var latestDir = Path.Combine(repoDir, "latest");
            var arsivDir = Path.Combine(repoDir, "arsiv");
            Directory.CreateDirectory(arsivDir);

            // 1) Mevcut latest varsa arsivle (zip)
            if (Directory.Exists(latestDir) && lastCommit != null)
            {
                var shortOldCommit = SafeSubstring(lastCommit, 7);
                var lastTracking = GetTrackingEntry(repo.Owner, repo.Name);
                var arsivDate = lastTracking?.Date ?? DateTime.Now.ToString("yyyy-MM-dd");
                var zipName = $"{arsivDate}_{shortOldCommit}.zip";
                var zipPath = Path.Combine(arsivDir, zipName);

                LogHelpers.Info($"Mevcut versiyon arsivleniyor: {zipName}", LogCategory.Sync, SRC);

                using (LogHelpers.MeasureTime($"ZIP olusturma: {repo.Owner}/{repo.Name}", LogCategory.Sync, SRC))
                {
                    await RetryHelper.ExecuteAsync(() =>
                    {
                        if (File.Exists(zipPath))
                            File.Delete(zipPath);
                        ZipFile.CreateFromDirectory(latestDir, zipPath, CompressionLevel.Optimal, false);
                        return Task.CompletedTask;
                    }, maxRetries: 3, delaySeconds: 5, operationName: $"ZIP arsivleme: {repo.Owner}/{repo.Name}");
                }
            }

            // 2) latest klasorunu guncelle
            if (Directory.Exists(latestDir))
                Directory.Delete(latestDir, true);

            LogHelpers.Info($"Latest guncelleniyor: {repo.Owner}/{repo.Name} -> {shortCommit}", LogCategory.Sync, SRC);

            using (LogHelpers.MeasureTime($"Dosya kopyalama: {repo.Owner}/{repo.Name}", LogCategory.Sync, SRC))
            {
                await RetryHelper.ExecuteAsync(
                    () => CopyDirectoryAsync(gitCacheDir, latestDir),
                    maxRetries: 3, delaySeconds: 5,
                    operationName: $"Dosya kopyalama: {repo.Owner}/{repo.Name}");
            }

            // 3) CHANGELOG olustur
            await GenerateChangelogAsync(repo, gitCacheDir, repoDir, lastCommit, currentCommit);

            // 4) Tracking guncelle
            SaveTrackingEntry(repo.Owner, repo.Name, currentCommit);

            // 5) Eski arsivleri temizle
            CleanOldArchives(arsivDir);

            LogHelpers.Info($"Senkronizasyon tamamlandi: {repo.Owner}/{repo.Name} (commit: {shortCommit})", LogCategory.Sync, SRC);
            return true;
        }

        private async Task GenerateChangelogAsync(RepositoryInfo repo, string gitCacheDir, string repoDir, string lastCommit, string currentCommit)
        {
            var sb = new StringBuilder();
            var shortCurrent = SafeSubstring(currentCommit, 7);

            sb.AppendLine("================================================================");
            sb.AppendLine($"  {repo.Owner}/{repo.Name} - Degisiklik Raporu");
            sb.AppendLine("================================================================");
            sb.AppendLine();
            sb.AppendLine($"Tarih       : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Yeni Commit : {currentCommit}");

            if (lastCommit != null)
            {
                sb.AppendLine($"Onceki Commit: {lastCommit}");
                sb.AppendLine();

                // Commit gecmisi
                sb.AppendLine("--- Commit Gecmisi ---");
                var logResult = await RunGitAsync(
                    $"log {lastCommit}..{currentCommit} --pretty=format:\"%h - %an - %ad - %s\" --date=format:\"%Y-%m-%d %H:%M\"",
                    gitCacheDir);

                if (logResult.Success && !string.IsNullOrWhiteSpace(logResult.Output))
                    sb.AppendLine(logResult.Output.Trim());
                else
                    sb.AppendLine("(commit gecmisi alinamadi)");

                sb.AppendLine();

                // Degisen dosyalar
                sb.AppendLine("--- Degisen Dosyalar ---");
                var diffResult = await RunGitAsync(
                    $"diff --stat {lastCommit}..{currentCommit}",
                    gitCacheDir);

                if (diffResult.Success && !string.IsNullOrWhiteSpace(diffResult.Output))
                    sb.AppendLine(diffResult.Output.Trim());
                else
                    sb.AppendLine("(dosya degisiklikleri alinamadi)");

                sb.AppendLine();

                // Ozet istatistik
                var shortlogResult = await RunGitAsync(
                    $"rev-list --count {lastCommit}..{currentCommit}",
                    gitCacheDir);

                var commitCount = shortlogResult.Success ? shortlogResult.Output.Trim() : "?";

                var numstatResult = await RunGitAsync(
                    $"diff --shortstat {lastCommit}..{currentCommit}",
                    gitCacheDir);

                sb.AppendLine("--- Ozet ---");
                sb.AppendLine($"Toplam commit: {commitCount}");
                if (numstatResult.Success && !string.IsNullOrWhiteSpace(numstatResult.Output))
                    sb.AppendLine(numstatResult.Output.Trim());
            }
            else
            {
                sb.AppendLine($"Onceki Commit: (ilk senkronizasyon)");
                sb.AppendLine();
                sb.AppendLine("--- Son 10 Commit ---");
                var logResult = await RunGitAsync(
                    "log -10 --pretty=format:\"%h - %an - %ad - %s\" --date=format:\"%Y-%m-%d %H:%M\"",
                    gitCacheDir);

                if (logResult.Success)
                    sb.AppendLine(logResult.Output.Trim());
            }

            sb.AppendLine();
            sb.AppendLine("================================================================");

            // CHANGELOG.txt yaz (repo ana klasorune)
            var changelogPath = Path.Combine(repoDir, "CHANGELOG.txt");

            // Mevcut changelog varsa basa ekle
            var existingContent = "";
            if (File.Exists(changelogPath))
                existingContent = File.ReadAllText(changelogPath, Encoding.UTF8);

            File.WriteAllText(changelogPath, sb.ToString() + Environment.NewLine + existingContent, Encoding.UTF8);

            LogHelpers.Info($"CHANGELOG guncellendi: {repo.Owner}/{repo.Name}", LogCategory.Sync, SRC);
        }

        private void CleanOldArchives(string arsivDir)
        {
            if (!Directory.Exists(arsivDir)) return;

            var archives = Directory.GetFiles(arsivDir, "*.zip")
                .OrderByDescending(f => f)
                .ToList();

            if (archives.Count <= AppSettings.MaxVersionsToKeep) return;

            var toDelete = archives.Skip(AppSettings.MaxVersionsToKeep);
            foreach (var file in toDelete)
            {
                try
                {
                    File.Delete(file);
                    LogHelpers.Info($"Eski arsiv silindi: {Path.GetFileName(file)}", LogCategory.Sync, SRC);
                }
                catch (Exception ex)
                {
                    LogHelpers.Warn($"Eski arsiv silinemedi: {file} - {ex.Message}", LogCategory.Sync, SRC);
                }
            }
        }

        #region Git Islemleri

        private async Task<GitResult> RunGitAsync(string arguments, string workingDirectory)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"-c safe.directory=* {arguments}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            psi.EnvironmentVariables["LANG"] = "en_US.UTF-8";
            psi.EnvironmentVariables["LC_ALL"] = "en_US.UTF-8";
            psi.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";

            if (!string.IsNullOrEmpty(workingDirectory))
                psi.WorkingDirectory = workingDirectory;

            using (var process = Process.Start(psi))
            {
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();

                if (!process.WaitForExit(120000)) // 2 dakika timeout
                {
                    try { process.Kill(); } catch { }
                    return new GitResult
                    {
                        Success = false,
                        Error = $"Git islemi zaman asimina ugradi (2 dk): git {arguments}"
                    };
                }

                return new GitResult
                {
                    Success = process.ExitCode == 0,
                    Output = output,
                    Error = error
                };
            }
        }

        #endregion

        #region Dosya Islemleri

        private async Task CopyDirectoryAsync(string source, string destination)
        {
            Directory.CreateDirectory(destination);

            foreach (var file in Directory.GetFiles(source))
            {
                var destFile = Path.Combine(destination, Path.GetFileName(file));
                using (var sourceStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
                using (var destStream = new FileStream(destFile, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
                {
                    await sourceStream.CopyToAsync(destStream);
                }
            }

            foreach (var dir in Directory.GetDirectories(source))
            {
                var dirName = Path.GetFileName(dir);
                if (dirName == ".git") continue;
                await CopyDirectoryAsync(dir, Path.Combine(destination, dirName));
            }
        }

        #endregion

        #region Tracking

        private TrackingEntry GetTrackingEntry(string owner, string repoName)
        {
            var tracking = LoadTracking();
            var key = $"{owner}/{repoName}";
            return tracking.ContainsKey(key) ? tracking[key] : null;
        }

        private void SaveTrackingEntry(string owner, string repoName, string commit)
        {
            var tracking = LoadTracking();
            var key = $"{owner}/{repoName}";
            tracking[key] = new TrackingEntry
            {
                Commit = commit,
                Date = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"),
                UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
            var json = JsonConvert.SerializeObject(tracking, Formatting.Indented);
            File.WriteAllText(_trackingFile, json);
        }

        private string GetLastTrackedCommit(string owner, string repoName)
        {
            var entry = GetTrackingEntry(owner, repoName);
            return entry?.Commit;
        }

        private Dictionary<string, TrackingEntry> LoadTracking()
        {
            if (!File.Exists(_trackingFile))
                return new Dictionary<string, TrackingEntry>();

            try
            {
                var json = File.ReadAllText(_trackingFile);
                return JsonConvert.DeserializeObject<Dictionary<string, TrackingEntry>>(json)
                       ?? new Dictionary<string, TrackingEntry>();
            }
            catch
            {
                return new Dictionary<string, TrackingEntry>();
            }
        }

        #endregion

        /// <summary>
        /// Gecerli bir git cache klasoru bulur. Onceligi: ana klasor, sonra tarihli alternatifler (en yeni once).
        /// Hicbiri gecerli degilse null doner.
        /// </summary>
        private string FindValidCache(string baseCacheDir)
        {
            // Ana klasor gecerli mi?
            if (IsValidGitCache(baseCacheDir))
                return baseCacheDir;

            // Tarihli alternatiflere bak (en yenisi once)
            var parentDir = Path.GetDirectoryName(baseCacheDir);
            var dirName = Path.GetFileName(baseCacheDir);

            if (parentDir != null && Directory.Exists(parentDir))
            {
                var found = Directory.GetDirectories(parentDir, dirName + "_*")
                    .Where(IsValidGitCache)
                    .OrderByDescending(d => d)
                    .FirstOrDefault();

                if (found != null)
                {
                    LogHelpers.Info($"Gecerli cache bulundu: {Path.GetFileName(found)}", LogCategory.Git, SRC);
                    return found;
                }
            }

            return null;
        }

        private bool IsValidGitCache(string dir)
        {
            return Directory.Exists(dir)
                && Directory.Exists(Path.Combine(dir, ".git"))
                && File.Exists(Path.Combine(dir, ".git", "HEAD"));
        }

        /// <summary>
        /// Bozuk/eski cache klasorlerini temizlemeye calisir. Silemezse sessizce gecer.
        /// </summary>
        private void CleanOldCaches(string baseCacheDir, string currentCacheDir)
        {
            var parentDir = Path.GetDirectoryName(baseCacheDir);
            var dirName = Path.GetFileName(baseCacheDir);
            if (parentDir == null || !Directory.Exists(parentDir)) return;

            // Ana klasor + tarihli klasorleri bul, aktif olani haric tut
            var allCaches = Directory.GetDirectories(parentDir, dirName + "*")
                .Where(d => !string.Equals(d, currentCacheDir, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var dir in allCaches)
            {
                try
                {
                    Directory.Delete(dir, true);
                    LogHelpers.Info($"Eski cache silindi: {Path.GetFileName(dir)}", LogCategory.Git, SRC);
                }
                catch
                {
                    LogHelpers.Debug($"Eski cache silinemedi (sonra tekrar denenecek): {Path.GetFileName(dir)}", LogCategory.Git, SRC);
                }
            }
        }

        private static string SafeSubstring(string value, int length)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Length <= length ? value : value.Substring(0, length);
        }

        private class GitResult
        {
            public bool Success { get; set; }
            public string Output { get; set; } = "";
            public string Error { get; set; } = "";
        }

        public class TrackingEntry
        {
            public string Commit { get; set; }
            public string Date { get; set; }
            public string UpdatedAt { get; set; }
        }
    }
}
