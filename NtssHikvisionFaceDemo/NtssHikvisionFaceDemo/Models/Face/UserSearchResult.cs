namespace NtssHikvisionFaceDemo.Models.Face;

/// <summary>
/// Ket qua tim kiem nguoi dung tren thiet bi, kem tong so ban ghi khop (neu thiet bi tra ve)
/// de UI co the phan trang. Neu thiet bi khong tra ve "numOfMatches" thi Total se lay tam
/// theo so luong ban ghi cua trang hien tai (front-end se tu suy luan con trang sau hay khong).
/// </summary>
public class UserSearchResult
{
    public List<PersonInfo> Items { get; set; } = new();
    public int Total { get; set; }
}
