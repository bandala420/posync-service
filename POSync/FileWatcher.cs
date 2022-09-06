// File system watcher class with all necessary parameters and
// methods to inizialize and manage the file watcher
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using System.Configuration;
using System.Threading.Tasks;
using System.Linq;
using System.Globalization;

namespace POSync
{
    static class FileWatcher
    {
        private static List<CustomFolderSettings> listFolders;
        public static List<CustomFolderSettings> listFoldersActive;
        private static List<System.Timers.Timer> listWatchTimer;
        private static List<System.Timers.Timer> listScheduledTimer;
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
            // Initialize the monitored directories list based on XML configuration file
            PopulateListFileWatchers();
            // Start timers for update and daily sync tasks
            StartScheduledTimers();
            // Start synchronization processes
            StartSync();
        }
        /// <summary>
        /// Start synchronization functions
        /// </summary>
        private static void StartSync()
        {
            // Start the file system watcher for each of the file specification and folders found on the List<>
            StartDirectoryWatcher();
            // Start initial synchronization
            InitialSync();
            // Validate device id with local ip
            if (!ValidatePosNumber())
            {
                CustomLog.CustomLogEvent("Error: Device identifier does not match network settings");
                CustomLog.Error();
            }
        }
        /// <summary>
        /// Stop and dispose synchronization timers
        /// </summary>
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
        }
        /// <summary>This function initialize thread for
        /// initial manual synchronization </summary>
        private static void InitialSync()
        {
            Task.Factory.StartNew(() =>
            {
                lock (Settings._threadLock)
                {
                    System.Threading.Thread.Sleep((int)TimeSpan.FromMinutes(2).TotalMilliseconds);
                    ManualSync(false);
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
            pushTimer.Elapsed += (sourceObj, elapsedEventArgs) => ServerPush.ServerPushProcess();
            pushTimer.AutoReset = true;
            pushTimer.Enabled = true;
            // Add to timers list
            listScheduledTimer.Add(pushTimer);
        }
        /// <summary>This function run a manual synchronization</summary>
        public static bool ManualSync(bool allFiles)
        {
            bool successSync = true;
            // Pause timers
            Pause();
            // Synchronize all folders
            if (listFoldersActive != null)
                successSync = FileTransfer.ManualSync(listFoldersActive,allFiles);
            // Upload log, posdata and counter files
            UploadInformationFiles();
            // Resume timers counting
            Continue();
            return successSync;
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
                FileTransfer.DownloadFiles(new string[] { Settings.serviceSettings.RemoteRoot + ConfigurationManager.AppSettings["RestFolder"] + @"/synclogs/config/"+ fileToDownload }, Path.GetFullPath(xmlConfigPath));
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
                    new CustomFolderSettings(@"boletas_bop", true, @"*.bop", @"{0}NewPos\files\Boletas\", false, @"/boletas/pos{0}/", true, false, false, 6, "H"),
                    new CustomFolderSettings(@"logs_log_dia", true, @"*.log;*.dia;*.tpa", @"{0}NewPos\files\Logs\", false, @"/logs/pos{0}/", true, false, true, 5, "M"),
                    new CustomFolderSettings(@"reprint_bop", true, @"*.bop", @"{0}NewPos\files\Reprint\", false, @"/boletas/pos{0}/reprint/", true, false, true, 10, "M"),
                    new CustomFolderSettings(@"logsxml_xml", true, @"*.xml", @"{0}NewPos\files\LogsXML\", true, @"/logsxml/pos{0}/", true, true, true, 5, "M"),
                    new CustomFolderSettings(@"queue_tlog", true, @"*.tlog", @"{0}NewPos\files\Queue\", true, @"/kvs/pos{0}/", true, false, true, 10, "M")
                };
            }
        }
        /// <summary>Initialize directory monitoring</summary>
        private static void StartDirectoryWatcher()
        {
            // Creates a new instance of the list of directories
            listFoldersActive = new List<CustomFolderSettings>();
            listWatchTimer = new List<System.Timers.Timer>();
            DirectoryInfo dir;
            double intervalTime;
            string driveLetter = AppInstaller.FindDirectory();
            // Loop the list to process each of the folder specifications found
            foreach (CustomFolderSettings customFolder in listFolders)
            {
                customFolder.FolderPath = string.Format(customFolder.FolderPath, driveLetter);
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
                    watchTimer.Elapsed += (sourceObj, elapsedEventArgs) => OnTimedEvent(customFolder.FolderID, customFolder.FolderPath, customFolder.RemoteFolderPath,
                        customFolder.FolderFilter, customFolder.FastSync, customFolder.MoveFiles, customFolder.FolderIncludeSub, customFolder.IntervalTime);
                    watchTimer.AutoReset = true;
                    watchTimer.Enabled = true;
                    // Add to timers list
                    listWatchTimer.Add(watchTimer);
                    // Add to active manual sync folders
                    if (customFolder.ManualSync)
                        listFoldersActive.Add(customFolder);
                    // Record a log entry into Windows Event Log
                    if (firstIni)
                        CustomLog.CustomLogEvent(String.Format("Starting to monitor files with extension ({0}) in the folder ({1})", customFolder.FolderFilter, customFolder.FolderPath));
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
        private static void OnTimedEvent(string folderID, string folderPath, string remotePath, string folderFilter, bool fastSync, bool moveFiles, bool folderIncludeSub, double intervalTime)
        {
            // Start task for sync files. Avoid event buffer overflow
            Task.Factory.StartNew(() =>
            {
                lock (Settings._threadLock)
                {
                    folderID = folderID.ToLower();
                    try
                    {
                        if (fastSync)
                        {
                            if (folderID == "logsxml_xml")
                                LogsXmlProcess(folderID, folderPath, folderFilter, remotePath, moveFiles);
                            else if (folderID == "queue_tlog")
                                KvsLogProcess(folderID, folderPath, folderFilter, folderIncludeSub, remotePath);
                            else
                            {
                                string[] filesToUpload = FileManager.PollDirectory(folderID, folderPath, folderFilter, folderIncludeSub);
                                if (filesToUpload.Length != 0)
                                {
                                    int attempts = Settings.serviceSettings.AttemptsSession;
                                    for (int i = 0; !FileTransfer.UploadFiles(filesToUpload, folderPath, remotePath, folderID) && i <= attempts; i++)
                                        System.Threading.Thread.Sleep((int)TimeSpan.FromSeconds(30).TotalMilliseconds);
                                }
                            }
                        }
                        else
                        {
                            int attempts = Settings.serviceSettings.AttemptsSession;
                            for (int i = 0; !FileTransfer.SyncFiles(folderPath, remotePath, folderFilter, folderIncludeSub) && i <= attempts; i++)
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
        /// Look for changes in xml files
        /// </summary>
        /// <param name="folderID">Folder identifier</param>
        /// <param name="folderPath">Folder local path</param>
        /// <param name="folderFilter">File filter</param>
        /// <param name="remotePath"></param>
        /// <param name="moveFiles"></param>
        private static void LogsXmlProcess(string folderID, string folderPath, string folderFilter, string remotePath, bool moveFiles)
        {
            // Get files from root path
            string[] rootFiles = FileManager.PollDirectory(folderID, folderPath, folderFilter, false);
            // Get folder id and name for each file
            string[] rootFoldersNames = new string[rootFiles.Length];
            for (int i = 0; i < rootFiles.Length; i++)
                rootFoldersNames[i] = remotePath;
            // Check if todays folder exists
            string todayFolder = DateTime.Now.ToString("yyyyMMdd");
            string sentRootPath = Path.Combine(folderPath, "Enviados");
            string sentPath = Path.Combine(sentRootPath, todayFolder);
            // Search for xml files in todays folder
            string[] filesFromToday;
            if (Directory.Exists(sentPath))
                filesFromToday = FileManager.PollDirectory("logsxml_today", sentPath, folderFilter, false);
            else
                filesFromToday = new string[0];
            // Get folder id and name for each file
            string[] todayFoldersNames = new string[filesFromToday.Length];
            for (int i = 0; i < filesFromToday.Length; i++)
                todayFoldersNames[i] = remotePath + todayFolder;
            // Search for generated xml files in sent folder
            string[] regenFiles;
            if (Directory.Exists(sentRootPath))
                regenFiles = FileManager.PollDirectory("logsxml_regen", sentRootPath, folderFilter,todayFolder);
            else
                regenFiles = new string[0];
            // Get folder id and name for each file
            string[] otherDayFoldersNamesAux = GetFolders(regenFiles);
            string[] otherDayFoldersNames = new string[regenFiles.Length];
            for (int i = 0; i < regenFiles.Length; i++)
                otherDayFoldersNames[i] = remotePath + otherDayFoldersNamesAux[i];
            // Concatenate information files to upload
            string[] filesToUpload = rootFiles.Concat(filesFromToday).ToArray().Concat(regenFiles).ToArray();
            string[] foldersToUpload = rootFoldersNames.Concat(todayFoldersNames).ToArray().Concat(otherDayFoldersNames).ToArray();
            // Upload xml files
            if (filesToUpload.Length != 0)
            {
                int attempts = Settings.serviceSettings.AttemptsSession;
                for (int i = 0; !FileTransfer.UploadFiles(filesToUpload, foldersToUpload, true) && i <= attempts; i++)
                    System.Threading.Thread.Sleep((int)TimeSpan.FromSeconds(30).TotalMilliseconds);
            }
            // Move and orgnanize files in folder Enviados by date
            if (moveFiles && rootFiles.Length>0)
            {
                string fileName,targetPath,targetFile;
                // Explode folderFilter for each extension
                string[] folderFilterParts = folderFilter.Split(';');
                List<string> filesToMove = new List<string>();
                foreach (string individualFilter in folderFilterParts)
                    filesToMove.AddRange(Directory.GetFiles(folderPath,individualFilter,SearchOption.TopDirectoryOnly));
                rootFiles = filesToMove.ToArray();
                string[] dateFolders = GetFolders(rootFiles);
                for (int i=0; i< rootFiles.Length ;i++)
                {
                    if (File.Exists(rootFiles[i]))
                    {
                        fileName = Path.GetFileName(rootFiles[i]);
                        targetPath = Path.Combine(sentRootPath, dateFolders[i]);
                        targetFile = Path.Combine(targetPath, fileName);
                        try
                        {
                            if (!Directory.Exists(targetPath))
                                Directory.CreateDirectory(targetPath);
                            // Move file or delete obsolete
                            if (File.Exists(targetFile))
                                File.Delete(rootFiles[i]);
                            else
                                File.Move(rootFiles[i], targetFile);
                        }
                        catch (Exception exc)
                        {
                            CustomLog.CustomLogEvent("Error moving xml files: " + exc.Message);
                            CustomLog.Error();
                        }
                    }
                }
            }
        }
        /// <summary>
        /// Look for changes in tlog files
        /// </summary>
        /// <param name="folderID">Folder identifier</param>
        /// <param name="folderPath">Folder local path</param>
        /// <param name="folderFilter">File filter</param>
        /// <param name="includeSub">Include subdirectories</param>
        /// <param name="remotePath">Remote server path</param>
        private static void KvsLogProcess(string folderID, string folderPath, string folderFilter, bool includeSub, string remotePath)
        {
            // get files to upload
            List<string> filesToUpload = new List<string>();
            List<string> foldersToUpload = new List<string>();
            string[] filesToProcess = FileManager.PollDirectory(folderID, folderPath, folderFilter, includeSub);
            string restCode = ConfigurationManager.AppSettings["RestFolder"];
            string posNumber = ConfigurationManager.AppSettings["PosFolder"];
            foreach (string filePath in filesToProcess)
            {
                string directoryPath = Path.GetDirectoryName(filePath);
                string lastFolderName = Path.GetFileName(directoryPath);
                // get folder name
                string folderToUpload = folderPath.Contains(lastFolderName) ? remotePath : remotePath + lastFolderName;
                if (filePath.ToLower().Contains(".gz"))
                {
                    filesToUpload.Add(filePath);
                    foldersToUpload.Add(folderToUpload);
                }
                else
                {
                    string fileName = Path.GetFileNameWithoutExtension(filePath);
                    string comFile = Path.Combine(directoryPath, string.Format("{0}_{1}_{2}_{3}_kvs.tlog.gz", restCode, posNumber, lastFolderName, fileName));
                    // compress tlog file
                    AppInstaller.Gzip(filePath, comFile);
                    File.Delete(filePath);
                    if (File.Exists(comFile))
                    {
                        filesToUpload.Add(comFile);
                        foldersToUpload.Add(folderToUpload);
                    }
                }
            }
            // Upload tlog files
            if (filesToUpload.Count > 0)
            {
                int attempts = Settings.serviceSettings.AttemptsSession;
                for (int i = 0; !FileTransfer.UploadFiles(filesToUpload.ToArray(),foldersToUpload.ToArray(),true) && i <= attempts; i++)
                    System.Threading.Thread.Sleep((int)TimeSpan.FromSeconds(30).TotalMilliseconds);
            }
        }
        /// <summary>
        /// Get folder date from xml filename
        /// </summary>
        /// <param name="filesArray">XML files array</param>
        /// <returns></returns>
        private static string[] GetFolders(string[] filesArray)
        {
            string[] foldersArray = new string[filesArray.Length];
            for (int i = 0; i < filesArray.Length; i++)
            {
                foldersArray[i] = "default";
                string fileName = Path.GetFileName(filesArray[i]);
                string[] nameParts = fileName.Split('_');
                if (nameParts.Length>3)
                {
                    string stringPart = nameParts[3];
                    if ((stringPart.StartsWith("202") || stringPart.StartsWith("203")) && stringPart.Length > 7)
                    {
                        string dateFolder = stringPart.Substring(0, 8);
                        try
                        {
                            foldersArray[i] = DateTime.ParseExact(dateFolder, "yyyyMMdd", CultureInfo.InvariantCulture).ToString("yyyyMMdd");
                        }
                        catch (FormatException)
                        {
                            foldersArray[i] = "default";
                        }
                    }
                }
            }
            return foldersArray;
        }
        /// <summary>
        /// Check for updates
        /// </summary>
        private static void PeriodicRevision()
        {
            lock (Settings._threadLock)
            {
                // Look for newpos updates
                AppInstaller.UpdateNewpos();
                // Check for updates for POSync service
                Updater.UpdateService();
                // Check if POSync Updater is running
                AppInstaller.StartService("POSync Updater");
                // Upload status information to server
                UploadInformationFiles();
            }
        }
        /// <summary>
        /// Update synchronization settings
        /// </summary>
        public static void UpdateSettings()
        {
            lock (Settings._threadLock)
            {
                // Stop and dispose timers
                Stop();
                // Start again service
                Start();
            }
        }
        /// <summary>
        /// Upload service status information to server
        /// </summary>
        private static void UploadInformationFiles()
        {
            string driveLetter = AppInstaller.FindDirectory();
            string posNumber = ConfigurationManager.AppSettings["PosFolder"];
            string remotePosdataPath = string.Format(@"/newpos/posdata/pos{0}/", posNumber);
            string remoteStatusPath = string.Format(Settings.serviceSettings.RemoteLogsPath, posNumber);
            // Count and validate files in each directory
            FileCounter();
            // Upload log and posdata files
            string[] fileToUpload = new string[] { CustomLog.GetLogPath(),
            AppDomain.CurrentDomain.BaseDirectory + ConfigurationManager.AppSettings["LogsPath"] + "LocalFiles.txt",
            string.Format(Settings.serviceSettings.LocalPosDataPath+@"posdb.xml", driveLetter),
            string.Format(Settings.serviceSettings.LocalPosDataPath+@"newprod.xml", driveLetter),
            string.Format(Settings.serviceSettings.LocalPosDataPath+@"screenmex.xml", driveLetter),
            string.Format(Settings.serviceSettings.LocalPosDataPath+@"storedb.xml", driveLetter) };
            string[] foldersToUpload = new string[] { remoteStatusPath, remoteStatusPath, remotePosdataPath, remotePosdataPath, remotePosdataPath, remotePosdataPath };
            int attempts = Settings.serviceSettings.AttemptsSession;
            for (int i = 0; !FileTransfer.UploadFiles(fileToUpload, foldersToUpload,false) && i <= attempts; i++)
                System.Threading.Thread.Sleep((int)TimeSpan.FromSeconds(30).TotalMilliseconds);
        }
        /// <summary>
        /// Count main files for validation in server side
        /// </summary>
        private static void FileCounter()
        {
            int daysBefore = Settings.serviceSettings.SyncDaysInterval*8;
            string localFilesPath = AppDomain.CurrentDomain.BaseDirectory + ConfigurationManager.AppSettings["LogsPath"] + "LocalFiles.txt";
            string[] localFilesLines = new string[daysBefore+1];
            for (int i=0;i<daysBefore+1;i++)
            {
                // Declare all variables
                int logs_log_count = 0, logs_tpa_count = 0, boletas_count = 0, reprint_count = 0, logs_dia_count = 0, pmix_count = 0,
                    skims_count = 0, cash_count = 0, ccard_count = 0, audit_count = 0, status_count = 0, sci_count = 0,
                    posdb_count = 0, newprod_count = 0, storedb_count = 0, tpa_log_count = 0, dia_dia_count = 0;
                long logs_log_size = 0, logs_tpa_size = 0, boletas_size = 0, reprint_size = 0, logs_dia_size = 0, pmix_size = 0,
                    skims_size = 0, cash_size = 0, ccard_size = 0, audit_size = 0, status_size = 0, sci_size = 0,
                    posdb_size = 0, newprod_size = 0, storedb_size = 0, tpa_log_size = 0, dia_dia_size = 0;
                string logs_log_stamp = "00:00:00", logs_tpa_stamp = "00:00:00", boletas_stamp = "00:00:00", reprint_stamp = "00:00:00", logs_dia_stamp = "00:00:00", pmix_stamp = "00:00:00",
                    skims_stamp = "00:00:00", cash_stamp = "00:00:00", ccard_stamp = "00:00:00", audit_stamp = "00:00:00", status_stamp = "00:00:00", sci_stamp = "00:00:00",
                    posdb_stamp = "00:00:00", newprod_stamp = "00:00:00", storedb_stamp = "00:00:00", tpa_log_stamp = "00:00:00", dia_dia_stamp = "00:00:00";
                string businessDayDate = DateTime.Now.AddDays(-i).ToString("yyyy-MM-dd");
                string businessDayCode = DateTime.Now.AddDays(-i).ToString("yyyyMMdd");
                FileInfo fileInfo; List<FileInfo> filesInfo;
                foreach (CustomFolderSettings listFolder in listFoldersActive)
                {
                    SearchOption searchOption = (listFolder.FolderIncludeSub) ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                    try
                    {
                        switch (listFolder.FolderID)
                        {
                            case "logs_log_dia":
                                // Search logs_log files
                                string fileName = listFolder.FolderPath + businessDayCode + @".log";
                                fileInfo = new FileInfo(fileName);
                                if (fileInfo.Exists && fileInfo.Length!=0)
                                {
                                    logs_log_count = 1;
                                    logs_log_size = fileInfo.Length;
                                    logs_log_stamp = fileInfo.LastWriteTime.ToString("H:mm:ss");
                                }
                                // Search and count newpos tpa files
                                filesInfo = new DirectoryInfo(listFolder.FolderPath).GetFiles("newpos*.tpa", searchOption).OrderBy(f=>f.LastWriteTime).ToList();
                                if (filesInfo.Count!=0)
                                {
                                    logs_tpa_count = filesInfo.Count;
                                    logs_tpa_size = filesInfo[filesInfo.Count - 1].Length;
                                    logs_tpa_stamp = filesInfo[filesInfo.Count - 1].LastWriteTime.ToString("H:mm:ss");
                                }
                                // Search logs_dia files
                                fileName = listFolder.FolderPath + businessDayCode + @".dia";
                                fileInfo = new FileInfo(fileName);
                                if (fileInfo.Exists && fileInfo.Length != 0)
                                {
                                    logs_dia_count = 1;
                                    logs_dia_size = fileInfo.Length;
                                    logs_dia_stamp = fileInfo.LastWriteTime.ToString("H:mm:ss");
                                }
                                break;
                            case "boletas_bop":
                                // Search and count boletas files
                                filesInfo = new DirectoryInfo(listFolder.FolderPath).GetFiles(listFolder.FolderFilter, searchOption)
                                    .Where(f => DateTime.Compare(DateTime.Parse(businessDayDate),f.LastWriteTime) < 0 && DateTime.Compare(DateTime.Parse(businessDayDate+" 23:59:59"), f.LastWriteTime) > 0)
                                    .OrderBy(f=>f.LastWriteTime).ToList();
                                if (filesInfo.Count != 0)
                                {
                                    boletas_count = filesInfo.Count;
                                    boletas_size = filesInfo[filesInfo.Count - 1].Length;
                                    boletas_stamp = filesInfo[filesInfo.Count - 1].LastWriteTime.ToString("H:mm:ss");
                                }
                                break;
                            case "reprint_bop":
                                // Search and count reprint files
                                filesInfo = new DirectoryInfo(listFolder.FolderPath).GetFiles(listFolder.FolderFilter, searchOption)
                                    .Where(f => DateTime.Compare(DateTime.Parse(businessDayDate), f.LastWriteTime) < 0 && DateTime.Compare(DateTime.Parse(businessDayDate + " 23:59:59"), f.LastWriteTime) > 0)
                                    .OrderBy(f => f.LastWriteTime).ToList();
                                if (filesInfo.Count != 0)
                                {
                                    reprint_count = filesInfo.Count;
                                    reprint_size = filesInfo[filesInfo.Count - 1].Length;
                                    reprint_stamp = filesInfo[filesInfo.Count - 1].LastWriteTime.ToString("H:mm:ss");
                                }
                                break;
                            case "logsxml_xml":
                                // Search and count xml_pmix files
                                string folderPathFixed = listFolder.FolderPath + @"enviados\" + businessDayCode;
                                if (!Directory.Exists(folderPathFixed)) { continue; }
                                filesInfo = new DirectoryInfo(folderPathFixed).GetFiles(@"*PMix*"+businessDayCode+@"*.xml", searchOption)
                                    .OrderBy(f => f.LastWriteTime).ToList();
                                if (filesInfo.Count != 0)
                                {
                                    foreach (FileInfo xmlFile in filesInfo)
                                    {
                                        if (xmlFile.Length != 0)
                                        {
                                            pmix_count += 1;
                                            pmix_size = xmlFile.Length;
                                            pmix_stamp = xmlFile.LastWriteTime.ToString("H:mm:ss");
                                        }
                                    }                                    
                                }
                                // Search and count xml_skims files
                                filesInfo = new DirectoryInfo(folderPathFixed).GetFiles(@"*Skims*" + businessDayCode + @"*.xml", searchOption)
                                    .OrderBy(f => f.LastWriteTime).ToList();
                                if (filesInfo.Count != 0)
                                {
                                    foreach (FileInfo xmlFile in filesInfo)
                                    {
                                        if (xmlFile.Length != 0)
                                        {
                                            skims_count += 1;
                                            skims_size = xmlFile.Length;
                                            skims_stamp = xmlFile.LastWriteTime.ToString("H:mm:ss");
                                        }
                                    }
                                }
                                // Search and count xml_cash files
                                filesInfo = new DirectoryInfo(folderPathFixed).GetFiles(@"*Cash*" + businessDayCode + @"*.xml", searchOption)
                                    .OrderBy(f => f.LastWriteTime).ToList();
                                if (filesInfo.Count != 0)
                                {
                                    foreach (FileInfo xmlFile in filesInfo)
                                    {
                                        if (xmlFile.Length != 0)
                                        {
                                            cash_count += 1;
                                            cash_size = xmlFile.Length;
                                            cash_stamp = xmlFile.LastWriteTime.ToString("H:mm:ss");
                                        }
                                    }
                                }
                                // Search and count xml_ccard files
                                filesInfo = new DirectoryInfo(folderPathFixed).GetFiles(@"*CCard*" + businessDayCode + @"*.xml", searchOption)
                                    .OrderBy(f => f.LastWriteTime).ToList();
                                if (filesInfo.Count != 0)
                                {
                                    foreach (FileInfo xmlFile in filesInfo)
                                    {
                                        if (xmlFile.Length != 0)
                                        {
                                            ccard_count += 1;
                                            ccard_size = xmlFile.Length;
                                            ccard_stamp = xmlFile.LastWriteTime.ToString("H:mm:ss");
                                        }
                                    }
                                }
                                // Search and count xml_audit files
                                filesInfo = new DirectoryInfo(folderPathFixed).GetFiles(@"*Audit*" + businessDayCode + @"*.xml", searchOption)
                                    .OrderBy(f => f.LastWriteTime).ToList();
                                if (filesInfo.Count != 0)
                                {
                                    foreach (FileInfo xmlFile in filesInfo)
                                    {
                                        if (xmlFile.Length != 0)
                                        {
                                            audit_count += 1;
                                            audit_size = xmlFile.Length;
                                            audit_stamp = xmlFile.LastWriteTime.ToString("H:mm:ss");
                                        }
                                    }
                                }
                                // Search and count xml_status files
                                filesInfo = new DirectoryInfo(folderPathFixed).GetFiles(@"*Status*" + businessDayCode + @"*.xml", searchOption)
                                    .OrderBy(f => f.LastWriteTime).ToList();
                                if (filesInfo.Count != 0)
                                {
                                    foreach (FileInfo xmlFile in filesInfo)
                                    {
                                        if (xmlFile.Length != 0)
                                        {
                                            status_count += 1;
                                            status_size = xmlFile.Length;
                                            status_stamp = xmlFile.LastWriteTime.ToString("H:mm:ss");
                                        }
                                    }
                                }
                                // Search and count xml_sci files
                                filesInfo = new DirectoryInfo(folderPathFixed).GetFiles(@"*SCI*" + businessDayCode + @"*.xml", searchOption)
                                    .OrderBy(f => f.LastWriteTime).ToList();
                                if (filesInfo.Count != 0)
                                {
                                    foreach (FileInfo xmlFile in filesInfo)
                                    {
                                        if (xmlFile.Length != 0)
                                        {
                                            sci_count += 1;
                                            sci_size = xmlFile.Length;
                                            sci_stamp = xmlFile.LastWriteTime.ToString("H:mm:ss");
                                        }
                                    }
                                }
                                // Search and count xml_posdb files
                                filesInfo = new DirectoryInfo(folderPathFixed).GetFiles(@"*PosDB*" + businessDayCode + @"*.xml", searchOption)
                                    .OrderBy(f => f.LastWriteTime).ToList();
                                if (filesInfo.Count != 0)
                                {
                                    foreach (FileInfo xmlFile in filesInfo)
                                    {
                                        if (xmlFile.Length != 0)
                                        {
                                            posdb_count += 1;
                                            posdb_size = xmlFile.Length;
                                            posdb_stamp = xmlFile.LastWriteTime.ToString("H:mm:ss");
                                        }
                                    }
                                }
                                // Search and count xml_newprod files
                                filesInfo = new DirectoryInfo(folderPathFixed).GetFiles(@"*NewProd*" + businessDayCode + @"*.xml", searchOption)
                                    .OrderBy(f => f.LastWriteTime).ToList();
                                if (filesInfo.Count != 0)
                                {
                                    foreach (FileInfo xmlFile in filesInfo)
                                    {
                                        if (xmlFile.Length != 0)
                                        {
                                            newprod_count += 1;
                                            newprod_size = xmlFile.Length;
                                            newprod_stamp = xmlFile.LastWriteTime.ToString("H:mm:ss");
                                        }
                                    }
                                }
                                // Search and count xml_storedb files
                                filesInfo = new DirectoryInfo(folderPathFixed).GetFiles(@"*StoreDB*" + businessDayCode + @"*.xml", searchOption)
                                    .OrderBy(f => f.LastWriteTime).ToList();
                                if (filesInfo.Count != 0)
                                {
                                    foreach (FileInfo xmlFile in filesInfo)
                                    {
                                        if (xmlFile.Length != 0)
                                        {
                                            storedb_count += 1;
                                            storedb_size = xmlFile.Length;
                                            storedb_stamp = xmlFile.LastWriteTime.ToString("H:mm:ss");
                                        }
                                    }
                                }
                                // Search and count xml_tpa_log files
                                filesInfo = new DirectoryInfo(folderPathFixed).GetFiles(@"*tpa*" + businessDayCode + @"*.Log", searchOption)
                                    .OrderBy(f => f.LastWriteTime).ToList();
                                if (filesInfo.Count != 0)
                                {
                                    foreach (FileInfo xmlFile in filesInfo)
                                    {
                                        if (xmlFile.Length != 0)
                                        {
                                            tpa_log_count += 1;
                                            tpa_log_size = xmlFile.Length;
                                            tpa_log_stamp = xmlFile.LastWriteTime.ToString("H:mm:ss");
                                        }
                                    }
                                }
                                // Search and count xml_dia_dia files
                                filesInfo = new DirectoryInfo(folderPathFixed).GetFiles(@"*dia*" + businessDayCode + @"*.Dia", searchOption)
                                    .OrderBy(f => f.LastWriteTime).ToList();
                                if (filesInfo.Count != 0)
                                {
                                    foreach (FileInfo xmlFile in filesInfo)
                                    {
                                        if (xmlFile.Length != 0)
                                        {
                                            dia_dia_count += 1;
                                            dia_dia_size = xmlFile.Length;
                                            dia_dia_stamp = xmlFile.LastWriteTime.ToString("H:mm:ss");
                                        }
                                    }
                                }
                                break;
                            default:
                                break;
                        }
                    }
                    catch (IOException exc)
                    {
                        CustomLog.CustomLogEvent("Error counting files: " + exc.Message);
                        CustomLog.Error();
                    }
                }
                localFilesLines[i] = businessDayDate+@","+logs_log_count+@"|"+logs_tpa_count+@"|"+boletas_count+@"|"+reprint_count+@"|"+logs_dia_count+@"|"+pmix_count+@"|"+
                    skims_count+@"|"+cash_count+@"|"+ccard_count+@"|"+audit_count+@"|"+status_count+@"|"+sci_count+@"|"+posdb_count+@"|"+newprod_count+@"|"+
                    storedb_count+@"|"+tpa_log_count+@"|"+dia_dia_count+@","+logs_log_size+@"|"+logs_tpa_size+@"|"+boletas_size+@"|"+reprint_size+@"|"+logs_dia_size+@"|"+pmix_size+@"|"+
                    skims_size+@"|"+cash_size+@"|"+ccard_size+@"|"+audit_size+@"|"+status_size+@"|"+sci_size+@"|"+posdb_size+@"|"+newprod_size+@"|"+
                    storedb_size+@"|"+tpa_log_size+@"|"+dia_dia_size+@","+logs_log_stamp+@"|"+logs_tpa_stamp+@"|"+boletas_stamp+@"|"+reprint_stamp+@"|"+logs_dia_stamp+@"|"+pmix_stamp+@"|"+
                    skims_stamp+@"|"+cash_stamp+@"|"+ccard_stamp+@"|"+audit_stamp+@"|"+status_stamp+@"|"+sci_stamp+@"|"+posdb_stamp+@"|"+newprod_stamp+@"|"+
                    storedb_stamp+@"|"+tpa_log_stamp+@"|"+dia_dia_stamp;
            }
            try
            {
                File.WriteAllLines(localFilesPath,localFilesLines);
            }
            catch (IOException exc)
            {
                CustomLog.CustomLogEvent("Error writing into local files register: " + exc.Message);
                CustomLog.Error();
            }
        }
        /// <summary>
        /// Validate the last local ip octet vs pos number. They must be equal to proceed
        /// </summary>
        /// <returns></returns>
        private static bool ValidatePosNumber()
        {
            string[] ipOctets = AppInstaller.GetPrivateIp().Split('.');
            string lastIpOctet = ipOctets[ipOctets.Length - 1];
            if (Int16.TryParse(lastIpOctet,out short lastOctet))
            {
                if (Int16.TryParse(ConfigurationManager.AppSettings["PosFolder"], out short posNumber))
                    return posNumber.Equals(lastOctet);
            }
            return false;
        }
    }
}
