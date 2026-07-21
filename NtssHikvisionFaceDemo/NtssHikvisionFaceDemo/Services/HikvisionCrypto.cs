using System.Security.Cryptography;
using System.Text;

public static class HikvisionCrypto
{
    public static string EncryptAES(string plainText, string key, string ivHex)
    {
        byte[] keyBytes = Encoding.UTF8.GetBytes(key);
        byte[] ivBytes = HexToBytes(ivHex);

        using var aes = Aes.Create();

        aes.Key = keyBytes;
        aes.IV = ivBytes;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();

        byte[] input = Encoding.UTF8.GetBytes(plainText);

        byte[] encrypted = encryptor.TransformFinalBlock(
            input,
            0,
            input.Length);

        return Convert.ToBase64String(encrypted);
    }

    public static string DecryptAES(string cipherBase64, string key, string ivHex)
    {
        byte[] keyBytes = Encoding.UTF8.GetBytes(key);
        byte[] ivBytes = HexToBytes(ivHex);

        using var aes = Aes.Create();

        aes.Key = keyBytes;
        aes.IV = ivBytes;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();

        byte[] encrypted = Convert.FromBase64String(cipherBase64);

        byte[] plain = decryptor.TransformFinalBlock(
            encrypted,
            0,
            encrypted.Length);

        return Encoding.UTF8.GetString(plain);
    }

    private static byte[] HexToBytes(string hex)
    {
        byte[] bytes = new byte[hex.Length / 2];

        for (int i = 0; i < bytes.Length; i++)
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);

        return bytes;
    }
}