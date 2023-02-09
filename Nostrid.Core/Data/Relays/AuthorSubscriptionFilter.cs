using Nostrid.Misc;
using NNostr.Client;
using Nostrid.Model;

namespace Nostrid.Data.Relays;

public class AuthorSubscriptionFilter : SubscriptionFilter
{
    private readonly string[] ids;

	public AuthorSubscriptionFilter(string id) : this(new[] { id })
    {
	}

    public AuthorSubscriptionFilter(string[] ids)
    {
        this.ids = ids;
    }

    public override NostrSubscriptionFilter[] GetFilters()
    {
        return new[] {
            new NostrSubscriptionFilter() { Authors = ids, Kinds = new[]{ NostrKind.Text, NostrKind.Contacts, NostrKind.Deletion, NostrKind.Repost, NostrKind.Reaction }, Limit = LimitFilterData?.Limit, Since = LimitFilterData?.Since, Until = LimitFilterData?.Until }
        };
    }

    public override SubscriptionFilter Clone()
    {
        return new AuthorSubscriptionFilter(ids);
    }
}

