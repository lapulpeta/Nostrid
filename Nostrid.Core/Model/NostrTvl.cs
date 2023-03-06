using System.Buffers.Binary;
using System.Text;

namespace Nostrid.Model;

public enum NostrTvlType
{
    Special = 0,
    Relay = 1,
    Author = 2,
    Kind = 3
}

public abstract class TvlEntity
{
    public abstract List<(NostrTvlType, byte[])> GetTvl();
}

// NIP-19: https://github.com/nostr-protocol/nips/blob/master/19.md

public class Nevent : TvlEntity
{
    public readonly string EventId;

    public Nevent(List<(NostrTvlType, byte[])> tvl)
    {
        EventId = Convert.ToHexString(tvl.FirstOrDefault(t => t.Item1 == NostrTvlType.Special).Item2).ToLower();
    }

    public override List<(NostrTvlType, byte[])> GetTvl()
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

    public Naddr(List<(NostrTvlType, byte[])> tvl)
    {
        D = Encoding.ASCII.GetString(tvl.FirstOrDefault(t => t.Item1 == NostrTvlType.Special).Item2);
        Pubkey = Convert.ToHexString(tvl.FirstOrDefault(t => t.Item1 == NostrTvlType.Author).Item2).ToLower();
        Kind = (int)BinaryPrimitives.ReadUInt32BigEndian(tvl.FirstOrDefault(t => t.Item1 == NostrTvlType.Kind).Item2);
    }

    public string ReplaceableId => EventExtension.GetReplaceableId(Pubkey, Kind, D)!;

    public override List<(NostrTvlType, byte[])> GetTvl()
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