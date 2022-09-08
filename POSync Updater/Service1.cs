//POSync updater and health checker functions
using System.ServiceProcess;
using System.Threading.Tasks;

namespace POSync_Updater
{
    public partial class Service1 : ServiceBase
    {
        public Service1()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            Task.Factory.StartNew(() =>
            {
                AppUpdater.Start();
            });
        }

        protected override void OnStop()
        {
            AppUpdater.Stop();
        }
    }
}
