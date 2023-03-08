using NBitcoin.Secp256k1;
using NNostr.Client;
using Nostrid.Data;
using Nostrid.Misc;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

namespace Nostrid.Model;

public class LocalSignerFactory
{
    private readonly IAesEncryptor aesEncryptor;

    private const int HashKeySize = 64;

    public LocalSignerFactory(IAesEncryptor aesEncryptor)
    {
        this.aesEncryptor = aesEncryptor;
    }

    public bool TryFromPrivKey(string privKey, [NotNullWhen(true)] out LocalSigner? signer)
    {
        try
        {
            signer = new LocalSigner(privKey, aesEncryptor);
        }
        catch
        {
            signer = null;
        }
        return signer != null;
    }

    public async Task<string?> DecryptPrivKey(EncryptedKey encryptedKey, string pwd)
    {
        if (encryptedKey == null || encryptedKey.Iv == null || encryptedKey.EncryptedPk == null || encryptedKey.PwdHashSalt == null || encryptedKey.PwdHash == null)
        {
            return null;
        }

        // Check if pwd matches hash
        var hashToCompare = Rfc2898DeriveBytes.Pbkdf2(pwd, encryptedKey.PwdHashSalt, encryptedKey.HashInterations, HashAlgorithmName.SHA512, 64);
        if (!hashToCompare.SequenceEqual(encryptedKey.PwdHash))
        {
            return null;
        }

        // Decrypt pwd
        return await aesEncryptor.Decrypt(Convert.ToBase64String(encryptedKey.EncryptedPk), Convert.ToBase64String(encryptedKey.Iv), Encoding.UTF8.GetBytes(pwd));
    }

    public async Task<string?> DecryptPrivKeyBech32(string encryptedKeyBech32, string pwd)
    {
        if (ByteTools.TryDecodeTvlBech32(encryptedKeyBech32, out var tvlEntity) && tvlEntity is EncryptedKey encryptedKey)
        {
            return await DecryptPrivKey(encryptedKey, pwd);
        }
        return null;
    }

    public async Task<EncryptedKey> EncryptPrivKey(string plaintextKey, string pwd)
    {
        var encryptedKey = new EncryptedKey()
        {
            HashInterations = 600000,
            PwdHashSalt = RandomNumberGenerator.GetBytes(HashKeySize),
        };

        // Hash pwd
        encryptedKey.PwdHash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(pwd),
            encryptedKey.PwdHashSalt,
            encryptedKey.HashInterations,
            HashAlgorithmName.SHA512,
            HashKeySize);

        // Encrypt key with pwd
        var (encryptedPwd64, iv64) = await aesEncryptor.Encrypt(plaintextKey, Encoding.UTF8.GetBytes(pwd));
        encryptedKey.EncryptedPk = Convert.FromBase64String(encryptedPwd64);
        encryptedKey.Iv = Convert.FromBase64String(iv64);

        return encryptedKey;
    }

    public async Task<string> EncryptPrivKeyBech32(string plaintextKey, string pwd)
    {
        return ByteTools.EncodeTvlBech32(await EncryptPrivKey(plaintextKey, pwd))!;
    }
}

public class LocalSigner : ISigner
{
    private readonly ECPrivKey ecPrivKey;
    private readonly string pubKey;
    private readonly IAesEncryptor aesEncryptor;

    public LocalSigner(string privKey, IAesEncryptor aesEncryptor)
    {
        if (!TryGetPubKeyFromPrivKey(privKey, out ecPrivKey, out pubKey))
            throw new FormatException("Invalid privkey");
        this.aesEncryptor = aesEncryptor;
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
            var sharedKey = GetSharedPubkey(pubkeyBytes, ecPrivKey).ToBytes().Skip(1).ToArray();
            var encryptedContent = content.Split("?iv=");
            var encryptedText = encryptedContent[0];
            var iv = encryptedContent[1];
            return await aesEncryptor.Decrypt(encryptedText, iv, sharedKey);
        }
        catch (Exception ex)
        {
            return null;
        }
    }

    public async Task<string?> EncryptNip04(string pubkey, string content)
    {
        try
        {
            var pubkeyBytes = NBitcoin.Secp256k1.Context.Instance.CreateXOnlyPubKey(pubkey.DecodHexData());
            var sharedKey = GetSharedPubkey(pubkeyBytes, ecPrivKey).ToBytes().Skip(1).ToArray();
            var (cipherText, iv) = await aesEncryptor.Encrypt(content, sharedKey);
            return $"{cipherText}?iv={iv}";
        }
        catch (Exception ex)
        {
            return null;
        }
    }

    public async Task<string?> GetPubKey()
    {
        return pubKey;
    }

    public async Task<bool> Sign(NostrEvent ev)
    {
        if (ev.PublicKey != pubKey)
            return false;

        await ev.ComputeIdAndSign(ecPrivKey, false);
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

