using NNostr.Client;
using Nostrid.Model;

namespace Nostrid.Data.Relays;

public class PastFollowsSubscriptionFilter : SubscriptionFilter
{
    private readonly string id;

    public PastFollowsSubscriptionFilter(string id)
    {
        this.id = id;
    }

    public override NostrSubscriptionFilter[] GetFilters()
    {
        return new[] {
            new NostrSubscriptionFilter() { Authors = new[] { id }, Kinds = new[]{ NostrKind.Contacts } }
        };
    }

    public override SubscriptionFilter Clone()
    {
        return new PastFollowsSubscriptionFilter(id);
    }
}

