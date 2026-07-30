namespace NtssHikvisionFaceDemo.Models;

public class HikvisionOptions
{
    public string DnsDomain { get; set; } = "";
    public string DefaultHost { get; set; } = "";
    public int DefaultPort { get; set; }
    public int Port { get; set; }
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}