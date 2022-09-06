using System;
using System.IO;
using System.Configuration;
using System.Linq;

namespace POSync
{
    static class Updater
    {
        /// <summary>
        /// Update system
        /// </summary>
        public static void UpdateService()
        {
            UpdateMainService();
            UpdateAuxiliarService();
        }
        /// <summary>
        /// Update synchronization service binary file
        /// </summary>
        private static void UpdateMainService()
        {
            string serviceVersionPath = AppDomain.CurrentDomain.BaseDirectory + ConfigurationManager.AppSettings["LogsPath"] + "ServiceVersion.txt";
            AppInstaller.ServerDownload(Path.GetFileName(serviceVersionPath), serviceVersionPath, false);
            if (File.Exists(serviceVersionPath))
            {
                string localPath = AppDomain.CurrentDomain.BaseDirectory + ConfigurationManager.AppSettings["VersionPath"];
                string localServiceVersionPath = localPath + "Service.txt";
                string localServiceVersion = File.ReadLines(localServiceVersionPath).First();
                string serverServiceVersionInfo = File.ReadLines(serviceVersionPath).First();
                string[] serverServiceVersion = serverServiceVersionInfo.Split('|');
                try
                {
                    // Compare versions and if needed runs update process
                    if (!string.Equals(localServiceVersion, serverServiceVersion[0]))
                    {
                        string[] filesToDownload = serverServiceVersion[1] == "1" ? new string[] { "WinSCP.exe", "WinSCPnet.dll", "POSync.exe" } : new string[] { "POSync.exe" };
                        if (!AppInstaller.DownloadBinary(filesToDownload, localPath))
                        {
                            foreach (string fileToDownload in filesToDownload)
                                File.Delete(localPath + fileToDownload);
                        }
                        else
                            CustomLog.CustomLogEvent("POSync update downloaded succesfully");
                        AppInstaller.InstallAutorunner(true);
                    }
                }
                catch (IOException exc)
                {
                    CustomLog.CustomLogEvent("Error updating system: " + exc.Message);
                    CustomLog.Error();
                }
                finally
                {
                    File.Delete(serviceVersionPath);
                }
            }
        }
        /// <summary>
        /// Update auxiliar background service
        /// </summary>
        private static void UpdateAuxiliarService()
        {
            // Check for release updater
            string updaterVersionPath = AppDomain.CurrentDomain.BaseDirectory + ConfigurationManager.AppSettings["LogsPath"] + "UpdaterVersion.txt";
            AppInstaller.ServerDownload(Path.GetFileName(updaterVersionPath), updaterVersionPath, false);
            if (File.Exists(updaterVersionPath))
            {
                string localPath = AppDomain.CurrentDomain.BaseDirectory + ConfigurationManager.AppSettings["VersionPath"];
                string localUpdaterVersionPath = localPath + "Updater.txt";
                try
                {
                    string localUpdaterVersion = File.ReadLines(localUpdaterVersionPath).First();
                    string serverUpdaterVersion = File.ReadLines(updaterVersionPath).First();
                    // Compare versions and if needed runs update process
                    if (!string.Equals(localUpdaterVersion, serverUpdaterVersion))
                    {
                        string serviceName = @"POSync Updater";
                        string binaryFile = serviceName + @".exe";
                        if (AppInstaller.DownloadBinary(new string[] { binaryFile }, localPath))
                        {
                            string serviceFile = AppDomain.CurrentDomain.BaseDirectory + binaryFile;
                            AppInstaller.StopService(serviceName);
                            File.Delete(serviceFile);
                            File.Move(localPath + binaryFile, serviceFile);
                            AppInstaller.StartService(serviceName);
                            CustomLog.CustomLogEvent(string.Format(@"<{0} service has been updated>", serviceName));
                        }
                    }
                }
                catch (IOException exc)
                {
                    CustomLog.CustomLogEvent("Error updating system: " + exc.Message);
                    CustomLog.Error();
                }
                finally
                {
                    File.Delete(updaterVersionPath);
                }
            }
        }
    }
}
