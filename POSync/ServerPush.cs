using System;
using System.Collections.Generic;
using System.IO;
using System.Configuration;
using System.Threading.Tasks;
using System.Linq;

namespace POSync
{
    static class ServerPush
    {
        /// <summary>
        /// Push process function
        /// </summary>
        public static void ServerPushProcess()
        {
            Task.Factory.StartNew(() =>
            {
                // Check if there is any server petition and attends it
                lock (Settings._threadLock)
                {
                    // Try to download server file for push process
                    string restFolder = ConfigurationManager.AppSettings["RestFolder"];
                    string serverPetitionFile = AppDomain.CurrentDomain.BaseDirectory + ConfigurationManager.AppSettings["LogsPath"] + "ServerPetition.txt";
                    string[] coorpAux = Path.GetFileNameWithoutExtension(AppInstaller.GetSshPrivateKeyFile()).Split('_');
                    string coorp = coorpAux[coorpAux.Length - 1];
                    //if (Settings.sshMode)
                    //    FileTransfer.DownloadFiles(new string[] { Settings.serviceSettings.RemoteRoot + restFolder + string.Format(@"/synclogs/config/{0}", Path.GetFileName(ConfigurationManager.AppSettings["ServerPetitionPath"])) }, Path. GetFullPath(serverPetitionFile));
                    try
                    {
                        using (MyWebClient client = new MyWebClient())
                            client.DownloadFile(string.Format(@"http://oceanodigital.mx/posync/config/{0}/{1}/{2}", coorp, restFolder, Path.GetFileName(ConfigurationManager.AppSettings["ServerPetitionPath"])), serverPetitionFile);
                    }
                    catch { }
                    // return if no new server petition
                    if (!File.Exists(serverPetitionFile)) { return; }
                    // Last attended petition file path
                    string lastPetitionPath = AppDomain.CurrentDomain.BaseDirectory + ConfigurationManager.AppSettings["ServerPetitionPath"];
                    try
                    {
                        // Get information of last attended petition
                        string lastPetitionInfo = File.ReadLines(lastPetitionPath).First();
                        string[] lastPetitionParams = lastPetitionInfo.Split('|');
                        // Get information of actual petition
                        string petitionInfo = File.ReadLines(serverPetitionFile).First();
                        string[] petitionParams = petitionInfo.Split('|');
                        // Replace local server petition file with new one
                        File.Delete(lastPetitionPath);
                        File.Move(serverPetitionFile, lastPetitionPath);
                        // Compare petitions datetime
                        if (!string.Equals(lastPetitionParams[0], petitionParams[0]) && petitionParams.Length > 1)
                        {
                            CustomLog.CustomLogEvent("Attending server petition...");
                            List<string> filesToDownload = new List<string>();
                            string driveLetter = AppInstaller.FindDirectory();
                            string localPosDataPath = string.Format(Settings.serviceSettings.LocalPosDataPath, driveLetter);
                            for (int i = 1; i < petitionParams.Length; i++)
                            {
                                string petitionCode = petitionParams[i].ToLower();
                                CustomLog.CustomLogEvent(string.Format("Operation {0}", petitionParams[i]));
                                if (petitionCode.Contains("sync."))
                                {
                                    if (Service1.deviceType.Contains("pos") && FileWatcher.listFoldersActive != null)
                                    {
                                        string dateCode = petitionParams[i].Split('.')[1];
                                        int attempts = Settings.serviceSettings.AttemptsSession;
                                        for (int j = 0; !FileTransfer.ManualSync(FileWatcher.listFoldersActive, dateCode) && j <= attempts; j++) { System.Threading.Thread.Sleep((int)TimeSpan.FromSeconds(30).TotalMilliseconds); }
                                    }
                                }
                                else if (petitionCode.Contains("update."))
                                {
                                    string updatePackage = petitionParams[i].Split('.')[1].ToLower();
                                    if (updatePackage.Contains("newpos"))
                                    {
                                        if (!Service1.deviceType.Contains("itona"))
                                            AppInstaller.UpdateNewpos();
                                        else
                                            Itona.UpdateNewpos(true);
                                    }
                                    else if (updatePackage.Contains("posync"))
                                    {
                                        Updater.UpdateService();
                                        if (Service1.deviceType.Contains("pc_gerentes"))
                                            FileStorage.UpdateSettings();
                                        else if (Service1.deviceType.Contains("pos"))
                                            FileWatcher.UpdateSettings();
                                        else if (Service1.deviceType.Contains("itona"))
                                            Itona.UpdateSettings();
                                    }
                                }
                                else if (petitionCode.Contains("query."))
                                {
                                    if (Service1.deviceType.Contains("pc_gerentes"))
                                    {
                                        string daysBeforeString = petitionParams[i].Split('.')[1];
                                        if (Int32.TryParse(daysBeforeString, out int daysBefore)) { ServerQuery.UploadTables(daysBefore); }
                                    }
                                }
                                else if (petitionCode.Contains("synchronization"))
                                {
                                    if (Service1.deviceType.Contains("pc_gerentes"))
                                    {
                                        for (int j = 0; !FileStorage.ManualSync() && j <= Settings.serviceSettings.AttemptsSession; j++)
                                            System.Threading.Thread.Sleep((int)TimeSpan.FromSeconds(30).TotalMilliseconds);
                                    }
                                    else if (Service1.deviceType.Contains("pos"))
                                    {
                                        for (int j = 0; !FileWatcher.ManualSync(true) && j <= Settings.serviceSettings.AttemptsSession; j++)
                                            System.Threading.Thread.Sleep((int)TimeSpan.FromSeconds(30).TotalMilliseconds);
                                    }
                                }
                                else if (petitionCode.Contains("autorun"))
                                {
                                    if (Service1.deviceType.Contains("pos") || Service1.deviceType.Contains("pc_gerentes"))
                                        AppInstaller.InstallAutorunner(true);
                                }
                                else if (petitionCode.Contains("restart"))
                                {
                                    if (Service1.deviceType.Contains("pos"))
                                        AppInstaller.RestartDevice();
                                }
                                else if (!string.IsNullOrWhiteSpace(petitionParams[i]) && !Service1.deviceType.Contains("itona"))
                                    filesToDownload.Add(Settings.serviceSettings.RemoteRoot + restFolder + @"/newpos/posdata/" + petitionParams[i]);
                            }
                            if (filesToDownload.Count > 0)
                            {
                                for (int i = 0; !FileTransfer.DownloadFiles(filesToDownload.ToArray(), localPosDataPath) && i <= Settings.serviceSettings.AttemptsSession; i++)
                                    System.Threading.Thread.Sleep((int)TimeSpan.FromSeconds(30).TotalMilliseconds);
                            }
                        }
                    }
                    catch (IOException exc)
                    {
                        CustomLog.CustomLogEvent("Error attending server petition: " + exc.Message);
                        CustomLog.Error();
                    }
                }
            });
        }
        /// <summary>
        /// Push process function for national soft point of sale
        /// </summary>
        public static void SoftServerPushProcess()
        {
            Task.Factory.StartNew(() =>
            {
                // Check if there is any server petition and attends it
                lock (Settings._threadLock)
                {
                    // Try to download server file for push process
                    string restFolder = ConfigurationManager.AppSettings["RestFolder"];
                    string serverPetitionFile = AppDomain.CurrentDomain.BaseDirectory + ConfigurationManager.AppSettings["LogsPath"] + "ServerPetition.txt";
                    string[] coorpAux = Path.GetFileNameWithoutExtension(AppInstaller.GetSshPrivateKeyFile()).Split('_');
                    string coorp = coorpAux[coorpAux.Length - 1];
                    if (Settings.sshMode)
                        FileTransfer.DownloadFiles(new string[] { Settings.serviceSettings.RemoteRoot + restFolder + string.Format(@"/synclogs/config/{0}", Path.GetFileName(ConfigurationManager.AppSettings["ServerPetitionPath"])) }, Path.GetFullPath(serverPetitionFile));
                    else
                    {
                        try
                        {
                            using (MyWebClient client = new MyWebClient())
                                client.DownloadFile(string.Format(@"http://oceanodigital.mx/posync/config/{0}/{1}/{2}", coorp, restFolder, Path.GetFileName(ConfigurationManager.AppSettings["ServerPetitionPath"])), serverPetitionFile);
                        }
                        catch { }
                    }
                    if (File.Exists(serverPetitionFile))
                    {
                        // Last attended petition file path
                        string lastPetitionPath = AppDomain.CurrentDomain.BaseDirectory + ConfigurationManager.AppSettings["ServerPetitionPath"];
                        try
                        {
                            // Get information of last attended petition
                            string lastPetitionInfo = File.ReadLines(lastPetitionPath).First();
                            string[] lastPetitionParams = lastPetitionInfo.Split('|');
                            // Get information of actual petition
                            string petitionInfo = File.ReadLines(serverPetitionFile).First();
                            string[] petitionParams = petitionInfo.Split('|');
                            // Compare petitions datetime
                            if (!string.Equals(lastPetitionParams[0], petitionParams[0]) && petitionParams.Length > 1)
                            {
                                CustomLog.CustomLogEvent("Attending server petition...");
                                List<string> filesToDownload = new List<string>();
                                string driveLetter = AppInstaller.FindDirectory();
                                string localPosDataPath = string.Format(Settings.serviceSettings.LocalPosDataPath, driveLetter);
                                for (int i = 1; i < petitionParams.Length; i++)
                                {
                                    CustomLog.CustomLogEvent(string.Format("Operation {0}", petitionParams[i]));
                                    if (petitionParams[i].ToLower().Contains("sync."))
                                        SoftSync.ManualSync();
                                    else if (petitionParams[i].ToLower().Contains("update."))
                                    {
                                        string updatePackage = petitionParams[i].Split('.')[1];
                                        if (updatePackage.ToLower().Contains("posync"))
                                        {
                                            Updater.UpdateService();
                                            SoftSync.UpdateSettings();
                                        }
                                    }
                                    else if (petitionParams[i].ToLower().Contains("query."))
                                    {
                                        string daysBeforeString = petitionParams[i].Split('.')[1];
                                        if (Int32.TryParse(daysBeforeString, out int daysBefore)) { ServerQuery.UploadTables(daysBefore); }
                                    }
                                    else if (petitionParams[i].ToLower().Contains("synchronization"))
                                        SoftSync.ManualSync();
                                    else if (!string.IsNullOrWhiteSpace(petitionParams[i]))
                                        filesToDownload.Add(Settings.serviceSettings.RemoteRoot + restFolder + @"/natsoft/posdata/" + petitionParams[i]);
                                }
                                if (filesToDownload.Count > 0)
                                {
                                    for (int i = 0; !FileTransfer.DownloadFiles(filesToDownload.ToArray(), localPosDataPath) && i <= Settings.serviceSettings.AttemptsSession; i++)
                                        System.Threading.Thread.Sleep((int)TimeSpan.FromSeconds(30).TotalMilliseconds);
                                }
                            }
                        }
                        catch (IOException exc)
                        {
                            CustomLog.CustomLogEvent("Error attending server petition: " + exc.Message);
                            CustomLog.Error();
                        }
                        finally
                        {
                            File.Delete(lastPetitionPath);
                            File.Move(serverPetitionFile, lastPetitionPath);
                        }
                    }
                }
            });
        }
    }
}
