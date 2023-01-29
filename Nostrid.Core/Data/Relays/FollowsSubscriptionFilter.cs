using Nostrid.Misc;
using NNostr.Client;

namespace Nostrid.Data.Relays;

public class FollowsSubscriptionFilter : SubscriptionFilter
{
    private readonly string id;

	public FollowsSubscriptionFilter(string id)
    {
        this.id = id;
	}

    public override NostrSubscriptionFilter[] GetFilters()
    {
        return new[] {
            new NostrSubscriptionFilter() { Authors = new[] { id }, Kinds = new[]{ NostrKind.Metadata, NostrKind.Contacts }, Limit = 1 } // Get the latest details
        };
    }

    public override SubscriptionFilter Clone()
    {
        return new FollowsSubscriptionFilter(id);
    }
}

