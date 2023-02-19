using Nostrid.Misc;
using NNostr.Client;
using Nostrid.Model;

namespace Nostrid.Data.Relays;

public class EventSubscriptionFilter : SubscriptionFilter
{
    private readonly string[] ids;

    public EventSubscriptionFilter(string id) : this(new[] { id })
    {
    }

    public EventSubscriptionFilter(string[] ids)
    {
        this.ids = ids;
    }

    public override NostrSubscriptionFilter[] GetFilters()
    {
        return new[] {
            new NostrSubscriptionFilter() { Ids = ids, Kinds = new[]{ NostrKind.Text, NostrKind.ChannelMessage } },
            new NostrSubscriptionFilter() { EventId = ids, Kinds = new[]{ NostrKind.Text, NostrKind.Deletion, NostrKind.Repost, NostrKind.Reaction, NostrKind.Zap } }
        };
    }

    public override SubscriptionFilter Clone()
    {
        return new EventSubscriptionFilter(ids);
    }
}

