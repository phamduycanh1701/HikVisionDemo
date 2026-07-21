using System.Security.Cryptography;
using System.Text;

namespace NtssHikvisionFaceDemo.Utils;

public static class LoginEncryptHelper
{
    public static string Sha256(string value)
    {
        using var sha = SHA256.Create();

        var hash = sha.ComputeHash(
            Encoding.UTF8.GetBytes(value));

        return Convert.ToHexString(hash)
            .ToLowerInvariant();
    }

    public static string Md5(string value)
    {
        using var md5 = MD5.Create();

        var hash = md5.ComputeHash(
            Encoding.UTF8.GetBytes(value));

        return Convert.ToHexString(hash)
            .ToLowerInvariant();
    }

    public static string GenerateRandom()
    {
        var timestamp = DateTimeOffset
            .UtcNow
            .ToUnixTimeMilliseconds()
            .ToString();

        var md5 = Md5(timestamp);

        var first8 = md5[..8];

        return Convert
            .ToInt64(first8, 16)
            .ToString()
            .Substring(0, 8);
    }

    public static string EncodePassword(
        string username,
        string password,
        string salt,
        string challenge,
        int iterations,
        bool irreversible)
    {
        if (irreversible)
        {
            var hash = Sha256(username + salt + password);

            hash = Sha256(hash + challenge);

            for (int i = 2; i < iterations; i++)
            {
                hash = Sha256(hash);
            }

            return hash;
        }

        var result = Sha256(password) + challenge;

        for (int i = 1; i < iterations; i++)
        {
            result = Sha256(result);
        }

        return result;
    }
}