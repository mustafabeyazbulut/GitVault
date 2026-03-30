using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using GitVault.Models;

namespace GitVault.Helpers
{
    public static class NetworkShareHelper
    {
        private const string SRC = "NetworkShare";

        [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
        private static extern int WNetAddConnection2(ref NETRESOURCE netResource, string password, string username, int flags);

        [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
        private static extern int WNetCancelConnection2(string name, int flags, bool force);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NETRESOURCE
        {
            public int dwScope;
            public int dwType;
            public int dwDisplayType;
            public int dwUsage;
            public string lpLocalName;
            public string lpRemoteName;
            public string lpComment;
            public string lpProvider;
        }

        private const int RESOURCETYPE_DISK = 1;

        /// <summary>
        /// UNC yolundan paylasim kokunu cikarir. Ornek: \\server\share\folder -> \\server\share
        /// </summary>
        private static string GetShareRoot(string uncPath)
        {
            if (string.IsNullOrEmpty(uncPath) || !uncPath.StartsWith(@"\\"))
                return uncPath;

            var parts = uncPath.TrimStart('\\').Split('\\');
            if (parts.Length >= 2)
                return $@"\\{parts[0]}\{parts[1]}";

            return uncPath;
        }

        /// <summary>
        /// App.config'te NAS kimlik bilgileri varsa ag paylasimina baglanir.
        /// Kimlik bilgileri yoksa sessizce atlar (mevcut oturum yetkileri kullanilir).
        /// </summary>
        public static bool EnsureConnected()
        {
            // Once mevcut erisimi kontrol et
            if (CanAccessDestination())
            {
                LogHelpers.Debug("NAS paylasimina mevcut oturumla erisilebiliyor, ek baglanti gerekmiyor", LogCategory.Sync, SRC);
                return true;
            }

            var username = AppSettings.NasUsername;
            var password = AppSettings.NasPassword;

            if (string.IsNullOrWhiteSpace(username))
            {
                LogHelpers.Warn("NAS erisimi yok ve kimlik bilgileri tanimlanmamis", LogCategory.Sync, SRC);
                return false;
            }

            var shareRoot = GetShareRoot(AppSettings.DestinationPath);

            var netResource = new NETRESOURCE
            {
                dwType = RESOURCETYPE_DISK,
                lpRemoteName = shareRoot
            };

            LogHelpers.Info($"NAS paylasimina baglaniliyor: {shareRoot} (kullanici: {username})", LogCategory.Sync, SRC);

            int result = WNetAddConnection2(ref netResource, password, username, 0);

            if (result == 0)
            {
                LogHelpers.Info("NAS paylasimina basariyla baglandi", LogCategory.Sync, SRC);
                return true;
            }

            // 1219: Zaten baska bir oturumla bagli - erisim varsa sorun yok
            if (result == 1219)
            {
                LogHelpers.Debug("NAS paylasimina zaten farkli bir oturumla bagli, mevcut baglanti kullanilacak", LogCategory.Sync, SRC);
                return true;
            }

            var errorMessage = new Win32Exception(result).Message;
            LogHelpers.Error($"NAS paylasimina baglanilamadi (hata kodu: {result}): {errorMessage}", LogCategory.Sync, SRC);
            return false;
        }

        private static bool CanAccessDestination()
        {
            try
            {
                Directory.Exists(AppSettings.DestinationPath);
                var testPath = Path.Combine(AppSettings.DestinationPath, ".gitvault-net-test");
                File.WriteAllText(testPath, "test");
                File.Delete(testPath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// NAS paylasim baglantisinyi kapatir.
        /// </summary>
        public static void Disconnect()
        {
            if (string.IsNullOrWhiteSpace(AppSettings.NasUsername))
                return;

            var shareRoot = GetShareRoot(AppSettings.DestinationPath);
            WNetCancelConnection2(shareRoot, 0, false);
            LogHelpers.Debug($"NAS baglantisi kapatildi: {shareRoot}", LogCategory.Sync, SRC);
        }
    }
}
