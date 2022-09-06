// File system storage class with all necessary parameters and
// methods to inizialize and manage the file storage
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using System.Configuration;
using System.Threading.Tasks;
using System.Globalization;

namespace POSync
{
    static class FileStorage
    {
        private static List<CustomStorageSettings> listFolders;
        private static List<CustomStorageSettings> listFoldersActive;
        private static List<System.Timers.Timer> listWatchTimer;
        private static List<System.Timers.Timer> listScheduledTimer;
        private static bool firstIni = true;
        private static string[] remotePos;
        public static void Start()
        {
            // Initialize the service configuration parameters
            Settings.PopulateServiceSettings();
            // Initialize sftp session
            FileTransfer.InitializeSession();
            // Initialize the list of monitored directories based on the XML configuration file
            PopulateListFileWatchers();
            // Get remote POS list
            remotePos = GetRemotePos();
            // Start the file system watcher for each of the file specification and folders found on the List<>
            StartDirectoryWatcher();
            // Start timers for update and daily sync tasks
            StartScheduledTimers();
            // Initialize watcher
            Watcher.Start(Settings._threadLock);
            // Start initial synchronization
            InitialSync();
        }
        public static void Stop()
        {
            if (listWatchTimer != null)
            {
                foreach (System.Timers.Timer timer in listWatchTimer)
                {
                    // Stop listening and dispose the Object
                    timer.Stop();
                    timer.Enabled = false;
                    timer.Dispose();
                }
            }
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
            // Stop watcher
            Watcher.Stop();
        }
        public static void InitialSync()
        {
            Task.Factory.StartNew(() =>
            {
                lock (Settings._threadLock)
                {
                    System.Threading.Thread.Sleep((int)TimeSpan.FromMinutes(2).TotalMilliseconds);
                    ManualSync();
                }
            });
        }
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
        public static bool ManualSync()
        {
            Pause();
            bool successSync = true;
            // Synchronize all folders
            if (listFoldersActive != null && remotePos.Length > 0)
                successSync = FileTransfer.PullFiles(listFoldersActive, remotePos);
            // Upload sql server queries
            ServerQuery.UploadTables(0);
            // Upload service information files
            UploadInformationFiles();
            Continue();
            return successSync;
        }
        public static void Pause()
        {
            if (listWatchTimer != null)
            {
                // Stop listening for new files
                foreach (System.Timers.Timer timer in listWatchTimer)
                    timer.Enabled = false;
            }
        }
        public static void Continue()
        {
            if (listWatchTimer != null)
            {
                // Start listening again
                foreach (System.Timers.Timer timer in listWatchTimer)
                    timer.Enabled = true;
            }
        }
        private static void PopulateListFileWatchers()
        {
            // Download configuration file from server
            string xmlConfigPath = AppDomain.CurrentDomain.BaseDirectory + ConfigurationManager.AppSettings["XMLFileFolderSettings"];
            AppInstaller.ServerDownload("FileStorageConfig.xml", xmlConfigPath, true);
            if (File.Exists(xmlConfigPath))
            {
                try
                {
                    XmlSerializer deserializer = new XmlSerializer(typeof(List<CustomStorageSettings>));
                    TextReader reader = new StreamReader(xmlConfigPath);
                    object obj = deserializer.Deserialize(reader);
                    // Close the TextReader object
                    reader.Close();
                    // Obtain a list of CustomFolderSettings from XML Input data
                    listFolders = obj as List<CustomStorageSettings>;
                }
                catch (IOException exc)
                {
                    CustomLog.CustomLogEvent("Error reading configuration file: " + exc.Message);
                    CustomLog.Error();
                }
                finally
                {
                    Settings.SecureDelete(xmlConfigPath);
                }
            }
            else
            {
                listFolders = new List<CustomStorageSettings>
                {
                    new CustomStorageSettings(@"logsxml_xml", true, @"*.xml", @"{0}NewPos\files\LogsXML\", false, @"/logsxml/pos{0}/", true, false, 30, "M")
                };
            }
        }
        private static string[] GetRemotePos()
        {
            List<string> posList = new List<string>();
            RestInfoCollection restInfoCollection = AppInstaller.PopulateRestInfo();
            if (restInfoCollection != null)
            {
                for (int i = 0; i < restInfoCollection.CustomRestInfo.Length; i++)
                {
                    if (restInfoCollection.CustomRestInfo[i].FolderName== ConfigurationManager.AppSettings["RestFolder"])
                    {
                        for (int j=0; j<restInfoCollection.CustomRestInfo[i].Device.Length;j++)
                        {
                            if (restInfoCollection.CustomRestInfo[i].Device[j].Connection.Contains("remoto"))
                                posList.Add(restInfoCollection.CustomRestInfo[i].Device[j].Value);
                        }
                        break;
                    }
                }
            }
            return posList.ToArray();
        }
        private static void StartDirectoryWatcher()
        {
            // Creates a new instance of the list of directories
            listFoldersActive = new List<CustomStorageSettings>();
            listWatchTimer = new List<System.Timers.Timer>();
            DirectoryInfo dir;
            double intervalTime;
            string driveLetter = AppInstaller.FindDirectory();
            // Loop the list to process each of the folder specifications found
            foreach (CustomStorageSettings customFolder in listFolders)
            {
                customFolder.FolderPath = string.Format(customFolder.FolderPath, driveLetter);
                dir = new DirectoryInfo(customFolder.FolderPath);
                // Checks whether the folder is enabled and also the directory is a valid location
                if (customFolder.FolderEnabled && dir.Exists)
                {
                    switch (customFolder.IntervalUnit)
                    {
                        case "S":
                        case "s":
                            intervalTime = TimeSpan.FromSeconds(customFolder.IntervalTime).TotalMilliseconds;
                            break;
                        case "M":
                        case "m":
                            intervalTime = TimeSpan.FromMinutes(customFolder.IntervalTime).TotalMilliseconds;
                            break;
                        case "H":
                        case "h":
                            intervalTime = TimeSpan.FromHours(customFolder.IntervalTime).TotalMilliseconds;
                            break;
                        default:
                            intervalTime = customFolder.IntervalTime;
                            break;
                    }
                    // Create a timer for each folder to be monitored
                    System.Timers.Timer watchTimer = new System.Timers.Timer(intervalTime);
                    // Hook up the Elapsed event for the timer with a lambda expression. 
                    watchTimer.Elapsed += (sourceObj, elapsedEventArgs) => OnTimedEvent(customFolder.FolderID, customFolder.FolderPath, customFolder.RemoteFolderPath,
                        customFolder.FolderFilter, customFolder.MoveFiles, customFolder.FolderIncludeSub, customFolder.IntervalTime);
                    watchTimer.AutoReset = true;
                    watchTimer.Enabled = true;
                    // Add to timers list
                    listWatchTimer.Add(watchTimer);
                    // Add to active manual sync folders
                    if (customFolder.ManualSync)
                        listFoldersActive.Add(customFolder);
                    if (firstIni)
                        // Record a log entry into Windows Event Log
                        CustomLog.CustomLogEvent(String.Format("Starting to synchronize local files with extension ({0}) in the folder ({1})", customFolder.FolderFilter, customFolder.FolderPath));
                }
                else if (!dir.Exists)
                {
                    CustomLog.CustomLogEvent(string.Format("Local directory {0} does not exist", customFolder.FolderPath));
                    CustomLog.Error();
                }
            }
            firstIni = false;
        }
        private static void OnTimedEvent(string folderID, string folderPath, string remotePath, string folderFilter, bool moveFiles, bool folderIncludeSub, double intervalTime)
        {
            // Start task for sync files. Avoid event buffer overflow
            Task.Factory.StartNew(() =>
            {
                lock (Settings._threadLock)
                {
                    try
                    {
                        if (remotePos.Length > 0)
                        {
                            int attempts = Settings.serviceSettings.AttemptsSession;
                            for (int i = 0; !FileTransfer.PullFiles(folderPath, remotePath, folderFilter, remotePos) && i <= attempts; i++)
                            {
                                System.Threading.Thread.Sleep((int)TimeSpan.FromSeconds(30).TotalMilliseconds);
                            }
                        }
                    }
                    catch (Exception exc)
                    {
                        CustomLog.CustomLogEvent(string.Format("Error on timer event: {0}", exc.Message));
                        CustomLog.Error();
                    }
                }
            });
        }
        public static string GetDateFolder(string fileName)
        {
            string folderName = "default";
            string[] nameParts = fileName.Split('_');
            if (nameParts.Length > 3)
            {
                string stringPart = nameParts[3];
                if ((stringPart.StartsWith("202") || stringPart.StartsWith("203")) && stringPart.Length > 7)
                {
                    string dateFolder = stringPart.Substring(0, 8);
                    try
                    {
                        folderName = DateTime.ParseExact(dateFolder, "yyyyMMdd", CultureInfo.InvariantCulture).ToString("yyyyMMdd");
                    }
                    catch (FormatException)
                    {
                        folderName = "default";
                    }
                }
            }
            return folderName;
        }
        private static void PeriodicRevision()
        {
            Task.Factory.StartNew(() =>
            {
                lock (Settings._threadLock)
                {
                    // Look for newpos updates
                    AppInstaller.UpdateNewpos();
                    UpdatesWatcher.UpdatePos();
                    // Upload sql server queries
                    ServerQuery.UploadTables(0);
                    // Check for updates for POSync service
                    Updater.UpdateService();
                    // Check if POSync Updater is running
                    AppInstaller.StartService("POSync Updater");
                }
            });
        }
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
        private static void UploadInformationFiles()
        {
            // Upload log and posdata files
            string driveLetter = AppInstaller.FindDirectory();
            string[] fileToUpload = { CustomLog.GetLogPath(),
                CustomLog.GetDownloadedLogPath(),
                string.Format(Settings.serviceSettings.LocalPosDataPath+@"operator.txt", driveLetter) };
            string[] foldersToUpload = { @"/synclogs/pc_gerentes/",
                @"/synclogs/pc_gerentes/",
                @"/newpos/posdata/" };
            int attempts = Settings.serviceSettings.AttemptsSession;
            for (int i = 0; !FileTransfer.UploadFiles(fileToUpload, foldersToUpload,false) && i <= attempts; i++)
                System.Threading.Thread.Sleep((int)TimeSpan.FromSeconds(30).TotalMilliseconds);
        }
    }
}
