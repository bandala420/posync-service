// File transfer class to manipulate WinSCP package
// classes parameters and methods
using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;

namespace POSync
{
    static class FileTransfer
    {
        private static string hostName,userName,sshPrivateKeyPath,remoteRootFolder,xmlFilter;
        private static int daysInterval, monthsInterval, attempts, processTimeout;
        private static readonly Process sftpProcess = new Process();
        public static void InitializeSession()
        {
            string sshPrivateKeyFile = AppInstaller.GetSshPrivateKeyFile();
            hostName = "gpomonse.dyndns.org";
            userName = Path.GetFileNameWithoutExtension(sshPrivateKeyFile);
            sshPrivateKeyPath = sshPrivateKeyFile;
            daysInterval = Settings.serviceSettings.SyncDaysInterval;
            monthsInterval = Settings.serviceSettings.InitialSyncMonthsInterval*30;
            attempts = Settings.serviceSettings.AttemptsSession;
            processTimeout = (int)TimeSpan.FromMinutes((double)Settings.serviceSettings.SessionTimeout).TotalMilliseconds;
            remoteRootFolder = Settings.serviceSettings.RemoteRoot + ConfigurationManager.AppSettings["RestFolder"];
            sftpProcess.StartInfo.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;
            sftpProcess.StartInfo.UseShellExecute = false;
            sftpProcess.StartInfo.RedirectStandardOutput = true;
            sftpProcess.StartInfo.CreateNoWindow = true;
            sftpProcess.EnableRaisingEvents = true;
            sftpProcess.StartInfo.FileName = "WinSCP.com";
            sftpProcess.ErrorDataReceived += new DataReceivedEventHandler(CmdError);
            string[] xmlFileFilters = Settings.serviceSettings.XmlExcludeList.Split(';');
            foreach (string xmlFileFilter in xmlFileFilters)
            {
                xmlFilter = xmlFilter + @"*" + xmlFileFilter + @"*;";
            }
        }
        public static int UploadFiles(string[] filesToUpload, string remoteFolderPath, bool removeFiles)
        {
            // Clear service logs 
            CustomLog.ClearLogs();
            string deleteSwitch = (removeFiles) ? "-delete" : "";
            List<string> scriptText = new List<string>
            {
                "option batch abort",
                "option confirm off",
                "option reconnecttime 40",
                "open sftp://" + userName + "@" + hostName + "/ -privatekey=\"" + sshPrivateKeyPath + "\" -hostkey=\"ssh-rsa 2048 5c:ad:f6:08:d0:8b:2b:0e:2a:00:03:02:cc:a2:96:25\" -timeout=10"
            };
            string remoteFolderPathFixed = remoteRootFolder + remoteFolderPath;
            scriptText.Add("cd " + remoteFolderPathFixed);
            // Make script commands file for winscp process
            foreach (string fileName in filesToUpload)
            {
                if (File.Exists(fileName))
                    scriptText.Add("put "+deleteSwitch+" \"" + fileName + "\"");
            }
            scriptText.Add("close");
            scriptText.Add("exit");
            WriteScript(scriptText.ToArray());

            // MS-DOS commands to be executed
            string cmdArgs = "/script=\"WinscpScript.txt\" /log=\"" + CustomLog.sessionLogPath + "\"";
            // Check for existence of directories
            CheckDirectories(remoteFolderPathFixed);
            string outputCmd = string.Empty;
            try
            {
                outputCmd = RunTransfer(cmdArgs);
            }
            catch (Exception exc)
            {
                CustomLog.CustomLogEvent(string.Format("Upload failed: {0}", exc.Message));
                CustomLog.Error();
            }
            finally
            {
                ExitScript();
            }
            return sftpProcess.ExitCode;
        }
        public static int UploadFiles(string[] filesToUpload, string[] foldersToUpload, bool removeFiles)
        {
            // Clear service logs 
            CustomLog.ClearLogs();
            string deleteSwitch = (removeFiles) ? "-delete" : "";
            List<string> scriptText = new List<string>
            {
                // Make script commands file for winscp process
                "option batch abort",
                "option confirm off",
                "option reconnecttime 40",
                "open sftp://" + userName + "@" + hostName + "/ -privatekey=\"" + sshPrivateKeyPath + "\" -hostkey=\"ssh-rsa 2048 5c:ad:f6:08:d0:8b:2b:0e:2a:00:03:02:cc:a2:96:25\" -timeout=10"
            };
            for (int i = 0; i < filesToUpload.Length; i++)
            {
                if (File.Exists(filesToUpload[i]))
                    scriptText.Add("put "+deleteSwitch+" \"" + filesToUpload[i] + "\" \"" + remoteRootFolder + foldersToUpload[i]+"/\"");
            }
            scriptText.Add("close");
            scriptText.Add("exit");
            WriteScript(scriptText.ToArray());
            // Check for existence of directories
            ArrayList noRepeatArray = new ArrayList();
            foreach (string folderToUpload in foldersToUpload)
            {
                if (!noRepeatArray.Contains(folderToUpload))
                {
                    CheckDirectories(remoteRootFolder + folderToUpload);
                    noRepeatArray.Add(folderToUpload);
                }
            }
            // MS-DOS commands to be executed
            string cmdArgs = "/script=\"WinscpScript.txt\" /log=\"" + CustomLog.sessionLogPath + "\"";
            string outputCmd = string.Empty;
            try
            {
                outputCmd = RunTransfer(cmdArgs);
            }
            catch (Exception exc)
            {
                CustomLog.CustomLogEvent(string.Format("Upload failed: {0}", exc.Message));
                CustomLog.Error();
            }
            finally
            {
                ExitScript();
            }
            return sftpProcess.ExitCode;
        }
        public static int SyncFiles(string localFolderPath,string remoteFolderPath,string folderFilter,bool folderIncludeSub)
        {
            // Clear service logs 
            CustomLog.ClearLogs();
            // Sync parameters
            string folderFilterFixed = folderFilter.Replace(";", ">{0}D;");
            string remoteFolderPathFixed = remoteRootFolder + remoteFolderPath;
            string fileMask = (folderIncludeSub) ? string.Format(folderFilterFixed + @">{0}D;20[123][0-9][01][0-9][0-3][0-9]/|" + xmlFilter, daysInterval) : string.Format(folderFilterFixed + ">{0}D|*/;" + xmlFilter, daysInterval);
            // MS-DOS commands to be executed
            string cmdArgs = "/C /command \"option batch abort\" \"option confirm off\" \"option reconnecttime 40\" \"open sftp://" + userName + "@" + hostName + "/ -privatekey=\"\"" + sshPrivateKeyPath + "\"\" -hostkey=\"\"ssh-rsa 2048 5c:ad:f6:08:d0:8b:2b:0e:2a:00:03:02:cc:a2:96:25\"\" -timeout=10 \" " +
                "\"synchronize -filemask=" + fileMask+ " remote \"\""+localFolderPath+"\"\" \"\""+remoteFolderPathFixed+"\"\"\" " +
                "\"close\" \"exit\" /log=\""+CustomLog.sessionLogPath+"\"";
            // Check for existence of directories
            CheckDirectories(remoteFolderPathFixed);
            string outputCmd = string.Empty;
            try
            {
                RunSynchronization(cmdArgs);
            }
            catch (Exception exc)
            {
                CustomLog.CustomLogEvent(string.Format("Synchronization failed: {0}", exc.Message));
                CustomLog.Error();
            }
            finally
            {
                ExitScript();
            }
            return sftpProcess.ExitCode;
        }
        public static int ManualSync(List<CustomFolderSettings> listFolders,bool allFiles)
        {
            // Clear service logs 
            CustomLog.ClearLogs();
            int monthsIntervalFixed = (allFiles) ? monthsInterval : 2;
            foreach (CustomFolderSettings lf in listFolders)
            {
                string folderPathFixed = (lf.FolderID == "logsxml_xml") ? lf.FolderPath + @"Enviados" : lf.FolderPath;
                if (!Directory.Exists(folderPathFixed)) { continue; }
                // Rename files to avoid repeated logs
                if (lf.FolderID == "logs_log_dia")
                    NoLogsRepeated(lf.FolderPath, lf.RemoteFolderPath);
                List<string> scriptText = new List<string>
                {
                    // Make script commands file for winscp process
                    "option batch abort",
                    "option confirm off",
                    "option reconnecttime 40",
                    "open sftp://" + userName + "@" + hostName + "/ -privatekey=\"" + sshPrivateKeyPath + "\" -hostkey=\"ssh-rsa 2048 5c:ad:f6:08:d0:8b:2b:0e:2a:00:03:02:cc:a2:96:25\" -timeout=10"
                };
                // Sync parameters
                string folderFilterFixed = lf.FolderFilter.Replace(";", ">{0}D;");
                string remoteFolderPathFixed = remoteRootFolder + lf.RemoteFolderPath;
                string fileMask = (lf.FolderIncludeSub) ? string.Format(folderFilterFixed + @">{0}D;20[123][0-9][01][0-9][0-3][0-9]/|" + xmlFilter, monthsIntervalFixed) : string.Format(folderFilterFixed + ">{0}D|*/;" + xmlFilter, monthsIntervalFixed);
                scriptText.Add("synchronize -filemask=" + fileMask + " remote \"" + folderPathFixed + "\" \"" + remoteFolderPathFixed + "\"");
                scriptText.Add("close");
                scriptText.Add("exit");
                WriteScript(scriptText.ToArray());
                // Check directories existence
                CheckDirectories(remoteFolderPathFixed);
                // MS-DOS commands to be executed
                string cmdArgs = "/script=\"WinscpScript.txt\" /log=\"" + CustomLog.sessionLogPath + "\"";
                string outputCmd = string.Empty;
                try
                {
                    RunSynchronization(cmdArgs);
                }
                catch (Exception exc)
                {
                    CustomLog.CustomLogEvent(string.Format("Synchronization failed: {0}", exc.Message));
                    CustomLog.Error();
                }
                finally
                {
                    ExitScript();
                }
            }
            return sftpProcess.ExitCode;
        }
        public static int ManualSync(List<CustomFolderSettings> listFolders, string dateCode)
        {
            // Clear service logs 
            CustomLog.ClearLogs();
            string dateToSync = DateTime.ParseExact(dateCode, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture).ToString("yyyy-MM-dd");
            foreach (CustomFolderSettings lf in listFolders)
            {
                // Prepare parameters variables for sync
                string folderFilterFixed, fileMask;
                string folderPathFixed = (lf.FolderID == "logsxml_xml") ? lf.FolderPath + @"Enviados" : lf.FolderPath;
                if (!Directory.Exists(folderPathFixed)) { continue; }
                string remoteFolderPathFixed = remoteRootFolder + lf.RemoteFolderPath;
                // Rename files to avoid repeated logs
                if (lf.FolderID == "logs_log_dia")
                    NoLogsRepeated(lf.FolderPath, lf.RemoteFolderPath);
                if (lf.FolderID=="boletas_bop" || lf.FolderID == "reprint_bop")
                {
                    // Set filemask filter for synchronize
                    folderFilterFixed = lf.FolderFilter.Replace(";", "={0};");
                    fileMask = (lf.FolderIncludeSub) ? string.Format(folderFilterFixed + @"={0};{1}/", dateToSync, dateCode) : string.Format(folderFilterFixed + @"={0};|*/", dateToSync);
                }
                else
                {
                    // Specific date in file name
                    folderFilterFixed = "";
                    foreach (string folderFilterPart in lf.FolderFilter.Split(';'))
                    {
                        folderFilterFixed += @"*"+dateCode+folderFilterPart+@">="+dateToSync+@";";
                    }
                    // Set filemask filter for synchronize
                    fileMask = (lf.FolderIncludeSub) ? string.Format(folderFilterFixed + @"{0}/|" + xmlFilter, dateCode) : folderFilterFixed + @"|*/;" + xmlFilter;
                }
                List<string> scriptText = new List<string>
                {
                    // Make script commands file for winscp process
                    "option batch abort",
                    "option confirm off",
                    "option reconnecttime 40",
                    "open sftp://" + userName + "@" + hostName + "/ -privatekey=\"" + sshPrivateKeyPath + "\" -hostkey=\"ssh-rsa 2048 5c:ad:f6:08:d0:8b:2b:0e:2a:00:03:02:cc:a2:96:25\" -timeout=10"
                };
                scriptText.Add("synchronize -filemask=" + fileMask + " remote \"" + folderPathFixed + "\" \"" + remoteFolderPathFixed + "\"");
                scriptText.Add("close");
                scriptText.Add("exit");
                WriteScript(scriptText.ToArray());
                // Check directories existence
                CheckDirectories(remoteFolderPathFixed);
                // MS-DOS commands to be executed
                string cmdArgs = "/script=\"WinscpScript.txt\" /log=\"" + CustomLog.sessionLogPath + "\"";
                string outputCmd = string.Empty;
                try
                {
                    RunSynchronization(cmdArgs);
                }
                catch (Exception exc)
                {
                    CustomLog.CustomLogEvent(string.Format("Synchronization failed: {0}", exc.Message));
                    CustomLog.Error();
                }
                finally
                {
                    ExitScript();
                }
            }
            return sftpProcess.ExitCode;
        }
        public static int DownloadFile(string remoteFile, string localPath)
        {
            // Clear service logs 
            CustomLog.ClearLogs();
            // MS-DOS commands to be executed
            string cmdArgs = "/C /command \"option batch abort\" \"option confirm off\" \"option reconnecttime 40\" \"open sftp://" + userName + "@" + hostName + "/ -privatekey=\"\"" + sshPrivateKeyPath + "\"\" -hostkey=\"\"ssh-rsa 2048 5c:ad:f6:08:d0:8b:2b:0e:2a:00:03:02:cc:a2:96:25\"\" -timeout=10 \" " +
                "\"get \"\"" + remoteFile+"\"\" \"\""+localPath+"\"\"\" " +
                "\"close\" \"exit\" /log=\"" + CustomLog.sessionLogPath + "\"";
            try
            {
                RunTransfer(cmdArgs);
            }
            catch (Exception exc)
            {
                CustomLog.CustomLogEvent(string.Format("Download file via SFTP failed: {0}", exc.Message));
                CustomLog.Error();
            }
            finally
            {
                ExitScript();
            }
            return sftpProcess.ExitCode;
        }
        public static int DownloadFiles(string[] remoteFiles, string localPath)
        {
            // Clear service logs 
            CustomLog.ClearLogs();
            // MS-DOS commands to be executed
            string cmdArgs = "/C /command \"option batch abort\" \"option confirm off\" \"option reconnecttime 40\" \"open sftp://" + userName + "@" + hostName + "/ -privatekey=\"\"" + sshPrivateKeyPath + "\"\" -hostkey=\"\"ssh-rsa 2048 5c:ad:f6:08:d0:8b:2b:0e:2a:00:03:02:cc:a2:96:25\"\" -timeout=10 \" ";
            foreach (string remoteFile in remoteFiles)
            {
                cmdArgs += "\"get \"\"" + remoteFile + "\"\" \"\"" + localPath + "\"\" \" ";
            }
            cmdArgs += "\"close\" \"exit\" /log=\"" + CustomLog.sessionLogPath + "\"";
            try
            {
                RunTransfer(cmdArgs);
            }
            catch (Exception exc)
            {
                CustomLog.CustomLogEvent(string.Format("Download file via SFTP failed: {0}", exc.Message));
                CustomLog.Error();
            }
            finally
            {
                ExitScript();
            }
            return sftpProcess.ExitCode;
        }
        public static int DownloadUpdatePackage(string localPath, string remotePath)
        {
            // Clear service logs
            CustomLog.ClearLogs();
            // Check if remote folder exists
            string remoteFolderPath = remoteRootFolder + remotePath;
            string cmdStatArgs = "/C /command \"option batch abort\" \"option confirm off\" \"option reconnecttime 40\" \"open sftp://" + userName + "@" + hostName + "/ -privatekey=\"\"" + sshPrivateKeyPath + "\"\" -hostkey=\"\"ssh-rsa 2048 5c:ad:f6:08:d0:8b:2b:0e:2a:00:03:02:cc:a2:96:25\"\" -timeout=10 \" " +
                "\"stat \"\"" + remoteFolderPath + "\"\" \" " +
                "\"close\" \"exit\" /log=\"" + CustomLog.sessionLogPath + "\"";
            string outputCmd = RunStat(cmdStatArgs);
            if (outputCmd.Contains("Drw"))
            {
                // Sync parameters
                string fileMask = "MX*.zip;MX*.log|*/";
                // MS-DOS commands to be executed
                string cmdArgs = "/C /command \"option batch abort\" \"option confirm off\" \"option reconnecttime 40\" \"open sftp://" + userName + "@" + hostName + "/ -privatekey=\"\"" + sshPrivateKeyPath + "\"\" -hostkey=\"\"ssh-rsa 2048 5c:ad:f6:08:d0:8b:2b:0e:2a:00:03:02:cc:a2:96:25\"\" -timeout=10 \" " +
                    "\"synchronize local -delete -filemask=" + fileMask + " \"\"" + localPath + "\"\" \"\"" + remoteFolderPath + "\"\"\" " +
                    "\"close\" \"exit\" /log=\"" + CustomLog.sessionLogPath + "\"";
                try
                {
                    RunSynchronization(cmdArgs);
                }
                catch (Exception exc)
                {
                    CustomLog.CustomLogEvent(string.Format("Downloading update package failed: {0}", exc.Message));
                    CustomLog.Error();
                }
                finally
                {
                    ExitScript();
                }
            }
            return sftpProcess.ExitCode;
        }
        private static string SftpStart(string cmdArgs)
        {
            // Set arguments for winscp executable
            sftpProcess.StartInfo.Arguments = cmdArgs;
            // Start process for sftp transaction
            sftpProcess.Start();
            // Read command line output
            string output = sftpProcess.StandardOutput.ReadToEnd();
            sftpProcess.WaitForExit(processTimeout);
            return output;
        }
        private static string RunTransfer(string cmdArgs)
        {
            int exitCode = 1;
            string outputCmd = string.Empty;
            for (int i = 0; i < attempts && exitCode==1; i++)
            {
                // Start process to send files
                outputCmd = SftpStart(cmdArgs);
                exitCode = sftpProcess.ExitCode;
            }
            return outputCmd;
        }
        private static string RunSynchronization(string cmdArgs)
        {
            int exitCode = 1;
            string outputCmd = string.Empty;
            for (int i = 0; i < attempts && exitCode == 1; i++)
            {
                // Start process to sync files
                outputCmd = SftpStart(cmdArgs);
                exitCode = sftpProcess.ExitCode;
            }
            return outputCmd;
        }
        private static string RunStat(string cmdArgs)
        {
            int exitCode = 1;
            string outputCmd = string.Empty;
            for (int i = 0; i < attempts && exitCode==1 && !outputCmd.Contains("rw") && !outputCmd.Contains("No such file or directory"); i++)
            {
                // Start process to check directories
                outputCmd = SftpStart(cmdArgs);
                exitCode = sftpProcess.ExitCode;
            }
            return outputCmd;
        }
        private static string RunMoveFile(string cmdArgs)
        {
            int exitCode = 1;
            string outputCmd = string.Empty;
            for (int i = 0; i < attempts && exitCode == 1; i++)
            {
                // Start process to check directories
                outputCmd = SftpStart(cmdArgs);
                exitCode = sftpProcess.ExitCode;
            }
            return outputCmd;
        }
        private static void CmdError(object sendingProcess,DataReceivedEventArgs errLine)
        {
            CustomLog.CustomLogEvent("Error in command line process: "+errLine.Data);
            CustomLog.Error();
        }
        private static void WriteScript(string[] scriptLines)
        {
            // Write script for winscp process
            string scriptPath = AppDomain.CurrentDomain.BaseDirectory + "WinscpScript.txt";
            try
            {
                File.WriteAllLines(scriptPath, scriptLines);
            }
            catch (IOException exc)
            {
                CustomLog.CustomLogEvent(string.Format("Error writing WinSCP script file: {0}", exc.Message));
                CustomLog.Error();
            }
        }
        public static void ExitScript()
        {
            try
            {
                while (!sftpProcess.HasExited)
                {
                    sftpProcess.Kill();
                    sftpProcess.WaitForExit();
                }
            }
            catch (Exception exc)
            {
                CustomLog.CustomLogEvent(string.Format("SFTP process could not be terminated: {0}", exc.Message));
                CustomLog.Error();
            }
            finally
            {
                string scriptPath = AppDomain.CurrentDomain.BaseDirectory + "WinscpScript.txt";
                if (File.Exists(scriptPath))
                {
                    File.Delete(scriptPath);
                }
            }
            foreach (Process sshProcess in Process.GetProcessesByName("winscp"))
            {
                try
                {
                    sshProcess.Kill();
                    CustomLog.CustomLogEvent("Remaining process " + sshProcess.ProcessName + " (" + sshProcess.Id.ToString() + ") killed");
                }
                catch (Exception exc)
                {
                    CustomLog.CustomLogEvent(string.Format("Remaining process " + sshProcess.ProcessName + " (" + sshProcess.Id.ToString() + ") could not be terminated: {0}", exc.Message));
                    CustomLog.Error();
                }
            }
        }
        private static void CheckDirectories(string mainPath)
        {
            int tryAttempts = 0;
            while (true)
            {
                try
                {
                    CreateDirectories(mainPath);
                    break;
                }
                catch (Exception exc)
                {
                    tryAttempts++;
                    if (tryAttempts > attempts)
                    {
                        CustomLog.CustomLogEvent(string.Format("Creation of remote directory {0} failed: {1}", mainPath, exc.Message));
                        CustomLog.Error();
                        break;
                    }
                }
            }
        }
        private static void CreateDirectories(string mainPath)
        {
            // MS-DOS commands to be executed
            string cmdArgs = "/C /command \"option batch abort\" \"option confirm off\" \"option reconnecttime 40\" \"open sftp://" + userName + "@" + hostName + "/ -privatekey=\"\"" + sshPrivateKeyPath + "\"\" -hostkey=\"\"ssh-rsa 2048 5c:ad:f6:08:d0:8b:2b:0e:2a:00:03:02:cc:a2:96:25\"\" -timeout=10 \" " +
                "\"stat \"\""+mainPath+"\"\" \" " +
                "\"close\" \"exit\" /log=\"" + CustomLog.sessionLogPath + "\"";
            string outputCmd = RunStat(cmdArgs);
            if (!outputCmd.Contains("Drw"))
            {
                string parentPath = Path.GetFullPath(Path.Combine(mainPath, "..")).Replace(@"\", @"/").Remove(0, 2);
                CreateDirectories(parentPath);
                // Create directory
                string cmdArgsCreate = "/C /command \"option batch abort\" \"open sftp://" + userName + "@" + hostName + "/ -privatekey=\"\"" + sshPrivateKeyPath + "\"\" -hostkey=\"\"ssh-rsa 2048 5c:ad:f6:08:d0:8b:2b:0e:2a:00:03:02:cc:a2:96:25\"\" -timeout=10 \" " +
                    "\"mkdir \"\"" + mainPath + "\"\" \" " +
                    "\"close\" \"exit\" /log=\"" + CustomLog.sessionLogPath + "\"";
                string outputMkdir = string.Empty;
                for (int i=0; i<attempts && !outputMkdir.Contains("Active");i++)
                {
                    outputMkdir = SftpStart(cmdArgsCreate);
                }
            }
        }
        public static void NoLogsRepeated(string localPath, string remotePath)
        {
            string remoteFolderPath = remoteRootFolder + remotePath;
            string todayFile = DateTime.Now.ToString("yyyyMMdd") + @".log";
            string completeRemotePath = remoteFolderPath + todayFile, completeLocalPath = localPath + todayFile;
            // MS-DOS commands to be executed
            string cmdArgs = "/C /command \"option batch abort\" \"option confirm off\" \"option reconnecttime 40\" \"open sftp://" + userName + "@" + hostName + "/ -privatekey=\"\"" + sshPrivateKeyPath + "\"\" -hostkey=\"\"ssh-rsa 2048 5c:ad:f6:08:d0:8b:2b:0e:2a:00:03:02:cc:a2:96:25\"\" -timeout=10 \" " +
                "\"stat \"\"" + completeRemotePath + "\"\" \" " +
                "\"close\" \"exit\" /log=\"" + CustomLog.sessionLogPath + "\"";
            string outputCmd = RunStat(cmdArgs);
            if (outputCmd.Contains("rw"))
            {
                if (File.Exists(completeLocalPath))
                {
                    string tomorrowFile = DateTime.Now.AddDays(1).ToString("yyyyMMdd") + @".log";
                    // MS-DOS commands to be executed
                    cmdArgs = "/C /command \"option batch abort\" \"open sftp://" + userName + "@" + hostName + "/ -privatekey=\"\"" + sshPrivateKeyPath + "\"\" -hostkey=\"\"ssh-rsa 2048 5c:ad:f6:08:d0:8b:2b:0e:2a:00:03:02:cc:a2:96:25\"\" -timeout=10 \" " +
                        "\"stat \"\"" + remoteFolderPath+tomorrowFile + "\"\" \" " +
                        "\"close\" \"exit\" /log=\"" + CustomLog.sessionLogPath + "\"";
                    outputCmd = RunStat(cmdArgs);
                    if (outputCmd.Contains("rw"))
                    {
                        RenameLogs(completeRemotePath);
                    }
                    else
                    {
                        string logsPath = AppDomain.CurrentDomain.BaseDirectory + ConfigurationManager.AppSettings["LogsPath"];
                        DownloadFile(completeRemotePath, logsPath);
                        string remoteFirstLine, localFirstLine = File.ReadAllLines(completeLocalPath)[0];
                        if (File.Exists(logsPath + todayFile))
                        {
                            remoteFirstLine = File.ReadAllLines(logsPath + todayFile)[0];
                            File.Delete(logsPath + todayFile);
                        }
                        else
                        {
                            remoteFirstLine = localFirstLine;
                        }
                        if (remoteFirstLine != localFirstLine)
                        {
                            RenameLogs(completeRemotePath);
                        }
                    }
                }
            }
        }
        private static void RenameLogs(string fileName)
        {
            char alphabetLetter = 'A';
            string outputCmd = string.Empty;
            // MS-DOS commands to be executed
            string cmdArgs = "/C /command \"option batch abort\" \"option confirm off\" \"option reconnecttime 40\" \"open sftp://" + userName + "@" + hostName + "/ -privatekey=\"\"" + sshPrivateKeyPath + "\"\" -hostkey=\"\"ssh-rsa 2048 5c:ad:f6:08:d0:8b:2b:0e:2a:00:03:02:cc:a2:96:25\"\" -timeout=10 \" " +
                "\"stat \"\"" + fileName + @"_" + alphabetLetter + "\"\" \" " +
                "\"close\" \"exit\" /log=\"" + CustomLog.sessionLogPath + "\"";
            outputCmd = RunStat(cmdArgs);
            while (outputCmd.Contains("rw"))
            {
                alphabetLetter++;
                cmdArgs = "/C /command \"option batch abort\" \"option confirm off\" \"option reconnecttime 40\" \"open sftp://" + userName + "@" + hostName + "/ -privatekey=\"\"" + sshPrivateKeyPath + "\"\" -hostkey=\"\"ssh-rsa 2048 5c:ad:f6:08:d0:8b:2b:0e:2a:00:03:02:cc:a2:96:25\"\" -timeout=10 \" " +
                "\"stat \"\"" + fileName + @"_" + alphabetLetter + "\"\" \" " +
                "\"close\" \"exit\" /log=\"" + CustomLog.sessionLogPath + "\"";
                outputCmd = RunStat(cmdArgs);
            }
            try
            {
                cmdArgs = "/C /command \"option batch abort\" \"option confirm off\" \"option reconnecttime 40\" \"open sftp://" + userName + "@" + hostName + "/ -privatekey=\"\"" + sshPrivateKeyPath + "\"\" -hostkey=\"\"ssh-rsa 2048 5c:ad:f6:08:d0:8b:2b:0e:2a:00:03:02:cc:a2:96:25\"\" -timeout=10 \" " +
                "\"mv \"\"" + fileName + "\"\" \"\"" + fileName + @"_" + alphabetLetter + "\"\" \" " +
                "\"close\" \"exit\" /log=\"" + CustomLog.sessionLogPath + "\"";
                RunMoveFile(cmdArgs);
            }
            catch (Exception exc)
            {
                CustomLog.CustomLogEvent(string.Format("Renaming file via SFTP failed: {0}", exc.Message));
                CustomLog.Error();
            }
        }
    }
}
