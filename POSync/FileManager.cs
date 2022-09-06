using System;
using System.IO;
using System.Linq;
using System.Configuration;
using System.Collections.Generic;

namespace POSync
{
    static class FileManager
    {
        /// <summary>
        /// Poll directory for changes
        /// </summary>
        /// <param name="folderID">Copy folder id</param>
        /// <param name="folderPath">Main folder path</param>
        /// <param name="folderFilter">File filter</param>
        /// <param name="includeSub">Include subdirectories</param>
        /// <returns></returns>
        public static string[] PollDirectory(string folderID, string folderPath, string folderFilter, bool includeSub)
        {
            // Explode folderFilter for each extension
            string[] folderFilterParts = folderFilter.Split(';');
            List<string> allFiles = new List<string>();
            SearchOption searchOption = includeSub ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            foreach (string individualFilter in folderFilterParts)
            {
                List<string> filesInPath = new List<string>();
                try
                {
                    foreach (FileInfo fileInPath in new DirectoryInfo(folderPath).GetFiles(individualFilter, searchOption))
                        filesInPath.Add(string.Format("{0}|{1}", fileInPath.FullName, fileInPath.Length));
                }
                catch (IOException exc)
                {
                    CustomLog.CustomLogEvent("Error polling directory: " + exc.Message);
                    CustomLog.Error();
                    return TmpFiles(folderID, folderPath, new string[0]);
                }
                allFiles.AddRange(filesInPath);
            }
            // Store files information from folder path and compare files
            return CompareFiles(folderID, folderPath, allFiles.ToArray());
        }
        /// <summary>
        /// Poll directory for changes considering actual date
        /// </summary>
        /// <param name="folderID">Copy folder id</param>
        /// <param name="folderPath">Main folder path</param>
        /// <param name="folderFilter">File filter</param>
        /// <param name="todayFolder">File date code</param>
        /// <returns></returns>
        public static string[] PollDirectory(string folderID, string folderPath, string folderFilter, string todayFolder)
        {
            // Get folders modified in the past couple hours
            List<string> allFiles = new List<string>();
            double intervalTime = (double)Settings.serviceSettings.StatusCheckHoursInterval;
            foreach (string directoryPath in new DirectoryInfo(folderPath).GetDirectories().
                Where(f => DateTime.Compare(DateTime.Now.AddHours(-intervalTime), f.LastWriteTime) < 0 && !f.Name.Contains(todayFolder)).Select(f => f.FullName))
            {
                // Explode folderFilter for each extension
                string[] folderFilterParts = folderFilter.Split(';');
                foreach (string individualFilter in folderFilterParts)
                {
                    List<string> filesInPath = new List<string>();
                    try
                    {
                        foreach (FileInfo fileInPath in new DirectoryInfo(directoryPath).GetFiles(individualFilter, SearchOption.TopDirectoryOnly))
                            filesInPath.Add(string.Format("{0}|{1}", fileInPath.FullName, fileInPath.Length));
                    }
                    catch (IOException exc)
                    {
                        CustomLog.CustomLogEvent("Error polling directory: " + exc.Message);
                        CustomLog.Error();
                        return TmpFiles(folderID, folderPath, new string[0]);
                    }
                    allFiles.AddRange(filesInPath);
                }
            }
            // Store files information from folder path and compare files
            return CompareFiles(folderID, folderPath, allFiles.ToArray());
        }
        /// <summary>
        /// Compare existing files with record
        /// </summary>
        /// <param name="folderID">Copy folders id</param>
        /// <param name="folderPath">Main fodler path</param>
        /// <param name="filesNames">Files name array</param>
        /// <returns></returns>
        private static string[] CompareFiles(string folderID, string folderPath, string[] filesNames)
        {
            // Get succesfully sent files
            string uploadedFilesPath = AppDomain.CurrentDomain.BaseDirectory + ConfigurationManager.AppSettings["LogsPath"] + "UploadedFiles_" + folderID + ".txt";
            string[] uploadedFiles = new string[0];
            if (File.Exists(uploadedFilesPath))
            {
                try { uploadedFiles = File.ReadAllLines(uploadedFilesPath).ToArray(); }
                catch (IOException exc)
                {
                    CustomLog.CustomLogEvent(string.Format("Error polling directory {0}: ", folderPath) + exc.Message);
                    CustomLog.Error();
                    return TmpFiles(folderID, folderPath, new string[0]);
                }
            }
            // Check if there is any difference
            string[] filesToCopy = filesNames.Except(uploadedFiles).ToArray();
            if (filesToCopy.Length > 0)
                return TmpFiles(folderID, folderPath, filesToCopy);
            else if ((filesNames.Length - uploadedFiles.Length) < 0)   // Condition if there are more files in register. Clean register
            {
                string[] uploadedFilesNew = filesNames.Intersect(uploadedFiles).ToArray();
                try { File.WriteAllLines(uploadedFilesPath, uploadedFilesNew); }
                catch (Exception exc)
                {
                    CustomLog.CustomLogEvent("Error writing into uploaded files register: " + exc.Message);
                    CustomLog.Error();
                }
            }
            return TmpFiles(folderID, folderPath, new string[0]);
        }
        /// <summary>
        /// Copy new files to backup folder, clean up folder and return all files in backup folder
        /// </summary>
        /// <param name="folderID">Copy folders id</param>
        /// <param name="folderPath">Main folder path</param>
        /// <param name="newFiles">Files to copy array</param>
        /// <returns></returns>
        private static string[] TmpFiles(string folderID, string folderPath, string[] newFiles)
        {
            string driveLetter = AppInstaller.FindDirectory();
            string folderName = Path.GetFileName(Path.GetDirectoryName(folderPath));
            string backupFolder = string.Format(@"{0}POSyncQueue\{1}", driveLetter, folderName);
            string[] originalFiles = (folderID == "logsxml_today" || folderID == "logsxml_regen") ? ExcludeXmlFiles(newFiles) : newFiles;
            if (!CreateTmpFolder(backupFolder))
                return new string[0];
            // Copy each file to temporary folder
            foreach (string originalFile in originalFiles)
            {
                string originalFileName = originalFile.Split('|')[0];
                string fileName = Path.GetFileName(originalFileName);
                string destFile = folderID != "queue_tlog" ? string.Format(@"{0}\{1}", backupFolder, fileName) : string.Format(@"{0}\{1}\{2}", backupFolder, Path.GetFileName(Path.GetDirectoryName(originalFileName)), fileName);
                string destFolder = Path.GetDirectoryName(destFile);
                try
                {
                    if (!Directory.Exists(destFolder)) { Directory.CreateDirectory(destFolder); }
                    if (!File.Exists(destFile)) { File.Copy(originalFileName, destFile); }
                    CustomLog.FileTransferred(originalFile, folderID);
                }
                catch (Exception exc)
                {
                    CustomLog.CustomLogEvent(string.Format("Error creating temporary file {0} for upload process: {1}", destFile, exc.Message));
                    CustomLog.Error();
                }
            }
            // Clean up folder to avoid overload
            AppInstaller.CleanDirectory(backupFolder);
            // Return all files in folder
            return Directory.GetFiles(backupFolder, "*.*", SearchOption.AllDirectories);
        }
        /// <summary>
        /// Copy new files to backup folder, clean up folder and return all files in backup folder
        /// </summary>
        /// <param name="folderPath">Main folder path</param>
        /// <param name="newFiles">Files to copy array</param>
        /// <returns></returns>
        public static string[] TmpFiles(string folderPath, string[] newFiles)
        {
            string driveLetter = AppInstaller.FindDirectory();
            string folderName = Path.GetFileName(Path.GetDirectoryName(folderPath));
            string backupFolder = string.Format(@"{0}POSyncQueue\{1}", driveLetter, folderName);
            if (!CreateTmpFolder(backupFolder))
                return new string[0];
            // Copy each file to temporary folder
            foreach (string originalFile in newFiles)
            {
                string originalFileName = originalFile.Split('|')[0];
                string fileName = Path.GetFileName(originalFileName);
                string destFile = string.Format(@"{0}\{1}", backupFolder, fileName);
                string destFolder = Path.GetDirectoryName(destFile);
                try
                {
                    if (!Directory.Exists(destFolder)) { Directory.CreateDirectory(destFolder); }
                    File.Copy(originalFileName, destFile);
                }
                catch (Exception exc)
                {
                    CustomLog.CustomLogEvent(string.Format("Error copying temporary file {0} for upload process: {1}", destFile, exc.Message));
                    CustomLog.Error();
                }
            }
            // Clean up folder to avoid overload
            AppInstaller.CleanDirectory(backupFolder);
            // Return all files in folder
            return Directory.GetFiles(backupFolder, "*.*", SearchOption.AllDirectories);
        }
        /// <summary>
        /// Copy new files to backup folder, clean up folder and return all files in backup folder
        /// </summary>
        /// <param name="folderPath">Main folder path</param>
        /// <returns></returns>
        public static string[] TmpFiles(string folderPath)
        {
            string driveLetter = AppInstaller.FindDirectory();
            string folderName = Path.GetFileName(Path.GetDirectoryName(folderPath));
            string backupFolder = string.Format(@"{0}POSyncQueue\{1}", driveLetter, folderName);
            if (!CreateTmpFolder(backupFolder))
                return new string[0];
            // Return all files in folder
            return Directory.GetFiles(backupFolder, "*.*", SearchOption.AllDirectories);
        }
        /// <summary>
        /// Create temportary folder
        /// </summary>
        /// <param name="folderPath">Folder path to create</param>
        /// <returns></returns>
        public static bool CreateTmpFolder(string folderPath)
        {
            try { if (!Directory.Exists(folderPath)) { Directory.CreateDirectory(folderPath); } }
            catch (IOException exc)
            {
                CustomLog.CustomLogEvent(string.Format("Error creating queue directory: {0}", exc.Message));
                CustomLog.Error();
                return false;
            }
            return true;
        }
        /// <summary>
        /// Exclude xml files from array
        /// </summary>
        /// <param name="filesArray">Array to search in</param>
        /// <returns></returns>
        private static string[] ExcludeXmlFiles(string[] filesArray)
        {
            List<string> cleanFiles = new List<string>();
            string[] filterList = Settings.serviceSettings.XmlExcludeList.Split(';');
            foreach (string fileName in filesArray)
            {
                bool flagSave = true;
                foreach (string xmlFilter in filterList)
                {
                    if (fileName.Contains(xmlFilter))
                    {
                        flagSave = false;
                        break;
                    }
                }
                if (flagSave)
                {
                    cleanFiles.Add(fileName);
                }
            }
            return cleanFiles.ToArray();
        }
    }
}
