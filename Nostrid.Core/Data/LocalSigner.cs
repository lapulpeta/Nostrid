using NBitcoin.Secp256k1;
using NNostr.Client;
using Nostrid.Data;
using System.Diagnostics.CodeAnalysis;

namespace Nostrid.Model;

public class LocalSignerFactory
{
	private readonly IAesEncryptor aesEncryptor;

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

