using System.Runtime.Versioning;
using System.Security.Cryptography;

namespace NNostr.Client;
public class AesEncryptor : IAesEncryptor
{

    [UnsupportedOSPlatform("browser")]
    public async Task<(string cipherText, string iv)> Encrypt(string plainText, byte[] key)
    {
        Aes aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();
        aes.Mode = CipherMode.CBC;
        ICryptoTransform cipher = aes.CreateEncryptor(aes.Key, aes.IV);

        using var ms = new MemoryStream();
        using (var cs = new CryptoStream(ms, cipher, CryptoStreamMode.Write))
        {
            using var sw = new StreamWriter(cs);
            await sw.WriteAsync(plainText);
        }
        return (Convert.ToBase64String(ms.ToArray()), Convert.ToBase64String(aes.IV));
    }

    public Task<string> Decrypt(string cipherText, string iv, byte[] key)
    {
        Aes aes = Aes.Create();
        aes.Key = key;
        aes.IV = Convert.FromBase64String(iv);
        aes.Mode = CipherMode.CBC;
        ICryptoTransform decipher = aes.CreateDecryptor(aes.Key, aes.IV);

        using var ms = new MemoryStream(Convert.FromBase64String(cipherText));
        using var cs = new CryptoStream(ms, decipher, CryptoStreamMode.Read);
        using var sr = new StreamReader(cs);
        return sr.ReadToEndAsync();
    }
}