using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace POSync_Updater
{
    [XmlRoot("UpdaterSettings")]
    public class UpdaterSettings
    {
        /// <summary>Check for update process interval in hours</summary>
        [XmlElement]
        public int ServiceCheckHoursInterval { get; set; }
        /// <summary>Check for update process interval in hours</summary>
        [XmlElement]
        public int ErrorCheckMinutesInterval { get; set; }
        /// <summary>Path for downloading new software version</summary>
        [XmlElement]
        public string NewVersionPath { get; set; }
        /// <summary>
        /// Class constructor
        /// </summary>
        public UpdaterSettings()
        {
            ServiceCheckHoursInterval = 1;
            ErrorCheckMinutesInterval = 10;
            NewVersionPath = @"version/";
        }
        public UpdaterSettings(int serviceCheckHoursInterval,int errorCheckMinutesInterval,string newVersionPath)
        {
            ServiceCheckHoursInterval = serviceCheckHoursInterval;
            ErrorCheckMinutesInterval = errorCheckMinutesInterval;
            NewVersionPath = newVersionPath;
        }
    }
}
