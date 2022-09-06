// Auxiliar class with functions for installation process
using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using System.Linq;
using System.Net;
using System.Reflection;
using System.ServiceProcess;
using System.Windows.Forms;
using System.Net.Sockets;
using Microsoft.Win32;
using System.Management;
using System.Net.NetworkInformation;

namespace POSync
{
    public static class AppInstaller
    {
        private static bool _success = false;
        private static readonly string mainVolume = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System));
        /// <summary>
        /// Check internet connection
        /// </summary>
        /// <returns></returns>
        public static bool CheckForInternetConnection()
        {
            try
            {
                using (var client = new WebClient())
                using (client.OpenRead("http://google.com/generate_204"))
                    return true;
            }
            catch
            {
                return false;
            }
        }
        /// <summary>
        /// WriteConfig
        /// </summary>
        /// <param name="assemblyPath">Assembly configuration path</param>
        /// <returns></returns>
        public static bool WriteConfig(string assemblyPath)
        {
            try
            {
                var configFile = ConfigurationManager.OpenExeConfiguration(assemblyPath);
                var settings = configFile.AppSettings.Settings;
                settings.Add("RestFolder", "");
                settings.Add("PosFolder", "");
                settings.Add("DriveLetter", "");
                settings.Add("Device", "");
                configFile.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection(configFile.AppSettings.SectionInformation.Name);
                return true;
            }
            catch
            {
                return false;
            }
        }
        /// <summary>
        /// Initialize configuration file
        /// </summary>
        /// <param name="assemblyPath">Assembly configuration path</param>
        /// <param name="rest">Rest identifier</param>
        /// <param name="pos">Device identifier</param>
        /// <param name="disk">Local storage device disk</param>
        /// <param name="deviceType">Device type</param>
        public static void SetConfig(string assemblyPath, string rest, string pos, string deviceType)
        {
            try
            {
                var configFile = ConfigurationManager.OpenExeConfiguration(assemblyPath);
                var settings = configFile.AppSettings.Settings;
                settings["RestFolder"].Value = rest;
                settings["PosFolder"].Value = pos;
                settings["Device"].Value = deviceType;
                configFile.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection(configFile.AppSettings.SectionInformation.Name);
            }
            catch (ConfigurationErrorsException exc)
            {
                MessageBox.Show("Error al escribir en el archivo de configuración: " + exc.Message + "\nSi el problema persiste, solicite asistencia técnica.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        internal static void RunConfigWindow(string assemblyPath)
        {
            IntPtr hwnd = IntPtr.Zero;
            foreach (var proc in Process.GetProcessesByName("msiexec"))
            {
                if (proc.MainWindowHandle == IntPtr.Zero)
                    continue;
                if (string.IsNullOrEmpty(proc.MainWindowTitle))
                    continue;
                hwnd = proc.MainWindowHandle;
                break;
            }
            WindowWrapper windowWrapper = new WindowWrapper(hwnd);
            AppSetup appSetup = new AppSetup(assemblyPath);
            if (windowWrapper != null)
                appSetup.ShowDialog(windowWrapper);
            else
                appSetup.ShowDialog();
        }
        public static void DownloadInfo(string coorp)
        {
            for (int i = 0; i < 3; i++)   // Three attempts to download info installer file
            {
                try
                {
                    using (MyWebClient client = new MyWebClient())
                        client.DownloadFile(string.Format(@"http://oceanodigital.mx/posync/config/{0}/InfoInstaller.xml", coorp), mainVolume + "InfoInstaller.xml");
                    break;
                }
                catch (Exception exc)
                {
                    if (i == 2)
                    {
                        if (Environment.UserInteractive)
                            MessageBox.Show("Error al intentar descargar el archivo de configuración: " + exc.Message + "\nEl programa se instalará en modo sin conexión.", "Advertencia", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        else
                            CustomLog.CustomLogEvent("Error downloading device information: " + exc.Message);
                    }
                }
            }
        }
        public static bool ServerDownload(string fileToDownload, string fileName, bool isConfig)
        {
            string restFolder = ConfigurationManager.AppSettings["RestFolder"];
            string[] coorpAux = Path.GetFileNameWithoutExtension(GetSshPrivateKeyFile()).Split('_');
            string coorp = coorpAux[coorpAux.Length - 1];
            int attempts = Settings.serviceSettings.AttemptsSession;
            for (int i = 0; i < attempts; i++)   // Three attempts to download config file
            {
                try
                {
                    using (WebClient client = new WebClient())
                    {
                        if (isConfig)
                            client.DownloadFile(string.Format(@"http://oceanodigital.mx/posync/config/{0}/{1}/{2}", coorp, restFolder, fileToDownload), fileName);
                        else
                            client.DownloadFile(string.Format(@"http://oceanodigital.mx/posync/release/{0}", fileToDownload), fileName);
                    }
                    return true;
                }
                catch (Exception exc)
                {
                    if (i == attempts - 1)
                    {
                        CustomLog.CustomLogEvent(string.Format("Error downloading configuration file {0}: {1}", fileToDownload, exc.Message));
                        CustomLog.Error();
                    }
                    System.Threading.Thread.Sleep((int)TimeSpan.FromSeconds(30).TotalMilliseconds);
                }
            }
            return false;
        }
        public static bool DownloadBinary(string[] filesToDownload, string localPath)
        {
            int attempts = Settings.serviceSettings.AttemptsSession;
            for (int i = 0; i < attempts; i++)   // Attempts to download config file
            {
                try
                {
                    using (WebClient client = new WebClient())
                    {
                        foreach (string fileToDownload in filesToDownload)
                            client.DownloadFile(string.Format(@"http://oceanodigital.mx/posync/bin/{0}", fileToDownload), localPath + fileToDownload);
                    }
                    return true;
                }
                catch (Exception exc)
                {
                    if (i == attempts - 1)
                    {
                        CustomLog.CustomLogEvent(string.Format("Error downloading binary file to local path {0}: {1}", localPath, exc.Message));
                        CustomLog.Error();
                    }
                    System.Threading.Thread.Sleep((int)TimeSpan.FromSeconds(30).TotalMilliseconds);
                }
            }
            return false;
        }
        public static RestInfoCollection PopulateRestInfo()
        {
            string[] coorpAux = Path.GetFileNameWithoutExtension(GetSshPrivateKeyFile()).Split('_');
            string coorp = coorpAux[coorpAux.Length - 1];
            DownloadInfo(coorp);
            string fileNameXML = mainVolume + "InfoInstaller.xml";
            if (!File.Exists(fileNameXML))
                fileNameXML = AssemblyDirectory + string.Format(@"config\InfoInstaller_{0}.xml", coorp);
            if (File.Exists(fileNameXML))
            {
                try
                {
                    XmlSerializer deserializer = new XmlSerializer(typeof(RestInfoCollection));
                    TextReader reader = new StreamReader(fileNameXML);
                    object obj = deserializer.Deserialize(reader);
                    reader.Close();
                    RestInfoCollection restInfo = obj as RestInfoCollection;
                    File.Delete(fileNameXML);
                    return restInfo;
                }
                catch (XmlException exc)
                {
                    if (Environment.UserInteractive)
                        MessageBox.Show("Error al intentar leer archivo de información de restaurantes: " + exc.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    else
                        CustomLog.CustomLogEvent(string.Format("Error reading operation information file {0}: {1}", fileNameXML, exc.Message));
                }
            }
            return new RestInfoCollection();
        }
        /// <summary>
        /// Find pos software main root
        /// </summary>
        /// <returns></returns>
        public static string FindDirectory()
        {
            DriveInfo[] drives = DriveInfo.GetDrives();
            if (drives == null) { return mainVolume; }
            // Search for Newpos folder
            foreach (DriveInfo drive in drives)
            {
                if (!drive.IsReady || drive.DriveType != DriveType.Fixed) { continue; }
                try
                {
                    string[] dirs = Directory.GetDirectories(drive.Name, "NewPos", SearchOption.TopDirectoryOnly);
                    if (dirs != null && dirs.Length > 0)
                    {
                        if (File.Exists(Path.Combine(dirs[0], @"Bin", @"NewPOS.exe")) && Directory.Exists(Path.Combine(dirs[0], @"PosData")) && Directory.Exists(Path.Combine(dirs[0], @"files")))
                            return drive.Name;
                    }
                }
                catch (Exception exc)
                {
                    CustomLog.CustomLogEvent(string.Format("Error searching data directory: {0}", exc.Message));
                    CustomLog.Error();
                }
            }
            // search for data folder
            foreach (DriveInfo drive in drives)
            {
                if (!drive.IsReady || drive.DriveType != DriveType.Fixed) { continue; }
                try
                {
                    string[] dirs = Directory.GetDirectories(drive.Name, "Data", SearchOption.TopDirectoryOnly);
                    if (dirs != null && dirs.Length > 0)
                    {
                        if (Directory.Exists(dirs[0] + @"\Newpos1") || Directory.Exists(dirs[0] + @"\Newpos2"))
                            return drive.Name;
                    }
                }
                catch (Exception exc)
                {
                    CustomLog.CustomLogEvent(string.Format("Error searching data directory: {0}", exc.Message));
                    CustomLog.Error();
                }
            }
            // search for nationalsoft folder
            foreach (DriveInfo drive in drives)
            {
                if (!drive.IsReady || drive.DriveType != DriveType.Fixed) { continue; }
                try
                {
                    string[] dirs = Directory.GetDirectories(drive.Name, "nationalsoft", SearchOption.TopDirectoryOnly);
                    if (dirs != null && dirs.Length > 0)
                    {
                        if (Directory.Exists(dirs[0] + @"\OpenSSL"))
                            return drive.Name;
                    }
                }
                catch (Exception exc)
                {
                    CustomLog.CustomLogEvent(string.Format("Error searching data directory: {0}", exc.Message));
                    CustomLog.Error();
                }
            }
            return mainVolume;
        }
        public static void EncryptAppSettings(string assemblyPath)
        {
            Configuration configuration = ConfigurationManager.OpenExeConfiguration(assemblyPath);
            ConfigurationSection protectedSection = configuration.GetSection("appSettings");
            if (protectedSection != null && !protectedSection.IsReadOnly() && !protectedSection.SectionInformation.IsProtected
                && !protectedSection.SectionInformation.IsLocked)
            {
                protectedSection.SectionInformation.ProtectSection("RsaProtectedConfigurationProvider");
                protectedSection.SectionInformation.ForceSave = true;
                configuration.Save(ConfigurationSaveMode.Full);
            }
        }
        public static void SetRecoveryOptions(string serviceName)
        {
            int exitCode;
            using (var process = new Process())
            {
                var startInfo = process.StartInfo;
                startInfo.FileName = "sc";
                startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                // Tell Windows that the service should restart if it fails
                startInfo.Arguments = string.Format("failure \"{0}\" reset= 0 actions= restart/60000", serviceName);
                process.Start();
                process.WaitForExit();
                exitCode = process.ExitCode;
            }
            if (exitCode != 0)
                throw new InvalidOperationException();
        }
        public static string GetSshPrivateKeyFile()
        {
            string folderPath = AssemblyDirectory + @"config\";
            string[] filesInPath = Directory.GetFiles(folderPath, "*.ppk", SearchOption.TopDirectoryOnly);
            if (filesInPath.Length > 0)
            {
                return filesInPath[0];
            }
            return string.Empty;
        }
        /// <summary>
        /// Download and copy updates to newpos directory
        /// </summary>
        public static void UpdateNewpos()
        {
            // Download available update packages
            string driveLetter = FindDirectory();
            int attempts = Settings.serviceSettings.AttemptsSession;
            string localPosData = Settings.serviceSettings.LocalPosDataPath;
            string localFolderPath = $"{AppDomain.CurrentDomain.BaseDirectory}{ConfigurationManager.AppSettings["VersionPath"]}";
            string remoteFolderPath = (Service1.deviceType.Contains("pos")) ? string.Format(@"/newpos/updates/pos{0}/", ConfigurationManager.AppSettings["PosFolder"]) : @"/newpos/updates/pc_gerentes/";
            string updatePath = string.Format(@"{0}POSyncUpdates\", driveLetter);
            for (int j = 0; !FileTransfer.DownloadUpdatePackage(localFolderPath, remoteFolderPath) && j <= attempts; j++) { System.Threading.Thread.Sleep((int)TimeSpan.FromSeconds(30).TotalMilliseconds); }
            // Create updates folder if no exists
            if (!Directory.Exists(updatePath))
            {
                try { Directory.CreateDirectory(updatePath); }
                catch (Exception exc)
                {
                    CustomLog.CustomLogEvent(string.Format("Error creating updates folder {0}: {1}", updatePath, exc.Message));
                    CustomLog.Error();
                    return;
                }
            }
            // Search for newpos update packages
            foreach (FileInfo zipFile in new DirectoryInfo(localFolderPath).GetFiles("*.zip", SearchOption.TopDirectoryOnly))
            {
                // Validate package files
                bool completeUpdate = true;
                string errorInPackage = string.Empty;
                string zipFileName = zipFile.FullName;
                string fileName = Path.GetFileNameWithoutExtension(zipFileName);
                string logFileName = localFolderPath + fileName + @".log";
                string updateFilesPath = string.Format(localPosData, localFolderPath);
                string[] updateFiles;
                // for the record
                CustomLog.CustomLogEvent(string.Format("Update downloaded: {0}", zipFile.Name));
                // validate update package
                if (File.Exists(logFileName))
                {
                    // Path variables
                    string posdataPath = string.Format(localPosData, driveLetter);
                    // Get specifications files for update package
                    updateFiles = File.ReadAllLines(logFileName);
                    // Decompress update files
                    Unzip(zipFileName, localFolderPath);
                    //string dllFilePath = localFolderPath + @"Newpos\Bin\CajitaFeliz.dll";
                    //FileInfo dllFileInfo = new FileInfo(dllFilePath);
                    // Validate dll and xml files from update package
                    //if (!dllFileInfo.Exists || (int)dllFileInfo.Length < 100000)
                    //{
                    //    completeUpdate = false;
                    //    errorInPackage = "DLL file error";
                    //}
                    // Validate and move each update package
                    foreach (string updateFile in updateFiles)
                    {
                        string[] fileSpec = updateFile.Split('|');
                        string xmlFilePath = Path.Combine(updateFilesPath, fileSpec[0]);
                        string localXmlFilePath = Path.Combine(posdataPath, fileSpec[0]);
                        if (fileSpec.Length < 3)
                        {
                            completeUpdate = false;
                            errorInPackage = "Incorrect file specifications - " + updateFile;
                            break;
                        }
                        FileInfo xmlFileInfo;
                        try { xmlFileInfo = new FileInfo(xmlFilePath); }
                        catch (IOException exc)
                        {
                            errorInPackage = string.Format("Error retrieving update files information - {0}", exc.Message);
                            completeUpdate = false;
                            break;
                        }
                        if (Int64.TryParse(fileSpec[1], out long sizeVal))
                        {
                            if (!xmlFileInfo.Exists || xmlFileInfo.Length != sizeVal)
                            {
                                errorInPackage = "File does not match specifications - " + updateFile;
                                completeUpdate = false;
                                break;
                            }
                        }
                        else
                        {
                            errorInPackage = "File size parse error - " + updateFile;
                            completeUpdate = false;
                            break;
                        }
                    }
                    try { Directory.Delete(localFolderPath + @"Newpos", true); }    // Delete decompressed file
                    catch (Exception exc)
                    {
                        CustomLog.CustomLogEvent(string.Format("Error deleting decompressed update package: {0}", exc.Message));
                        CustomLog.Error();
                    }
                    // If update package complete copy zip file to newpos directory
                    if (completeUpdate)
                    {
                        // Move update package
                        try { File.Copy(zipFileName, Path.Combine(updatePath, zipFile.Name), true); }
                        catch (Exception exc)
                        {
                            CustomLog.CustomLogEvent(string.Format("Error copying update package: {0}", exc.Message));
                            CustomLog.Error();
                        }
                    }
                    else
                    {
                        CustomLog.CustomLogEvent(string.Format("Error in update package ({0}): {1}", zipFile.Name, errorInPackage));
                        CustomLog.Error();
                    }
                    File.Delete(logFileName);
                }
                File.Delete(zipFileName);
            }
            // Copy configuration files to itona
            UpdateItonaNewpos();
        }
        /// <summary>
        /// Update itona settings from POS
        /// </summary>
        private static void UpdateItonaNewpos()
        {
            // Generate local paths
            string driveLetter = FindDirectory();
            string[] localFiles = new string[] {
                string.Format(Settings.serviceSettings.LocalPosDataPath+@"NewProd.xml", driveLetter),
                string.Format(Settings.serviceSettings.LocalPosDataPath+@"ScreenMex.xml", driveLetter)};
            // Itona root path
            string[] octets = GetPrivateIp().Split('.');
            if (octets.Length < 3)
                octets = new string[] { "0", "0", "0" };
            // network path variables
            string rootIp = $"{octets[0]}.{octets[1]}.{octets[2]}.";
            string itonaFolder, itonaRootFolder, deviceAddress;
            for (int i = 90; i < 100; i++)
            {
                deviceAddress = rootIp + i;
                if (!PingHost(deviceAddress))
                    continue;
                itonaRootFolder = @"\\" + deviceAddress + @"\Data";
                try
                {
                    if (Directory.Exists(itonaRootFolder))
                    {
                        foreach (string dir in Directory.GetDirectories(itonaRootFolder, "NewPos*", SearchOption.TopDirectoryOnly))
                        {
                            itonaFolder = dir + @"\PosData";
                            if (Directory.Exists(itonaFolder))
                            {
                                foreach (string localFile in localFiles)
                                {
                                    try { File.Copy(localFile, Path.Combine(itonaFolder, Path.GetFileName(localFile)), true); }
                                    catch (Exception exc)
                                    {
                                        CustomLog.CustomLogEvent(string.Format("Error updating configuration files: {0}", exc.Message));
                                        CustomLog.Error();
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception exc)
                {
                    CustomLog.CustomLogEvent(string.Format("Error retrieving network data: {0}", exc.Message));
                    CustomLog.Error();
                }
            }
        }
        /// <summary>
        /// Write into configuration file
        /// </summary>
        public static void WriteNewConfiguration()
        {
            if (ConfigurationManager.AppSettings["ConfigPath"] == null)
            {
                try
                {
                    var configFile = ConfigurationManager.OpenExeConfiguration(AssemblyPath);
                    var settings = configFile.AppSettings.Settings;
                    settings.Add("ConfigPath", @"config\");
                    settings.Remove("SshPrivateKeyPath");
                    configFile.Save(ConfigurationSaveMode.Modified);
                    ConfigurationManager.RefreshSection(configFile.AppSettings.SectionInformation.Name);
                }
                catch (ConfigurationErrorsException exc)
                {
                    CustomLog.CustomLogEvent("Error al escribir en el archivo de configuración: " + exc.Message);
                    CustomLog.Error();
                }
            }
        }
        /// <summary>
        /// Start windows service
        /// </summary>
        /// <param name="serviceName"></param>
        public static void StartService(string serviceName)
        {
            try
            {
                ServiceController serviceController = new ServiceController(serviceName);
                if (serviceController.Status.Equals(ServiceControllerStatus.Stopped) && !serviceController.Status.Equals(ServiceControllerStatus.StartPending))
                    serviceController.Start();
                serviceController.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(240));
            }
            catch (SystemException exc)
            {
                CustomLog.CustomLogEvent("El sistema no ha podido iniciar el servicio " + serviceName + ": \n" + exc.Message);
            }
        }
        /// <summary>
        /// Stop windows service
        /// </summary>
        /// <param name="serviceName"></param>
        public static void StopService(string serviceName)
        {
            try
            {
                ServiceController serviceController = new ServiceController(serviceName);
                if (!serviceController.Status.Equals(ServiceControllerStatus.Stopped) && !serviceController.Status.Equals(ServiceControllerStatus.StopPending))
                    serviceController.Stop();
                serviceController.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(240));
            }
            catch (SystemException exc)
            {
                CustomLog.CustomLogEvent("El sistema no ha podido detener el servicio " + serviceName + ": \n" + exc.Message);
            }
        }
        /// <summary>
        /// Remove old files from directory
        /// </summary>
        /// <param name="directoryPath">Directory to clean</param>
        public static void CleanDirectory(string directoryPath)
        {
            long totalSize = 0;
            foreach (FileInfo fileInfo in new DirectoryInfo(directoryPath).GetFiles("*", SearchOption.AllDirectories))
            {
                if (DateTime.Compare(DateTime.Now.AddMonths(-6), fileInfo.CreationTime) > 0)
                {
                    try { File.Delete(fileInfo.FullName); }
                    catch (Exception exc)
                    {
                        CustomLog.CustomLogEvent(string.Format("Error deleting temporary file {0}: {1}", fileInfo.FullName, exc.Message));
                        CustomLog.Error();
                    }
                }
                totalSize += fileInfo.Length;
            }
            if (totalSize > 500000000) // Max size 500Mb
            {
                foreach (string fileName in new DirectoryInfo(directoryPath).GetFiles("*", SearchOption.AllDirectories).Where(f => DateTime.Compare(DateTime.Now.AddMonths(-1), f.CreationTime) > 0).Select(f => f.FullName))
                {
                    try { File.Delete(fileName); }
                    catch (Exception exc)
                    {
                        CustomLog.CustomLogEvent(string.Format("Error deleting temporary file {0}: {1}", fileName, exc.Message));
                        CustomLog.Error();
                    }
                }
            }
        }
        /// <summary>
        /// Delete temporary files
        /// </summary>
        /// <param name="directoryPath"></param>
        /// <param name="patternSearch"></param>
        /// <param name="extensionFiles"></param>
        public static void DeleteTmpFiles(string directoryPath, string patternSearch, string extensionFiles)
        {
            DirectoryInfo di = new DirectoryInfo(directoryPath);
            FileInfo[] files = di.GetFiles(patternSearch).Where(p => p.Extension == extensionFiles).ToArray();
            foreach (FileInfo file in files)
            {
                try
                {
                    file.Attributes = FileAttributes.Normal;
                    File.Delete(file.FullName);
                }
                catch (IOException exc)
                {
                    MessageBox.Show("Error al intentar borrar archivos temporales: \n" + exc.Message, "Advertencia", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }
        public static void Gzip(string fileToCompress, string outputFile)
        {
            string zipFile = @"7za.exe";
            string zipBinPath = AppDomain.CurrentDomain.BaseDirectory + zipFile;
            if (!File.Exists(zipBinPath))
                DownloadBinary(new string[] { zipFile }, AppDomain.CurrentDomain.BaseDirectory);
            try
            {
                ProcessStartInfo processInfo = new ProcessStartInfo
                {
                    FileName = zipBinPath,
                    Arguments = "a -y -tgzip \"" + outputFile + "\" \"" + fileToCompress + "\"",
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process unzipProcess = Process.Start(processInfo);
                unzipProcess.WaitForExit();
            }
            catch (Exception exc)
            {
                CustomLog.CustomLogEvent(string.Format("Error compressing file {0}: {1}", Path.GetFileName(fileToCompress), exc.Message));
                CustomLog.Error();
            }
        }
        public static void Unzip(string fileToDecompress, string pathToCopy)
        {
            string zipFile = @"7za.exe";
            string zipBinPath = AppDomain.CurrentDomain.BaseDirectory + zipFile;
            if (!File.Exists(zipBinPath))
                DownloadBinary(new string[] { zipFile }, AppDomain.CurrentDomain.BaseDirectory);
            try
            {
                ProcessStartInfo processInfo = new ProcessStartInfo
                {
                    FileName = zipBinPath,
                    Arguments = "x -y -tzip \"" + fileToDecompress + "\" -o\"" + pathToCopy + "\"",
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process unzipProcess = Process.Start(processInfo);
                unzipProcess.WaitForExit();
            }
            catch (Exception exc)
            {
                CustomLog.CustomLogEvent(string.Format("Error decompressing file {0}: {1}", Path.GetFileName(fileToDecompress), exc.Message));
                CustomLog.Error();
            }
        }
        public static string GetPublicIp()
        {
            string address = "";
            int attempts = Settings.serviceSettings.AttemptsSession;
            for (int i = 0; i < attempts && address == ""; i++)
            {
                try { address = new WebClient().DownloadString("http://bot.whatismyipaddress.com"); } catch { }
                if (address.Contains(":") || !IsIPAddress(address)) { try { address = new WebClient().DownloadString("http://icanhazip.com"); } catch { } }
                if (address.Contains(":") || !IsIPAddress(address)) { try { address = new WebClient().DownloadString("http://ipinfo.io/ip"); } catch { } }
                if (address.Contains(":") || !IsIPAddress(address)) { try { address = new WebClient().DownloadString("http://ipv4bot.whatismyipaddress.com"); } catch { } }
            }
            return address;
        }
        public static string GetPrivateIp()
        {
            if (System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                        return ip.ToString();
                }
            }
            return "0.0.0.0";
        }
        private static bool IsIPAddress(string ipAddress)
        {
            bool retVal = false;
            try
            {
                IPAddress address;
                retVal = IPAddress.TryParse(ipAddress, out address);
            } catch (Exception) { }
            return retVal;
        }
        /// <summary>
        /// Give servie user SQL Server access permissions
        /// </summary>
        public static void GrantQueryPermissions()
        {
            int exitCode = 1;
            string processOutput = string.Empty;
            string[] coorpAux = Path.GetFileNameWithoutExtension(GetSshPrivateKeyFile()).Split('_');
            string coorp = coorpAux[coorpAux.Length - 1];
            string serverInstance = coorp == "chorsco" ? @".\NATIONALSOFT" : @".\";
            try
            {
                ProcessStartInfo processInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/C sqlcmd -b -S {serverInstance} -Q \"EXEC master..sp_addsrvrolemember @loginame = N'NT AUTHORITY\\SYSTEM', @rolename = N'sysadmin'\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process queryProcess = Process.Start(processInfo);
                string output = queryProcess.StandardOutput.ReadToEnd();
                queryProcess.WaitForExit();
                exitCode = queryProcess.ExitCode;
                processOutput = output;
            }
            catch (Exception exc)
            {
                if (Environment.UserInteractive)
                    MessageBox.Show(string.Format("Error on SQL permission grant: {0}", exc.Message), "Advertencia", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                else
                    CustomLog.CustomLogEvent(string.Format("Error on SQL permission grant: {0}", exc.Message));
            }
            if (Environment.UserInteractive)
            {
                if (exitCode != 0)
                    MessageBox.Show(string.Format(@"Error al modificar los permisos del usuario NT AUTHORITY\SYSTEM. {0}", processOutput), "Advertencia", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                if (exitCode != 0)
                    CustomLog.CustomLogEvent(string.Format(@"Error al modificar los permisos del usuario NT AUTHORITY\SYSTEM. {0}", processOutput));
            }
        }
        /// <summary>
        /// Install autorunner service
        /// </summary>
        public static void InstallAutorunner(bool download_update)
        {
            string appName = "POSync Autorun";
            string exeFile = appName + ".exe";
            string exeBinPath = Path.Combine(AssemblyDirectory, exeFile);
            if (download_update)
            {
                string localPath = Path.Combine(AssemblyDirectory, ConfigurationManager.AppSettings["VersionPath"]);
                string tmpBinPath = Path.Combine(localPath, exeFile);
                // Download most recent autorunner app
                DownloadBinary(new string[] { exeFile }, localPath);
                // Move file to main path
                if (File.Exists(tmpBinPath))
                {
                    // Update autorunner executable file
                    try
                    {
                        FileInfo exeFileInfo = new FileInfo(exeBinPath);
                        FileInfo tmpFileInfo = new FileInfo(tmpBinPath);
                        // Compare file size  (tmpFileInfo.CreationTime != exeFileInfo.CreationTime)
                        if (tmpFileInfo.Exists && (!exeFileInfo.Exists || tmpFileInfo.Length != exeFileInfo.Length || tmpFileInfo.LastWriteTime != exeFileInfo.LastWriteTime || tmpFileInfo.CreationTime != exeFileInfo.CreationTime))
                        {
                            // stop autorunner service
                            KillProcess(exeFile);
                            // Copy file to main path
                            File.Copy(tmpBinPath, exeBinPath, true);
                        }
                    }
                    catch (Exception exc)
                    {
                        CustomLog.CustomLogEvent(string.Format("Error updating autorunner app: {0}", exc.Message));
                        CustomLog.Error();
                    }
                    finally
                    {
                        File.Delete(tmpBinPath);
                    }
                }
            }
            // if bin file exists install it
            if (File.Exists(exeBinPath))
            {
                // Set autostart
                string os_info = GetOSInfo();
                CustomLog.CustomLogEvent(string.Format("OS Detected: Windows {0}", os_info));
                if (os_info == "XP" || os_info == "Vista" || os_info == "7")
                {
                    // winlogon\userinit registry for win xp, vista and 7
                    string keyString = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon";
                    RegistryKey registryKey = Registry.LocalMachine.OpenSubKey(keyString, true);
                    if (registryKey != null)
                    {
                        try
                        {
                            string actualValue = registryKey.GetValue("Userinit").ToString();
                            if (!actualValue.ToLower().Contains(exeFile.ToLower()))
                            {
                                string newValue = $"{actualValue},,\"{exeBinPath}\"";
                                registryKey.SetValue("Userinit", newValue);
                            }
                            CustomLog.CustomLogEvent("Autorunner has been installed successfully (Userinit registry)");
                        }
                        catch (Exception exc)
                        {
                            CustomLog.CustomLogEvent(string.Format("Error modifying Userinit registry: {0}", exc.Message));
                            CustomLog.Error();
                        }
                    }
                }
                else
                {
                    // run registry for win 8, 10, 11
                    string keyString = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
                    RegistryKey registryKey = Registry.LocalMachine.OpenSubKey(keyString, true);
                    if (registryKey != null)
                    {
                        try
                        {
                            registryKey.SetValue(appName, $"\"{exeBinPath}\"");
                            registryKey.Close();
                            CustomLog.CustomLogEvent("Autorunner has been installed successfully (Run registry)");
                        }
                        catch (Exception exc)
                        {
                            CustomLog.CustomLogEvent(string.Format("Error modifying Run registry: {0}", exc.Message));
                            CustomLog.Error();
                        }
                    }
                }
            }
        }
        /// <summary>
        /// Uninstall autorunner service
        /// </summary>
        public static void UninstallAutorunner()
        {
            string fileName = @"POSync Autorun.exe";
            // Stop process
            KillProcess(fileName);
            // Remove autostart setup
            string keyString = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon";
            RegistryKey registryKey = Registry.LocalMachine.OpenSubKey(keyString, true);
            if (registryKey != null)
            {
                try
                {
                    string newValue = "";
                    string actualValue = registryKey.GetValue("Userinit").ToString();
                    foreach (string valuePart in actualValue.Split(','))
                    {
                        if (!valuePart.ToLower().Contains(fileName.ToLower()))
                            newValue += valuePart + ",";
                    }
                    if (newValue.EndsWith(",,"))
                        newValue = newValue.Substring(0, newValue.Length - 2);
                    if (newValue.EndsWith(","))
                        newValue = newValue.Substring(0, newValue.Length - 1);
                    if (newValue.ToLower().Contains("userinit"))
                        registryKey.SetValue("Userinit", newValue);
                }
                catch (Exception) { }
            }
        }
        /// <summary>
        /// Kill a process by its name
        /// </summary>
        /// <param name="processName">Process name</param>
        private static bool KillProcess(string processName)
        {
            bool existingProcess = false;
            string processWoExt = Path.GetFileNameWithoutExtension(processName);
            foreach (var p in Process.GetProcessesByName(processName))
            {
                KillProcessAndChildren(p.Id);
                existingProcess = true;
            }
            foreach (var p in Process.GetProcessesByName(processWoExt))
            {
                KillProcessAndChildren(p.Id);
                existingProcess = true;
            }
            System.Threading.Thread.Sleep((int)TimeSpan.FromSeconds(30).TotalMilliseconds);
            return existingProcess;
        }
        /// <summary>
        /// Kill a process, and all of its children, grandchildren, etc.
        /// </summary>
        /// <param name="pid">Process ID.</param>
        private static void KillProcessAndChildren(int pid)
        {
            // Cannot close 'system idle process'.
            if (pid == 0)
                return;
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("Select * From Win32_Process Where ParentProcessID=" + pid);
            ManagementObjectCollection moc = searcher.Get();
            foreach (ManagementObject mo in moc)
                KillProcessAndChildren(Convert.ToInt32(mo["ProcessID"]));
            try
            {
                Process proc = Process.GetProcessById(pid);
                proc.Kill();
            }
            catch (ArgumentException)
            {
                // Process already exited.
            }
        }
        /// <summary>
        /// Ping server and return connection status
        /// </summary>
        /// <param name="nameOrAddress">Server address</param>
        /// <returns></returns>
        public static bool PingHost(string nameOrAddress)
        {
            bool pingable = false;
            try
            {
                using (Ping pinger = new Ping())
                {
                    PingReply reply = pinger.Send(nameOrAddress);
                    pingable = reply.Status == IPStatus.Success;
                }
            }
            catch (PingException) { }
            return pingable;
        }
        /// <summary>
        /// Get OS System information
        /// </summary>
        /// <returns></returns>
        private static string GetOSInfo()
        {
            //Get Operating system information.
            OperatingSystem os = Environment.OSVersion;
            //Get version information about the os.
            Version vs = os.Version;
            //Variable to hold our return value
            string operatingSystem = "";
            if (os.Platform == PlatformID.Win32Windows)
            {
                //This is a pre-NT version of Windows
                switch (vs.Minor)
                {
                    case 0:
                        operatingSystem = "95";
                        break;
                    case 10:
                        if (vs.Revision.ToString() == "2222A")
                            operatingSystem = "98SE";
                        else
                            operatingSystem = "98";
                        break;
                    case 90:
                        operatingSystem = "Me";
                        break;
                    default:
                        break;
                }
            }
            else if (os.Platform == PlatformID.Win32NT)
            {
                switch (vs.Major)
                {
                    case 3:
                        operatingSystem = "NT 3.51";
                        break;
                    case 4:
                        operatingSystem = "NT 4.0";
                        break;
                    case 5:
                        if (vs.Minor == 0)
                            operatingSystem = "2000";
                        else
                            operatingSystem = "XP";
                        break;
                    case 6:
                        if (vs.Minor == 0)
                            operatingSystem = "Vista";
                        else if (vs.Minor == 1)
                            operatingSystem = "7";
                        else if (vs.Minor == 2)
                            operatingSystem = "8";
                        else
                            operatingSystem = "8.1";
                        break;
                    case 10:
                        operatingSystem = "10";
                        break;
                    default:
                        break;
                }
            }
            //Make sure we actually got something in our OS check
            //We don't want to just return " Service Pack 2" or " 32-bit"
            //That information is useless without the OS version.
            //if (operatingSystem != "")
            //{
            //    //Got something.  Let's prepend "Windows" and get more info.
            //    operatingSystem = "Windows " + operatingSystem;
            //    //See if there's a service pack installed.
            //    if (os.ServicePack != "")
            //    {
            //        //Append it to the OS name.  i.e. "Windows XP Service Pack 3"
            //        operatingSystem += " " + os.ServicePack;
            //    }
            //    //Append the OS architecture.  i.e. "Windows XP Service Pack 3 32-bit"
            //    //operatingSystem += " " + getOSArchitecture().ToString() + "-bit";
            //}
            //Return the information we've gathered.
            return operatingSystem;
        }
        /// <summary>
        /// Assembly installer directory path
        /// </summary>
        public static void RestartDevice()
        {
            CustomLog.CustomLogEvent("Restarting device...");
            Process.Start("ShutDown", "/r");
        }
        public static string AssemblyDirectory
        {
            get
            {
                string codeBase = Assembly.GetExecutingAssembly().CodeBase;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                return Path.GetDirectoryName(path)+@"\";
            }
        }
        /// <summary>
        /// Assembly installer file path
        /// </summary>
        public static string AssemblyPath
        {
            get
            {
                string codeBase = Assembly.GetExecutingAssembly().CodeBase;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                return path;
            }
        }
        /// <summary>
        /// Success installation flag
        /// </summary>
        public static bool Successs
        {
            get
            {
                return _success;
            }
            set
            {
                _success = value;
            }
        }
    }
    /// <summary>
    /// Extended WebClient class
    /// </summary>
    public class MyWebClient : WebClient
    {
        protected override WebRequest GetWebRequest(Uri uri)
        {
            WebRequest w = base.GetWebRequest(uri);
            w.Timeout = (int) TimeSpan.FromSeconds(4).TotalMilliseconds;
            return w;
        }
    }
    /// <summary>
    /// Extended WinWrapper class
    /// </summary>
    public class WindowWrapper : System.Windows.Forms.IWin32Window
    {
        private readonly IntPtr _hwnd;
        public WindowWrapper(IntPtr handle)
        {
            _hwnd = handle;
        }
        public IntPtr Handle
        {
            get { return _hwnd; }
        }
    }
}
