// File system storage class with all necessary parameters and
// methods to inizialize and manage the file storage
using System;
using System.IO;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Threading.Tasks;

namespace POSync
{
    static class Itona
    {
        private static List<System.Timers.Timer> listScheduledTimer;
        /// <summary>
        /// Start itona device timers
        /// </summary>
        public static void Start()
        {
            // Initialize the service configuration parameters
            Settings.PopulateServiceSettings();
            // Initialize sftp session
            FileTransfer.InitializeSession();
            // Start timers for update and daily sync tasks
            StartScheduledTimers();
            // Upload service status files
            UploadInformationFiles();
            // Update posdata files
            UpdateNewpos(false);
        }
        /// <summary>
        /// Stop timers
        /// </summary>
        public static void Stop()
        {
            if (listScheduledTimer != null)
            {
                foreach (System.Timers.Timer timer in listScheduledTimer)
                {
                    // Stop listening and dispose the Object
                    timer.Stop();
                    timer.Enabled = false;
                    timer.Dispose();
                }
            }
        }
        /// <summary>
        /// Initialize periodic timers
        /// </summary>
        private static void StartScheduledTimers()
        {
            listScheduledTimer = new List<System.Timers.Timer>();
            double intervalTime = (double)Settings.serviceSettings.UpdateHoursInterval;
            // Create a timer for update task
            System.Timers.Timer schTimer = new System.Timers.Timer(TimeSpan.FromHours(intervalTime).TotalMilliseconds);
            // Hook up the Elapsed event for the timer with a lambda expression 
            schTimer.Elapsed += (sourceObj, elapsedEventArgs) => UpdateSettings();
            schTimer.AutoReset = true;
            schTimer.Enabled = true;
            // Add to timers list
            listScheduledTimer.Add(schTimer);

            intervalTime = (double)Settings.serviceSettings.StatusCheckHoursInterval;
            // Create a timer for server petition
            System.Timers.Timer updtTimer = new System.Timers.Timer(TimeSpan.FromHours(intervalTime).TotalMilliseconds);
            // Hook up the Elapsed event for the timer with a lambda expression 
            updtTimer.Elapsed += (sourceObj, elapsedEventArgs) => PeriodicRevision();
            updtTimer.AutoReset = true;
            updtTimer.Enabled = true;
            // Add to timers list
            listScheduledTimer.Add(updtTimer);

            intervalTime = (double)Settings.serviceSettings.ServerPushMinutesInterval;
            // Create a timer for server push process
            System.Timers.Timer pushTimer = new System.Timers.Timer(TimeSpan.FromMinutes(intervalTime).TotalMilliseconds);
            // Hook up the Elapsed event for the timer with a lambda expression 
            pushTimer.Elapsed += (sourceObj, elapsedEventArgs) => ServerPush.ServerPushProcess();
            pushTimer.AutoReset = true;
            pushTimer.Enabled = true;
            // Add to timers list
            listScheduledTimer.Add(pushTimer);
        }
        /// <summary>
        /// Update newpos process
        /// </summary>
        public static void UpdateNewpos(bool restartPc)
        {
            // Download products files
            string localPath = AppDomain.CurrentDomain.BaseDirectory + ConfigurationManager.AppSettings["LogsPath"];
            string remoteRoot = Settings.serviceSettings.RemoteRoot + ConfigurationManager.AppSettings["RestFolder"];
            List<string> localFiles = new List<string>(new string[] { localPath + @"NewProd.xml", localPath + @"ScreenMex.xml" });
            List<string> filesToDownload = new List<string>();
            foreach (string localFile in localFiles)
                filesToDownload.Add(remoteRoot + @"/newpos/posdata/" + Path.GetFileName(localFile));
            for (int i = 0; !FileTransfer.DownloadFiles(filesToDownload.ToArray(), localPath) && i <= Settings.serviceSettings.AttemptsSession; i++)
                System.Threading.Thread.Sleep((int)TimeSpan.FromSeconds(30).TotalMilliseconds);
            // Check if there are any file to copy
            foreach (string localFile in localFiles)
            {
                if (!File.Exists(localFile))
                    localFiles.Remove(localFile);
            }
            if (localFiles.Count>0)
            {
                // Copy files
                try{ CopyUpdateFiles(localFiles.ToArray()); if (restartPc) { Process.Start("ShutDown", "/r"); } }
                catch (Exception exc)
                {
                    CustomLog.CustomLogEvent(string.Format("Error updating configuration files: {0}", exc.Message));
                    CustomLog.Error();
                }
            }
        }
        /// <summary>
        /// Copy configuration files
        /// </summary>
        /// <param name="localFiles"></param>
        private static void CopyUpdateFiles(string[] localFiles)
        {
            string rootFolder = AppInstaller.FindDirectory() + "Data";
            if (Directory.Exists(rootFolder))
            {
                string[] directories = Directory.GetDirectories(rootFolder,"Newpos*",SearchOption.TopDirectoryOnly);
                if (directories != null)
                {
                    foreach (string directory in directories)
                    {
                        string filesFolder = directory + @"\Posdata";
                        if (Directory.Exists(filesFolder))
                        {
                            foreach (string localFile in localFiles)
                                File.Copy(localFile, Path.Combine(filesFolder, Path.GetFileName(localFile)),true);
                        }
                    }
                }
            }
            foreach (string localFile in localFiles)
                File.Delete(localFile);
        }
        /// <summary>
        /// Check for updates
        /// </summary>
        private static void PeriodicRevision()
        {
            Task.Factory.StartNew(() =>
            {
                lock (Settings._threadLock)
                {
                    // Check for updates for POSync service
                    Updater.UpdateService();
                    // Check if POSync Updater is running
                    AppInstaller.StartService("POSync Updater");
                }
            });
        }
        /// <summary>
        /// Update synchronization settings
        /// </summary>
        public static void UpdateSettings()
        {
            Task.Factory.StartNew(() =>
            {
                lock (Settings._threadLock)
                {
                    // Stop and dispose timers
                    Stop();
                    // Start again service
                    Start();
                }
            });
        }
        /// <summary>
        /// Upload service status information to server
        /// </summary>
        private static void UploadInformationFiles()
        {
            // Upload log and posdata files
            string[] fileToUpload = { CustomLog.GetLogPath() };
            string[] foldersToUpload = { string.Format(@"/synclogs/itona{0}/", ConfigurationManager.AppSettings["PosFolder"]) };
            int attempts = Settings.serviceSettings.AttemptsSession;
            for (int i = 0; !FileTransfer.UploadFiles(fileToUpload, foldersToUpload, false) && i <= attempts; i++)
                System.Threading.Thread.Sleep((int)TimeSpan.FromSeconds(30).TotalMilliseconds);
        }
    }
}
