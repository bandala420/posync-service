// Class for log file management
using System;
using System.Configuration;
using System.IO;

namespace POSync
{
    static class CustomLog
    {
        private static int err_count = 0;
        private static readonly string serviceLogPath = AppDomain.CurrentDomain.BaseDirectory + ConfigurationManager.AppSettings["LogsPath"] + "ServiceLog.log";
        private static readonly string uploadedFilesPath = AppDomain.CurrentDomain.BaseDirectory + ConfigurationManager.AppSettings["LogsPath"] + "UploadedFiles_{0}.txt";
        public static readonly string sessionLogPath = AppDomain.CurrentDomain.BaseDirectory + ConfigurationManager.AppSettings["LogsPath"] + "WinscpSessionLog.log";
        private static readonly string pmixLogPath = AppDomain.CurrentDomain.BaseDirectory + ConfigurationManager.AppSettings["LogsPath"] + "UploadedFiles_logsxml_xml_pmix.txt";
        private static readonly string serviceVersionPath = AppDomain.CurrentDomain.BaseDirectory + ConfigurationManager.AppSettings["VersionPath"] + "Service.txt";
        public static void Start()
        {
            ClearLogs();
            PrintServiceVersion();
            CustomLogEvent("<Service started> Oceano Digital - POSync "+typeof(Service1).Assembly.GetName().Version.ToString());
            CustomLogEvent(string.Format("Group: {0}", Path.GetFileNameWithoutExtension(AppInstaller.GetSshPrivateKeyFile())));
            CustomLogEvent(string.Format("Restaurant: MX{0}", ConfigurationManager.AppSettings["RestFolder"]));
            CustomLogEvent(string.Format("Device: {0} {1}", ConfigurationManager.AppSettings["Device"], ConfigurationManager.AppSettings["PosFolder"]));
            CustomLogEvent(string.Format("Public IP: {0}", AppInstaller.GetPublicIp()));
            CustomLogEvent(string.Format("Private IP: {0}", AppInstaller.GetPrivateIp()));
        }
        public static void Stop()
        {
            CustomLogEvent("<Service stopped> "+err_count.ToString()+ " error(s) detected" + Environment.NewLine + "<-------------------------------------------------------------------------------->");
        }
        public static void Error()
        {
            err_count += 1;
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
                Console.WriteLine("POSync Windows Service could not write into log file.\n" + exc.Message + "\n\nContact your administrator.");
            }
            ClearServiceLog();
        }
        public static void FileTransferred(string fullName,string folderID)
        {
            try
            {
                using (StreamWriter logWriter = File.AppendText(string.Format(uploadedFilesPath, folderID)))
                {
                    logWriter.WriteLine(fullName);
                    logWriter.Flush();
                    logWriter.Close();
                }
            }
            catch (IOException exc)
            {
                CustomLogEvent("POSync Windows Service could not write into uploaded register file: " + exc.Message + "\nContact your administrator.");
                Error();
            }
        }
        public static void FileTransferred(string[] fullNames, string folderID)
        {
            try
            {
                using (StreamWriter logWriter = File.AppendText(string.Format(uploadedFilesPath, folderID)))
                {
                    foreach (string fullName in fullNames)
                    {
                        logWriter.WriteLine(fullName);
                    }
                    logWriter.Flush();
                    logWriter.Close();
                }
            }
            catch (IOException exc)
            {
                CustomLogEvent("POSync Windows Service could not write into uploaded register file: " + exc.Message + "\nContact your administrator.");
                Error();
            }
        }
        public static void ClearLogs()
        {
            ClearSessionLog();
            ClearServiceLog();
        }
        private static void PrintServiceVersion()
        {
            try
            {
                File.WriteAllText(serviceVersionPath, typeof(Service1).Assembly.GetName().Version.ToString());
            }
            catch (IOException exc)
            {
                CustomLogEvent("POSync Windows Service could not write into version log file.\n" + exc.Message + "\nContact your administrator.");
                Error();
            }
        }
        private static void ClearServiceLog()
        {
            try
            {
                FileInfo logInfo = new FileInfo(serviceLogPath);
                while (logInfo.Exists && logInfo.Length > (0.5 * 1024 * 1024))    // 500kB max file size
                {
                    string[] lines = File.ReadAllLines(serviceLogPath);
                    string[] skippedLines = new string[lines.Length - 2500];
                    Array.Copy(lines, 2500, skippedLines, 0, lines.Length - 2500);
                    File.WriteAllLines(serviceLogPath, skippedLines);
                    logInfo = new FileInfo(serviceLogPath);
                }
            }
            catch (IOException exc)
            {
                Console.WriteLine("POSync Windows Service could not write into log file.\n" + exc.Message + "\nContact your administrator.");
            }
        }
        private static void ClearSessionLog()
        {
            try
            {
                File.WriteAllText(sessionLogPath, string.Empty);
                if (File.Exists(pmixLogPath))
                {
                    File.WriteAllText(pmixLogPath, string.Empty);
                }
            }
            catch (IOException exc)
            {
                CustomLogEvent("POSync Windows Service could not write into session log file.\n" + exc.Message + "\nContact your administrator.");
                Error();
            }
        }
        public static string GetLogPath()
        {
            return serviceLogPath;
        }
    }
}
