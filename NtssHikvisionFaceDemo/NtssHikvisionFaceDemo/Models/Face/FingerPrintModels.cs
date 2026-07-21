namespace NtssHikvisionFaceDemo.Models.Face;

/// <summary>
/// Ket qua chup van tay tho tu cam bien (CaptureFingerPrint) — request/response XML thuan,
/// KHONG ma hoa. fingerData la Base64 thuan cua template van tay, se duoc ma hoa o buoc SetUp.
/// </summary>
public class FingerPrintCaptureResult
{
    public string FingerData { get; set; } = string.Empty;
    public int FingerNo { get; set; }
    public int FingerPrintQuality { get; set; }
}

/// <summary>
/// Du lieu tu form UI gui len de dang ky (ghi) 1 van tay da chup vao thiet bi.
/// </summary>
public class FingerPrintSetUpRequest
{
    public string EmployeeNo { get; set; } = string.Empty;
    public string FingerData { get; set; } = string.Empty; // base64 tho, chua ma hoa (lay tu CaptureFingerPrint)
    public int FingerNo { get; set; } = 1;
}

/// <summary>
/// Du lieu tu form UI gui len de xoa van tay cua MOT ngon cu the (khong con xoa toan bo).
/// </summary>
public class FingerPrintDeleteRequest
{
    public string EmployeeNo { get; set; } = string.Empty;
    public int FingerNo { get; set; } = 1;
}

/// <summary>
/// Response cua FingerPrint/SetUp — dang rieng, khac HikBaseResponse, tra ve trang thai
/// nhan du lieu van tay cua tung dau doc (card reader) tren thiet bi.
/// </summary>
public class FingerPrintSetUpResponse
{
    public FingerPrintStatusWrapper? FingerPrintStatus { get; set; }
}

public class FingerPrintStatusWrapper
{
    public List<CardReaderRecvStatusModel> StatusList { get; set; } = new();
}

public class CardReaderRecvStatusModel
{
    public int Id { get; set; }
    public int CardReaderRecvStatus { get; set; }
}

public class FingerPrintGetRequest
{
    public string EmployeeNo { get; set; } = string.Empty;
    public int CardReaderNo { get; set; } = 1;
}

public class FingerPrintInfoModel
{
    public int CardReaderNo { get; set; }
    public int FingerPrintID { get; set; }
    public string FingerType { get; set; } = string.Empty;
    public string FingerData { get; set; } = string.Empty;
}

