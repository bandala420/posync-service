// Folder settings for directories to be synced
using System.Xml.Serialization;

namespace POSync
{
    public class CustomStorageSettings
    {
        /// <summary>Unique identifier of the combination File type/folder.
        /// Arbitrary number (for instance 001, 002, and so on)</summary>
        [XmlAttribute]
        public string FolderID { get; set; }
        /// <summary>If TRUE: the file type and folder will be monitored</summary>
        [XmlElement]
        public bool FolderEnabled { get; set; }
        /// <summary>Filter to select the type of files to be monitored.
        /// (Examples: *.shp, *.*, Project00*.zip)</summary>
        [XmlElement]
        public string FolderFilter { get; set; }
        /// <summary>Full path to be monitored
        /// (i.e.: D:\files\projects\shapes\ )</summary>
        [XmlElement]
        public string FolderPath { get; set; }
        /// <summary>If TRUE: the folder and its subfolders will be monitored</summary>
        [XmlElement]
        public bool FolderIncludeSub { get; set; }
        /// <summary>Remote full path to synchronize with</summary>
        [XmlElement]
        public string RemoteFolderPath { get; set; }
        /// <summary>Manual sync process</summary>
        [XmlElement]
        public bool ManualSync { get; set; }
        /// <summary>Enable movw files process</summary>
        [XmlElement]
        public bool MoveFiles { get; set; }
        /// <summary>Interval time between polling process</summary>
        [XmlElement]
        public double IntervalTime { get; set; }
        /// <summary>Remote full path to synchronize with</summary>
        [XmlElement]
        public string IntervalUnit { get; set; }
        /// <summary>Default constructor of the class</summary>      
        public CustomStorageSettings() { }
        public CustomStorageSettings(string folderId, bool folderEnabled, string folderFilter, string folderPath, bool folderIncludeSub, string remoteFolderPath, bool manualSync, bool moveFiles, double intervalTime, string intervalUnit)
        {
            FolderID = folderId;
            FolderEnabled = folderEnabled;
            FolderFilter = folderFilter;
            FolderPath = folderPath;
            FolderIncludeSub = folderIncludeSub;
            RemoteFolderPath = remoteFolderPath;
            ManualSync = manualSync;
            MoveFiles = moveFiles;
            IntervalTime = intervalTime;
            IntervalUnit = intervalUnit;
        }
    }
}
