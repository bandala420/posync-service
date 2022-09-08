// Class for log file management
using System;
using System.IO;
using System.Linq;

namespace POSync_Updater
{
    static class CustomLog
    {
        private static readonly string serviceLogPath = AppDomain.CurrentDomain.BaseDirectory + @"logs\" + "ServiceLog.log";
        private static readonly string updaterVersionPath = AppDomain.CurrentDomain.BaseDirectory + @"version\" + "Updater.txt";
        public static void Start()
        {
            try
            {
                File.WriteAllText(updaterVersionPath, typeof(Service1).Assembly.GetName().Version.ToString());
            }
            catch (IOException exc)
            {
                CustomLogEvent("POSync Updater Service could not write into version file.\n" + exc.Message);
            }
        }
        // Write process information into log file
        public static void CustomLogEvent(string logEvent)
        {
            string date = DateTime.Now.ToString("[dd-MM-yyyy HH:mm:ss] ");
            try
            {
                using (StreamWriter logWriter = File.AppendText(serviceLogPath))
                {
                    logWriter.WriteLine(date+logEvent);
                    logWriter.Flush();
                    logWriter.Close();
                }
            }
            catch (IOException exc)
            {
                Console.WriteLine("POSync Updater Service could not write into log file.\n" + exc.Message + "\n\nContact your administrator.");
            }
            ClearServiceLog();
        }
        private static void ClearServiceLog()
        {
            try
            {
                FileInfo logInfo = new FileInfo(serviceLogPath);
                while (logInfo.Exists && logInfo.Length > (0.5 * 1024 * 1024))    // 500kB max file size
                {
                    string[] lines = File.ReadLines(serviceLogPath).Skip(2500).ToArray();
                    File.WriteAllLines(serviceLogPath, lines);
                    logInfo = new FileInfo(serviceLogPath);
                }
            }
            catch (IOException exc)
            {
                Console.WriteLine("POSync Windows Service could not write into log file.\n" + exc.Message + "\nContact your administrator.");
            }
        }
    }
}
