using System.Collections.Immutable;

namespace Nostrid.Model;

public enum NostrTvlType
{
    Special = 0,
    Relay = 1,
    Author = 2,
    Kind = 3
}

public class TvlEntity
{

}

public class Nevent : TvlEntity
{
    public readonly string EventId;

    public Nevent(List<(NostrTvlType, byte[])> tvl)
    {
        EventId = Convert.ToHexString(tvl.FirstOrDefault(t => t.Item1 == NostrTvlType.Special).Item2).ToLower();
    }
}

