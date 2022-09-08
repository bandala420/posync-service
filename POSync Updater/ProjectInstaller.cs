using System.ComponentModel;
using System.Configuration.Install;

namespace POSync_Updater
{
    [RunInstaller(true)]
    public partial class ProjectInstaller : Installer
    {
        public ProjectInstaller() : base()
        {
            InitializeComponent();
        }
        private void serviceInstaller1_Committed(object sender, InstallEventArgs e)
        {
            base.OnCommitted(e.SavedState);
            AppInstaller.SetRecoveryOptions(serviceInstaller1.ServiceName);
        }
        private void serviceInstaller1_AfterInstall(object sender, InstallEventArgs e)
        {
            base.OnAfterInstall(e.SavedState);
            AppInstaller.StartService(serviceInstaller1.ServiceName);
        }
        private void serviceInstaller1_BeforeUninstall(object sender, InstallEventArgs e)
        {
            base.OnBeforeUninstall(e.SavedState);
            AppInstaller.StopService(serviceInstaller1.ServiceName);
        }
    }
}
