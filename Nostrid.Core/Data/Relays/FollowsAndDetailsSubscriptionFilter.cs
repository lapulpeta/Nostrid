using NNostr.Client;

namespace Nostrid.Data.Relays;

public class FollowsAndDetailsSubscriptionFilter : SubscriptionFilter
{
    private readonly string id;

    public FollowsAndDetailsSubscriptionFilter(string id)
    {
        this.id = id;
    }

    public override NostrSubscriptionFilter[] GetFilters()
    {
        return new[] {
            new NostrSubscriptionFilter() { Authors = new[] { id }, Kinds = new[]{ NostrKind.Metadata }, Limit = 1 }, // Get the latest details
            new NostrSubscriptionFilter() { Authors = new[] { id }, Kinds = new[]{ NostrKind.Contacts }, Limit = 1 } // Get the latest follows
        };
    }

    public override SubscriptionFilter Clone()
    {
        return new FollowsAndDetailsSubscriptionFilter(id);
    }
}

