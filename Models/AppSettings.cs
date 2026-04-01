using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;

namespace GitVault.Models
{
    public static class AppSettings
    {
        // GitHub Ayarlari
        public static string GitHubToken => ConfigurationManager.AppSettings["GitHub:Token"] ?? "";

        public static List<string> Organizations => ParseList(ConfigurationManager.AppSettings["GitHub:Organizations"]);

        public static List<string> Users => ParseList(ConfigurationManager.AppSettings["GitHub:Users"]);

        public static List<string> IgnoreRepos => ParseList(ConfigurationManager.AppSettings["GitHub:IgnoreRepos"]);

        // NAS Kimlik Bilgileri
        public static string NasUsername => ConfigurationManager.AppSettings["Nas:Username"] ?? "";
        public static string NasPassword => ConfigurationManager.AppSettings["Nas:Password"] ?? "";

        // Senkronizasyon Ayarlari
        public static string DestinationPath => ConfigurationManager.AppSettings["Sync:DestinationPath"] ?? @"\\atsb-nas\Yazilim";

        public static int CheckIntervalMinutes
        {
            get
            {
                var val = ConfigurationManager.AppSettings["Sync:CheckIntervalMinutes"];
                return int.TryParse(val, out var result) ? result : 30;
            }
        }

        public static int MaxVersionsToKeep
        {
            get
            {
                var val = ConfigurationManager.AppSettings["Sync:MaxVersionsToKeep"];
                return int.TryParse(val, out var result) ? result : 5;
            }
        }

        // Email Ayarlari
        public static string EmailTo         => ConfigurationManager.AppSettings["Email:To"] ?? "";
        public static string EmailSmtpHost   => ConfigurationManager.AppSettings["Email:SmtpHost"]   ?? "";
        public static int    EmailSmtpPort
        {
            get
            {
                var val = ConfigurationManager.AppSettings["Email:SmtpPort"];
                return int.TryParse(val, out var result) ? result : 587;
            }
        }
        public static string EmailSmtpUser     => ConfigurationManager.AppSettings["Email:SmtpUser"]     ?? "";
        public static string EmailSmtpPassword => ConfigurationManager.AppSettings["Email:SmtpPassword"] ?? "";
        public static bool   EmailSmtpSsl
        {
            get
            {
                var val = ConfigurationManager.AppSettings["Email:SmtpSsl"];
                return val == null || bool.TryParse(val, out var result) && result;
            }
        }

        private static List<string> ParseList(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return new List<string>();

            return value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim())
                        .Where(s => !string.IsNullOrEmpty(s))
                        .ToList();
        }
    }
}
