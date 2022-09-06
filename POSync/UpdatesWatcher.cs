// Updates watcher utility class
using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace POSync
{
    static class UpdatesWatcher
    {
        /// <summary>
        /// Update pos function
        /// </summary>
        public static void UpdatePos()
        {
            // Updates path variables
            string folderId = "POSyncUpdates";
            string driveLetter = AppInstaller.FindDirectory();
            string updatePath = string.Format(@"{0}{1}\", driveLetter,folderId);
            // Check if directory exists
            if (!Directory.Exists(updatePath))
                return;
            // current date
            DateTime nowDateTime = DateTime.Now;
            // Search for newpos update packages
            foreach (FileInfo zipFile in new DirectoryInfo(updatePath).GetFiles("*.zip", SearchOption.TopDirectoryOnly).OrderBy(f=>f.CreationTime))
            {
                // Update timestamp
                string updateFileNameNoExt = Path.GetFileNameWithoutExtension(zipFile.Name);
                string updateCode = updateFileNameNoExt.Substring(Math.Max(0, updateFileNameNoExt.Length - 14));
                if (!DateTime.TryParseExact(updateCode, "yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime updateTimestamp))
                    updateTimestamp = DateTime.Now;
                if (DateTime.Compare(nowDateTime, updateTimestamp) < 0)
                    continue;
                // Decompress update package
                AppInstaller.Unzip(zipFile.FullName, updatePath);
                // Search files in package
                foreach (FileInfo updateFile in new DirectoryInfo(updatePath).GetFiles("*.xml", SearchOption.AllDirectories))
                {
                    string targetFile = updateFile.FullName.Replace($@"{folderId}\", "");
                    // Copy - Overwrite update files
                    try { File.Copy(updateFile.FullName, targetFile, true); }
                    catch (Exception exc)
                    {
                        CustomLog.CustomLogEvent((string.Format("Error updating file {0}: {1}", targetFile, exc.Message)));
                        CustomLog.Error();
                    }
                }
                // Delete decompressed file
                try { Directory.Delete(updatePath + @"Newpos", true); }
                catch (Exception exc)
                {
                    CustomLog.CustomLogEvent((string.Format("Error deleting decompressed update package: {0}", exc.Message)));
                    CustomLog.Error();
                }
                // Mark package as update applied
                UpdateApplied(zipFile);
            }
            // Clean up updates directory
            CleanUpdatesPath(driveLetter);
        }
        /// <summary>
        /// Notify update and move package file
        /// </summary>
        /// <param name="updateFile">Update package FileInfo</param>
        private static void UpdateApplied(FileInfo updateFile)
        {
            string appliedFolder = Path.Combine(updateFile.DirectoryName,"applied");
            // for the record
            CustomLog.CustomLogEvent(string.Format("Update applied: {0}", updateFile.Name));
            // Create folder if it does not exist
            if (!Directory.Exists(appliedFolder))
            {
                try { Directory.CreateDirectory(appliedFolder); }
                catch (Exception exc)
                {
                    MessageBox.Show(string.Format("Error creating folder {0} : {1}", appliedFolder, exc.Message), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            // move update package to applied updates folder
            try {
                File.Copy(updateFile.FullName,Path.Combine(appliedFolder,updateFile.Name),true);
                File.Delete(updateFile.FullName);
            }
            catch (Exception exc)
            {
                MessageBox.Show(string.Format("Error moving update package {0} : {1}", updateFile.Name, exc.Message), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            // remove old files from folder
            foreach (var fi in new DirectoryInfo(appliedFolder).GetFiles().OrderByDescending(x => x.LastWriteTime).Skip(40))
                fi.Delete();
        }
        /// <summary>
        /// Delete all files and directories in updates path
        /// </summary>
        /// <param name="driveLetter">Drive letter volume where POS system is installed</param>
        private static void CleanUpdatesPath(string driveLetter)
        {
            // remove all files and directories in updates path
            string updatesPath = string.Format(@"{0}NewPos\Updates", driveLetter);
            DirectoryInfo di;
            if (Directory.Exists(updatesPath))
            {
                di = new DirectoryInfo(updatesPath);
                foreach (FileInfo file in di.EnumerateFiles())
                    file.Delete();
                foreach (DirectoryInfo dir in di.EnumerateDirectories())
                    dir.Delete(true);
            }
            // remove all files and directories in applied updates path
            updatesPath = string.Format(@"{0}NewPos\Updates Applied", driveLetter);
            if (Directory.Exists(updatesPath))
            {
                di = new DirectoryInfo(updatesPath);
                foreach (FileInfo file in di.EnumerateFiles())
                    file.Delete();
                foreach (DirectoryInfo dir in di.EnumerateDirectories())
                    dir.Delete(true);
            }
        }
    }
}
