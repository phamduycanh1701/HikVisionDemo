using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using NtssHikvisionFaceDemo.Models;
using NtssHikvisionFaceDemo.Models.Face;
using NtssHikvisionFaceDemo.Services;
using NtssHikvisionFaceDemo.Utils;

namespace NtssHikvisionFaceDemo.Controllers;

public class HikvisionController : Controller
{
    private readonly HikvisionClient _hik;

    public HikvisionController(HikvisionClient hik)
    {
        _hik = hik;
    }

    public async Task<IActionResult> DeviceInfo()
    {
        var xml = await _hik.GetAsync("/ISAPI/System/deviceInfo");

        return Content(xml, "application/xml");
    }

    /// <summary>
    /// Proxy anh tu thiet bi (faceURL / pictureURL) qua backend de tranh loi mixed-content
    /// khi trang web chay HTTPS (VD https://localhost:7193) con thiet bi chi co HTTP.
    /// Front-end goi: /Hikvision/ProxyImage?url={faceUrl hoac pictureURL da encode}
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ProxyImage([FromQuery] string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return BadRequest();

        var result = await _hik.DownloadImageAsync(url);

        if (result == null)
            return NotFound();

        return File(result.Value.Bytes, result.Value.ContentType);
    }
    
    [HttpPost]
    public async Task<IActionResult> UploadFace(
        IFormFile file,
        string employeeNo)
    {
        if (file == null)
            return BadRequest();

        var originalPath =
            Path.Combine(
                Path.GetTempPath(),
                Guid.NewGuid() + ".jpg");
        
        var resizedPath = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid()}_704x864.jpg");

        using (var fs = System.IO.File.Create(originalPath))
        {
            await file.CopyToAsync(fs);
        }
        
        // Resize về 704x864 quality 80
        ImageHelper.ResizeJpeg(
            originalPath,
            resizedPath,
            704,
            864,
            80);
        
        var fileLength1 = new FileInfo(originalPath).Length;
        decimal fileSize1 = fileLength1 / 1024;
        
        var fileLength2 = new FileInfo(resizedPath).Length;
        decimal fileSize2 = fileLength2 / 1024;

        var result =
            await _hik.UploadFaceAsync(
                new FaceUploadRequest
                {
                    EmployeeNo = employeeNo,
                    ImagePath = resizedPath
                });

        if (System.IO.File.Exists(originalPath))
            System.IO.File.Delete(originalPath);

        if (System.IO.File.Exists(resizedPath))
            System.IO.File.Delete(resizedPath);

        return Ok(result);
    }
    
    [HttpPost]
    public async Task<IActionResult> ListUser(
        [FromBody] PersonInfo personInfo,
        [FromQuery] int page = 0,
        [FromQuery] int pageSize = 12)
    {
        InfoEncryption.GetInitVector(out string iv);
        string keyword = !string.IsNullOrEmpty(personInfo.EmployeeNo) ? personInfo.EmployeeNo : personInfo.Name;
        var result = await _hik.SearchUserListAsync(iv, keyword, page, pageSize);
        
        return Ok(result);
    }
    
    [HttpPost]
    public async Task<IActionResult> GetUser([FromBody]PersonInfo personInfo)
    {
        InfoEncryption.GetInitVector(out string iv);
        
        var result = await _hik.GetUserAsync(personInfo.EmployeeNo, iv);
        return Ok(result);
    }
    
    [HttpPost]
    public async Task<IActionResult> CreateUserInfo([FromBody]PersonInfo personInfo)
    {
        var result = await _hik.CreateUserAsync(personInfo);

        return Ok(result);
    }
    
    [HttpPost]
    public async Task<IActionResult> UpdateUser([FromBody]PersonInfo personInfo)
    {
        InfoEncryption.GetInitVector(out string iv);
        
        var update = await _hik.UpdateUserAsync(iv, personInfo);
        return Ok(update);
    }
    
    [HttpPost]
    public async Task<IActionResult> DeleteUser([FromBody]PersonInfo personInfo)
    {
        InfoEncryption.GetInitVector(out string iv);
        var result = await _hik.DeleteUserListAsync(iv, personInfo.EmployeeNo);
        return Ok(result);
    }
    
    [HttpPost]
    public async Task<IActionResult> SearchAcsEventUser([FromBody]AcsEventSearch request)
    {
        InfoEncryption.GetInitVector(out string iv);
        var result = await _hik.SearchAcsEventAsync(iv, request.EmployeeNo, request.StartTime.ToString("yyyy-MM-ddTHH:mm:ss"), request.EndTime.ToString("yyyy-MM-ddTHH:mm:ss"));
        return Ok(result);
    }

    /// <summary>
    /// Buoc 1: yeu cau thiet bi chup van tay tu cam bien. Nguoi dung phai dat tay len dau doc
    /// TRUOC hoac trong luc goi API nay (API chi doc ket qua cam bien hien co, khong tu cho).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CaptureFingerPrint([FromQuery] int fingerNo = 1)
    {
        var result = await _hik.CaptureFingerPrintAsync(fingerNo);
        return Ok(result);
    }

    /// <summary>
    /// Buoc 2: dang ky van tay da chup (tu CaptureFingerPrint) vao thu vien van tay cua nhan vien.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SetUpFingerPrint([FromBody] FingerPrintSetUpRequest request)
    {
        var result = await _hik.SetUpFingerPrintAsync(request.EmployeeNo, request.FingerData, request.FingerNo);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> DeleteFingerPrint([FromBody] FingerPrintDeleteRequest request)
    {
        var result = await _hik.DeleteFingerPrintAsync(request.EmployeeNo, request.FingerNo);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> DeleteFace([FromBody] PersonInfo personInfo)
    {
        var result = await _hik.DeleteFaceAsync(personInfo.EmployeeNo);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> GetFingerPrints([FromBody] FingerPrintGetRequest request)
    {
        var result = await _hik.GetFingerPrintsAsync(request.EmployeeNo, request.CardReaderNo);
        return Ok(result);
    }
}