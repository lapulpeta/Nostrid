using NNostr.Client;

namespace Nostrid.Data.Relays;

public class FollowersSubscriptionFilter : SubscriptionFilter
{
    private readonly string id;

    public FollowersSubscriptionFilter(string id)
    {
        this.id = id;
    }

    public override NostrSubscriptionFilter[] GetFilters()
    {
        return new[] {
            new NostrSubscriptionFilter() { PublicKey = new[] { id }, Kinds = new[]{ NostrKind.Metadata, NostrKind.Contacts }, Limit = limitFilterData?.Limit, Since = limitFilterData?.Since, Until = limitFilterData?.Until }
        };
    }

    public override SubscriptionFilter Clone()
    {
        return new FollowersSubscriptionFilter(id);
    }
}

