using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Mail;
using System.Text;
using GitVault.Helpers;
using GitVault.Models;

namespace GitVault.Services
{
    public static class EmailService
    {
        private const string SRC = "EmailService";

        public static void SendSyncReport(
            List<string> updatedRepos,
            List<string> noChangeRepos,
            List<string> errorRepos)
        {
            if (string.IsNullOrWhiteSpace(AppSettings.EmailTo)
                || string.IsNullOrWhiteSpace(AppSettings.EmailSmtpHost)
                || string.IsNullOrWhiteSpace(AppSettings.EmailSmtpUser)
                || string.IsNullOrWhiteSpace(AppSettings.EmailSmtpPassword))
            {
                LogHelpers.Debug("Email ayarlari eksik (Email:To, SmtpHost, SmtpUser, SmtpPassword gerekli), mail atlanacak.", LogCategory.Service, SRC);
                return;
            }

            if (updatedRepos.Count == 0 && errorRepos.Count == 0)
            {
                LogHelpers.Debug("Degisiklik veya hata yok, mail atlanacak.", LogCategory.Service, SRC);
                return;
            }

            try
            {
                var subject = BuildSubject(updatedRepos.Count, errorRepos.Count);
                var body    = BuildHtmlBody(updatedRepos, noChangeRepos, errorRepos);

                using (var client = new SmtpClient(AppSettings.EmailSmtpHost, AppSettings.EmailSmtpPort))
                {
                    client.EnableSsl    = AppSettings.EmailSmtpSsl;
                    client.Credentials  = new NetworkCredential(AppSettings.EmailSmtpUser, AppSettings.EmailSmtpPassword);
                    client.Timeout      = 30000;

                    var mail = new MailMessage
                    {
                        From            = new MailAddress(AppSettings.EmailSmtpUser, "GitVault"),
                        Subject         = subject,
                        Body            = body,
                        IsBodyHtml      = true,
                        BodyEncoding    = Encoding.UTF8,
                        SubjectEncoding = Encoding.UTF8
                    };

                    foreach (var to in AppSettings.EmailTo.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var addr = to.Trim();
                        if (!string.IsNullOrEmpty(addr))
                            mail.To.Add(addr);
                    }

                    client.Send(mail);
                    LogHelpers.Info($"Senkronizasyon raporu maili gonderildi: {AppSettings.EmailTo}", LogCategory.Service, SRC);
                }
            }
            catch (Exception ex)
            {
                LogHelpers.Error("Mail gonderilemedi", ex, LogCategory.Service, SRC);
            }
        }

        private static string HtmlEncode(string value)
        {
            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
        }

        private static string BuildSubject(int updatedCount, int errorCount)
        {
            var date = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            if (errorCount > 0)
                return $"[GitVault] {date} — {updatedCount} repo yedeklendi, {errorCount} HATA";
            if (updatedCount > 0)
                return $"[GitVault] {date} — {updatedCount} repo yedeklendi";
            return $"[GitVault] {date} — Degisiklik yok";
        }

        private static string BuildHtmlBody(
            List<string> updatedRepos,
            List<string> noChangeRepos,
            List<string> errorRepos)
        {
            var date     = DateTime.Now.ToString("dd MMMM yyyy, HH:mm");
            var dest     = HtmlEncode(AppSettings.DestinationPath);

            // Durum rengi: hata varsa kirmizi, guncelleme varsa yesil, yoksa gri
            string statusColor, statusText;
            if (errorRepos.Count > 0)
            {
                statusColor = "#e53935";
                statusText  = "Hatalar Mevcut";
            }
            else if (updatedRepos.Count > 0)
            {
                statusColor = "#2e7d32";
                statusText  = "Basariyla Tamamlandi";
            }
            else
            {
                statusColor = "#546e7a";
                statusText  = "Degisiklik Yok";
            }

            var sb = new StringBuilder();

            sb.Append(@"<!DOCTYPE html>
<html lang=""tr"">
<head>
<meta charset=""UTF-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
</head>
<body style=""margin:0;padding:0;background:#f4f6f8;font-family:'Segoe UI',Arial,sans-serif;"">

<table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background:#f4f6f8;padding:32px 0;"">
<tr><td align=""center"">
<table width=""620"" cellpadding=""0"" cellspacing=""0"" style=""background:#ffffff;border-radius:8px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,0.08);"">

  <!-- HEADER -->
  <tr>
    <td style=""background:#1a1a2e;padding:28px 36px;"">
      <table width=""100%"" cellpadding=""0"" cellspacing=""0"">
        <tr>
          <td>
            <span style=""font-size:22px;font-weight:700;color:#ffffff;letter-spacing:1px;"">&#x1F5C4; GitVault</span>
            <span style=""font-size:13px;color:#90caf9;margin-left:10px;"">Yedekleme Servisi</span>
          </td>
          <td align=""right"">
            <span style=""display:inline-block;background:").Append(statusColor).Append(@";color:#fff;font-size:12px;font-weight:600;padding:5px 14px;border-radius:20px;"">").Append(statusText).Append(@"</span>
          </td>
        </tr>
      </table>
    </td>
  </tr>

  <!-- TARIH -->
  <tr>
    <td style=""background:#e8eaf6;padding:10px 36px;"">
      <span style=""font-size:12px;color:#5c6bc0;"">&#x1F550; ").Append(date).Append(@"&nbsp;&nbsp;&nbsp;&#x1F4C2; ").Append(dest).Append(@"</span>
    </td>
  </tr>

  <!-- OZET KARTLAR -->
  <tr>
    <td style=""padding:28px 36px 8px;"">
      <table width=""100%"" cellpadding=""0"" cellspacing=""0"">
        <tr>
          <td width=""31%"" align=""center"" style=""background:#e8f5e9;border-radius:8px;padding:18px 8px;"">
            <div style=""font-size:32px;font-weight:700;color:#2e7d32;"">").Append(updatedRepos.Count).Append(@"</div>
            <div style=""font-size:12px;color:#388e3c;margin-top:4px;"">Yedeklendi</div>
          </td>
          <td width=""4%""></td>
          <td width=""31%"" align=""center"" style=""background:#fff3e0;border-radius:8px;padding:18px 8px;"">
            <div style=""font-size:32px;font-weight:700;color:#e65100;"">").Append(errorRepos.Count).Append(@"</div>
            <div style=""font-size:12px;color:#ef6c00;margin-top:4px;"">Hata</div>
          </td>
          <td width=""4%""></td>
          <td width=""31%"" align=""center"" style=""background:#eceff1;border-radius:8px;padding:18px 8px;"">
            <div style=""font-size:32px;font-weight:700;color:#455a64;"">").Append(noChangeRepos.Count).Append(@"</div>
            <div style=""font-size:12px;color:#546e7a;margin-top:4px;"">Degismeyen</div>
          </td>
        </tr>
      </table>
    </td>
  </tr>

");

            // YEDEKLENEN REPOLAR
            if (updatedRepos.Count > 0)
            {
                sb.Append(@"  <tr>
    <td style=""padding:24px 36px 0;"">
      <div style=""font-size:13px;font-weight:700;color:#2e7d32;text-transform:uppercase;letter-spacing:0.5px;margin-bottom:10px;"">
        &#x2705; Yedeklenen Repolar
      </div>
      <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""border:1px solid #c8e6c9;border-radius:6px;overflow:hidden;"">
");
                var rowBg = true;
                foreach (var repo in updatedRepos)
                {
                    var bg = rowBg ? "#f1f8f1" : "#ffffff";
                    sb.Append($@"        <tr>
          <td style=""padding:9px 14px;font-size:13px;color:#1b5e20;background:{bg};"">
            <span style=""color:#4caf50;margin-right:8px;"">&#x25CF;</span>{HtmlEncode(repo)}
          </td>
        </tr>
");
                    rowBg = !rowBg;
                }
                sb.Append(@"      </table>
    </td>
  </tr>
");
            }

            // HATALI REPOLAR
            if (errorRepos.Count > 0)
            {
                sb.Append(@"  <tr>
    <td style=""padding:24px 36px 0;"">
      <div style=""font-size:13px;font-weight:700;color:#c62828;text-transform:uppercase;letter-spacing:0.5px;margin-bottom:10px;"">
        &#x26A0; Hatali Repolar
      </div>
      <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""border:1px solid #ffcdd2;border-radius:6px;overflow:hidden;"">
");
                var rowBg = true;
                foreach (var repo in errorRepos)
                {
                    var bg = rowBg ? "#fff5f5" : "#ffffff";
                    sb.Append($@"        <tr>
          <td style=""padding:9px 14px;font-size:13px;color:#b71c1c;background:{bg};"">
            <span style=""color:#f44336;margin-right:8px;"">&#x25CF;</span>{HtmlEncode(repo)}
          </td>
        </tr>
");
                    rowBg = !rowBg;
                }
                sb.Append(@"      </table>
    </td>
  </tr>
");
            }


            // FOOTER
            sb.Append(@"
  <!-- FOOTER -->
  <tr>
    <td style=""padding:28px 36px 32px;"">
      <div style=""border-top:1px solid #eceff1;padding-top:20px;text-align:center;"">
        <span style=""font-size:11px;color:#b0bec5;"">Bu e-posta <strong>GitVault</strong> tarafindan otomatik olarak gonderilmistir.</span>
      </div>
    </td>
  </tr>

</table>
</td></tr>
</table>

</body>
</html>");

            return sb.ToString();
        }
    }
}
