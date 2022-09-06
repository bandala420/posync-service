// Class for query functions on sql server
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Xml.Serialization;

namespace POSync
{
    static class ServerQuery
    {
        // Export data tables from sql server and upload them to server
        public static bool UploadTables(int daysBefore)
        {
            var outputDataList = new List<string>();
            var filesToUpload = new List<string>();
            var foldersToUpload = new List<string>();
            // Get sql server query configuration
            ServerQuerySettings serverQuerySettings = QuerySettings();
            if (serverQuerySettings==null) { return false; }
            if (serverQuerySettings.QuerySettings==null) { return true; }
            if (serverQuerySettings.QuerySettings.Length==0) { return true; }
            // Generate export data parameters
            string serverInstance,cmdQuery, outputFile, remoteFolder;
            foreach (QuerySettings querySettings in serverQuerySettings.QuerySettings)
            {
                if (daysBefore == 0)
                {
                    cmdQuery = $"use {querySettings.Table.Split('.')[0]} declare @daysago datetime declare @now datetime set @now = getdate() set @daysago = dateadd(day, -{querySettings.DaysBefore}, @now) SELECT {querySettings.Columns} FROM {querySettings.Table} {querySettings.Conditional}";
                    outputFile = AppDomain.CurrentDomain.BaseDirectory + ConfigurationManager.AppSettings["LogsPath"] + querySettings.FileName + "_" + ConfigurationManager.AppSettings["RestFolder"] + "_" + DateTime.Now.ToString("yyyy-MM-dd") + ".csv";
                }
                else
                {
                    cmdQuery = $"use {querySettings.Table.Split('.')[0]} declare @daysago datetime declare @now datetime set @now = getdate() set @daysago = dateadd(day, -{daysBefore}, @now) SELECT {querySettings.Columns} FROM {querySettings.Table} {querySettings.Conditional}";
                    outputFile = AppDomain.CurrentDomain.BaseDirectory + ConfigurationManager.AppSettings["LogsPath"] + querySettings.FileName + "_" + ConfigurationManager.AppSettings["RestFolder"] + "_MANUAL.csv";
                }
                outputDataList.Add($"{querySettings.Instance}|{cmdQuery}|{outputFile}|{querySettings.RemoteFolder}");
            }
            // Clean up previous files
            AppInstaller.DeleteTmpFiles(AppDomain.CurrentDomain.BaseDirectory + ConfigurationManager.AppSettings["LogsPath"], "*.gz", ".gz");
            // Export data and compress files
            int exitCode = 1;
            foreach (string outputData in outputDataList)
            {
                string[] outputDataParts = outputData.Split('|');
                serverInstance = outputDataParts[0];
                cmdQuery = outputDataParts[1];
                outputFile = outputDataParts[2];
                remoteFolder = outputDataParts[3];
                exitCode = ExecuteQuery(serverInstance,cmdQuery, outputFile);
                if (File.Exists(outputFile) && exitCode == 0)
                {
                    string comOutputFile = outputFile + ".gz";
                    AppInstaller.Gzip(outputFile, comOutputFile);
                    if (File.Exists(comOutputFile))
                    {
                        filesToUpload.Add(comOutputFile);
                        foldersToUpload.Add(remoteFolder);
                    }
                    File.Delete(outputFile);
                }
                else
                {
                    File.Delete(outputFile);
                    CustomLog.CustomLogEvent(string.Format("Error exporting table {0}",Path.GetFileName(outputFile)));
                    CustomLog.Error();
                    break;
                }
            }
            if (exitCode == 0)
            {
                // Upload data files to server
                int attempts = Settings.serviceSettings.AttemptsSession;
                for (int i = 0; i < attempts && !FileTransfer.UploadFiles(filesToUpload.ToArray(), foldersToUpload.ToArray(),true); i++) { System.Threading.Thread.Sleep((int)TimeSpan.FromSeconds(30).TotalMilliseconds); }
            }
            // Delete all data files
            foreach (string comOutputFile in filesToUpload)
                if (File.Exists(comOutputFile)) { File.Delete(comOutputFile); }
            // Log exit code from sql query
            CustomLog.CustomLogEvent(string.Format("Server query exit code: {0}", exitCode));
            return (exitCode == 0);
        }
        // <summary>Executes sql command and generate output file</summary>
        private static int ExecuteQuery(string serverInstance,string cmdQuery, string outputFile)
        {
            try
            {
                ProcessStartInfo processInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/C sqlcmd -b -S .\\{serverInstance} -Q \"{cmdQuery}\" -o \"{outputFile}\" -h-1 -s\"|\" -W",
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process queryProcess = Process.Start(processInfo);
                queryProcess.WaitForExit();
                return queryProcess.ExitCode;
            }
            catch (Exception exc)
            {
                CustomLog.CustomLogEvent(string.Format("Error on SQL command: {0}", exc.Message));
                CustomLog.Error();
            }
            return 1;
        }
        private static ServerQuerySettings QuerySettings()
        {
            // Download configuration file from server
            string xmlConfigPath = AppDomain.CurrentDomain.BaseDirectory + @"config\ServerQueryConfig.xml";
            AppInstaller.ServerDownload("ServerQueryConfig.xml", xmlConfigPath, true);
            ServerQuerySettings serverQuerySettings = null;
            if (File.Exists(xmlConfigPath))
            {
                XmlSerializer deserializer = new XmlSerializer(typeof(ServerQuerySettings));
                try
                {
                    TextReader reader = new StreamReader(xmlConfigPath);
                    object obj = deserializer.Deserialize(reader);
                    reader.Close();
                    // return the server query settings parameters
                    serverQuerySettings = obj as ServerQuerySettings;
                }
                catch (IOException exc)
                {
                    CustomLog.CustomLogEvent(string.Format("Error reading configuration file {0}: {1}",Path.GetFileName(xmlConfigPath),exc.Message));
                    CustomLog.Error();
                }
                finally
                {
                    File.Delete(xmlConfigPath);
                }
            }
            return serverQuerySettings;
        }
    }
}
