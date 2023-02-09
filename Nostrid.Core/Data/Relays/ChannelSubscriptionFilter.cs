using Nostrid.Misc;
using NNostr.Client;
using Nostrid.Model;

namespace Nostrid.Data.Relays;

/// <summary>
/// Returns all events for a given channel Id
/// </summary>
public class ChannelSubscriptionFilter : SubscriptionFilter
{
    private readonly string[] ids;

    public ChannelSubscriptionFilter(string id) : this(new[] {id })
    {
    }

    public ChannelSubscriptionFilter(string[] ids)
    {
        this.ids = ids;
    }

    public override NostrSubscriptionFilter[] GetFilters()
    {
        return new[] {
            new NostrSubscriptionFilter() { EventId = ids, Kinds = new[]{ NostrKind.ChannelMessage }, Limit = LimitFilterData?.Limit, Since = LimitFilterData?.Since, Until = LimitFilterData?.Until },
        };
    }

    public static List<ChannelSubscriptionFilter> CreateInBatch(string[] ids, int batchSize = 150, int? limit = null, DateTimeOffset? destroyOn = null, bool destroyEose = false)
    {
        int index = 0;
        var segment = new ArraySegment<string>(ids);
        var ret = new List<ChannelSubscriptionFilter>();
        while (index < ids.Length)
        {
            var filter = new ChannelSubscriptionFilter(segment.Slice(index, Math.Min(batchSize, ids.Length - index)).ToArray());
            filter.LimitFilterData.Limit = limit;
            filter.DestroyOn = destroyOn;
            filter.DestroyOnEose = destroyEose;
            ret.Add(filter);
            index += batchSize;
        }
        return ret;
    }

    public override SubscriptionFilter Clone()
    {
        return new ChannelSubscriptionFilter(ids);
    }
}

