using System.Xml.Serialization;

namespace NtssHikvisionFaceDemo.Models.Login;

[XmlRoot("SessionLoginCap", Namespace = "http://www.isapi.org/ver20/XMLSchema")]
public class SessionLoginCap
{
    [XmlElement("sessionID")]
    public string SessionID { get; set; } = "";

    [XmlElement("challenge")]
    public string Challenge { get; set; } = "";

    [XmlElement("iterations")]
    public int Iterations { get; set; }

    [XmlElement("isIrreversible")]
    public bool IsIrreversible { get; set; }

    [XmlElement("salt")]
    public string Salt { get; set; } = "";

    [XmlElement("isSupportSessionTag")]
    public bool IsSupportSessionTag { get; set; }

    [XmlElement("sessionIDVersion")]
    public int SessionIDVersion { get; set; }
    
    [XmlElement("securityVersion")]
    public int SecurityVersion { get; set; }
}