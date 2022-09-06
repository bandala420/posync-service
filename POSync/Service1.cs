// POSync service primary functions
using System.ServiceProcess;
using System.Configuration;
using System.Threading.Tasks;

namespace POSync
{
    public partial class Service1 : ServiceBase
    {
        public static readonly string deviceType = ConfigurationManager.AppSettings["Device"].ToLower().Trim();
        public Service1()
        {
            InitializeComponent();
        }
        protected override void OnStart(string[] args)
        {
            Task.Factory.StartNew(() =>
            {
                CustomLog.Start();
                switch (deviceType)
                {
                    case "pos":
                        FileWatcher.Start(); break;
                    case "itona":
                        Itona.Start(); break;
                    case "pc_gerentes":
                        FileStorage.Start(); break;
                    case "soft_server":
                        SoftSync.Start(); break;
                    default:
                        CustomLog.CustomLogEvent(string.Format("Invalid device type: {0}", deviceType)); break;
                }
            });  
        }
        protected override void OnStop()
        {
            switch (deviceType)
            {
                case "pos":
                    FileWatcher.Stop(); break;
                case "itona":
                    Itona.Stop(); break;
                case "pc_gerentes":
                    FileStorage.Stop(); break;
                case "soft_server":
                    SoftSync.Stop(); break;
                default:
                    CustomLog.CustomLogEvent(string.Format("Invalid device type: {0}", deviceType)); break;
            }
            CustomLog.Stop();
        }
        protected override bool OnPowerEvent(PowerBroadcastStatus powerStatus)
        {
            if ((int)powerStatus == 4)
            {
                switch (deviceType)
                {
                    case "pos":
                        FileWatcher.Pause(); break;
                    case "pc_gerentes":
                        FileStorage.Pause(); break;
                    case "soft_server":
                        SoftSync.Pause(); break;
                    default:
                        CustomLog.CustomLogEvent(string.Format("Invalid device type: {0}", deviceType)); break;
                }
            }
            if ((int)powerStatus == 7)
            {
                switch (deviceType)
                {
                    case "pos":
                        FileWatcher.ManualSync(false); break;
                    case "pc_gerentes":
                        FileStorage.ManualSync(); break;
                    case "soft_server":
                        SoftSync.ManualSync(); break;
                    default:
                        CustomLog.CustomLogEvent(string.Format("Invalid device type: {0}", deviceType)); break;
                }
            }
            return base.OnPowerEvent(powerStatus);
        }
    }
}
