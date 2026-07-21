using System.Net;
using System.Net.Http.Headers;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using NtssHikvisionFaceDemo.Models;
using NtssHikvisionFaceDemo.Models.Face;
using NtssHikvisionFaceDemo.Models.Login;
using NtssHikvisionFaceDemo.Utils;

namespace NtssHikvisionFaceDemo.Services;

public class HikvisionClient
{
    private readonly HikvisionOptions _options;

    private readonly CookieContainer _cookie = new();

    private readonly HttpClient _client;

    private string? _salt;

    private string? _sessionTag;

    private bool _loggedIn;

    private readonly SemaphoreSlim _loginLock = new(1, 1);

    public HikvisionClient(IOptions<HikvisionOptions> options)
    {
        _options = options.Value;

        var handler = new HttpClientHandler
        {
            CookieContainer = _cookie,
            UseCookies = true,
            AutomaticDecompression =
                DecompressionMethods.Deflate |
                DecompressionMethods.GZip
        };

        _client = new HttpClient(handler);

        _client.Timeout = TimeSpan.FromSeconds(30);

        _client.BaseAddress =
            new Uri($"http://{_options.Host}:{_options.Port}");
    }

    private async Task EnsureLoginAsync()
    {
        if (_loggedIn)
            return;

        await _loginLock.WaitAsync();

        try
        {
            if (_loggedIn)
                return;

            await LoginAsync();

            _loggedIn = true;
        }
        finally
        {
            _loginLock.Release();
        }
    }

    private async Task LoginAsync()
    {
        var random = LoginEncryptHelper.GenerateRandom();

        var capXml = await _client.GetStringAsync(
            $"/ISAPI/Security/sessionLogin/capabilities?username={_options.Username}&random={random}");

        var cap = XmlHelper.Deserialize<SessionLoginCap>(capXml);

        _salt = cap.Salt;

        var password = LoginEncryptHelper.EncodePassword(
            _options.Username,
            _options.Password,
            cap.Salt,
            cap.Challenge,
            cap.Iterations,
            cap.IsIrreversible);

        var xml =
            $"<SessionLogin><userName>{_options.Username}</userName><password>{password}</password><sessionID>{cap.SessionID}</sessionID><isSessionIDValidLongTerm>true</isSessionIDValidLongTerm><sessionIDVersion>{cap.SessionIDVersion}</sessionIDVersion><isNeedSessionTag>true</isNeedSessionTag></SessionLogin>";

        var response = await _client.PostAsync(
            $"/ISAPI/Security/sessionLogin?timeStamp={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            new StringContent(xml, Encoding.UTF8, "application/x-www-form-urlencoded"));

        var body = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();

        var loginXml = await response.Content.ReadAsStringAsync();

        var login =
            XmlHelper.Deserialize<SessionLoginResponse>(loginXml);

        if (login.StatusValue != 200)
            throw new Exception(login.StatusString);


        _sessionTag = login.SessionTag;
    }

    private HttpRequestMessage CreateRequest(
        HttpMethod method,
        string url)
    {
        var request = new HttpRequestMessage(method, url);

        if (!string.IsNullOrWhiteSpace(_sessionTag))
        {
            request.Headers.Add("SessionTag", _sessionTag);
        }

        return request;
    }

    /// <summary>
    /// Goi request toi thiet bi; neu thiet bi tra ve 401 (session/SessionTag da het han hoac bi thu hoi),
    /// tu dong dang nhap lai va thu lai request 1 lan duy nhat. Nho co ham nay ma ung dung khong bi
    /// "ket cung" (mac dinh _loggedIn=true mai mai) moi khi thiet bi tu ket thuc phien lam viec —
    /// truoc day phai restart lai app moi dang nhap lai duoc.
    /// buildRequest duoc goi lai (rebuild request moi) vi HttpRequestMessage chi gui duoc 1 lan.
    /// </summary>
    private async Task<HttpResponseMessage> SendWithSessionRetryAsync(
        Func<HttpRequestMessage> buildRequest,
        HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead,
        CancellationToken cancellationToken = default)
    {
        await EnsureLoginAsync();

        var response = await _client.SendAsync(buildRequest(), completionOption, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            response.Dispose();

            _loggedIn = false; // ep dang nhap lai vi phien hien tai da bi thiet bi tu choi
            await EnsureLoginAsync();

            response = await _client.SendAsync(buildRequest(), completionOption, cancellationToken);
        }

        return response;
    }

    private string EncryptFieldData(string value, string iv)
    {
        InfoEncryption.getEncryptKey(_options.Username, _salt, _options.Password, out var szOut, 100, true);
        InfoEncryption.getEncryptContent(iv, szOut, value, out string valueEncrypt, 1);
        return valueEncrypt;
    }

    /// <summary>
    /// Ma hoa cho du lieu DA LA BASE64 SAN (VD: fingerData tu CaptureFingerPrint la base64 cua
    /// template van tay nhi phan). KHONG dung EncryptFieldData o day vi ham do se Base64-hoa THEM
    /// 1 lop nua (danh cho gia tri text thuong nhu employeeNo/name), gay ra base64 long 2 lop va
    /// thiet bi tra loi "badParameters" / errorMsg "fingerData". O day chi AES + hex dung 1 lop.
    /// </summary>
    private string EncryptRawBase64Field(string alreadyBase64Value, string iv)
    {
        InfoEncryption.getEncryptKey(_options.Username, _salt, _options.Password, out var szAESKey, 100, true);

        byte[] ivUtf8 = Encoding.UTF8.GetBytes(iv);
        byte[] ivBytes = AESEncryption.convertCharArrayToByteArray(ivUtf8, ivUtf8.Length);

        byte[] aesData = AESEncryption.AesEncrypt(alreadyBase64Value, szAESKey, ivBytes);
        byte[] hexBytes = AESEncryption.converByteArrayToCharArray(aesData, aesData.Length);

        return Encoding.UTF8.GetString(hexBytes);
    }

    private string DecryptRawBase64Field(string encryptedHexValue, string iv)
    {
        InfoEncryption.getEncryptKey(_options.Username, _salt, _options.Password, out var szAESKey, 100, true);

        byte[] ivUtf8 = Encoding.UTF8.GetBytes(iv);
        byte[] ivBytes = AESEncryption.convertCharArrayToByteArray(ivUtf8, ivUtf8.Length);

        byte[] hexBytes = Encoding.UTF8.GetBytes(encryptedHexValue);
        byte[] aesData = AESEncryption.convertCharArrayToByteArray(hexBytes, hexBytes.Length);

        return AESEncryption.AesDecrypt(aesData, szAESKey, ivBytes);
    }

    private string DecryptFieldData(string value, string iv)
    {
        InfoEncryption.getEncryptKey(_options.Username, _salt, _options.Password, out var szOut, 100, true);
        InfoEncryption.getDecryptContent(iv, szOut, value, out string valueDecrypt, 1);
        return valueDecrypt;
    }

    public async Task<string> GetAsync(string url)
    {
        var response = await SendWithSessionRetryAsync(() => CreateRequest(HttpMethod.Get, url));

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Tải ảnh thô từ thiết bị (VD: faceURL, pictureURL cua su kien checkin) va tra ve raw bytes + content-type.
    /// Dung de proxy anh qua backend: trinh duyet dang chay HTTPS (VD https://localhost:7193) se bi chan
    /// "mixed content" neu <img src> tro thang toi http://{ip-thiet-bi}/..., nen phai de server goi ho
    /// (server-to-server khong bi trinh duyet ap dung chinh sach mixed-content) roi tra anh ve qua HTTPS.
    /// </summary>
    public async Task<(byte[] Bytes, string ContentType)?> DownloadImageAsync(
        string imageUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return null;

        // faceURL / pictureURL thiet bi tra ve la URL tuyet doi (http://host:port/...),
        // HttpClient cho phep goi URL tuyet doi ngay ca khi da co BaseAddress rieng.
        using var response = await SendWithSessionRetryAsync(
            () => CreateRequest(HttpMethod.Get, imageUrl),
            HttpCompletionOption.ResponseContentRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
            return null;

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";

        return (bytes, contentType);
    }

    public async Task<string> CreateUserAsync(PersonInfo person)
    {
        await EnsureLoginAsync();

        var payload = new JObject
        {
            ["UserInfo"] = new JObject
            {
                ["employeeNo"] = person.EmployeeNo,
                ["name"] = person.Name,
                ["userType"] = person.UserType,
                ["gender"] = person.Gender,
                ["Valid"] = new JObject
                {
                    ["enable"] = true,
                    ["beginTime"] = person.ValidFrom.ToString("yyyy-MM-ddTHH:mm:ss"),
                    ["endTime"] = person.ValidTo.ToString("yyyy-MM-ddTHH:mm:ss")
                },
                ["doorRight"] = "1",
                ["RightPlan"] = new JArray
                {
                    new JObject { ["doorNo"] = 1, ["planTemplateNo"] = "1" }
                }
            }
        };

        var fdToken = await GetFDTokenAsync();

        var json = payload.ToString();

        var response = await SendWithSessionRetryAsync(() =>
        {
            var req = CreateRequest(HttpMethod.Post,
                $"/ISAPI/AccessControl/UserInfo/Record?format=json&token={fdToken}");
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
            return req;
        });

        var body = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();
        return body;
    }

    public async Task<FaceUploadResponse> UploadFaceAsync(
        FaceUploadRequest request)
    {
        await EnsureLoginAsync();

        if (!File.Exists(request.ImagePath))
            throw new FileNotFoundException(request.ImagePath);

        var faceInfo =
            $"{{\"faceLibType\":\"blackFD\",\"FDID\":\"1\",\"FPID\":\"{request.EmployeeNo}\"}}";

        var fileName = Path.GetFileName(request.ImagePath);
        var imageBytes = await File.ReadAllBytesAsync(request.ImagePath);

        // Build lai toan bo request (bao gom body multipart) moi lan goi, vi HttpRequestMessage/
        // StreamContent chi gui duoc 1 lan — can thiet de co the thu lai khi phien het han (401).
        HttpRequestMessage BuildRequest(string fdTokenValue)
        {
            var boundary = "----WebKitFormBoundary" + Guid.NewGuid().ToString("N")[..16];
            var ms = new MemoryStream();

            var header1 =
                $"--{boundary}\r\n" +
                $"Content-Disposition: form-data; name=\"FaceDataRecord\"\r\n" +
                $"\r\n" +
                $"{faceInfo}\r\n";
            var header1Bytes = Encoding.ASCII.GetBytes(header1);
            ms.Write(header1Bytes);

            var header2 =
                $"--{boundary}\r\n" +
                $"Content-Disposition: form-data; name=\"img\"; filename=\"{fileName}\"\r\n" +
                $"Content-Type: image/jpeg\r\n" +
                $"\r\n";
            var header2Bytes = Encoding.ASCII.GetBytes(header2);
            ms.Write(header2Bytes);

            ms.Write(imageBytes);
            ms.Write(Encoding.ASCII.GetBytes("\r\n"));

            var footer = $"--{boundary}--\r\n";
            ms.Write(Encoding.ASCII.GetBytes(footer));

            ms.Position = 0;

            var req = CreateRequest(
                HttpMethod.Put,
                $"/ISAPI/Intelligent/FDLib/FDSetUp?format=json&token={fdTokenValue}");

            req.Headers.Accept.ParseAdd("*/*");
            req.Headers.Add("X-Requested-With", "XMLHttpRequest");

            req.Content = new StreamContent(ms);
            req.Content.Headers.ContentType =
                MediaTypeHeaderValue.Parse($"multipart/form-data; boundary={boundary}");

            return req;
        }

        var fdToken = await GetFDTokenAsync();

        var response = await SendWithSessionRetryAsync(() => BuildRequest(fdToken));

        var body = await response.Content.ReadAsStringAsync();

        Console.WriteLine(body);

        response.EnsureSuccessStatusCode();

        return JsonSerializer.Deserialize<FaceUploadResponse>(body);
    }

    private async Task<string> GetFDTokenAsync()
    {
        var response = await SendWithSessionRetryAsync(() =>
        {
            var req = CreateRequest(HttpMethod.Get, "/ISAPI/Security/token?format=json");
            req.Headers.Accept.ParseAdd("application/json");
            return req;
        });

        response.EnsureSuccessStatusCode();

        var json =
            await response.Content.ReadAsStringAsync();

        using var doc =
            JsonDocument.Parse(json);

        return doc
            .RootElement
            .GetProperty("Token")
            .GetProperty("value")
            .GetString()!;
    }

    public async Task<UserSearchResult> SearchUserListAsync(
        string iv,
        string keyword,
        int page = 0,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        await EnsureLoginAsync();

        string searchId = Guid.NewGuid().ToString("N");

        var a = EncryptFieldData("MzAxGCxZFUikh0ghFUjMhlKpJii8FE61FGi0fFlVFji8iF6FFRioDmoFFUisfEM1Fkiki4blJjicFKCVJiiwi52xJVichbKNFli0FKrJFDiYBr6lFUicgcX9FDiUfPmdJjjEl8whJTiQBuUFFZiMd2pxJCioBXJdFDisfSX9JDiwEE8dFIio7mEpJFi4dQvyJOi4bgAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAYRMAMCJEEakKhfEOEQTTDYFOZRRFgAwG7w4SMRQGfAoeIYGqDnzSLAESdBAGkWcUE40FDsEMFBXkCYurH0aDugsUb1MCMdwSBTlBIHIhE31REwQQAASIZxZAwscRd8FyEBFUAYYuUgIx2QN8YE0EM94Hi3oABHE3AhTvHUAxuwCHKnXGe+APlwAAAAAAe2I=", "2563c847ab7384b1e00f0da40b9c8b4c");

        var requestObj = new
        {
            UserInfoSearchCond = new
            {
                searchID = searchId,
                maxResults = pageSize,
                searchResultPosition = page * pageSize,
                fuzzySearch = !string.IsNullOrWhiteSpace(keyword) ? EncryptFieldData(keyword, iv) : ""
            }
        };

        string json = JsonSerializer.Serialize(requestObj);

        using var response = await SendWithSessionRetryAsync(() =>
        {
            var req = CreateRequest(HttpMethod.Post,
                $"/ISAPI/AccessControl/UserInfo/Search?format=json&security=1&iv={iv}");
            req.Headers.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
            req.Content = new StringContent(json, Encoding.UTF8, "multipart/form-data");
            return req;
        }, HttpCompletionOption.ResponseContentRead, cancellationToken);

        string body = await response.Content.ReadAsStringAsync(cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = new UserSearchResult();

        using var doc = JsonDocument.Parse(body);

        if (!doc.RootElement.TryGetProperty("UserInfoSearch", out var search))
            return result;

        // Thiet bi Hikvision thuong tra ve "numOfMatches" hoac "totalMatches" trong UserInfoSearch
        if (search.TryGetProperty("numOfMatches", out var numOfMatches))
            result.Total = numOfMatches.GetInt32();
        else if (search.TryGetProperty("totalMatches", out var totalMatches))
            result.Total = totalMatches.GetInt32();

        if (!search.TryGetProperty("UserInfo", out var users))
            return result;

        foreach (var item in users.EnumerateArray())
        {
            var beginTime = DateTime.Now;
            var endTime = DateTime.Now.AddYears(10);
            if (item.TryGetProperty("Valid", out var valid))
            {
                beginTime = valid.TryGetProperty("beginTime", out var beginTimeValue)
                    ? beginTimeValue.GetDateTime()
                    : DateTime.MinValue;
                endTime = valid.TryGetProperty("endTime", out var endTimeValue)
                    ? endTimeValue.GetDateTime()
                    : DateTime.MinValue;
            }

            result.Items.Add(new PersonInfo
            {
                EmployeeNo = DecryptFieldData(
                    item.GetProperty("employeeNo").GetString() ?? "",
                    iv),

                Name = DecryptFieldData(
                    item.GetProperty("name").GetString() ?? "",
                    iv),

                Gender = item.TryGetProperty("gender", out var gender)
                    ? gender.GetString() ?? ""
                    : "",

                UserType = item.TryGetProperty("userType", out var userType)
                    ? userType.GetString() ?? ""
                    : "",

                // userType "blackList" nghia la nhan vien dang bi vo hieu hoa (theo quy uoc
                // UpdateUserAsync dang dung: Enabled=false => luu userType="blackList").
                Enabled = (userType.ValueKind != JsonValueKind.Undefined ? userType.GetString() : "normal") != "blackList",

                FaceUrl = item.TryGetProperty("faceURL", out var faceUrl)
                    ? faceUrl.GetString() + "?token=" + await GetFDTokenAsync() ?? ""
                    : "",

                NumOfCard = item.TryGetProperty("numOfCard", out var numOfCard)
                    ? numOfCard.GetInt32()
                    : 0,

                NumOfFace = item.TryGetProperty("numOfFace", out var numOfFace)
                    ? numOfFace.GetInt32()
                    : 0,

                NumOfFingerprint = item.TryGetProperty("numOfFP", out var numOfFp)
                    ? numOfFp.GetInt32()
                    : 0,

                LocalUIRight = item.TryGetProperty("localUIRight", out var localUIRight)
                               && localUIRight.GetBoolean(),

                ValidFrom = beginTime,
                ValidTo = endTime
            });
        }

        if (result.Total == 0)
            result.Total = result.Items.Count; // fallback: thiet bi khong tra tong, dung tam so ban ghi trang hien tai

        return result;
    }

    public async Task<HikBaseResponse> UpdateUserAsync(
        string iv, PersonInfo personInfo,
        CancellationToken cancellationToken = default)
    {
        await EnsureLoginAsync();

        var employeeNoEncrypt = EncryptFieldData(personInfo.EmployeeNo, iv);
        var employeeNameEncrypt = EncryptFieldData(personInfo.Name, iv);
        var beginTime = personInfo.ValidFrom.ToString("yyyy-MM-ddTHH:mm:ss");
        var endTime = personInfo.ValidTo.ToString("yyyy-MM-ddTHH:mm:ss");

        var requestObj = new
        {
            UserInfo = new
            {
                employeeNo = employeeNoEncrypt,
                name = employeeNameEncrypt,
                userType = personInfo.Enabled ? "normal" : "blackList",
                Valid = new
                {
                    enable = true,
                    beginTime = beginTime,
                    endTime = endTime,
                    timeType = "local",
                },
                rightPlan = new[]
                {
                    new
                    {
                        doorNo = 1,
                        planTemplateNo = "1"
                    }
                },
                doorRight = "1",
                gender = personInfo.Gender,
                localUIRight = personInfo.LocalUIRight,
                userVerifyMode = "",
                floorNumbers = new object[0],
                callNumbers = new object[0],
                password = "",
                closeDelayEnabled = false,
                onlyVerify = false
            }
        };

        string json = JsonSerializer.Serialize(requestObj);

        var response = await SendWithSessionRetryAsync(() =>
        {
            var req = CreateRequest(HttpMethod.Put,
                $"/ISAPI/AccessControl/UserInfo/Modify?format=json&security=1&iv={iv}");
            req.Headers.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
            req.Content = new StringContent(json, Encoding.UTF8, "multipart/form-data");
            return req;
        });

        var body = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();

        return JsonSerializer.Deserialize<HikBaseResponse>(body);
    }

    public async Task<HikBaseResponse> DeleteUserListAsync(
        string iv,
        string employeeNo,
        CancellationToken cancellationToken = default)
    {
        await EnsureLoginAsync();

        string searchId = Guid.NewGuid().ToString("N");

        var employeeNoEncrypt = EncryptFieldData(employeeNo, iv);

        var requestObj = new
        {
            UserInfoDetail = new
            {
                mode = "byEmployeeNo",
                EmployeeNoList = new[]
                {
                    new
                    {
                        employeeNo = employeeNoEncrypt
                    }
                }
            }
        };

        string json = JsonSerializer.Serialize(requestObj);

        using var response = await SendWithSessionRetryAsync(() =>
        {
            var req = CreateRequest(HttpMethod.Put,
                $"/ISAPI/AccessControl/UserInfoDetail/Delete?format=json&security=1&iv={iv}");
            req.Headers.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
            req.Content = new StringContent(json, Encoding.UTF8, "multipart/form-data");
            return req;
        }, HttpCompletionOption.ResponseContentRead, cancellationToken);

        string body = await response.Content.ReadAsStringAsync(cancellationToken);

        response.EnsureSuccessStatusCode();

        return JsonSerializer.Deserialize<HikBaseResponse>(body);
    }

    public async Task<List<PersonInfo>> GetUserAsync(
        string employeeNo, string iv,
        CancellationToken cancellationToken = default)
    {
        await EnsureLoginAsync();

        string searchId = Guid.NewGuid().ToString("N");

        var employeeNoEncrypt = EncryptFieldData(employeeNo, iv);

        var requestObj = new
        {
            UserInfoSearchCond = new
            {
                searchID = searchId,
                maxResults = 20,
                searchResultPosition = 0,
                EmployeeNoList = new[]
                {
                    new
                    {
                        employeeNo = employeeNoEncrypt
                    }
                }
            }
        };

        string json = JsonSerializer.Serialize(requestObj);

        using var response = await SendWithSessionRetryAsync(() =>
        {
            var req = CreateRequest(HttpMethod.Post,
                $"/ISAPI/AccessControl/UserInfo/Search?format=json&security=1&iv={iv}");
            req.Headers.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
            req.Content = new StringContent(json, Encoding.UTF8, "multipart/form-data");
            return req;
        }, HttpCompletionOption.ResponseContentRead, cancellationToken);

        string body = await response.Content.ReadAsStringAsync(cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = new List<PersonInfo>();

        using var doc = JsonDocument.Parse(body);

        if (!doc.RootElement.TryGetProperty("UserInfoSearch", out var search))
            return result;

        if (!search.TryGetProperty("UserInfo", out var users))
            return result;

        foreach (var item in users.EnumerateArray())
        {
            var beginTime = DateTime.Now;
            var endTime = DateTime.Now.AddYears(10);
            if (item.TryGetProperty("Valid", out var valid))
            {
                beginTime = valid.TryGetProperty("beginTime", out var beginTimeValue)
                    ? beginTimeValue.GetDateTime()
                    : DateTime.MinValue;
                endTime = valid.TryGetProperty("endTime", out var endTimeValue)
                    ? endTimeValue.GetDateTime()
                    : DateTime.MinValue;
            }

            result.Add(new PersonInfo
            {
                EmployeeNo = DecryptFieldData(
                    item.GetProperty("employeeNo").GetString() ?? "",
                    iv),

                Name = DecryptFieldData(
                    item.GetProperty("name").GetString() ?? "",
                    iv),

                Gender = item.TryGetProperty("gender", out var gender)
                    ? gender.GetString() ?? ""
                    : "",

                UserType = item.TryGetProperty("userType", out var userType)
                    ? userType.GetString() ?? ""
                    : "",

                // userType "blackList" nghia la nhan vien dang bi vo hieu hoa (theo quy uoc
                // UpdateUserAsync dang dung: Enabled=false => luu userType="blackList").
                Enabled = (userType.ValueKind != JsonValueKind.Undefined ? userType.GetString() : "normal") != "blackList",

                FaceUrl = item.TryGetProperty("faceURL", out var faceUrl)
                    ? faceUrl.GetString() + "?token=" + await GetFDTokenAsync() ?? ""
                    : "",

                NumOfCard = item.TryGetProperty("numOfCard", out var numOfCard)
                    ? numOfCard.GetInt32()
                    : 0,

                NumOfFace = item.TryGetProperty("numOfFace", out var numOfFace)
                    ? numOfFace.GetInt32()
                    : 0,

                NumOfFingerprint = item.TryGetProperty("numOfFP", out var numOfFp)
                    ? numOfFp.GetInt32()
                    : 0,

                LocalUIRight = item.TryGetProperty("localUIRight", out var localUIRight)
                               && localUIRight.GetBoolean(),

                ValidFrom = beginTime,
                ValidTo = endTime
            });
        }

        return result;
    }

    public async Task<List<PersonEventInfo>> SearchAcsEventAsync(
        string iv,
        string employeeNo,
        string startTime,
        string endTime,
        int page = 0,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        await EnsureLoginAsync();

        string searchId = Guid.NewGuid().ToString("N");

        var requestObj = new
        {
            AcsEventCond = new
            {
                searchID = searchId,
                searchResultPosition = 0,
                maxResults = 50,
                major = 5,
                minor = 0,
                startTime = startTime,
                endTime = endTime,
                employeeNoString = EncryptFieldData(employeeNo, iv),
                timeReverseOrder = true
            }
        };

        string json = JsonSerializer.Serialize(requestObj);

        using var response = await SendWithSessionRetryAsync(() =>
        {
            var req = CreateRequest(HttpMethod.Post, $"/ISAPI/AccessControl/AcsEvent?format=json&security=1&iv={iv}");
            req.Headers.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
            req.Content = new StringContent(json, Encoding.UTF8, "multipart/form-data");
            return req;
        }, HttpCompletionOption.ResponseContentRead, cancellationToken);

        string body = await response.Content.ReadAsStringAsync(cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = new List<PersonEventInfo>();

        using var doc = JsonDocument.Parse(body);

        if (!doc.RootElement.TryGetProperty("AcsEvent", out var search))
            return result;

        if (!search.TryGetProperty("InfoList", out var events))
            return result;

        var fdToken = await GetFDTokenAsync();

        foreach (var item in events.EnumerateArray())
        {
            var itemPerson = new PersonEventInfo();
            try
            {
                itemPerson.EmployeeNo = DecryptFieldData(
                    item.GetProperty("employeeNoString").GetString() ?? "",
                    iv);
                
                var employeeName = item.TryGetProperty("name", out var name)
                    ? name.GetString() ?? ""
                    : "";

                itemPerson.Name = DecryptFieldData(employeeName, iv);

                itemPerson.UserType = item.TryGetProperty("userType", out var userType)
                    ? userType.GetString() ?? ""
                    : "";

                itemPerson.CurrentVerifyMode = item.GetProperty("currentVerifyMode").GetString() ?? "";

                itemPerson.Time = item.TryGetProperty("time", out var time)
                    ? time.GetDateTime()
                    : DateTime.MinValue;

                itemPerson.PictureURL = item.TryGetProperty("pictureURL", out var pictureURL)
                    ? pictureURL.GetString() + "?token=" + fdToken ?? ""
                    : "";
                
                result.Add(itemPerson);
            }
            catch (Exception ex)
            {
                
            }
        }

        return result;
    }

    /// <summary>
    /// Buoc 1: yeu cau thiet bi chup van tay tu cam bien (nguoi dung dat tay len dau doc truoc khi goi).
    /// Request/response cua endpoint nay la XML THUAN, KHONG ma hoa (khong co security=1&iv=...),
    /// dung y het curl da bat duoc tu web UI goc.
    /// fingerData tra ve la Base64 tho cua template van tay — se duoc ma hoa o buoc SetUpFingerPrintAsync.
    /// </summary>
    public async Task<FingerPrintCaptureResult> CaptureFingerPrintAsync(
        int fingerNo = 1,
        CancellationToken cancellationToken = default)
    {
        var xmlBody =
            "<CaptureFingerPrintCond version=\"2.0\" xmlns=\"http://www.isapi.org/ver20/XMLSchema\">" +
            $"<fingerNo>{fingerNo}</fingerNo>" +
            "</CaptureFingerPrintCond>";

        using var response = await SendWithSessionRetryAsync(() =>
        {
            var req = CreateRequest(HttpMethod.Post, "/ISAPI/AccessControl/CaptureFingerPrint");
            req.Headers.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
            req.Content = new StringContent(xmlBody, Encoding.UTF8, "application/x-www-form-urlencoded");
            return req;
        }, HttpCompletionOption.ResponseContentRead, cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        response.EnsureSuccessStatusCode();

        // Response la XML thuan: <CaptureFingerPrint>...<fingerData>...<fingerNo>...<fingerPrintQuality>...
        var doc = XDocument.Parse(body);
        var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;

        var fingerData = doc.Root?.Element(ns + "fingerData")?.Value ?? "";
        var fingerNoStr = doc.Root?.Element(ns + "fingerNo")?.Value ?? fingerNo.ToString();
        var qualityStr = doc.Root?.Element(ns + "fingerPrintQuality")?.Value ?? "0";

        return new FingerPrintCaptureResult
        {
            FingerData = fingerData,
            FingerNo = int.TryParse(fingerNoStr, out var fn) ? fn : fingerNo,
            FingerPrintQuality = int.TryParse(qualityStr, out var q) ? q : 0
        };
    }

    /// <summary>
    /// Buoc 2: dang ky (ghi) van tay da chup o buoc 1 vao thu vien van tay cua nhan vien tren thiet bi.
    /// employeeNo va fingerData deu duoc ma hoa AES bang EncryptFieldData — dung het co che
    /// security=1&iv=... da dung cho UserInfo/Search, UserInfo/Modify, UserInfoDetail/Delete...
    /// </summary>
    public async Task<FingerPrintSetUpResponse?> SetUpFingerPrintAsync(
        string employeeNo,
        string fingerDataBase64,
        int fingerNo = 1,
        int[]? enableCardReader = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureLoginAsync();

        InfoEncryption.GetInitVector(out var iv);

        var employeeNoEncrypt = EncryptFieldData(employeeNo, iv);
        var fingerDataEncrypt = EncryptRawBase64Field(fingerDataBase64, iv);

        var fdToken = await GetFDTokenAsync();

        var requestObj = new
        {
            FingerPrintCfg = new
            {
                employeeNo = employeeNoEncrypt,
                fingerPrintID = fingerNo,
                fingerType = "normalFP",
                fingerData = fingerDataEncrypt,
                enableCardReader = enableCardReader ?? new[] { 1, 2 }
            }
        };

        string json = JsonSerializer.Serialize(requestObj);

        using var response = await SendWithSessionRetryAsync(() =>
        {
            var req = CreateRequest(HttpMethod.Post,
                $"/ISAPI/AccessControl/FingerPrint/SetUp?format=json&security=1&iv={iv}&token={fdToken}");
            req.Headers.TryAddWithoutValidation("Accept", "*/*");
            req.Headers.Add("X-Requested-With", "XMLHttpRequest");
            req.Content = new StringContent(json, Encoding.UTF8, "multipart/form-data");
            return req;
        }, HttpCompletionOption.ResponseContentRead, cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        response.EnsureSuccessStatusCode();

        // Response cua thiet bi lan lon ca PascalCase ("FingerPrintStatus","StatusList") lan
        // camelCase ("id","cardReaderRecvStatus"), nen phai deserialize khong phan biet hoa/thuong.
        return JsonSerializer.Deserialize<FingerPrintSetUpResponse>(
            body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    /// <summary>
    /// Xoa vân tay của MỘT ngón cụ thể (fingerNo) của nhân viên trên thiết bị — dùng chung endpoint
    /// FingerPrint/SetUp (giống lúc dang ky vân tay) nhung kem co "deleteFingerPrint": true.
    /// </summary>
    public async Task<HikDeleteFPBaseResponse?> DeleteFingerPrintAsync(
        string employeeNo,
        int fingerNo,
        CancellationToken cancellationToken = default)
    {
        await EnsureLoginAsync();

        InfoEncryption.GetInitVector(out var iv);

        var employeeNoEncrypt = EncryptFieldData(employeeNo, iv);

        var requestObj = new
        {
            FingerPrintCfg = new
            {
                employeeNo = employeeNoEncrypt,
                fingerPrintID = fingerNo,
                fingerType = "normalFP",
                deleteFingerPrint = true
            }
        };

        string json = JsonSerializer.Serialize(requestObj);

        using var response = await SendWithSessionRetryAsync(() =>
        {
            var req = CreateRequest(HttpMethod.Post,
                $"/ISAPI/AccessControl/FingerPrint/SetUp?format=json&security=1&iv={iv}");
            req.Headers.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
            req.Content = new StringContent(json, Encoding.UTF8, "multipart/form-data");
            return req;
        }, HttpCompletionOption.ResponseContentRead, cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        response.EnsureSuccessStatusCode();

        return JsonSerializer.Deserialize<HikDeleteFPBaseResponse>(body);
    }

    /// <summary>
    /// Xoa anh khuon mat hien co cua 1 nhan vien khoi thu vien khuon mat (FDLib) tren thiet bi.
    /// LUU Y: cung nhu DeleteFingerPrintAsync, endpoint nay CHUA duoc bat traffic thuc te — trien khai
    /// theo dung quy uoc chuan cua nhom API FDLib (FPID = employeeNo o dang PLAIN, khong ma hoa,
    /// giong het cach UploadFaceAsync/FDSetUp dang gui FPID). Hay kiem tra lai bang cach bat traffic
    /// khi bam "Xoa khuon mat" tren web UI goc cua thiet bi neu goi khong thanh cong.
    /// </summary>
    public async Task<HikBaseResponse?> DeleteFaceAsync(
        string employeeNo,
        CancellationToken cancellationToken = default)
    {
        await EnsureLoginAsync();

        var requestObj = new
        {
            searchResultPosition = 0,
            maxResults = 1,
            faceLibType = "blackFD",
            FDID = "1",
            FPID = new[] { employeeNo }
        };

        string json = JsonSerializer.Serialize(requestObj);

        using var response = await SendWithSessionRetryAsync(() =>
        {
            var req = CreateRequest(HttpMethod.Put, "/ISAPI/Intelligent/FDLib/FDSearch/Delete?format=json");
            req.Headers.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
            return req;
        }, HttpCompletionOption.ResponseContentRead, cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        response.EnsureSuccessStatusCode();

        return JsonSerializer.Deserialize<HikBaseResponse>(body);
    }

    public async Task<List<FingerPrintInfoModel>> GetFingerPrintsAsync(
        string employeeNo,
        int cardReaderNo = 1,
        CancellationToken cancellationToken = default)
    {
        await EnsureLoginAsync();

        InfoEncryption.GetInitVector(out var iv);
        var resultList = new List<FingerPrintInfoModel>();

        string searchId = Guid.NewGuid().ToString("N");
        var employeeNoEncrypt = EncryptFieldData(employeeNo, iv);

        int maxIterations = 100;
        int iteration = 0;

        while (iteration < maxIterations)
        {
            iteration++;
            var requestObj = new
            {
                FingerPrintCond = new
                {
                    searchID = searchId,
                    employeeNo = employeeNoEncrypt,
                    cardReaderNo = cardReaderNo
                }
            };

            string json = JsonSerializer.Serialize(requestObj);

            using var response = await SendWithSessionRetryAsync(() =>
            {
                var req = CreateRequest(HttpMethod.Post,
                    $"/ISAPI/AccessControl/FingerPrintUpload?format=json&security=1&iv={iv}");
                req.Headers.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
                req.Content = new StringContent(json, Encoding.UTF8, "multipart/form-data");
                return req;
            }, HttpCompletionOption.ResponseContentRead, cancellationToken);

            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("FingerPrintInfo", out var info))
            {
                break;
            }

            if (info.TryGetProperty("status", out var statusProp))
            {
                string status = statusProp.GetString() ?? "";
                if (status == "NoFP")
                {
                    break;
                }
            }

            if (info.TryGetProperty("FingerPrintList", out var listProp))
            {
                foreach (var item in listProp.EnumerateArray())
                {
                    var fp = new FingerPrintInfoModel();
                    if (item.TryGetProperty("cardReaderNo", out var crNo))
                        fp.CardReaderNo = crNo.GetInt32();

                    if (item.TryGetProperty("fingerPrintID", out var fpId))
                        fp.FingerPrintID = fpId.GetInt32();

                    if (item.TryGetProperty("fingerType", out var fpType))
                        fp.FingerType = fpType.GetString() ?? "";

                    if (item.TryGetProperty("fingerData", out var fpData))
                    {
                        var encryptedFpData = fpData.GetString() ?? "";
                        if (!string.IsNullOrEmpty(encryptedFpData))
                        {
                            fp.FingerData = DecryptRawBase64Field(encryptedFpData, iv);
                        }
                    }

                    resultList.Add(fp);
                }
            }
            else
            {
                break;
            }
        }

        return resultList;
    }
}