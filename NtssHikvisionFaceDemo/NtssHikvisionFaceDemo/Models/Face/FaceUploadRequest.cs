namespace NtssHikvisionFaceDemo.Models.Face;

public class FaceUploadRequest
{
    public string EmployeeNo { get; set; } = "";

    public string FaceLibType { get; set; } = "blackFD";

    public string FDID { get; set; } = "1";

    public string ImagePath { get; set; } = "";
}