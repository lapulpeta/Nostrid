using Nostrid.Misc;
using System.Buffers.Binary;
using System.Text;

namespace Nostrid.Model;

public class NostrTvlType
{
    public const byte Special = 0;
    public const byte Relay = 1;
    public const byte Author = 2;
    public const byte Kind = 3;
}

public abstract class TvlEntity
{
    public abstract Tvl GetTvl();
}

// NIP-19: https://github.com/nostr-protocol/nips/blob/master/19.md

public class Nevent : TvlEntity
{
    public readonly string EventId;

    public Nevent(Tvl tvl)
    {
        EventId = Convert.ToHexString(tvl.FirstOrDefault(t => t.Type == NostrTvlType.Special).Item2).ToLower();
    }

    public override Tvl GetTvl()
    {
        return new() { (NostrTvlType.Special, Convert.FromHexString(EventId)) };
    }
}

public class Naddr : TvlEntity
{
    public readonly string D;
    public readonly string Pubkey;
    public readonly int Kind;

    public Naddr(string replaceableId)
    {
        var exploded = EventExtension.ExplodeReplaceableId(replaceableId);
        if (exploded == null)
        {
            return;
        }
        Pubkey = exploded.Value.pubkey;
        Kind = exploded.Value.kind;
        D = exploded.Value.dstr;
    }

    public bool IsValid => D.IsNotNullOrEmpty() && Pubkey.IsNotNullOrEmpty();

    public Naddr(Tvl tvl)
    {
        D = Encoding.ASCII.GetString(tvl.FirstOrDefault(t => t.Type == NostrTvlType.Special).Data);
        Pubkey = Convert.ToHexString(tvl.FirstOrDefault(t => t.Type == NostrTvlType.Author).Data).ToLower();
        Kind = (int)BinaryPrimitives.ReadUInt32BigEndian(tvl.FirstOrDefault(t => t.Type == NostrTvlType.Kind).Data);
    }

    public string ReplaceableId => EventExtension.GetReplaceableId(Pubkey, Kind, D)!;

    public override Tvl GetTvl()
    {
        byte[] kind = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32BigEndian(kind, (uint)Kind);
        return new() {
            (NostrTvlType.Special, Encoding.ASCII.GetBytes(D)),
            (NostrTvlType.Author, Convert.FromHexString(Pubkey)),
            (NostrTvlType.Kind, kind),
        };
    }
}