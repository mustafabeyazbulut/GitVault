# GitVault Service

GitHub repolarini otomatik olarak belirtilen ag klasorune versiyonlu sekilde senkronize eden Windows Service uygulamasi.

## Ne Yapar?

- Belirtilen GitHub **organizasyon** ve/veya **kullanicilarin** tum repolarini periyodik olarak kontrol eder
- **Ignore listesindeki** repolar haric hepsini hedef klasore kopyalar
- `latest` klasorunde her zaman **guncel kaynak kod** bulunur
- Degisiklik tespit edildiginde eski versiyon **ZIP olarak arsivlenir** (disk tasarrufu)
- Her guncelleme icin **CHANGELOG.txt** olusturur (commit gecmisi, degisen dosyalar, istatistik)
- Eski arsivler otomatik temizlenir (varsayilan: son 5 arsiv)
- Sadece **degisiklik varsa** islem yapar (commit takibi ile)

## Nasil Calisir?

1. Servis belirlenen aralikta (varsayilan 30 dk) GitHub API'yi sorgular
2. Organizasyon ve/veya kullanicilarin tum repolarini listeler
3. Ignore listesindeki repolari atlar
4. Her repo icin son commit'i kontrol eder
5. Degisiklik varsa:
   - Mevcut `latest` klasorunu ZIP'leyip `arsiv` klasorune tasir
   - Yeni hali `latest` klasorune kopyalar
   - CHANGELOG.txt'ye degisiklik raporunu yazar
6. Eski arsivleri temizler (son 5 arsiv tutulur)

## Gereksinimler

- .NET Framework 4.8
- Git (PATH'te tanimli olmali)
- GitHub Personal Access Token (private repolar icin zorunlu)

---

## Kurulum

### 1. GitHub Personal Access Token Olusturma

Token, servisin GitHub API'ye erisimi icin gereklidir. Private repolari yedeklemek icin **zorunludur**.
Public repolar icin token olmadan da calisir ancak API limiti saatte 60 istekle sinirli kalir.

**Adim adim:**

1. GitHub'a giris yapin
2. Sag ustteki profil resminize tiklayin > **Settings**
3. Sol menuden en altta **Developer settings**'e tiklayin
4. **Personal access tokens** > **Tokens (classic)** secin
5. **Generate new token** > **Generate new token (classic)** tiklayin
6. GitHub sifrenizi girin
7. Token ayarlari:
   - **Note**: `GitVault Service` (token'a isim verin)
   - **Expiration**: `No expiration` (suresi dolmasin) veya istediginiz sure
   - **Scopes**: Asagidaki kutulari isaretleyin:
     - [x] `repo` (Full control of private repositories)
       - Bu scope private repolara erismek icin yeterlidir
       - Public repolar icin `public_repo` da yeterlidir
8. **Generate token** butonuna tiklayin
9. Olusturulan token'i **hemen kopyalayin** (sayfa kapatildiktan sonra bir daha goremezsiniz!)
   - Token `ghp_` ile baslar, ornek: `ghp_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx`

**Onemli:**
- Token'i guvenli bir yerde saklayin
- Token'i baskasi ile paylasmyin
- Token ile tum private repolara erisilebilir, bu yuzden dikkatli olun
- Token suresi dolarsa yenisini olusturup `App.config`'i guncellemeniz gerekir

### 2. Yapilandirma (App.config)

Build aldiktan sonra `GitVault.exe.config` dosyasini (veya kaynak kodda `App.config`) duzenleyin:

```xml
<appSettings>
    <!-- GitHub Personal Access Token -->
    <add key="GitHub:Token" value="ghp_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx" />

    <!-- Takip edilecek organizasyonlar (virgul ile ayirin) -->
    <add key="GitHub:Organizations" value="sirket-org,diger-org" />

    <!-- Takip edilecek kullanicilar (virgul ile ayirin) -->
    <add key="GitHub:Users" value="mehmet,ahmet" />

    <!-- Ignore edilecek repolar (virgul ile ayirin) -->
    <add key="GitHub:IgnoreRepos" value="test-repo,eski-proje,deneme" />

    <!-- Hedef klasor (UNC path destekler) -->
    <add key="Sync:DestinationPath" value="\\atsb-nas\Yazilim" />

    <!-- Kontrol araligi (dakika) -->
    <add key="Sync:CheckIntervalMinutes" value="30" />

    <!-- Saklanacak maksimum arsiv sayisi (repo basina) -->
    <add key="Sync:MaxVersionsToKeep" value="5" />
</appSettings>
```

#### Ayar Aciklamalari

| Ayar | Aciklama | Ornek | Zorunlu |
|------|----------|-------|---------|
| `GitHub:Token` | GitHub Personal Access Token | `ghp_abc123...` | Private repolar icin evet |
| `GitHub:Organizations` | Takip edilecek GitHub organizasyonlari (virgul ile) | `sirket-org,diger-org` | En az biri* |
| `GitHub:Users` | Takip edilecek GitHub kullanicilari (virgul ile) | `mehmet,ahmet` | En az biri* |
| `GitHub:IgnoreRepos` | Yedeklenmeyecek repo isimleri (virgul ile) | `test,arsiv,fork` | Hayir |
| `Sync:DestinationPath` | Yedeklerin kaydedilecegi klasor | `\\atsb-nas\Yazilim` | Evet |
| `Sync:CheckIntervalMinutes` | Kontrol araligi (dakika) | `30` | Hayir (varsayilan: 30) |
| `Sync:MaxVersionsToKeep` | Repo basina saklanacak arsiv sayisi | `5` | Hayir (varsayilan: 5) |

> *Organizations ve Users alanlarindan **en az biri** dolu olmalidir. Ikisi birden de kullanilabilir.
> Ikisi birden bos olursa servis hic repo bulamaz.

#### Kullanim Senaryolari

**Senaryo 1: Sadece organizasyon repolari**
```xml
<add key="GitHub:Organizations" value="sirket-org" />
<add key="GitHub:Users" value="" />
```

**Senaryo 2: Sadece belirli kullanicilarin repolari**
```xml
<add key="GitHub:Organizations" value="" />
<add key="GitHub:Users" value="mehmet,ahmet" />
```

**Senaryo 3: Hem organizasyon hem kullanicilar**
```xml
<add key="GitHub:Organizations" value="sirket-org,diger-org" />
<add key="GitHub:Users" value="mehmet,ahmet" />
```
Bu durumda her ikisinin de tum repolari (ignore harici) yedeklenir.
Hedef klasorde organizasyonlar ve kullanicilar ayri klasorlerde tutulur:
```
\\atsb-nas\Yazilim\
├── sirket-org\        # Organizasyon repolari
│   ├── proje-a\
│   └── proje-b\
├── diger-org\         # Diger organizasyon
│   └── proje-c\
├── mehmet\            # Kullanici repolari
│   └── kisisel-proje\
└── ahmet\
    └── baska-proje\
```

**Senaryo 4: Bazi repolari haric tutma**
```xml
<add key="GitHub:IgnoreRepos" value="test-repo,deneme,arsiv,fork-proje" />
```
Ignore listesi hem organizasyon hem kullanici repolari icin gecerlidir.
Repo adi buyuk/kucuk harf duyarsizdir (`Test-Repo` = `test-repo`).

### 3. Service Kurulumu

Release olarak build aldiktan sonra:

```bash
# Kurulum (Admin komut satiri ile calistirin)
%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\InstallUtil.exe GitVault.exe

# Baslatma
net start GitVaultService

# Durdurma
net stop GitVaultService

# Kaldirma (once durdurun)
net stop GitVaultService
%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\InstallUtil.exe /u GitVault.exe
```

Service **otomatik baslatma** modunda kurulur. Bilgisayar yeniden basladiginda servis otomatik calisir.

> **Not:** Service **LocalSystem** hesabi altinda calisir. UNC path (`\\server\share`) kullaniminda
> bu hesabin ag paylasimlarina yazma yetkisi olmalidir. Gerekirse service hesabini
> Services.msc > GitVaultService > Properties > Log On sekmesinden degistirebilirsiniz.

---

## Debug Modu

Visual Studio'da **Debug** modunda calistirdiginizda service kurulmadan konsol olarak calisir:

```
=======================================
 GitVault Service - DEBUG Mode
=======================================

Secim yapin:
1. Senkronizasyonu baslat
2. Cikis
```

Service kurmadan once bu modda test etmeniz onerilir.

---

## Klasor Yapisi (Hedef)

Senkronizasyon sonrasi hedef klasorde su yapi olusur:

```
\\atsb-nas\Yazilim\
│
├── .git-cache\                              # Git repo cache (gizli, dokunmayin)
│   ├── sirket-org\
│   │   ├── proje-a\                         # Git repo (fetch icin kullanilir)
│   │   └── proje-b\
│   └── mehmet\
│       └── kisisel-proje\
│
├── .github-pull-tracking.json               # Commit takip dosyasi
│
├── sirket-org\
│   ├── proje-a\
│   │   ├── latest\                          # Guncel tam proje (her zaman hazir)
│   │   ├── arsiv\
│   │   │   ├── 2026-03-26_09-00-00_aaa1111.zip
│   │   │   ├── 2026-03-27_14-30-00_bbb2222.zip
│   │   │   ├── 2026-03-28_09-00-00_ccc3333.zip
│   │   │   ├── 2026-03-29_14-30-00_ddd4444.zip
│   │   │   └── 2026-03-30_09-00-00_eee5555.zip   # Son 5 arsiv tutulur
│   │   └── CHANGELOG.txt                   # Tum degisiklik gecmisi
│   └── proje-b\
│       ├── latest\
│       ├── arsiv\
│       └── CHANGELOG.txt
│
└── mehmet\
    └── kisisel-proje\
        ├── latest\
        ├── arsiv\
        └── CHANGELOG.txt
```

### latest Klasoru

Her zaman projenin **guncel tam hali** burada bulunur.
- `.git` klasoru dahil edilmez, temiz kaynak kodu
- Dogrudan acip kullanilabilir, herhangi bir islem gerektirmez
- Her senkronizasyonda guncellenir (degisiklik varsa)

### arsiv Klasoru

Her guncelleme oncesinde mevcut `latest` klasorunun tamami **ZIP olarak sikistirilip** arsive alinir.
- Dosya adi formati: `tarih_commitHash.zip` (ornek: `2026-03-30_09-00-00_abc1234.zip`)
- Eski bir versiyona bakmak icin ilgili zip dosyasini acmaniz yeterlidir
- ZIP icinde projenin o anki tam hali bulunur
- `MaxVersionsToKeep` sayisindan (varsayilan 5) fazla arsiv otomatik silinir
- ZIP sikistirma sayesinde eski versiyonlar ~%90 daha az disk alani kaplar

### CHANGELOG.txt

Her repo icin tutulan degisiklik raporu. Yeni degisiklikler dosyanin **basina** eklenir.
Acmadan hangi versiyonda ne degistigini gorebilirsiniz.

Icerdigi bilgiler:
- Tarih ve commit bilgisi
- **Commit gecmisi**: Kim, ne zaman, ne degistirmis (commit mesajlari)
- **Degisen dosya listesi**: Hangi dosyalar eklendi/silindi/degisti, kac satir degisti
- **Ozet istatistik**: Toplam commit, dosya ve satir sayilari

Ornek:

```
================================================================
  sirket-org/proje-a - Degisiklik Raporu
================================================================

Tarih       : 2026-03-30 10:00:05
Yeni Commit : def5678abc123456789...
Onceki Commit: abc1234def567890123...

--- Commit Gecmisi ---
def5678 - Mehmet - 2026-03-30 09:15 - Login sayfasina captcha eklendi
ccc4444 - Ahmet  - 2026-03-29 16:40 - Veritabani baglanti hatasi duzeltildi
bbb3333 - Ayse   - 2026-03-29 10:20 - Kullanici raporlama modulu eklendi

--- Degisen Dosyalar ---
 src/Login.cs                        | 57 +++++++++++------
 src/Captcha/CaptchaService.cs       | 120 ++++++++++++++++++++++++++++++++
 src/Database/DbContext.cs           | 6 +--
 src/Reports/UserReport.cs           | 230 ++++++++++++++++++++++++++++++++
 src/OldModule/Legacy.cs             | 85 -------------------
 5 files changed, 398 insertions(+), 100 deletions(-)

--- Ozet ---
Toplam commit: 3
5 files changed, 398 insertions(+), 100 deletions(-)

================================================================
```

---

## Tracking Dosyasi

`.github-pull-tracking.json` her repo icin son senkronizasyon bilgisini tutar:

```json
{
  "sirket-org/proje-a": {
    "Commit": "def5678abc123456789...",
    "Date": "2026-03-30_10-00-05",
    "UpdatedAt": "2026-03-30 10:00:05"
  },
  "sirket-org/proje-b": {
    "Commit": "aaa1111bbb222333...",
    "Date": "2026-03-30_10-01-12",
    "UpdatedAt": "2026-03-30 10:01:12"
  }
}
```

Bu dosya sayesinde servis sadece degisiklik olan repolari gunceller.
Dosyayi silmeniz durumunda tum repolar tekrar sifirdan klonlanir.

---

## Log Dosyalari

Log dosyalari exe'nin bulundugu klasorde `Log\AppLog\` altinda olusur:

| Dosya | Icerik |
|-------|--------|
| `2026-03-30_All.txt` | Tum loglar |
| `2026-03-30_Errors.txt` | Sadece WARN ve ERROR |
| `2026-03-30_Service.txt` | Service yasam dongusu (baslama, durma, zamanlama) |
| `2026-03-30_GitHub.txt` | GitHub API islemleri (repo listeleme, rate limit) |
| `2026-03-30_Sync.txt` | Dosya kopyalama / ZIP arsivleme islemleri |
| `2026-03-30_Git.txt` | Git clone / fetch / reset islemleri |

Log formati:
```
2026-03-30 10:00:05.123 | INFO  | T04 | Service  | [GitVaultService] === Senkronizasyon basladi ===
2026-03-30 10:00:06.456 | INFO  | T04 | GitHub   | [GitHubApi] Toplam 15 repo bulundu (ignore harici)
2026-03-30 10:00:12.789 | INFO  | T04 | Sync     | [RepoSync] Mevcut versiyon arsivleniyor: 2026-03-29_14-30-00_abc1234.zip
2026-03-30 10:00:18.321 | INFO  | T04 | Sync     | [RepoSync] Latest guncelleniyor: sirket-org/proje-a -> def5678
2026-03-30 10:00:25.654 | INFO  | T04 | Sync     | [RepoSync] CHANGELOG guncellendi: sirket-org/proje-a
```

Hata durumunda:
```
2026-03-30 10:00:30.111 | ERROR | T04 | Git      | [RepoSync] Git fetch basarisiz: sirket-org/proje-x - fatal: repository not found
2026-03-30 10:00:30.222 | ERROR | T04 | Service  | [GitVaultService] Repo senkronizasyonu basarisiz: sirket-org/proje-x | Exception: ...
```

---

## Sik Sorulan Sorular

**Token olmadan calisir mi?**
Evet, ancak sadece public repolar icin. API limiti saatte 60 istekle sinirli kalir.
Token ile saatte 5000 istek yapilabilir.

**Ignore listesi nasil calisir?**
Repo adi buyuk/kucuk harf duyarsizdir. `Test-Repo` yazarsaniz `test-repo`, `TEST-REPO` gibi
varyasyonlar da ignore edilir. Ignore listesi hem organizasyon hem kullanici repolari icin gecerlidir.

**Disk alani ne kadar gerekir?**
Ilk senkronizasyonda tum repolarin `latest` kopyasi olusturulur. Sonraki guncellemelerde
eski versiyonlar ZIP olarak arsivlenir (~%90 sikistirma). Ornegin 500 MB'lik bir proje ZIP'te ~50 MB yer kaplar.

**Tracking dosyasini silersem ne olur?**
Tum repolar sifirdan klonlanir ve yeni `latest` kopyalari olusturulur.
Mevcut arsivler etkilenmez.

**Service durduktan sonra kaldigi yerden devam eder mi?**
Evet. Tracking dosyasi sayesinde servis her zaman son commit'ten itibaren kontrol eder.

**Ayni anda hem organizasyon hem kullanici takip edebilir miyim?**
Evet. `GitHub:Organizations` ve `GitHub:Users` alanlarini ayni anda doldurabilirsiniz.
Ikisinin repolari ayri klasorlerde tutulur.

---

## Teknik Detaylar

- **Framework:** .NET Framework 4.8
- **Dil:** C# 9
- **Proje Tipi:** Windows Service (ServiceBase)
- **GitHub API:** Octokit kutuphanesi
- **Loglama:** Dosya tabanli, kategorili, thread-safe
- **Zamanlama:** CancellationToken.WaitHandle ile hassas bekleme
- **Kurulum:** InstallUtil + ProjectInstaller sinifi
- **Hesap:** LocalSystem (varsayilan)
