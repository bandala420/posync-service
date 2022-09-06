// Server query settings
using System.Xml.Serialization;

namespace POSync
{
    [XmlRoot("ServerQuerySettings")]
    public class ServerQuerySettings
    {
        [XmlElement("QuerySettings")]
        public QuerySettings[] QuerySettings { get; set; }
    }
    public class QuerySettings
    {
        /// <summary>Server instance name</summary>
        [XmlElement]
        public string Instance { get; set; }
        /// <summary>Table name from sql server</summary>
        [XmlElement]
        public string Table { get; set; }
        /// <summary>Columns names from table</summary>
        [XmlElement]
        public string Columns { get; set; }
        /// <summary>Conditional for functional command</summary>
        [XmlElement]
        public string Conditional { get; set; }
        /// <summary>Filename nomenclatura</summary>
        [XmlElement]
        public string FileName { get; set; }
        /// <summary>Folder name on server</summary>
        [XmlElement]
        public string RemoteFolder { get; set; }
        /// <summary>Days before for sql query</summary>
        [XmlElement]
        public int DaysBefore { get; set; }
    }
}
