using System.Security.Cryptography;
using System.Text;

namespace NtssHikvisionFaceDemo.Utils;

public class InfoEncryption
{
    /// <summary>
    /// Generate initialization vector (iv)
    /// </summary>
    /// <param name="strInitVector"></param>
    /// <returns></returns>
    public static void GetInitVector(out string strInitVector)
    {
        byte[] szInitVector = new byte[16];
        Random ra = new Random();
        ra.NextBytes(szInitVector);
        byte[] byHexAes = AESEncryption.converByteArrayToCharArray(szInitVector, szInitVector.Length);
        strInitVector = Encoding.UTF8.GetString(byHexAes).ToLower();//This must be in lowercase.
    }
    
    /// <summary>
    /// Encrypt data with SHA256 algorithm. The data encrypted by this algorithm is reversible.
    /// </summary>
    /// <param name="strData"></param>
    /// <returns></returns>
    public static string sha256(string strData)
    {
        byte[] szData = Encoding.UTF8.GetBytes(strData);
        byte[] szHash = SHA256Managed.Create().ComputeHash(szData);
        byte[] szSha256 = AESEncryption.converByteArrayToCharArray(szHash, 32);//The standard algorithm is Hash. The length 32 is set according to the sample document.
        return Encoding.UTF8.GetString(szSha256);
    }
    
    /// <summary>
    /// Encrypt (user name+salt+password) with SHA256 algorithm. The password encrypted by this algorithm is irreversible.
    /// </summary>
    /// <param name="strUser"></param>
    /// <param name="szSalt"></param>
    /// <param name="strPassword"></param>
    /// <returns></returns>
    public static string calcSha256(string strUser, byte[] szSalt, string strPassword)
    {
        string strSrcData = strUser;
        byte[] szRealSalt = new byte[64]; //The size of the salt value is 64 bits.
        if (szSalt.Length > 64)
        {
            return null;
        }

        Array.Copy(szSalt, szRealSalt, szSalt.Length);
        strSrcData = strSrcData + Encoding.UTF8.GetString(szRealSalt) + strPassword;
        return sha256(strSrcData);
    }

    /// <summary>
    /// Generate encryption key
    /// </summary>
    /// <param name="strUserName"></param>
    /// <param name="strSalt"></param>
    /// <param name="strPassword"></param>
    /// <param name="szOut"></param>
    /// <param name="iKeyIterateNum"></param>
    /// <param name="bIrreversible"></param>
    /// <returns></returns>
    public static void getEncryptKey(string strUserName, string strSalt, string strPassword, out byte[] szOut, int
        iKeyIterateNum, bool bIrreversible)
    {
        byte[] szSalt = null;
        if (strSalt != null)
        {
            szSalt = Encoding.UTF8.GetBytes(strSalt);
        }
        
        string strSrcData = string.Empty;
        if (bIrreversible && szSalt.Length > 0)
        {
            string strIrrPsw = calcSha256(strUserName, szSalt, strPassword);
            if (strIrrPsw.Length > 64)
            {
                strSrcData = strIrrPsw.Substring(0,64);
            }
            else
            {
                strSrcData = strIrrPsw;
            }
        }
        else
        {
            if (strPassword.Length > 64)
            {
                strSrcData = strPassword.Substring(0,64);
            }
            else
            {
                strSrcData = strPassword;
            }
        }
        strSrcData +="AaBbCcDd1234!@#$";
        if (iKeyIterateNum <= 0)//Iterations
        {
            iKeyIterateNum = 100;
        }
        //For the SHA256 iteration, iKeyIterateNum represents the number of iterations returned by the capability set.
        for (int i = 0; i < iKeyIterateNum; i++)
        {
            strSrcData = sha256(strSrcData);
        }
        byte[] szSHA256 = Encoding.UTF8.GetBytes(strSrcData);
        //The result calculated with SHA256 algorithm is converted to a byte array for the last time.
        byte[] szByteArray = AESEncryption.convertCharArrayToByteArray(szSHA256, szSHA256.Length);
        if (szByteArray.Length > 32)
        {
            szOut = new byte[32];
            Array.Copy(szByteArray, szOut, 32);
        }
        else
        {
            szOut = new byte[szByteArray.Length];
            Array.Copy(szByteArray, szOut, szByteArray.Length);
        }
    }
    
    /// <summary>
    /// Encrypt content (sensitive information)
    /// </summary>
    /// <param name="strInitVextor"></param>
    /// <param name="szAESKey"></param>
    /// <param name="strSrcContent"></param>
    /// <param name="strOut"></param>
    /// <param name="iSecurityVersion"></param>
    /// <returns></returns>
    public static void getEncryptContent(string strInitVextor, byte[] szAESKey, string strSrcContent, out string strOut, int
        iSecurityVersion)
    {
        if (iSecurityVersion != 1)//1 refers to the AES-128 algorithm. Currently only 1 is supported.
        {
            strOut = strSrcContent;
            return;
        }
        //Convert to UTF-8
        byte[] szInitVextor = Encoding.UTF8.GetBytes(strInitVextor);
        byte[] szInitVextorByteArray = AESEncryption.convertCharArrayToByteArray(szInitVextor, szInitVextor.Length);
        byte[] szSrcBytes = Encoding.UTF8.GetBytes(strSrcContent);
        string strSrcBase64 = Convert.ToBase64String(szSrcBytes);
        //AES encryption
        byte[] szAesData = AESEncryption.AesEncrypt(strSrcBase64, szAESKey, szInitVextorByteArray);
        byte[] szOut = AESEncryption.converByteArrayToCharArray(szAesData, szAesData.Length);
        strOut = Encoding.UTF8.GetString(szOut);
    }
    
    /// <summary>
    /// Decrypt content (sensitive information)
    /// </summary>
    /// <param name="strInitVextor"></param>
    /// <param name="szAESKey"></param>
    /// <param name="strSrcContent"></param>
    /// <param name="strOut"></param>
    /// <param name="iSecurityVersion"></param>
    /// <returns></returns>
    public static void getDecryptContent(string strInitVextor, byte[] szAESKey, string strSrcContent, out string strOut, int
        iSecurityVersion)
    {
        if (iSecurityVersion != 1)//1 refers to the AES-128 algorithm. Currently only 1 is supported.
        {
            strOut = strSrcContent;
            return;
        }
        //Convert to UTF-8
        byte[] szInitVextor = Encoding.UTF8.GetBytes(strInitVextor);
        byte[] szInitVextorByteArray = AESEncryption.convertCharArrayToByteArray(szInitVextor, szInitVextor.Length);
        byte[] szSrcBytes = Encoding.UTF8.GetBytes(strSrcContent);
        
        byte[] szSrcByteArray = AESEncryption.convertCharArrayToByteArray(szSrcBytes, szSrcBytes.Length);
        string strAesData = AESEncryption.AesDecrypt(szSrcByteArray, szAESKey, szInitVextorByteArray);
        strAesData = FixBase64(strAesData);
        byte[] szOut = Convert.FromBase64String(strAesData);
        strOut = Encoding.UTF8.GetString(szOut);
    }
    private static string FixBase64(string input)
    {
        int mod = input.Length % 4;
        if (mod > 0)
            input += new string('=', 4 - mod);

        return input;
    }
}

public static class AESEncryption
{
    /// <summary>
    /// Convert hexadecimal to binary
    /// </summary>
    /// <param name="chstr"></param>
    /// <returns></returns>
    public static byte hexToBinary(byte chstr)
    {
        char crtn = '\0';
        if (('0' <= chstr) && ('9' >= chstr))
        {
            crtn = (char)(chstr & 0x0F);
        }
        else if (('A' <= chstr) && ('F' >= chstr))
        {
            crtn = (char)(chstr - 'A' + 10);
        }
        else if (('a' <= chstr) && ('f' >= chstr))
        {
            crtn = (char)(chstr - 'a' + 10);
        }
        return (byte)crtn;
    }
    
    /// <summary>
    /// Convert a character array to a byte array
    /// </summary>
    /// <param name="pSrc"></param>
    /// <param name="nSrcLen"></param>
    /// <returns></returns>
    public static byte[] convertCharArrayToByteArray(byte[] pSrc, int nSrcLen)
    {
        byte[] byChallengeDst2 = new byte[nSrcLen / 2];
        for (int i = 0; i < nSrcLen; i = i + 2)
        {
            byChallengeDst2[i / 2] = (byte)(hexToBinary(pSrc[i]) << 4);
            byChallengeDst2[i / 2] += (byte)hexToBinary(pSrc[i + 1]);
        }
        return byChallengeDst2;
    }
    
    /// <summary>
    /// Convert a byte array to a character array
    /// </summary>
    /// <param name="pSrc"></param>
    /// <param name="nSrcLen"></param>
    /// <returns></returns>
    public static byte[] converByteArrayToCharArray(byte[] pSrc, int nSrcLen)
    {
        StringBuilder strB = new StringBuilder();
        for (int i = 0; i < nSrcLen; i++)
        {
            strB.Append(pSrc[i].ToString("x2"));//Here x must be in lowercase, which means converting to a lowercase hexadecimal character.
        }
        return Encoding.UTF8.GetBytes(strB.ToString());
    }
    
    /// <summary>
    /// Encrypt in AES-CBC mode
    /// </summary>
    /// <param name="strSrcContent"></param>
    /// <param name="szAESKey"></param>
    /// <param name="iv"></param>
    /// <returns></returns>
    public static byte[] AesEncrypt(string strSrcContent, byte[] szAESKey, byte[] iv)
    {
        RijndaelManaged rijndaelCipher = new RijndaelManaged();
        rijndaelCipher.Mode = CipherMode.CBC;
        rijndaelCipher.Padding = PaddingMode.Zeros;//The end is all-zero padding
        rijndaelCipher.KeySize = 128;
        rijndaelCipher.BlockSize = 128;
        byte[] keyBytes = new byte[16];
        int len = szAESKey.Length;
        if (len > keyBytes.Length)
        {
            len = keyBytes.Length;
        }
        
        System.Array.Copy(szAESKey, keyBytes, len);
        rijndaelCipher.Key = keyBytes;
        rijndaelCipher.IV = iv;
        ICryptoTransform transform = rijndaelCipher.CreateEncryptor();
        byte[] szSrcContent = Encoding.UTF8.GetBytes(strSrcContent);
        byte[] szDstContent = transform.TransformFinalBlock(szSrcContent, 0, szSrcContent.Length);
        return szDstContent;
    }
    
    /// <summary>
    /// Decrypt in AES-CBC mode
    /// </summary>
    /// <param name="szSrcContent"></param>
    /// <param name="szAESKey"></param>
    /// <param name="iv"></param>
    /// <returns></returns>
    public static string AesDecrypt(byte[] szSrcContent, byte[] szAESKey, byte[] iv)
    {
        RijndaelManaged rijndaelCipher = new RijndaelManaged();
        rijndaelCipher.Mode = CipherMode.CBC;
        rijndaelCipher.Padding = PaddingMode.Zeros;//The end is all-zero padding
        rijndaelCipher.KeySize = 128;
        rijndaelCipher.BlockSize = 128;
        byte[] keyBytes = new byte[16];
        int len = szAESKey.Length;
        if (len > keyBytes.Length)
        {
            len = keyBytes.Length;
        }
        System.Array.Copy(szAESKey, keyBytes, len);
        rijndaelCipher.Key = keyBytes;
        rijndaelCipher.IV = iv;
        ICryptoTransform transform = rijndaelCipher.CreateDecryptor();
        byte[] szDstContent = transform.TransformFinalBlock(szSrcContent, 0, szSrcContent.Length);
        //After decrypting the last 16 bytes of the ciphertext, the decrypted data of the last 16 bytes will be checked.
        //If the value of the last byte is 16, the 16-byte string data will be discarded.
        //If the value of the last byte is smaller than 16, it indicates that the original text was padded during encryption. The padding will be discarded.
        for (int i = 0; i < szDstContent.Length; i++)
        {
            if (szDstContent[i] <= 16)
            {
                szDstContent[i] = 0;
            }
        }
        //The \0 at the end needs to be removed.
        return Encoding.UTF8.GetString(szDstContent).Replace("\0", "");
    }
}