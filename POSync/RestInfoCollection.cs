// Folder settings for directories to be monitored
using System.Xml.Serialization;

namespace POSync
{
    [XmlRootAttribute("RestInfo")]
    public class RestInfoCollection
    {
        [XmlElement("Rest")]
        public CustomRestInfo[] CustomRestInfo { get; set; }
        /// <summary>
        /// Class constructor
        /// </summary>
        public RestInfoCollection()
        {
            // default information
            this.CustomRestInfo = new CustomRestInfo[]{
                new CustomRestInfo("MX00000", "Default", "00000", new Device[] { new Device("0", "Device", "Default", "Default")})
            };
            /*this.CustomRestInfo = new CustomRestInfo[]{
                new CustomRestInfo("MX00080", "Universidad", "00080", new Device[] { new Device("1", "POS", "MOSTRADOR", "cableado UTP"), new Device("2", "POS", "MOSTRADOR", "cableado UTP"), new Device("3", "POS", "MOSTRADOR", "cableado UTP"),new Device("4", "POS", "MOSTRADOR", "cableado UTP"),new Device("100", "PC_GERENTES", "", "LAN")}),
                new CustomRestInfo("MX00851", "Tuxtepec", "00851", new Device[] { new Device("2", "POS", "MOSTRADOR", "lan"), new Device("3", "POS", "MOSTRADOR", "lan"), new Device("4", "POS", "AUTOMAC_COBRADORA", "lan"),new Device("6", "POS", "CPO_REMOTO", "lan"), new Device("7", "POS", "CPO_REMOTO", "lan"), new Device("100", "PC_GERENTES", "", "LAN")})
            };*/
        }
    }
    public class CustomRestInfo
    {
        /// <summary>Unique identifier of the restaurant information</summary>
        [XmlAttribute]
        public string ID { get; set; }
        /// <summary>Name of rest</summary>
        [XmlElement]
        public string RestName { get; set; }
        /// <summary>Folder name on server</summary>
        [XmlElement]
        public string FolderName { get; set; }
        /// <summary> Available devices information</summary>
        [XmlElement("Device")]
        public Device[] Device { get; set; }
        /// <summary>
        /// Class constructor
        /// </summary>
        public CustomRestInfo() { }
        public CustomRestInfo(string id, string restName, string folderName, Device[] device)
        {
            ID = id;
            RestName = restName;
            FolderName = folderName;
            Device = device;
        }
    }
    public class Device
    {
        /// <summary> Available device number value</summary>
        [XmlText]
        public string Value { get; set; }
        /// <summary> Device type </summary>
        [XmlAttribute]
        public string Type { get; set; }
        /// <summary> Device entrance </summary>
        [XmlAttribute]
        public string Entrance { get; set; }
        /// <summary> Device connection type </summary>
        [XmlAttribute]
        public string Connection { get; set; }
        /// <summary>
        /// Class constructor
        /// </summary>
        public Device() { }
        public Device(string value,string type, string entrance, string connection)
        {
            Value = value;
            Type = type;
            Entrance = entrance;
            Connection = connection;
        }
    }
}
