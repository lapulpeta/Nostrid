using NNostr.Client;
using Nostrid.Model;

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
            new NostrSubscriptionFilter() { PublicKey = new[] { id }, Kinds = new[]{ NostrKind.Metadata, NostrKind.Contacts }, Limit = LimitFilterData?.Limit, Since = LimitFilterData?.Since, Until = LimitFilterData?.Until }
        };
    }

    public override SubscriptionFilter Clone()
    {
        return new FollowersSubscriptionFilter(id);
    }
}

