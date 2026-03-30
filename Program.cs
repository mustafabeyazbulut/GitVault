using System;
using System.ServiceProcess;
using System.Threading.Tasks;
using GitVault.Helpers;

namespace GitVault
{
    internal static class Program
    {
        static void Main(string[] args)
        {
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
    }
}
