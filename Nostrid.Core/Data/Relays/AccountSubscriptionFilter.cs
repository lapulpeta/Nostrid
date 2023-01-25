using Nostrid.Misc;
using NNostr.Client;

namespace Nostrid.Data.Relays;

public class AccountSubscriptionFilter : SubscriptionFilter
{
    private readonly string[] ids;

	public AccountSubscriptionFilter(string id) : this(new[] { id })
    {
	}

    public AccountSubscriptionFilter(string[] ids)
    {
        this.ids = ids;
    }

    public override NostrSubscriptionFilter[] GetFilters()
    {
        return new[] {
            new NostrSubscriptionFilter() { PublicKey = ids, Kinds = new[]{ NostrKind.Text, NostrKind.Deletion, NostrKind.Repost, NostrKind.Reaction }, Limit = limitFilterData?.Limit, Since = limitFilterData?.Since, Until = limitFilterData?.Until },
            new NostrSubscriptionFilter() { Authors = ids, Kinds = new[]{ NostrKind.Text, NostrKind.Contacts, NostrKind.Deletion, NostrKind.Repost, NostrKind.Reaction }, Limit = limitFilterData?.Limit, Since = limitFilterData?.Since, Until = limitFilterData?.Until }
        };
    }

    public override SubscriptionFilter Clone()
    {
        return new AccountSubscriptionFilter(ids);
    }
}

