using LiteDB;
using NBitcoin.Secp256k1;
using NNostr.Client;

namespace Nostrid.Model;

public class Account
{
    private ECPrivKey privKey;

    public string Id { get; set; }

    public string PrivKey { get; set; }

    public AccountDetails Details { get; set; }

    public List<string> FollowList { get; set; } = new();

    public List<string> FollowerList { get; set; } = new();

    public DateTimeOffset? FollowsLastUpdate { get; set; }

    public DateTimeOffset? DetailsLastUpdate { get; set; }

    public DateTimeOffset? LastNotificationRead { get; set; }

    public Account()
    {
    }

    public Account(string privKey)
    {
        PrivKey = privKey;
        PrepareKey();
    }

    private void PrepareKey()
    {
        if (privKey != null)
        {
            return;
        }
        privKey = NostrExtensions.ParseKey(PrivKey);
        Id = NostrExtensions.ToHex(privKey.CreatePubKey().ToXOnlyPubKey());
    }

    public async void ComputeIdAndSign(NostrEvent ev)
    {
        PrepareKey();
        await ev.ComputeIdAndSign(privKey);
    }
}

