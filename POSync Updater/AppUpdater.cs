using System;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace POSync_Updater
{
    static class AppUpdater
    {
        private static UpdaterSettings updaterSettings = new UpdaterSettings();
        private static System.Timers.Timer schTimer,errorTimer;
        private static readonly object _threadLock = new object();
        /// <summary>Initialize updater service processes</summary>
        public static void Start()
        {
            CustomLog.Start();
            // Serialize updater settings
            PopulateUpdaterSettings();
            // Start updater timer
            StartTimer();
            // Check for new updates
            UpdateService();
        }
        /// <summary>Stop all updater service processes</summary>
        public static void Stop()
        {
            if (schTimer!=null)
            {
                schTimer.Stop();
                schTimer.Enabled = false;
                schTimer.Dispose();
            }
            if (errorTimer != null)
            {
                errorTimer.Stop();
                errorTimer.Enabled = false;
                errorTimer.Dispose();
            }
        }
        /// <summary>Populate configuration parameters for updater service</summary>
        private static void PopulateUpdaterSettings()
        {
            // Download configuration file from server
            string xmlConfigPath = AppDomain.CurrentDomain.BaseDirectory + @"config\UpdaterSettings.xml";
            AppInstaller.ServerDownload("UpdaterSettings.xml", xmlConfigPath);
            if (File.Exists(xmlConfigPath))
            {
                XmlSerializer deserializer = new XmlSerializer(typeof(UpdaterSettings));
                try
                {
                    TextReader reader = new StreamReader(xmlConfigPath);
                    object obj = deserializer.Deserialize(reader);
                    // Close the TextReader object
                    reader.Close();
                    // Obtain the service settings parameters
                    updaterSettings = obj as UpdaterSettings;
                }
                catch (IOException exc)
                {
                    CustomLog.CustomLogEvent("Error reading updater configuration file: " + exc.Message);
                }
                finally
                {
                    File.Delete(xmlConfigPath);
                }
            }
        }
        private static void StartTimer()
        {
            double intervalTime = (double)updaterSettings.ServiceCheckHoursInterval;
            // Create a timer for update task
            schTimer = new System.Timers.Timer(TimeSpan.FromHours(intervalTime).TotalMilliseconds);
            // Hook up the Elapsed event for the timer with a lambda expression 
            schTimer.Elapsed += (sourceObj, elapsedEventArgs) => PeriodicRevision();
            schTimer.AutoReset = true;
            schTimer.Enabled = true;

            intervalTime = (double)updaterSettings.ErrorCheckMinutesInterval;
            // Create a timer for update task
            errorTimer = new System.Timers.Timer(TimeSpan.FromMinutes(intervalTime).TotalMilliseconds);
            // Hook up the Elapsed event for the timer with a lambda expression 
            errorTimer.Elapsed += (sourceObj, elapsedEventArgs) => PeriodicErrorRevision(intervalTime);
            errorTimer.AutoReset = true;
            errorTimer.Enabled = true;
        }
        private static void PeriodicRevision()
        {
            lock (_threadLock)
            {
                //Check if there is any update
                UpdateService();
                // Check if POSync service is running
                AppInstaller.StartService("POSync");
            }
        }
        /// <summary>
        /// Update main service binary file
        /// </summary>
        private static void UpdateService()
        {
            string newPosyncPath = AppDomain.CurrentDomain.BaseDirectory + @"version\POSync.exe";
            FileInfo fileInfo = null;
            try{ fileInfo = new FileInfo(newPosyncPath); }
            catch (Exception exc){ CustomLog.CustomLogEvent("Error obtaining binary file information: " + exc.Message); }
            if (fileInfo!=null)
            {
                if (fileInfo.Exists && fileInfo.Length > 75000)
                {
                    string serviceName = "POSync";
                    string oldPosyncPath = AppDomain.CurrentDomain.BaseDirectory + @"POSync.exe",
                        newWinscpPath = AppDomain.CurrentDomain.BaseDirectory + @"version\WinSCP.exe";
                    AppInstaller.StopService(serviceName);
                    try
                    {
                        File.Delete(oldPosyncPath);
                        File.Move(newPosyncPath, oldPosyncPath);
                        fileInfo = new FileInfo(newWinscpPath);
                        if (fileInfo.Exists && fileInfo.Length > 1)
                        {
                            string oldWinscpPath = AppDomain.CurrentDomain.BaseDirectory + @"WinSCP.exe",
                                newDllPath = AppDomain.CurrentDomain.BaseDirectory + @"version\WinSCPnet.dll",
                                oldDllPath = AppDomain.CurrentDomain.BaseDirectory + @"WinSCPnet.dll";
                            File.Delete(oldWinscpPath);
                            File.Delete(oldDllPath);
                            File.Move(newWinscpPath, oldWinscpPath);
                            File.Move(newDllPath, oldDllPath);
                        }
                        CustomLog.CustomLogEvent("<POSync service has been updated>");
                    }
                    catch (IOException exc)
                    {
                        CustomLog.CustomLogEvent("Error updating POSync service: " + exc.Message);
                    }
                    finally
                    {
                        AppInstaller.StartService(serviceName);
                    }
                }
            }
        }
        /// <summary>Check if synchronization service is stuck or for any fatal errors in session log </summary>
        private static void PeriodicErrorRevision(double intervalTime)
        {
            lock (_threadLock)
            {
                string sessionLogPath = AppDomain.CurrentDomain.BaseDirectory + @"logs\WinscpSessionLog.log";
                if (File.Exists(sessionLogPath))
                {
                    intervalTime = 2 * intervalTime;
                    DateTime lastWriteTime = File.GetLastWriteTime(sessionLogPath);
                    if (DateTime.Compare(lastWriteTime, DateTime.Now.AddMinutes(-intervalTime)) <= 0)
                    {
                        string sessionLogLastLine = "";
                        try { sessionLogLastLine = File.ReadLines(sessionLogPath).Last(); }
                        catch (IOException exc)
                        {
                            CustomLog.CustomLogEvent(string.Format("Error reading session file: {0}",exc.Message));
                        }
                        if (sessionLogLastLine.Contains("Script: Terminated by user") || sessionLogLastLine.Contains("Script: Network error"))
                        {
                            CustomLog.CustomLogEvent(string.Format("Internal error ({0})", sessionLogLastLine));
                            RestartService();
                        }
                        else if (DateTime.Compare(lastWriteTime, DateTime.Now.AddHours(-6)) < 0)
                            RestartService();
                    }
                }
            }
        }
        private static void RestartService()
        {
            string serviceName = "POSync";
            CustomLog.CustomLogEvent("Restarting synchronization service...");
            AppInstaller.StopService(serviceName);
            AppInstaller.StartService(serviceName);
        }
    }
}
