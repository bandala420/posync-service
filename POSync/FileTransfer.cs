// File transfer class to manipulate WinSCP package
// classes parameters and methods
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using WinSCP;

namespace POSync
{
    static class FileTransfer
    {
        private static SessionOptions sessionOptions;
        private static string remoteRootFolder,xmlFilter;
        private static int daysInterval, monthsInterval;
        public static void InitializeSession()
        {
            string sshPrivateKeyFile = AppInstaller.GetSshPrivateKeyFile();
            sessionOptions = new SessionOptions
            {
                Protocol = Protocol.Sftp,
                HostName = "oceanodigital.mx",
                UserName = Path.GetFileNameWithoutExtension(sshPrivateKeyFile),
                SshPrivateKeyPath = sshPrivateKeyFile,
                SshHostKeyFingerprint = "confidential",
                PortNumber = 22,
                Timeout = TimeSpan.FromSeconds(10)
            };
            daysInterval = Settings.serviceSettings.SyncDaysInterval;
            monthsInterval = Settings.serviceSettings.InitialSyncMonthsInterval*30;
            remoteRootFolder = $"{Settings.serviceSettings.RemoteRoot}{ConfigurationManager.AppSettings["RestFolder"]}";
            string[] xmlFileFilters = Settings.serviceSettings.XmlExcludeList.Split(';');
            foreach (string xmlFileFilter in xmlFileFilters)
                xmlFilter = xmlFilter + @"*" + xmlFileFilter + @"*;";
            CustomLog.ClearLogs();
        }
        private static Session NewSession()
        {
            CustomLog.ClearLogs();
            Session newSession =  new Session()
            {
                SessionLogPath = $"{AppDomain.CurrentDomain.BaseDirectory}{ConfigurationManager.AppSettings["LogsPath"]}WinscpSessionLog.log",
                Timeout = TimeSpan.FromMinutes((double)Settings.serviceSettings.SessionTimeout),
                ReconnectTime = TimeSpan.FromSeconds(20)
            };
            newSession.FileTransferred += FileTransferred;
            newSession.QueryReceived += QueryReceived;
            return newSession;
        }
        private static TransferOptions NewTransferOptions()
        {
            TransferOptions newTransferOptions = new TransferOptions
            {
                TransferMode = TransferMode.Binary,
            };
            newTransferOptions.ResumeSupport.State = TransferResumeSupportState.On;
            newTransferOptions.AddRawSettings("ExcludeEmptyDirectories", "1");
            return newTransferOptions;
        }
        public static bool UploadFiles(string[] filesToUpload, string localFolderPath, string remoteFolderPath, string folderID)
        {
            string remoteFolderPathFixed = remoteRootFolder + remoteFolderPath;
            try
            {
                using (Session SFTPsession = NewSession())
                {
                    // Open ssh connection for SFTP file transfer
                    SFTPsession.Open(sessionOptions);
                    // Check for existence of directories
                    CheckDirectories(SFTPsession,remoteFolderPathFixed);
                    if (SFTPsession.FileExists(remoteFolderPathFixed))
                    {
                        TransferOptions transferOptions = NewTransferOptions();
                        // Rename files to avoid repeated logs
                        if (folderID == "logs_log_dia")
                            NoLogsRepeated(SFTPsession, localFolderPath, remoteFolderPath);
                        // Upload each file
                        foreach (string fileName in filesToUpload)
                        {
                            if (File.Exists(fileName))
                                SFTPsession.PutFileToDirectory(fileName, remoteFolderPathFixed, true, transferOptions);
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                CustomLog.CustomLogEvent(string.Format("Upload failed: {0}", exc.Message));
                CustomLog.Error();
                return false;
            }
            return true;
        }
        public static bool UploadFiles(string[] filesToUpload, string[] foldersToUpload, bool removeFiles)
        {
            try
            {
                using (Session SFTPsession = NewSession())
                {
                    // Open ssh connection for SFTP file transfer
                    SFTPsession.Open(sessionOptions);
                    // Set filemask for upload
                    TransferOptions transferOptions = NewTransferOptions();
                    for (int i = 0; i < filesToUpload.Length; i++)
                    {
                        string remoteFolderPathFixed = remoteRootFolder + foldersToUpload[i];
                        // Check for existence of directories
                        CheckDirectories(SFTPsession,remoteFolderPathFixed);
                        if (SFTPsession.FileExists(remoteFolderPathFixed) && File.Exists(filesToUpload[i]))
                            // Try to upload file to its respective directory
                            SFTPsession.PutFileToDirectory(filesToUpload[i], remoteFolderPathFixed, removeFiles, transferOptions);
                    }
                }
            }
            catch (Exception exc)
            {
                CustomLog.CustomLogEvent(string.Format("Upload failed: {0}", exc.Message));
                CustomLog.Error();
                return false;
            }
            return true;
        }
        public static bool UploadFiles(string[] filesToUpload, string remoteFolderPath, bool removeFiles)
        {
            string remoteFolderPathFixed = remoteRootFolder + remoteFolderPath;
            try
            {
                using (Session SFTPsession = NewSession())
                {
                    // Open ssh connection for SFTP file transfer
                    SFTPsession.Open(sessionOptions);
                    // Check for existence of directories
                    CheckDirectories(SFTPsession,remoteFolderPathFixed);
                    if (SFTPsession.FileExists(remoteFolderPathFixed))
                    {
                        // Set filemask for upload
                        TransferOptions transferOptions = NewTransferOptions();
                        foreach (string fileName in filesToUpload)
                        {
                            if (File.Exists(fileName))
                                SFTPsession.PutFileToDirectory(fileName, remoteFolderPathFixed, removeFiles, transferOptions);
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                CustomLog.CustomLogEvent(string.Format("Upload failed: {0}", exc.Message));
                CustomLog.Error();
                return false;
            }
            return true;
        }
        public static bool SyncFiles(string localFolderPath,string remoteFolderPath,string folderFilter,bool folderIncludeSub)
        {
            string remoteFolderPathFixed = remoteRootFolder + remoteFolderPath;
            try
            {
                using (Session SFTPsession = NewSession())
                {
                    // Open ssh connection for SFTP file transfer
                    SFTPsession.Open(sessionOptions);
                    // Check for existence of directories
                    CheckDirectories(SFTPsession,remoteFolderPathFixed);
                    if (SFTPsession.FileExists(remoteFolderPathFixed))
                    {
                        // Set filemask for synchronization
                        TransferOptions transferOptions = NewTransferOptions();
                        // Set filemask filter for synchronize
                        string folderFilterFixed = folderFilter.Replace(";", ">{0}D;");
                        transferOptions.FileMask = (folderIncludeSub) ? string.Format(folderFilterFixed + @">{0}D;20[123][0-9][01][0-9][0-3][0-9]/|" + xmlFilter, daysInterval) : string.Format(folderFilterFixed + @">{0}D|*/;" + xmlFilter, daysInterval);
                        // Synchronize files
                        SynchronizationResult synchronizationResult;
                        synchronizationResult = SFTPsession.SynchronizeDirectories(SynchronizationMode.Remote, localFolderPath, remoteFolderPathFixed, false, false, SynchronizationCriteria.Either, transferOptions);
                        // Throw on any error
                        synchronizationResult.Check();
                    }
                }
            }
            catch (Exception e)
            {
                CustomLog.CustomLogEvent(string.Format("Synchronization failed: {0}", e.Message));
                CustomLog.Error();
                return false;
            }
            return true;
        }
        public static bool ManualSync(List<CustomFolderSettings> listFolders, bool allFiles)
        {
            try
            {
                using (Session SFTPsession = NewSession())
                {
                    // Open ssh connection for SFTP file transfer
                    SFTPsession.Open(sessionOptions);
                    // Sync all folders to be monitored  
                    TransferOptions transferOptions = NewTransferOptions();
                    foreach (CustomFolderSettings lf in listFolders)
                    {
                        string remoteFolderPathFixed = remoteRootFolder + lf.RemoteFolderPath;
                        string folderPathFixed = (lf.FolderID == "logsxml_xml") ? lf.FolderPath + @"Enviados" : lf.FolderPath;
                        if (!Directory.Exists(folderPathFixed)) { continue; }
                        // Check for existence of directories
                        CheckDirectories(SFTPsession,remoteFolderPathFixed);
                        if (SFTPsession.FileExists(remoteFolderPathFixed))
                        {
                            // Prepare files for sync
                            string folderFilterFixed = lf.FolderFilter.Replace(";", ">{0}D;");
                            int daysAgo = (allFiles) ? monthsInterval : 2;
                            // Set filemask filter for synchronize
                            transferOptions.FileMask = (lf.FolderIncludeSub) ? string.Format(folderFilterFixed + @">{0}D;20[123][0-9][01][0-9][0-3][0-9]/|" + xmlFilter, daysAgo) : string.Format(folderFilterFixed + @">{0}D|*/;" + xmlFilter, daysAgo);
                            // Rename files to avoid repeated logs
                            if (lf.FolderID == "logs_log_dia")
                                NoLogsRepeated(SFTPsession, lf.FolderPath, lf.RemoteFolderPath);
                            // Synchronize files
                            SynchronizationResult synchronizationResult;
                            synchronizationResult = SFTPsession.SynchronizeDirectories(SynchronizationMode.Remote, folderPathFixed, remoteFolderPathFixed, false, false, SynchronizationCriteria.Either, transferOptions);
                            // Throw on any error
                            synchronizationResult.Check();
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                CustomLog.CustomLogEvent(string.Format("Synchronization failed: {0}", exc.Message));
                CustomLog.Error();
                return false;
            }
            return true;
        }
        public static bool ManualSync(List<CustomFolderSettings> listFolders, string dateCode)
        {
            try
            {
                using (Session SFTPsession = NewSession())
                {
                    // Open ssh connection for SFTP file transfer
                    SFTPsession.Open(sessionOptions);
                    // Transfer options for synchronization
                    TransferOptions transferOptions = NewTransferOptions();
                    string dateToSync = DateTime.Now.ToString("yyyy-MM-dd");
                    if (DateTime.TryParseExact(dateCode, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime dateTimeToSync))
                        dateToSync = dateTimeToSync.ToString("yyyy-MM-dd");
                    // Sync all folders to be monitored  
                    foreach (CustomFolderSettings lf in listFolders)
                    {
                        string remoteFolderPathFixed = remoteRootFolder + lf.RemoteFolderPath;
                        string folderPathFixed = (lf.FolderID == "logsxml_xml") ? lf.FolderPath + @"Enviados" : lf.FolderPath;
                        if (!Directory.Exists(folderPathFixed)) { continue; }
                        // Check for existence of directories
                        CheckDirectories(SFTPsession,remoteFolderPathFixed);
                        if (SFTPsession.FileExists(remoteFolderPathFixed))
                        {
                            string folderFilterFixed;
                            if (lf.FolderID == "boletas_bop" || lf.FolderID == "reprint_bop")
                            {
                                // Set filemask filter for synchronize
                                folderFilterFixed = lf.FolderFilter.Replace(";", "={0};");
                                transferOptions.FileMask = (lf.FolderIncludeSub) ? string.Format(folderFilterFixed + @"={0};{1}/", dateToSync, dateCode) : string.Format(folderFilterFixed + @"={0};|*/", dateToSync);
                            }
                            else
                            {
                                // Specific date in file name
                                folderFilterFixed = "";
                                foreach (string folderFilterPart in lf.FolderFilter.Split(';'))
                                    folderFilterFixed += $"*{dateCode}{folderFilterPart}>={dateToSync};";
                                // Set filemask filter for synchronize
                                transferOptions.FileMask = (lf.FolderIncludeSub) ? string.Format(folderFilterFixed + @"{0}/|" + xmlFilter, dateCode) : folderFilterFixed + @"|*/;" + xmlFilter;
                            }
                            // Rename files to avoid repeated logs
                            if (lf.FolderID == "logs_log_dia")
                                NoLogsRepeated(SFTPsession, lf.FolderPath, lf.RemoteFolderPath);
                            // Synchronize files
                            SynchronizationResult synchronizationResult;
                            synchronizationResult = SFTPsession.SynchronizeDirectories(SynchronizationMode.Remote, folderPathFixed, remoteFolderPathFixed, false, false, SynchronizationCriteria.Either, transferOptions);
                            // Throw on any error
                            synchronizationResult.Check();
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                CustomLog.CustomLogEvent(string.Format("Synchronization failed: {0}", exc.Message));
                CustomLog.Error();
                return false;
            }
            return true;
        }
        public static bool PullFiles(string localFolderPath, string remoteFolderPath, string folderFilter, string[] remotePos)
        {
            try
            {
                using (Session SFTPsession = NewSession())
                {
                    // Open ssh connection for SFTP file transfer
                    SFTPsession.Open(sessionOptions);
                    // Transfer options for pulling files
                    TransferOptions transferOptions = NewTransferOptions();
                    foreach (string posNum in remotePos)
                    {
                        string remoteGenericFolderPath = string.Format(remoteFolderPath, posNum);
                        string remoteFolderPathFixed = remoteRootFolder + remoteGenericFolderPath;
                        // Check for existence of directories
                        if (SFTPsession.FileExists(remoteFolderPathFixed))
                        {
                            // Pull files to directory
                            RemoteDirectoryInfo remoteDirectory = SFTPsession.ListDirectory(remoteFolderPathFixed);
                            foreach (RemoteFileInfo fileInfo in remoteDirectory.Files)
                            {
                                bool containsExtension = false;
                                string[] filterList = folderFilter.Split(';');
                                foreach (string individualFilter in filterList)
                                {
                                    if (fileInfo.Name.Contains(individualFilter.Replace(@"*", "")))
                                        containsExtension = true;
                                }
                                if (!fileInfo.IsDirectory && containsExtension)
                                {
                                    SFTPsession.GetFileToDirectory(remoteFolderPathFixed + fileInfo.Name, localFolderPath, false, transferOptions);
                                    string dateFolder = FileStorage.GetDateFolder(fileInfo.Name);
                                    string targetFile = remoteFolderPathFixed + dateFolder + @"/" + fileInfo.Name;
                                    CheckDirectories(SFTPsession,remoteFolderPathFixed + dateFolder);
                                    if (SFTPsession.FileExists(targetFile))
                                        SFTPsession.RemoveFile(remoteFolderPathFixed + fileInfo.Name);
                                    else
                                        SFTPsession.MoveFile(remoteFolderPathFixed + fileInfo.Name, targetFile);
                                    CustomLog.FileTransferred(fileInfo.Name);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                CustomLog.CustomLogEvent(string.Format("Pulling files failed: {0}", exc.Message));
                CustomLog.Error();
                return false;
            }
            return true;
        }
        public static bool PullFiles(List<CustomStorageSettings> listFolders, string[] remotePos)
        {
            try
            {
                using (Session SFTPsession = NewSession())
                {
                    // Open ssh connection for SFTP file transfer
                    SFTPsession.Open(sessionOptions);
                    // Transfer options for pulling files
                    TransferOptions transferOptions = NewTransferOptions();
                    // Sync all folders to be monitored  
                    foreach (CustomStorageSettings lf in listFolders)
                    {
                        foreach (string posNum in remotePos)
                        {
                            string remoteGenericFolderPath = string.Format(lf.RemoteFolderPath, posNum);
                            string remoteFolderPathFixed = remoteRootFolder + remoteGenericFolderPath;
                            // Check for existence of directories
                            if (SFTPsession.FileExists(remoteFolderPathFixed))
                            {
                                // Pull files to directory
                                RemoteDirectoryInfo remoteDirectory = SFTPsession.ListDirectory(remoteFolderPathFixed);
                                foreach (RemoteFileInfo fileInfo in remoteDirectory.Files)
                                {
                                    bool containsExtension = false;
                                    string[] filterList = lf.FolderFilter.Split(';');
                                    foreach (string individualFilter in filterList)
                                    {
                                        if (fileInfo.Name.Contains(individualFilter.Replace(@"*", "")))
                                            containsExtension = true;
                                    }
                                    if (!fileInfo.IsDirectory && containsExtension)
                                    {
                                        SFTPsession.GetFileToDirectory(remoteFolderPathFixed + fileInfo.Name, lf.FolderPath, false, transferOptions);
                                        string dateFolder = FileStorage.GetDateFolder(fileInfo.Name);
                                        string targetFile = remoteFolderPathFixed + dateFolder + @"/" + fileInfo.Name;
                                        CheckDirectories(SFTPsession,remoteFolderPathFixed + dateFolder);
                                        if (SFTPsession.FileExists(targetFile))
                                            SFTPsession.RemoveFile(remoteFolderPathFixed + fileInfo.Name);
                                        else
                                            SFTPsession.MoveFile(remoteFolderPathFixed + fileInfo.Name, targetFile);
                                        CustomLog.FileTransferred(fileInfo.Name);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                CustomLog.CustomLogEvent(string.Format("Pulling files failed: {0}", exc.Message));
                CustomLog.Error();
                return false;
            }
            return true;
        }
        private static bool DownloadFile(Session SFTPsession, string remoteFile, string localPath)
        {
            // Transfer options for downloading files
            TransferOptions transferOptions = NewTransferOptions();
            if (SFTPsession.FileExists(remoteFile))
                SFTPsession.GetFileToDirectory(remoteFile, localPath, false, transferOptions);
            else
            {
                CustomLog.CustomLogEvent(string.Format("Remote file {0} does not exist", remoteFile));
                CustomLog.Error();
                return false;
            }
            return true;
        }
        public static bool DownloadFiles(string[] remoteFiles, string localPath)
        {
            try
            {
                using (Session SFTPsession = NewSession())
                {
                    // Open ssh connection for SFTP file transfer
                    SFTPsession.Open(sessionOptions);
                    // Transfer options for downloading files
                    TransferOptions transferOptions = NewTransferOptions();
                    foreach (string remoteFile in remoteFiles)
                    {
                        if (SFTPsession.FileExists(remoteFile))
                            SFTPsession.GetFileToDirectory(remoteFile, localPath, false, transferOptions);
                        else
                        {
                            CustomLog.CustomLogEvent(string.Format("Remote file {0} does not exist", remoteFile));
                            CustomLog.Error();
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                CustomLog.CustomLogEvent(string.Format("Downloading file via SFTP failed: {0}", exc.Message));
                CustomLog.Error();
                return false;
            }
            return true;
        }
        public static bool DownloadUpdatePackage(string localPath, string remotePath)
        {
            try
            {
                using (Session SFTPsession = NewSession())
                {
                    // Open ssh connection for SFTP file transfer
                    SFTPsession.Open(sessionOptions);
                    // Transfer options for pulling files
                    TransferOptions transferOptions = NewTransferOptions();
                    string remoteFolderPath = remoteRootFolder + remotePath;
                    // Check for existence of directory path
                    if (SFTPsession.FileExists(remoteFolderPath))
                    {
                        // Check files in directory
                        IEnumerable<RemoteFileInfo> remoteDirectory = SFTPsession.EnumerateRemoteFiles(remoteFolderPath,"*.zip",EnumerationOptions.None);
                        foreach (RemoteFileInfo remoteFileInfo in remoteDirectory)
                        {
                            if (!remoteFileInfo.IsDirectory && remoteFileInfo.Name.Contains(".zip") && Path.GetFileNameWithoutExtension(remoteFileInfo.Name).Length == 22 && remoteFileInfo.Name.Contains("MX"))
                            {
                                SFTPsession.GetFileToDirectory(remoteFileInfo.FullName, localPath, false, transferOptions);
                                string remoteLogFile = remoteFolderPath + Path.GetFileNameWithoutExtension(remoteFileInfo.Name) + @".log";
                                if (SFTPsession.FileExists(remoteLogFile))
                                {
                                    SFTPsession.GetFileToDirectory(remoteLogFile, localPath, true, transferOptions);
                                    SFTPsession.RemoveFile(remoteFileInfo.FullName);
                                }
                            }
                        }
                    }
                    else
                    {
                        CustomLog.CustomLogEvent(string.Format("Remote path {0} does not exist",remoteFolderPath));
                        CustomLog.Error();
                    }
                }
            }
            catch (Exception exc)
            {
                CustomLog.CustomLogEvent(string.Format("Downloading update package failed: {0}", exc.Message));
                CustomLog.Error();
                return false;
            }
            return true;
        }
        private static void FileTransferred(object sender, TransferEventArgs e)
        {
            if (e.Error != null)
            {
                CustomLog.CustomLogEvent(string.Format("Transfer of {0} failed: {1}", e.FileName, e.Error));
                CustomLog.Error();
            }
            if (e.Chmod != null)
            {
                if (e.Chmod.Error != null)
                {
                    CustomLog.CustomLogEvent(string.Format("Setting permissions of {0} failed: {1}", e.Chmod.FileName, e.Chmod.Error));
                    CustomLog.Error();
                }
            }
            if (e.Touch != null)
            {
                if (e.Touch.Error != null)
                {
                    CustomLog.CustomLogEvent(string.Format("Setting timestamp of {0} failed: {1}", e.Touch.FileName, e.Touch.Error));
                    CustomLog.Error();
                }
            }
        }
        private static void CheckDirectories(Session SFTPsession, string mainPath)
        {
            if (SFTPsession.Opened)
                CreateDirectories(SFTPsession, mainPath);
        }
        private static void CreateDirectories(Session SFTPsession,string mainPath)
        {
            while (!SFTPsession.FileExists(mainPath))
            {
                string parentPath = Path.GetFullPath(Path.Combine(mainPath, "..")).Replace(@"\", @"/").Remove(0, 2);
                CreateDirectories(SFTPsession,parentPath);
                SFTPsession.CreateDirectory(mainPath);
            }
        }
        public static void NoLogsRepeated(Session SFTPsession, string localPath, string remotePath)
        {
            if (SFTPsession.Opened)
            {
                string remoteFolderPath = remoteRootFolder + remotePath;
                string todayFile = DateTime.Now.ToString("yyyyMMdd") + @".log";
                string completeRemotePath = remoteFolderPath + todayFile, completeLocalPath = localPath + todayFile;
                // Check if exists a file from later date
                if (SFTPsession.FileExists(completeRemotePath) && File.Exists(completeLocalPath))
                {
                    string tomorrowFile = DateTime.Now.AddDays(1).ToString("yyyyMMdd") + @".log";
                    if (SFTPsession.FileExists(remoteFolderPath + tomorrowFile))
                        RenameLogs(SFTPsession, completeRemotePath);
                    else
                    {
                        string logsPath = $"{AppDomain.CurrentDomain.BaseDirectory}{ConfigurationManager.AppSettings["LogsPath"]}";
                        DownloadFile(SFTPsession, completeRemotePath, logsPath);
                        string remoteFirstLine, localFirstLine = File.ReadLines(completeLocalPath).First();
                        if (File.Exists(logsPath + todayFile))
                        {
                            remoteFirstLine = File.ReadLines(logsPath + todayFile).First();
                            File.Delete(logsPath + todayFile);
                            if (remoteFirstLine != localFirstLine)
                                RenameLogs(SFTPsession, completeRemotePath);
                        }                        
                    }
                }
            }
        }
        private static void RenameLogs(Session SFTPsession, string fileName)
        {
            if (SFTPsession.Opened)
            {
                char alphabetLetter = 'A';
                while (SFTPsession.FileExists(fileName + @"_" + alphabetLetter))
                    alphabetLetter++;
                SFTPsession.MoveFile(fileName, fileName + @"_" + alphabetLetter);
            }
        }
        private static void QueryReceived(object sender, QueryReceivedEventArgs e)
        {
            CustomLog.CustomLogEvent("Query received: "+e.Message);
            e.Abort();
        }
    }
}
