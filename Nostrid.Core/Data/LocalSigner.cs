using NBitcoin.Secp256k1;
using NNostr.Client;
using Nostrid.Data;
using System.Diagnostics.CodeAnalysis;

namespace Nostrid.Model;

public class LocalSigner : ISigner
{
    private ECPrivKey _ecPrivKey;
    private string _pubKey;

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
}

