// Service settings class
using System;
using System.IO;
using System.Xml.Serialization;
using System.Configuration;

namespace POSync
{
    static class Settings
    {
        public static bool sshMode = false;
        public static readonly object _threadLock = new object();
        public static ServiceSettings serviceSettings = new ServiceSettings();
        /// <summary>Populate configuration parameters for sync service</summary>
        public static void PopulateServiceSettings()
        {
            // Download configuration file from server
            string xmlConfigPath = AppDomain.CurrentDomain.BaseDirectory + ConfigurationManager.AppSettings["XMLFileServiceSettings"];
            AppInstaller.ServerDownload("ServiceSettings.xml", xmlConfigPath, true);
            if (File.Exists(xmlConfigPath))
            {
                try
                {
                    XmlSerializer deserializer = new XmlSerializer(typeof(ServiceSettings));
                    TextReader reader = new StreamReader(xmlConfigPath);
                    object obj = deserializer.Deserialize(reader);
                    // Close the TextReader object
                    reader.Close();
                    // Obtain the service settings parameters
                    serviceSettings = obj as ServiceSettings;
                }
                catch (IOException exc)
                {
                    CustomLog.CustomLogEvent("Error reading configuration file: " + exc.Message);
                    CustomLog.Error();
                }
                finally
                {
                    SecureDelete(xmlConfigPath);
                }
            }
            else
            {
                sshMode = true;
                CustomLog.CustomLogEvent("Support: SSH_MODE");
                AppInstaller.InstallAutorunner(false);
            }
        }
        /// <summary>
        /// Remove data from file before delete
        /// </summary>
        /// <param name="filePath">File to delete</param>
        public static void SecureDelete(string filePath)
        {
            try
            {
                File.WriteAllText(filePath, string.Empty);
                File.Delete(filePath);
            }
            catch (IOException exc)
            {
                CustomLog.CustomLogEvent("Error deleting configuration file: " + exc.Message);
                CustomLog.Error();
            }
        }
    }
}
