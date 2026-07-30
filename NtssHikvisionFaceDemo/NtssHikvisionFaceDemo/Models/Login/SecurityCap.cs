using System.Xml.Serialization;

[XmlRoot("SecurityCap", Namespace = "http://www.isapi.org/ver20/XMLSchema")]
public class SecurityCap
{
    [XmlAttribute("version")]
    public string Version { get; set; }

    [XmlElement("supportUserNums")]
    public int SupportUserNums { get; set; }

    [XmlElement("userBondIpNums")]
    public int UserBondIpNums { get; set; }

    [XmlElement("userBondMacNums")]
    public int UserBondMacNums { get; set; }

    [XmlElement("isSupCertificate")]
    public bool IsSupCertificate { get; set; }

    [XmlElement("issupIllegalLoginLock")]
    public bool IsSupIllegalLoginLock { get; set; }

    [XmlElement("isSupportOnlineUser")]
    public bool IsSupportOnlineUser { get; set; }

    [XmlElement("isSupportAnonymous")]
    public bool IsSupportAnonymous { get; set; }

    [XmlElement("isSupportStreamEncryption")]
    public bool IsSupportStreamEncryption { get; set; }

    [XmlElement("securityVersion")]
    public SecurityVersion SecurityVersion { get; set; }

    [XmlElement("keyIterateNum")]
    public int KeyIterateNum { get; set; }

    [XmlElement("isSupportUserCheck")]
    public bool IsSupportUserCheck { get; set; }

    [XmlElement("isSupportGUIDFileDataExport")]
    public bool IsSupportGUIDFileDataExport { get; set; }

    [XmlElement("isSupportSecurityQuestionConfig")]
    public bool IsSupportSecurityQuestionConfig { get; set; }

    [XmlElement("isSupportGetOnlineUserListSC")]
    public bool IsSupportGetOnlineUserListSC { get; set; }

    [XmlElement("SecurityLimits")]
    public SecurityLimits SecurityLimits { get; set; }

    [XmlElement("RSAKeyLength")]
    public OptionElement RSAKeyLength { get; set; }

    [XmlElement("isSupportONVIFUserManagement")]
    public bool IsSupportONVIFUserManagement { get; set; }

    [XmlElement("WebCertificateCap")]
    public WebCertificateCap WebCertificateCap { get; set; }

    [XmlElement("isSupportConfigFileImport")]
    public bool IsSupportConfigFileImport { get; set; }

    [XmlElement("isSupportConfigFileExport")]
    public bool IsSupportConfigFileExport { get; set; }

    [XmlElement("cfgFileSecretKeyLenLimit")]
    public MinMaxElement CfgFileSecretKeyLenLimit { get; set; }

    [XmlElement("salt")]
    public string Salt { get; set; } = "";

    [XmlElement("isSupportDeviceCertificatesManagement")]
    public bool IsSupportDeviceCertificatesManagement { get; set; }

    [XmlElement("isSupportSecurityEmail")]
    public bool IsSupportSecurityEmail { get; set; }

    [XmlElement("isSupportEncryptCertificate")]
    public bool IsSupportEncryptCertificate { get; set; }

    [XmlElement("isSupportCertificateCustomID")]
    public bool IsSupportCertificateCustomID { get; set; }
}

public class SecurityVersion
{
    [XmlAttribute("opt")]
    public string Opt { get; set; }

    [XmlText]
    public string Value { get; set; } = "";
}

public class OptionElement
{
    [XmlAttribute("opt")]
    public string Opt { get; set; }

    [XmlText]
    public string Value { get; set; } = "";
}

public class MinMaxElement
{
    [XmlAttribute("min")]
    public int Min { get; set; }

    [XmlAttribute("max")]
    public int Max { get; set; }
}

public class SecurityLimits
{
    [XmlElement("LoginPasswordLenLimit")]
    public MinMaxElement LoginPasswordLenLimit { get; set; }

    [XmlElement("SecurityAnswerLenLimit")]
    public MinMaxElement SecurityAnswerLenLimit { get; set; }
}

public class WebCertificateCap
{
    [XmlElement("CertificateType")]
    public OptionElement CertificateType { get; set; }
}