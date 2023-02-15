using Microsoft.JSInterop;
using NNostr.Client;
using System.Security.Cryptography;

internal class AesSubtleCrypto : IAesEncryptor
{
    private readonly Lazy<Task<IJSObjectReference>> moduleTask;

    public AesSubtleCrypto(IJSRuntime jsRuntime)
    {
        moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>("import", "./scripts.js").AsTask());
    }
    public async Task<string> Decrypt(string cipherText, string iv, byte[] key)
    {
        var module = await moduleTask.Value;
        var cipherTextBytes = Convert.FromBase64String(cipherText);
        var ivBytes = Convert.FromBase64String(iv);
        var ret = await module.InvokeAsync<string>("decryptAes", cipherTextBytes, key, ivBytes);
        return ret;
    }

    public async Task<(string cipherText, string iv)> Encrypt(string plainText, byte[] key)
    {
        var module = await moduleTask.Value;
        var ivBytes = new byte[16];
        RandomNumberGenerator.Create().GetBytes(ivBytes);
        var ret = await module.InvokeAsync<byte[]>("encryptAes", plainText, key, ivBytes);
        return (Convert.ToBase64String(ret), Convert.ToBase64String(ivBytes));
    }
}