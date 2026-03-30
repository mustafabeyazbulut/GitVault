using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;

namespace GitVault
{
    [RunInstaller(true)]
    public partial class ProjectInstaller : Installer
    {
        private ServiceProcessInstaller serviceProcessInstaller1;
        private ServiceInstaller gitVaultServiceInstaller;

        public ProjectInstaller()
        {
            InitializeComponent();
        }
    }
}
