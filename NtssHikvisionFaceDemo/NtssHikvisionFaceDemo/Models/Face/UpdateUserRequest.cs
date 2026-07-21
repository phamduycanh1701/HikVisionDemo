namespace NtssHikvisionFaceDemo.Models.Face;

public class UpdateUserRequest
{
    public string EmployeeNo { get; set; } = "";

    public string Name { get; set; } = "";

    public bool Enable { get; set; } = true;

    public string UserType { get; set; } = "normal";
}