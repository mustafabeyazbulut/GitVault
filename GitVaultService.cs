using System;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using GitVault.Helpers;
using GitVault.Models;
using GitVault.Services;

namespace GitVault
{
    public partial class GitVaultService : ServiceBase
    {
        private const string SRC = "GitVaultService";
        private CancellationTokenSource _cts;
        private readonly GitHubApiService _gitHubApiService;
        private readonly RepoSyncService _syncService;

        public GitVaultService()
        {
            InitializeComponent();
            _gitHubApiService = new GitHubApiService();
            _syncService = new RepoSyncService();
            LogHelpers.Info("Service olusturuldu", LogCategory.Service, SRC);
        }

        protected override void OnStart(string[] args)
        {
            LogHelpers.Info("Service baslatiliyor...", LogCategory.Service, SRC);
            LogHelpers.Info($"Hedef klasor: {AppSettings.DestinationPath}", LogCategory.Service, SRC);
            LogHelpers.Info($"Kontrol araligi: {AppSettings.CheckIntervalMinutes} dakika", LogCategory.Service, SRC);
            LogHelpers.Info($"Ignore listesi: {string.Join(", ", AppSettings.IgnoreRepos)}", LogCategory.Service, SRC);
            LogHelpers.Info($"Organizasyonlar: {string.Join(", ", AppSettings.Organizations)}", LogCategory.Service, SRC);
            LogHelpers.Info($"Kullanicilar: {string.Join(", ", AppSettings.Users)}", LogCategory.Service, SRC);

            _cts = new CancellationTokenSource();
            Task.Run(() => ScheduleNextRunTime(_cts.Token));
        }

        protected override void OnStop()
        {
            LogHelpers.Info("Service durduruluyor...", LogCategory.Service, SRC);
            _cts?.Cancel();
            KillGitProcesses();
            LogHelpers.Flush();
        }

        private void KillGitProcesses()
        {
            try
            {
                foreach (var proc in System.Diagnostics.Process.GetProcessesByName("git"))
                {
                    try
                    {
                        proc.Kill();
                        LogHelpers.Debug($"Git process sonlandirildi (PID: {proc.Id})", LogCategory.Service, SRC);
                    }
                    catch { }
                    finally { proc.Dispose(); }
                }
            }
            catch (Exception ex)
            {
                LogHelpers.Warn($"Git processleri sonlandirilamadi: {ex.Message}", LogCategory.Service, SRC);
            }
        }

        public void OnDebug()
        {
            LogHelpers.Info("DEBUG modunda calistiriliyor", LogCategory.Service, SRC);
            RunTaskAsync().GetAwaiter().GetResult();
            LogHelpers.Flush();
        }

        private async Task ScheduleNextRunTime(CancellationToken ct)
        {
            // Ilk calistirmada hemen basla
            await RunTaskAsync();

            while (!ct.IsCancellationRequested)
            {
                var intervalMs = AppSettings.CheckIntervalMinutes * 60 * 1000;
                var nextRun = DateTime.Now.AddMinutes(AppSettings.CheckIntervalMinutes);
                LogHelpers.Info($"Sonraki calistirma: {nextRun:yyyy-MM-dd HH:mm:ss}", LogCategory.Service, SRC);

                ct.WaitHandle.WaitOne(intervalMs);

                if (!ct.IsCancellationRequested)
                {
                    await RunTaskAsync();
                }
            }
        }

        private async Task RunTaskAsync()
        {
            try
            {
                LogHelpers.Info("=== Senkronizasyon basladi ===", LogCategory.Service, SRC);

                // NAS baglantisi kur (kimlik bilgileri tanimliysa)
                if (!NetworkShareHelper.EnsureConnected())
                {
                    LogHelpers.Error($"NAS paylasimina baglanilamadi: {AppSettings.DestinationPath}. Bu dongu atlanacak.", LogCategory.Service, SRC);
                    return;
                }

                // NAS erisim kontrolu
                if (!_syncService.CheckDestinationAccess())
                {
                    LogHelpers.Error($"Hedef klasore erisilemedi: {AppSettings.DestinationPath}. Bu dongu atlanacak.", LogCategory.Service, SRC);
                    return;
                }

                using (LogHelpers.MeasureTime("Toplam senkronizasyon", LogCategory.Service, SRC))
                {
                    var repos = await _gitHubApiService.GetAllRepositoriesAsync();

                    if (repos.Count == 0)
                    {
                        LogHelpers.Warn("Hic repo bulunamadi. GitHub ayarlarini kontrol edin.", LogCategory.Service, SRC);
                        return;
                    }

                    var updatedRepos  = new List<string>();
                    var noChangeRepos = new List<string>();
                    var errorRepos    = new List<string>();

                    foreach (var repo in repos)
                    {
                        try
                        {
                            var updated = await _syncService.SyncRepositoryAsync(repo);
                            if (updated)
                                updatedRepos.Add($"{repo.Owner}/{repo.Name}");
                            else
                                noChangeRepos.Add($"{repo.Owner}/{repo.Name}");
                        }
                        catch (Exception ex)
                        {
                            errorRepos.Add($"{repo.Owner}/{repo.Name}");
                            LogHelpers.Error($"Repo senkronizasyonu basarisiz: {repo.Owner}/{repo.Name}", ex, LogCategory.Service, SRC);
                        }
                    }

                    LogHelpers.Info($"=== Senkronizasyon tamamlandi === Guncellenen: {updatedRepos.Count}, Degismeyen: {noChangeRepos.Count}, Hatali: {errorRepos.Count}, Toplam: {repos.Count}", LogCategory.Service, SRC);

                    EmailService.SendSyncReport(updatedRepos, noChangeRepos, errorRepos);
                }
            }
            catch (Exception ex)
            {
                LogHelpers.Error("Senkronizasyon dongusunde kritik hata", ex, LogCategory.Service, SRC);
            }
        }
    }
}
