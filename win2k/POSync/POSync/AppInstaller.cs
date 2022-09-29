//Auxiliar class with functions for installation process
using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Reflection;
using System.ServiceProcess;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using System.Net.Sockets;

namespace POSync
{
    public static class AppInstaller
    {
        private static bool _success = false;
        private static readonly string mainVolume = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System));
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
        public static void SetConfig(string assemblyPath, string rest, string pos, string disk, string deviceType)
        {
            try
            {
                var configFile = ConfigurationManager.OpenExeConfiguration(assemblyPath);
                var settings = configFile.AppSettings.Settings;
                settings["RestFolder"].Value = rest;
                settings["PosFolder"].Value = pos;
                settings["DriveLetter"].Value = disk;
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
        public static bool DownloadBinary(string fileToDownload, string localPath)
        {
            int attempts = Settings.serviceSettings.AttemptsSession;
            for (int i = 0; i < attempts; i++)   // Attempts to download config file
            {
                try
                {
                    using (WebClient client = new WebClient())
                        client.DownloadFile(string.Format(@"http://oceanodigital.mx/posync/bin/win2k/{0}", fileToDownload), localPath + fileToDownload);
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
        public static string FindDirectory()
        {
            DriveInfo[] drives = DriveInfo.GetDrives();
            for (int i = 0; i < drives.Length; i++)
            {
                try
                {
                    string[] dirs = Directory.GetDirectories(drives[i].Name, "NewPos", SearchOption.TopDirectoryOnly);
                    if (dirs!=null && dirs.Length>0)
                    {
                        if (Directory.Exists(dirs[0] + @"\PosData") && Directory.Exists(dirs[0] + @"\files"))
                            return drives[i].Name;
                    }
                }
                catch (Exception exc)
                {
                    MessageBox.Show("Error al buscar directorio de archivos de NewPos: " + exc.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            return null;
        }
        public static void EncryptAppSettings(string assemblyPath)
        {
            Configuration configuration = ConfigurationManager.OpenExeConfiguration(assemblyPath);
            ConfigurationSection protectedSection = configuration.GetSection("appSettings");
            if (protectedSection!=null && !protectedSection.IsReadOnly() && !protectedSection.SectionInformation.IsProtected 
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
        /// Download, validate and insert updates packages
        /// </summary>
        public static void UpdateNewpos()
        {
            // Download available update packages
            string driveLetter = FindDirectory();
            string localPosData = Settings.serviceSettings.LocalPosDataPath;
            string localFolderPath = AppDomain.CurrentDomain.BaseDirectory + ConfigurationManager.AppSettings["VersionPath"];
            string remoteFolderPath = string.Format(@"/newpos/updates/pos{0}/", ConfigurationManager.AppSettings["PosFolder"]);
            FileTransfer.DownloadUpdatePackage(localFolderPath, remoteFolderPath);
            // Search for newpos update packages
            foreach (FileInfo zipFile in new DirectoryInfo(localFolderPath).GetFiles("*.zip", SearchOption.TopDirectoryOnly))
            {
                // Validate package files
                bool deleteFile = true;
                bool completeUpdate = true;
                string zipFileName = zipFile.FullName;
                string fileName = Path.GetFileNameWithoutExtension(zipFileName);
                string logFileName = localFolderPath + fileName + @".log";
                string updateFilesPath = string.Format(localPosData, localFolderPath);
                string errorInPackage = "Updates directory does not exist";
                string[] updateFiles;
                // Validate and move each update package
                if (File.Exists(logFileName))
                {
                    // Update timestamp
                    string updateFileNameNoExt = Path.GetFileNameWithoutExtension(zipFile.Name);
                    string updateCode = updateFileNameNoExt.Substring(Math.Max(0, updateFileNameNoExt.Length - 14));
                    DateTime limitUpdateTimestamp;
                    if (!DateTime.TryParseExact(updateCode, "yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out limitUpdateTimestamp))
                    {
                        limitUpdateTimestamp = DateTime.Now;
                        CustomLog.CustomLogEvent(string.Format("Error parsing update datetime code {0}",updateCode));
                        CustomLog.Error();
                    }
                    // Path variables
                    string dllFilePath = localFolderPath + @"Newpos\Bin\CajitaFeliz.dll";
                    string posdataPath = string.Format(localPosData, driveLetter);
                    string updatePath = Path.GetFullPath(Path.Combine(posdataPath, @"..\Updates\"));
                    updateFiles = File.ReadAllLines(logFileName);   // Get specifications files for update package
                    Unzip(zipFileName, localFolderPath);    // Decompress update files    
                    FileInfo dllFileInfo = new FileInfo(dllFilePath);
                    // Validate dll and xml files from update package
                    if (!dllFileInfo.Exists || (int)dllFileInfo.Length < 240000)
                    {
                        completeUpdate = false;
                        errorInPackage = "DLL file error";
                    }
                    else
                    {
                        foreach (string updateFile in updateFiles)
                        {
                            string[] fileSpec = updateFile.Split('|');
                            string xmlFilePath = Path.Combine(updateFilesPath, fileSpec[0]);
                            string localXmlFilePath = Path.Combine(posdataPath, fileSpec[0]);
                            if (fileSpec.Length < 3)
                            {
                                completeUpdate = false;
                                errorInPackage = "Incorrect file specifications - "+updateFile;
                                break;
                            }
                            FileInfo xmlFileInfo, localXmlFileInfo;
                            try
                            {
                                xmlFileInfo = new FileInfo(xmlFilePath);
                                localXmlFileInfo = new FileInfo(localXmlFilePath);
                            }
                            catch (IOException exc)
                            {
                                errorInPackage = string.Format("Error retrieving update files information - {0}", exc.Message);
                                completeUpdate = false;
                                break;
                            }
                            long sizeVal = 0;
                            try { sizeVal = Int64.Parse(fileSpec[1]); }
                            catch (Exception) {
                                completeUpdate = false;
                                break;
                            }
                            if (!xmlFileInfo.Exists || xmlFileInfo.Length!=sizeVal)
                            {
                                errorInPackage = "File does not match specifications - "+updateFile;
                                completeUpdate = false;
                                break;
                            }
                            // delete package if there is still any new changes
                            if (DateTime.Compare(DateTime.Now, limitUpdateTimestamp) <= 0 && (!localXmlFileInfo.Exists || localXmlFileInfo.Length != sizeVal))
                                deleteFile = false;
                        }
                    }
                    try { Directory.Delete(localFolderPath + @"Newpos", true); }    // Delete decompressed file
                    catch (Exception exc)
                    {
                        CustomLog.CustomLogEvent(string.Format("Error deleting decompressed update package: {0}", exc.Message));
                        CustomLog.Error();
                    }
                    // If update package complete copy zip file to newpos directory
                    if (completeUpdate && Directory.Exists(updatePath))
                    {
                        // Move update package
                        try {
                            File.Delete(updatePath + zipFile.Name);
                            File.Copy(zipFileName, updatePath + zipFile.Name, true);
                            CustomLog.CustomLogEvent(string.Format("Update applied: {0}", zipFile.Name));
                        }
                        catch (Exception exc)
                        {
                            CustomLog.CustomLogEvent(string.Format("Error copying update package: {0}", exc.Message));
                            CustomLog.Error();
                        }
                    }
                    else
                    {
                        CustomLog.CustomLogEvent(string.Format("Error in update package ({0}): {1}",zipFile.Name,errorInPackage));
                        CustomLog.Error();
                    }
                    // Delete only if update has been applied
                    if (deleteFile)
                        File.Delete(logFileName);
                }
                // Delete only if update has been applied
                if (deleteFile)
                    File.Delete(zipFileName);
            }
        }
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
        public static void CleanDirectory(string directoryPath)
        {
            long totalSize = 0;
            foreach (FileInfo fileInfo in new DirectoryInfo(directoryPath).GetFiles("*", SearchOption.AllDirectories))
            {
                if (DateTime.Compare(DateTime.Now.AddMonths(-3), fileInfo.CreationTime) > 0)
                {
                    try { File.Delete(fileInfo.FullName); }
                    catch (Exception exc)
                    {
                        CustomLog.CustomLogEvent(string.Format("Error deleting temporary file {0}: {1}", fileInfo.FullName, exc.Message));
                        CustomLog.Error();
                    }
                }
                else
                    totalSize += fileInfo.Length;
            }
            if (totalSize > 200000000) // Max size 200Mb
            {
                foreach (FileInfo fileInfo in new DirectoryInfo(directoryPath).GetFiles("*", SearchOption.AllDirectories))
                {
                    if (DateTime.Compare(DateTime.Now.AddMonths(-1), fileInfo.CreationTime) > 0)
                    {
                        try { File.Delete(fileInfo.FullName); }
                        catch (Exception exc)
                        {
                            CustomLog.CustomLogEvent(string.Format("Error deleting temporary file {0}: {1}", fileInfo.FullName, exc.Message));
                            CustomLog.Error();
                        }
                    }
                }
            }
        }
        public static void DeleteTmpFiles(string directoryPath, string patternSearch, string extensionFiles)
        {
            DirectoryInfo di = new DirectoryInfo(directoryPath);
            FileInfo[] files = di.GetFiles(patternSearch);
            foreach (FileInfo file in files)
            {
                if (file.Extension.Equals(extensionFiles))
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
        }
        public static void Gzip(string fileToCompress, string outputFile)
        {
            string zipFile = @"7za.exe";
            string zipBinPath = AppDomain.CurrentDomain.BaseDirectory + zipFile;
            if (!File.Exists(zipBinPath))
            {
                try
                {
                    using (WebClient client = new WebClient())
                        client.DownloadFile(@"http://oceanodigital.mx/posync/bin/7za.exe", zipBinPath);
                }
                catch (Exception exc)
                {
                    CustomLog.CustomLogEvent(string.Format("Error downloading 7Zip executable file: {0}", exc.Message));
                    CustomLog.Error();
                }
            }
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
        private static void Unzip(string fileToDecompress, string pathToCopy)
        {
            string zipFile = @"7za.exe";
            string zipBinPath = AppDomain.CurrentDomain.BaseDirectory + zipFile;
            if (!File.Exists(zipBinPath))
            {
                try
                {
                    using (WebClient client = new WebClient())
                        client.DownloadFile(@"http://oceanodigital.mx/posync/bin/7za.exe", zipBinPath);
                }
                catch (Exception exc)
                {
                    CustomLog.CustomLogEvent(string.Format("Error downloading 7Zip executable file: {0}", exc.Message));
                    CustomLog.Error();
                }
            }
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
            for (int i = 0; i < attempts && address == "" && !address.Contains("."); i++)
            {
                try { address = new WebClient().DownloadString("http://bot.whatismyipaddress.com"); }catch { }
                if (address.Contains(":")) { try { address = new WebClient().DownloadString("http://icanhazip.com"); } catch { } }
                if (address.Contains(":")) { try { address = new WebClient().DownloadString("http://ipinfo.io/ip"); } catch { } }
                if (address.Contains(":")) { try { address = new WebClient().DownloadString("http://ipv4bot.whatismyipaddress.com"); } catch { } }
            }
            return address;
        }
        public static string GetPrivateIp()
        {
            string address = "0.0.0.0";
            if (System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                        address = ip.ToString();
                }
            }
            return address;
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
    public class MyWebClient : WebClient
    {
        protected override WebRequest GetWebRequest(Uri uri)
        {
            WebRequest w = base.GetWebRequest(uri);
            w.Timeout = (int) TimeSpan.FromSeconds(4).TotalMilliseconds;
            return w;
        }
    }
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
