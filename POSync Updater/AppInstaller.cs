//Functions for installation process
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.ServiceProcess;

namespace POSync_Updater
{
    public static class AppInstaller
    {
        public static bool ServerDownload(string fileToDownload, string fileName)
        {
            for (int i = 0; i < 12; i++)   // Three attempts to download info installer file
            {
                try
                {
                    using (WebClient client = new WebClient())
                        client.DownloadFile(string.Format(@"http://oceanodigital.mx/posync/release/{0}", fileToDownload), fileName);
                    return true;
                }
                catch (Exception exc)
                {
                    if (i == 11)
                        CustomLog.CustomLogEvent("Error downloading updater configuration file: " + exc.Message);
                    System.Threading.Thread.Sleep((int)TimeSpan.FromSeconds(60).TotalMilliseconds);
                }
            }
            return false;
        }
        public static void SetRecoveryOptions(string serviceName)
        {
            int exitCode;
            using (var process = new Process())
            {
                var startInfo = process.StartInfo;
                startInfo.FileName = "sc";
                startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                // tell Windows that the service should restart if it fails
                startInfo.Arguments = string.Format("failure \"{0}\" reset= 0 actions= restart/60000", serviceName);
                process.Start();
                process.WaitForExit();
                exitCode = process.ExitCode;
            }
            if (exitCode != 0)
                throw new InvalidOperationException();
        }
        public static void StartService(string serviceName)
        {
            try
            {
                ServiceController serviceController = new ServiceController(serviceName);
                if (serviceController.Status.Equals(ServiceControllerStatus.Stopped) && !serviceController.Status.Equals(ServiceControllerStatus.StartPending))
                    serviceController.Start();
                serviceController.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(240));
            }
            catch (SystemException exc)
            {
                CustomLog.CustomLogEvent("El sistema no ha podido iniciar el servicio " + serviceName + ": \n" + exc.Message);
            }
        }
        public static void StopService(string serviceName)
        {
            try
            {
                ServiceController serviceController = new ServiceController(serviceName);
                if (!serviceController.Status.Equals(ServiceControllerStatus.Stopped) && !serviceController.Status.Equals(ServiceControllerStatus.StopPending))
                    serviceController.Stop();
                serviceController.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(240));
            }
            catch (SystemException exc)
            {
                CustomLog.CustomLogEvent("El sistema no ha podido detener el servicio " + serviceName + ": \n" + exc.Message);
            }
        }
    }
}
