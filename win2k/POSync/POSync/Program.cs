//POSync
//GRUPO MONSERRAT SA DE CV
//Windows service - Point of sale synchronizer
using System.ServiceProcess;

namespace POSync
{
    static class Program
    {
        public static ServiceBase POSyncService;
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            POSyncService = new Service1
            {
                CanHandlePowerEvent = true
            };
            ServiceBase.Run(POSyncService);
        }
    }
}
