using Nostrid.Misc;
using NNostr.Client;
using Nostrid.Model;

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
             // No need to limit to 1 becase these kinds are replaceable
            new NostrSubscriptionFilter() { Authors = ids, Kinds = new[]{ NostrKind.Metadata , NostrKind.Contacts, NostrKind.Mutes } },
            new NostrSubscriptionFilter() { Authors = ids, Kinds = new[]{ NostrKind.Deletion } }
        };
    }

    public override SubscriptionFilter Clone()
    {
        return new MainAccountSubscriptionFilter(ids[0]);
    }
}

