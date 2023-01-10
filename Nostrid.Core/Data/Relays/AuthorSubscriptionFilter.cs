using Nostrid.Misc;
using NNostr.Client;

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
        ParamsId = Utils.HashWithSHA256("aasf:" + ids.OrderBy(x => x).Aggregate((a, b) => $"{a}:{b}"));
    }

    public override NostrSubscriptionFilter[] GetFilters()
    {
        return new[] {
            new NostrSubscriptionFilter() { Authors = ids, Kinds = new[]{ NostrKind.Text, NostrKind.Contacts, NostrKind.Deletion, NostrKind.Repost, NostrKind.Reaction }, Limit = limitFilterData?.Limit, Since = limitFilterData?.Since, Until = limitFilterData?.Until }
        };
    }

    public override SubscriptionFilter Clone()
    {
        return new AuthorSubscriptionFilter(ids);
    }
}

