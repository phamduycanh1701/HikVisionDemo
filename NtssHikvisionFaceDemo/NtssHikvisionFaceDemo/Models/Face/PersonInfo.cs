namespace NtssHikvisionFaceDemo.Models.Face;

public class PersonInfo
{
    public string EmployeeNo { get; set; } = string.Empty;   // vi du: "1001"
    public string Name { get; set; } = string.Empty;         // ten hien thi
    public string Gender { get; set; } = string.Empty;
    public string UserType { get; set; } = "normal";         // normal | visitor | blackList
    public bool Enabled { get; set; } = true;                // false: không set thời hạn (là người dùng lâu dài), true: set thời hạn (theo hợp đồng) 
    public DateTime ValidFrom { get; set; } = DateTime.Now;
    public DateTime ValidTo { get; set; } = DateTime.Now.AddYears(10);
    public string FaceUrl { get; set; }
    public int NumOfCard { get; set; }
    public int NumOfFace { get; set; }
    public int NumOfFingerprint { get; set; }
    public bool LocalUIRight { get; set; }
}

public class PersonEventInfo
{
    public string EmployeeNo { get; set; } = string.Empty;   // vi du: "1001"
    public string Name { get; set; } = string.Empty;         // ten hien thi
    public string UserType { get; set; } = "normal";         // normal | visitor | blackList
    public string CurrentVerifyMode { get; set; }
    public DateTime Time { get; set; }
    public string PictureURL { get; set; }
}

public class AcsEventSearch
{
    public string EmployeeNo { get; set; } = string.Empty;
    public DateTime StartTime { get; set; } = DateTime.Now;
    public DateTime EndTime { get; set; } = DateTime.Now;
}