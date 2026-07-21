using System.Xml.Serialization;

namespace NtssHikvisionFaceDemo.Models.Login;

[XmlRoot("SessionLogin", Namespace = "http://www.isapi.org/ver20/XMLSchema")]
public class SessionLoginResponse
{
    [XmlElement("statusValue")]
    public int StatusValue { get; set; }

    [XmlElement("statusString")]
    public string StatusString { get; set; } = "";

    [XmlElement("sessionTag")]
    public string SessionTag { get; set; } = "";

    [XmlElement("sessionID")]
    public string SessionID { get; set; } = "";

    [XmlElement("sessionIDVersion")]
    public int SessionIDVersion { get; set; }
}