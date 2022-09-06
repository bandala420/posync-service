using System;
using System.IO;
using System.Configuration;
using System.Threading.Tasks;

namespace POSync
{
    public static class Watcher
    {
        private static FileSystemWatcher watcher;
        private static object _threadLock;
        /// <summary>
        /// Initialize file watcher
        /// </summary>
        public static void Start(object mainLock)
        {
            string scheduleFile = Settings.serviceSettings.SchedulePath.Split('|')[0];
            if (File.Exists(scheduleFile))
            {
                watcher = new FileSystemWatcher(Path.GetDirectoryName(scheduleFile))
                {
                    NotifyFilter = NotifyFilters.Size
                };
                watcher.Changed += OnChanged;
                watcher.Filter = Path.GetFileName(scheduleFile);
                watcher.IncludeSubdirectories = false;
                watcher.EnableRaisingEvents = true;
                _threadLock = mainLock;
            }
            else
            {
                CustomLog.CustomLogEvent(string.Format("Schedule file {0} does not exist",scheduleFile));
                CustomLog.Error();
            }
        }
        /// <summary>
        /// Stop watcher and dispose object
        /// </summary>
        public static void Stop()
        {
            if (watcher != null)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
        }
        /// <summary>
        /// On change event handler
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void OnChanged(object sender, FileSystemEventArgs e)
        {
            string scheduleFile = e.FullPath;
            // Validate event type and file existence
            if (e.ChangeType != WatcherChangeTypes.Changed || !File.Exists(scheduleFile))
                return;
            // Start task for sync files. Avoid event buffer overflow
            Task.Factory.StartNew(() =>
            {
                // Initialize variables
                string remotePath = string.Empty;
                string restCode = ConfigurationManager.AppSettings["RestFolder"];
                string tmpFile = Path.Combine(Path.GetDirectoryName(scheduleFile), string.Format("MX{0}{1}.txt", restCode, DateTime.Now.ToString("yyyyMMddHHmmss")));
                // Get remote path
                try { remotePath = Settings.serviceSettings.SchedulePath.Split('|')[1]; } catch (Exception) { return; }
                // Read first line in modified file
                using (StreamReader streamReader = new StreamReader(File.Open(scheduleFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                {
                    try
                    {
                        string scheduleString = streamReader.ReadToEnd();
                        // Compare first line string
                        if (!scheduleString.Contains("HORARIO POR EMPLEADO"))
                            return;
                        // Copy temporary file
                        File.WriteAllText(tmpFile, scheduleString);
                    }
                    catch (Exception exc)
                    {
                        CustomLog.CustomLogEvent(string.Format("Error creating temporary schedule file {0}: {1}", Path.GetFileName(tmpFile), exc.Message));
                        CustomLog.Error();
                        File.Delete(tmpFile);
                        return;
                    }
                }    
                // Compress file
                string comFile = tmpFile + ".gz";
                AppInstaller.Gzip(tmpFile, comFile);
                File.Delete(tmpFile);
                // Upload file
                if (File.Exists(comFile))
                {
                    lock (_threadLock)
                    {
                        int attempts = Settings.serviceSettings.AttemptsSession;
                        for (int i = 0; !FileTransfer.UploadFiles(new string[] { comFile }, remotePath, true) && i <= attempts; i++)
                            System.Threading.Thread.Sleep((int)TimeSpan.FromSeconds(30).TotalMilliseconds);
                        File.Delete(comFile);
                    }
                }
            });
        }
    }
}
