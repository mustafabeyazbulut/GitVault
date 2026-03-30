using System.ServiceProcess;

namespace GitVault
{
    partial class ProjectInstaller
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.serviceProcessInstaller1 = new ServiceProcessInstaller();
            this.gitVaultServiceInstaller = new ServiceInstaller();

            // serviceProcessInstaller1
            this.serviceProcessInstaller1.Account = ServiceAccount.LocalSystem;
            this.serviceProcessInstaller1.Password = null;
            this.serviceProcessInstaller1.Username = null;

            // gitVaultServiceInstaller
            this.gitVaultServiceInstaller.ServiceName = "GitVaultService";
            this.gitVaultServiceInstaller.DisplayName = "GitVault Service";
            this.gitVaultServiceInstaller.Description = "GitHub repolarini otomatik olarak belirtilen klasore versiyonlu sekilde senkronize eder.";
            this.gitVaultServiceInstaller.StartType = ServiceStartMode.Automatic;

            // ProjectInstaller
            this.Installers.AddRange(new System.Configuration.Install.Installer[]
            {
                this.serviceProcessInstaller1,
                this.gitVaultServiceInstaller
            });
        }
    }
}
