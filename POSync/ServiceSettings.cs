// Folder settings for directories to be monitored
using System.Xml.Serialization;

namespace POSync
{
    [XmlRoot("ServiceSettings")]
    public class ServiceSettings
    {
        /// <summary>Attempts for session command</summary>
        [XmlElement]
        public int AttemptsSession { get; set; }
        /// <summary>Session timeout for WinSCP in minutes</summary>
        [XmlElement]
        public int SessionTimeout { get; set; }
        /// <summary>Synchronization days interval</summary>
        [XmlElement]
        public int SyncDaysInterval { get; set; }
        /// <summary>Months interval for initial synchronization</summary>
        [XmlElement]
        public int InitialSyncMonthsInterval { get; set; }
        /// <summary>Time interval for update process</summary>
        [XmlElement]
        public int UpdateHoursInterval { get; set; }
        /// <summary>Time interval for server petition process</summary>
        [XmlElement]
        public int StatusCheckHoursInterval { get; set; }
        /// <summary>Time interval for server push process</summary>
        [XmlElement]
        public int ServerPushMinutesInterval { get; set; }
        /// <summary>Remote folder root</summary>
        [XmlElement]
        public string RemoteRoot { get; set; }
        /// <summary>Remote log folder path</summary>
        [XmlElement]
        public string RemoteLogsPath { get; set; }
        /// <summary>Local folder path for posdata files</summary>
        [XmlElement]
        public string LocalPosDataPath { get; set; }
        /// <summary>List filter for xml files</summary>
        [XmlElement]
        public string XmlExcludeList { get; set; }
        /// <summary>Local schedule path</summary>
        [XmlElement]
        public string SchedulePath { get; set; }
        // Constructor class
        public ServiceSettings()
        {
            AttemptsSession = 1;
            SessionTimeout = 1;
            SyncDaysInterval = 7;
            InitialSyncMonthsInterval = 6;
            UpdateHoursInterval = 6;
            StatusCheckHoursInterval = 3;
            ServerPushMinutesInterval = 5;
            RemoteRoot = @"/rest/";
            RemoteLogsPath = @"/synclogs/pos{0}/";
            LocalPosDataPath = @"{0}NewPos\PosData\";
            XmlExcludeList = @"Echo;Emit;PosDB;Status;SOS";
            SchedulePath = @"C:\Schedule\HORARIOS.txt|/horarios/empleados/";
        }
    }
}
