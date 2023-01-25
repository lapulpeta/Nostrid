using Nostrid.Misc;
using NNostr.Client;

namespace Nostrid.Data.Relays;

public class MainAccountSubscriptionFilter : SubscriptionFilter
{
    private readonly string[] ids;

	public MainAccountSubscriptionFilter(string id)
    {
        ids = new[] { id };
    }

    public override NostrSubscriptionFilter[] GetFilters()
    {
        return new[] {
            new NostrSubscriptionFilter() { Authors = ids, Kinds = new[]{ NostrKind.Metadata }, Limit = 1 },
            new NostrSubscriptionFilter() { Authors = ids, Kinds = new[]{ NostrKind.Contacts }, Limit = 1 },
            new NostrSubscriptionFilter() { Authors = ids, Kinds = new[]{ NostrKind.Deletion } }
        };
    }

    public override SubscriptionFilter Clone()
    {
        return new MainAccountSubscriptionFilter(ids[0]);
    }
}

