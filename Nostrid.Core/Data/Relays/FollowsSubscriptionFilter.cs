using Nostrid.Misc;
using NNostr.Client;

namespace Nostrid.Data.Relays;

public class FollowsSubscriptionFilter : SubscriptionFilter
{
    private readonly string[] ids;

	public FollowsSubscriptionFilter(string id) : this(new[] { id })
    {
	}

    public FollowsSubscriptionFilter(string[] ids)
    {
        this.ids = ids;
    }

    public override NostrSubscriptionFilter[] GetFilters()
    {
        return new[] {
            new NostrSubscriptionFilter() { Authors = ids, Kinds = new[]{ NostrKind.Metadata, NostrKind.Contacts }, Limit = 1 } // Get the latest details
        };
    }

    public override SubscriptionFilter Clone()
    {
        return new FollowsSubscriptionFilter(ids);
    }
}

