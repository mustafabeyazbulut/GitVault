# GitVault - Proje Rehberi

## Proje Hakkinda

GitHub repolarini otomatik olarak belirtilen ag klasorune (NAS) versiyonlu sekilde senkronize eden Windows Service uygulamasi.

## Teknoloji

- .NET Framework 4.8 (Windows Service - ServiceBase)
- C# 9
- Octokit (GitHub API)
- Newtonsoft.Json
- InstallUtil + ProjectInstaller ile kurulum

## Proje Yapisi

```
GitVault/
├── GitVault.sln / GitVault.csproj     # Solution ve proje dosyasi (ayni klasorde)
├── Program.cs                          # Entry point (DEBUG: konsol menu, RELEASE: ServiceBase.Run)
├── GitVaultService.cs                  # Ana servis sinifi (ServiceBase)
├── GitVaultService.Designer.cs         # InitializeComponent
├── ProjectInstaller.cs                 # [RunInstaller(true)]
├── ProjectInstaller.Designer.cs        # ServiceInstaller ayarlari
├── App.config                          # Ayarlar (gitignore'da - token iceriyor)
├── Helpers/
│   ├── LogHelpers.cs                   # Kategorili, thread-safe dosya loglama
│   └── RetryHelper.cs                  # Retry mekanizmasi (3 deneme, delay ile)
├── Models/
│   └── AppSettings.cs                  # ConfigurationManager ile App.config okuma
├── Services/
│   ├── GitHubApiService.cs             # Octokit ile repo listeleme
│   └── RepoSyncService.cs             # Git clone/fetch, ZIP arsivleme, CHANGELOG
├── Properties/
│   └── AssemblyInfo.cs
└── sertifika/
    └── 154126523.pfx                   # Code signing sertifikasi
```

## Build

MSBuild ile build alinir (.NET Framework 4.8 gerekli):

```
MSBuild.exe GitVault.sln -verbosity:minimal
```

Build sonrasi signtool ile otomatik imzalama yapilir (csproj icerisinde Target tanimli).

## Onemli Kurallar

- **App.config** gitignore'da cunku GitHub token iceriyor. Commit etme!
- **sertifika/154126523.pfx** code signing sertifikasi, sifre: 154126523
- Sertifika imzalama csproj icinde `SignAfterBuild` target ile otomatik calisir
- NAS erisim sorunlari icin retry mekanizmasi var (3 deneme, 5-10 sn aralik)
- Guvenlik duvari anlik baglanti engelleyebilir, bu yuzden retry kritik
- CHANGELOG ve git ciktilari **UTF-8** encoding ile yazilir (Turkce karakter destegi)
- Organizasyon: AundeTeknik-Org
- Hedef NAS: \\atsbnas01\system\MBEYAZBULUT\Projeler

## Servis Ayarlari (App.config)

| Ayar | Aciklama |
|------|----------|
| GitHub:Token | GitHub PAT (repo scope) |
| GitHub:Organizations | Virgul ile org listesi |
| GitHub:Users | Virgul ile kullanici listesi |
| GitHub:IgnoreRepos | Yedeklenmeyecek repolar |
| Sync:DestinationPath | Hedef klasor (UNC destekler) |
| Sync:CheckIntervalMinutes | Kontrol araligi (dk) |
| Sync:MaxVersionsToKeep | Repo basina arsiv limiti |

## Windows Service

- Service adi: GitVaultService
- Display name: GitVault Service
- Hesap: LocalSystem
- Baslama: Automatic
- Kurulum: InstallUtil.exe GitVault.exe
