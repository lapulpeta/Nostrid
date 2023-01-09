using Nostrid.Misc;
using NNostr.Client;

namespace Nostrid.Data.Relays;

public class MentionSubscriptionFilter : SubscriptionFilter
{
    private readonly string[] ids;

	public MentionSubscriptionFilter(string id) : this(new[] { id })
    {
	}

    public MentionSubscriptionFilter(string[] ids)
    {
        this.ids = ids;
        ParamsId = Utils.HashWithSHA256("msf:" + ids.OrderBy(x => x).Aggregate((a, b) => $"{a}:{b}"));
    }

    public override NostrSubscriptionFilter[] GetFilters()
    {
        return new[] {
            new NostrSubscriptionFilter() { PublicKey = ids, Kinds = new[]{ NostrKind.Text, NostrKind.Deletion, NostrKind.Repost }, Limit = limitFilterData?.Limit, Since = limitFilterData?.Since, Until = limitFilterData?.Until },
        };
    }

    public override SubscriptionFilter Clone()
    {
        return new MentionSubscriptionFilter(ids);
    }
}

