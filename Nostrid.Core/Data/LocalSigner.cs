using NBitcoin.Secp256k1;
using NNostr.Client;
using Nostrid.Data;
using System.Diagnostics.CodeAnalysis;

namespace Nostrid.Model;

public class LocalSigner : ISigner
{
    private ECPrivKey _ecPrivKey;
    private string _pubKey;

    private static readonly Lazy<IAesEncryptor> _encryptor = new(new AesEncryptor());

    public LocalSigner(string privKey)
    {
        if (!TryGetPubKeyFromPrivKey(privKey, out _ecPrivKey, out _pubKey))
            throw new FormatException("Invalid privkey");
    }

    public static bool TryFromPrivKey(string privKey, [NotNullWhen(true)] out LocalSigner? signer)
    {
        try
        {
            signer = new LocalSigner(privKey);
        }
        catch
        {
            signer = null;
        }
        return signer != null;
    }


    public static bool TryGetPubKeyFromPrivKey(string privKey, [NotNullWhen(true)] out ECPrivKey? ecPrivKey, [NotNullWhen(true)] out string? pubKey)
    {
        try
        {
            ecPrivKey = NostrExtensions.ParseKey(privKey);
            pubKey = NostrExtensions.ToHex(ecPrivKey.CreatePubKey().ToXOnlyPubKey());
            return true;
        }
        catch
        {
            ecPrivKey = null;
            pubKey = null;
            return false;
        }
    }

    public async Task<string?> DecryptNip04(string pubkey, string content)
    {
        try
        {
            var pubkeyBytes = NBitcoin.Secp256k1.Context.Instance.CreateXOnlyPubKey(pubkey.DecodHexData());
            var sharedKey = GetSharedPubkey(pubkeyBytes, _ecPrivKey).ToBytes().Skip(1).ToArray();
            var encryptedContent = content.Split("?iv=");
            var encryptedText = encryptedContent[0];
            var iv = encryptedContent[1];
            return await _encryptor.Value.Decrypt(encryptedText, iv, sharedKey);
        }
        catch (Exception ex)
        {
            return string.Empty;
        }
    }

    public Task<string?> EncryptNip04(string pubkey, string content)
    {
        throw new NotImplementedException();
    }

    public async Task<string?> GetPubKey()
    {
        return _pubKey;
    }

    public async Task<bool> Sign(NostrEvent ev)
    {
        if (ev.PublicKey != _pubKey)
            return false;

        await ev.ComputeIdAndSign(_ecPrivKey);
        return true;
    }

    private static readonly byte[] posBytes = new[] { (byte)02 };
    private static ECPubKey? GetSharedPubkey(ECXOnlyPubKey ecxOnlyPubKey, ECPrivKey key)
    {
        byte[] pubkey = ecxOnlyPubKey.ToBytes();
        byte[] pubkeyext = new byte[pubkey.Length + posBytes.Length];
        Array.Copy(posBytes, 0, pubkeyext, 0, posBytes.Length);
        Array.Copy(pubkey, 0, pubkeyext, posBytes.Length, pubkey.Length);
        if (NBitcoin.Secp256k1.Context.Instance.TryCreatePubKey(pubkeyext, out var mPubKey))
        {
            return mPubKey.GetSharedPubkey(key);
        }
        return null;
    }
}

