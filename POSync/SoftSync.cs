// Synchronizer for soft server
using System;
using System.IO;
using System.Xml.Serialization;
using System.Configuration;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace POSync
{
    static class SoftSync
    {
        private static List<CustomFolderSettings> listFolders;
        private static List<CustomFolderSettings> listFoldersActive;
        private static List<System.Timers.Timer> listScheduledTimer;
        private static List<System.Timers.Timer> listWatchTimer;
        private static List<FileSystemWatcher> listFileWatcher;
        private static bool firstIni = true;
        /// <summary>
        /// Initialize variables and processes for synchronization system
        /// </summary>
        public static void Start()
        {
            // Initialize the service configuration parameters
            Settings.PopulateServiceSettings();
            // Initialize sftp session
            FileTransfer.InitializeSession();
            // Initialize the list of monitored directories based on the XML configuration file
            PopulateListFileWatchers();
            // Start timers for update and daily sync tasks
            StartScheduledTimers();
            // Start the file system watcher for each of the file specification and folders found on the List<>
            StartDirectoryWatcher();
            // Start initial synchronization
            InitialSync();
        }
        /// <summary>
        /// Stop and dispose synchronization timers
        /// </summary>
        public static void Stop()
        {
            if (listFileWatcher != null)
            {
                foreach (FileSystemWatcher wtc in listFileWatcher)
                {
                    // Stop listening and dispose the Object
                    wtc.EnableRaisingEvents = false;
                    wtc.Dispose();
                }
            }
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
        }
        /// <summary>This function initialize thread for
        /// initial manual synchronization </summary>
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
        /// <summary>This function initialize timers for update
        /// settings and parameters for the service </summary>
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
            pushTimer.Elapsed += (sourceObj, elapsedEventArgs) => ServerPush.SoftServerPushProcess();
            pushTimer.AutoReset = true;
            pushTimer.Enabled = true;
            // Add to timers list
            listScheduledTimer.Add(pushTimer);
        }
        /// <summary>This function run a manual synchronization</summary>
        public static void ManualSync()
        {
            Pause();
            // Upload sql server queries
            ServerQuery.UploadTables(0);
            // Upload service information files
            UploadInformationFiles();
            // continue monitoring files
            Continue();
        }
        /// <summary>
        /// Pause synchronizer timers
        /// </summary>
        public static void Pause()
        {
            if (listWatchTimer != null)
            {
                // Stop listening for new files
                foreach (System.Timers.Timer timer in listWatchTimer)
                    timer.Enabled = false;
            }
        }
        /// <summary>
        /// Resume synchronizer timers
        /// </summary>
        public static void Continue()
        {
            if (listWatchTimer != null)
            {
                // Start listening again
                foreach (System.Timers.Timer timer in listWatchTimer)
                    timer.Enabled = true;
            }
        }
        /// <summary>Populate custom folder settings class from xml file</summary>
        private static void PopulateListFileWatchers()
        {
            // Download configuration file from server
            string xmlConfigPath = AppDomain.CurrentDomain.BaseDirectory + ConfigurationManager.AppSettings["XMLFileFolderSettings"];
            string fileToDownload = string.Format("FileWatcherConfig_{0}.xml", ConfigurationManager.AppSettings["PosFolder"]);
            if (Settings.sshMode)
                FileTransfer.DownloadFiles(new string[] { Settings.serviceSettings.RemoteRoot + ConfigurationManager.AppSettings["RestFolder"] + @"/synclogs/config/" + fileToDownload }, Path.GetFullPath(xmlConfigPath));
            else
                AppInstaller.ServerDownload(fileToDownload, xmlConfigPath, true);
            if (File.Exists(xmlConfigPath))
            {
                try
                {
                    XmlSerializer deserializer = new XmlSerializer(typeof(List<CustomFolderSettings>));
                    TextReader reader = new StreamReader(xmlConfigPath);
                    object obj = deserializer.Deserialize(reader);
                    // Close the TextReader object
                    reader.Close();
                    // Obtain a list of CustomFolderSettings from XML Input data
                    listFolders = obj as List<CustomFolderSettings>;
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
                listFolders = new List<CustomFolderSettings>
                {
                    new CustomFolderSettings(@"ticket", true, @"formato.txt", @"{0}nationalsoft\Softrestaurant10.0\temporalSR\", false, @"/bop/pos{0}/", true, false, false, 10, "M"),
                    new CustomFolderSettings(@"report", true, @"reporte.txt", @"{0}nationalsoft\Softrestaurant10.0\temporalSR\", false, @"/bop/pos{0}/", true, false, false, 10, "M")
                };
            }
        }
        /// <summary>Initialize directory monitoring</summary>
        private static void StartDirectoryWatcher()
        {
            // Creates a new instance of the list of directories
            listFoldersActive = new List<CustomFolderSettings>();
            listWatchTimer = new List<System.Timers.Timer>();
            listFileWatcher = new List<FileSystemWatcher>();
            DirectoryInfo dir;
            double intervalTime;
            // Loop the list to process each of the folder specifications found
            foreach (CustomFolderSettings customFolder in listFolders)
            {
                customFolder.FolderPath = string.Format(customFolder.FolderPath, ConfigurationManager.AppSettings["DriveLetter"]);
                dir = new DirectoryInfo(customFolder.FolderPath);
                // Checks whether the folder is enabled and also the directory is a valid location
                if (customFolder.FolderEnabled && dir.Exists)
                {
                    customFolder.RemoteFolderPath = string.Format(customFolder.RemoteFolderPath, ConfigurationManager.AppSettings["PosFolder"]);
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
                    watchTimer.Elapsed += (sourceObj, elapsedEventArgs) => OnTimedEvent(customFolder.FolderID, customFolder.FolderPath, customFolder.RemoteFolderPath);
                    watchTimer.AutoReset = true;
                    watchTimer.Enabled = true;
                    // Add to timers list
                    listWatchTimer.Add(watchTimer);
                    // Create watcher for each folder to be monitored
                    FileSystemWatcher fileWatcher = new FileSystemWatcher(Path.GetDirectoryName(customFolder.FolderPath))
                    {
                        NotifyFilter = NotifyFilters.Size
                    };
                    fileWatcher.Changed += (sender, e) => OnChanged(e, customFolder.FolderID, customFolder.FolderPath);
                    fileWatcher.Created += (sender, e) => OnChanged(e, customFolder.FolderID, customFolder.FolderPath);
                    fileWatcher.Filter = customFolder.FolderFilter;
                    fileWatcher.IncludeSubdirectories = customFolder.FolderIncludeSub;
                    fileWatcher.EnableRaisingEvents = true;
                    // Add to timers list
                    listFileWatcher.Add(fileWatcher);
                    // Add to active manual sync folders
                    if (customFolder.ManualSync)
                        listFoldersActive.Add(customFolder);
                    if (firstIni)
                        // Record a log entry into Windows Event Log
                        CustomLog.CustomLogEvent(String.Format("Starting to synchronize local files with filter ({0}) in the folder ({1})", customFolder.FolderFilter, customFolder.FolderPath));
                }
                else if (!dir.Exists)
                {
                    CustomLog.CustomLogEvent(string.Format("Local directory {0} does not exist", customFolder.FolderPath));
                    CustomLog.Error();
                }
            }
            firstIni = false;
        }
        /// <summary>Event function for each timer</summary>
        private static void OnTimedEvent(string folderID, string folderPath, string remotePath)
        {
            // Start task for sync files. Avoid event buffer overflow
            Task.Factory.StartNew(() =>
            {
                lock (Settings._threadLock)
                {
                    try
                    {
                        string[] filesToUpload = FileManager.TmpFiles(folderPath);
                        if (filesToUpload.Length != 0)
                        {
                            int attempts = Settings.serviceSettings.AttemptsSession;
                            for (int i = 0; !FileTransfer.UploadFiles(filesToUpload, folderPath, remotePath, folderID) && i <= attempts; i++)
                                System.Threading.Thread.Sleep((int)TimeSpan.FromSeconds(30).TotalMilliseconds);
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
        /// <summary>
        /// On change event handler
        /// </summary>
        /// <param name="e">FileSystem event arguments</param>
        /// <param name="folderId">Folder identifier</param>
        /// <param name="folderPath">Folder path</param>
        private static void OnChanged(FileSystemEventArgs e, string folderId, string folderPath)
        {
            string originalFile = e.FullPath;
            // Validate event type and file existence
            if ((e.ChangeType != WatcherChangeTypes.Changed && e.ChangeType != WatcherChangeTypes.Created) || !File.Exists(originalFile))
                return;
            // Create temporary file path
            string tmpFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigurationManager.AppSettings["LogsPath"], string.Format("{0}_{1}.bop", folderId, DateTime.Now.ToString("yyyyMMddHHmmss")));
            // Read all file and copy
            using (StreamReader streamReader = new StreamReader(File.Open(originalFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            {
                try
                {
                    // Copy temporary file
                    string scheduleString = streamReader.ReadToEnd();
                    File.WriteAllText(tmpFile, scheduleString);
                }
                catch (Exception exc)
                {
                    CustomLog.CustomLogEvent(string.Format("Error creating temporary file {0}: {1}", Path.GetFileName(tmpFile), exc.Message));
                    CustomLog.Error();
                }
            }
            // Copy file to backup folder
            FileManager.TmpFiles(folderPath, new string[] { tmpFile });
            File.Delete(tmpFile);
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
                    // Upload sql server queries
                    ServerQuery.UploadTables(0);
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
            string deviceNumber = ConfigurationManager.AppSettings["PosFolder"];
            string remoteLogPath = string.Format(Settings.serviceSettings.RemoteLogsPath, deviceNumber);
            string[] fileToUpload = { CustomLog.GetLogPath() };
            string[] foldersToUpload = { remoteLogPath };
            int attempts = Settings.serviceSettings.AttemptsSession;
            for (int i = 0; !FileTransfer.UploadFiles(fileToUpload, foldersToUpload, false) && i <= attempts; i++)
                System.Threading.Thread.Sleep((int)TimeSpan.FromSeconds(30).TotalMilliseconds);
        }
    }
}
