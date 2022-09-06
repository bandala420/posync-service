using System.ComponentModel;
using System.Configuration.Install;

namespace POSync
{
    [RunInstaller(true)]
    public partial class ProjectInstaller : Installer
    {
        public ProjectInstaller():base()
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
            if (!AppInstaller.WriteConfig(this.Context.Parameters["assemblypath"]))
                throw new InstallException("ALERTA: Ha ocurrido algún error al escribir en el archivo de configuración, revise los permisos de usuario y vuelva a intentarlo. \nSi el problema persiste solicite asistencia técnica.");
            AppInstaller.RunConfigWindow(this.Context.Parameters["assemblypath"]);
            if (AppInstaller.Successs)
            {
                AppInstaller.DeleteTmpFiles(AppInstaller.AssemblyDirectory,"*.tmp", ".tmp");
                AppInstaller.StartService(serviceInstaller1.ServiceName);
            }
            else
                throw new InstallException("Se ha detenido la instalación");
        }
        private void serviceInstaller1_BeforeUninstall(object sender, InstallEventArgs e)
        {
            base.OnBeforeUninstall(e.SavedState);
            AppInstaller.UninstallAutorunner();
            AppInstaller.DeleteTmpFiles(AppInstaller.AssemblyDirectory + @"\logs\", "*.txt", ".txt");
            AppInstaller.DeleteTmpFiles(AppInstaller.AssemblyDirectory + @"\logs\", "*.gz", ".gz");
            AppInstaller.DeleteTmpFiles(AppInstaller.AssemblyDirectory + @"\logs\", "*.zip", ".zip");
            AppInstaller.DeleteTmpFiles(AppInstaller.AssemblyDirectory + @"\logs\", "*.csv", ".csv");
            AppInstaller.DeleteTmpFiles(AppInstaller.AssemblyDirectory + @"\logs\", "*.bop", ".bop");
            AppInstaller.DeleteTmpFiles(AppInstaller.AssemblyDirectory + @"\logs\", "*.filepart", ".filepart");
            AppInstaller.DeleteTmpFiles(AppInstaller.AssemblyDirectory + @"\version\", "*.exe", ".exe");
            AppInstaller.DeleteTmpFiles(AppInstaller.AssemblyDirectory + @"\version\", "*.zip", ".zip");
            AppInstaller.DeleteTmpFiles(AppInstaller.AssemblyDirectory + @"\version\", "*.log", ".log");
            AppInstaller.DeleteTmpFiles(AppInstaller.AssemblyDirectory + @"\version\", "*.filepart", ".filepart");
            AppInstaller.StopService(serviceInstaller1.ServiceName);
        }
    }
}
