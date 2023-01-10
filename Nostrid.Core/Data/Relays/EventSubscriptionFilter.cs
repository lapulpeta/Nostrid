using Nostrid.Misc;
using NNostr.Client;

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
        ParamsId = Utils.HashWithSHA256("esf:" + ids.OrderBy(x => x).Aggregate((a, b) => $"{a}:{b}"));
    }

    public override NostrSubscriptionFilter[] GetFilters()
    {
        return new[] {
            new NostrSubscriptionFilter() { Ids = ids, Kinds = new[]{ NostrKind.Text, NostrKind.Deletion, NostrKind.Repost, NostrKind.Reaction } },
            new NostrSubscriptionFilter() { EventId = ids, Kinds = new[]{ NostrKind.Text, NostrKind.Deletion, NostrKind.Repost, NostrKind.Reaction } }
        };
    }

    public override SubscriptionFilter Clone()
    {
        return new EventSubscriptionFilter(ids);
    }
}

