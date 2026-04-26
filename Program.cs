using System;
using System.Net;
using System.ServiceProcess;
using System.Threading.Tasks;
using GitVault.Helpers;

namespace GitVault
{
    internal static class Program
    {
        static void Main(string[] args)
        {
            ConfigureNetworking();

#if DEBUG
            Console.WriteLine("=======================================");
            Console.WriteLine(" GitVault Service - DEBUG Mode");
            Console.WriteLine("=======================================");
            Console.WriteLine();
            Console.WriteLine("Secim yapin:");
            Console.WriteLine("1. Senkronizasyonu baslat");
            Console.WriteLine("2. Cikis");
            Console.Write("\nSeciminiz: ");

            var input = Console.ReadLine();

            if (input == "1")
            {
                Console.WriteLine("\nSenkronizasyon baslatiliyor...\n");
                var service = new GitVaultService();
                service.OnDebug();
                Console.WriteLine("\nSenkronizasyon tamamlandi. Cikmak icin bir tusa basin...");
                Console.ReadKey();
            }
#else
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]
            {
                new GitVaultService()
            };

            LogHelpers.Info("Service baslatiliyor (Release modu)", LogCategory.Service, "Program");
            ServiceBase.Run(ServicesToRun);
#endif
        }

        private static void ConfigureNetworking()
        {
            // GitHub TLS 1.2+ zorunlu kiliyor. .NET Framework 4.8 'SystemDefault' icin
            // makine registry'sine bagimli kaliyor — explicit zorla.
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            // Servis aralikli sync'ler arasi uyuyor; bu sirada DNS cache ve TCP baglanti
            // havuzu bayatliyor. NAT/firewall idle baglantiyi sessizce dusurunce ilk
            // istek "An error occurred while sending the request" ile patliyor.
            // 60 sn'de bir DNS'i yenile, 60 sn'de bir TCP baglantiyi yeniden kur.
            ServicePointManager.DnsRefreshTimeout = 60_000;
            ServicePointManager.FindServicePoint(new Uri("https://api.github.com"))
                .ConnectionLeaseTimeout = 60_000;
        }
    }
}
