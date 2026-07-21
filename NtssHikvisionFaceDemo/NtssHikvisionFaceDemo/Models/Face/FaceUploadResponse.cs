using System.Text.Json.Serialization;
using System.Xml.Serialization;
using Newtonsoft.Json;

namespace NtssHikvisionFaceDemo.Models.Face;

[Serializable]
public class FaceUploadResponse
{
    public int statusCode { get; set; }

    public string statusString { get; set; } = "";

    public string subStatusCode { get; set; } = "";
}

[Serializable]
public class HikBaseResponse
{
    public int statusCode { get; set; }

    public string statusString { get; set; } = "";

    public string subStatusCode { get; set; } = "";
}

[Serializable]
public class HikDeleteFPBaseResponse
{
    public FingerPrintStatus FingerPrintStatus { get; set; }
}

[Serializable]
public class FingerPrintStatus
{
    public string status { get; set; } = "";
}