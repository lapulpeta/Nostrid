using Nostrid.Misc;
using NNostr.Client;
using System;
using Nostrid.Model;

namespace Nostrid.Data.Relays;

public class AccountDetailsSubscriptionFilter : SubscriptionFilter
{
    private readonly string[] ids;

	public AccountDetailsSubscriptionFilter(string id) : this(new[] { id })
    {
	}

    public AccountDetailsSubscriptionFilter(string[] ids)
    {
        this.ids = ids;
    }

    public override NostrSubscriptionFilter[] GetFilters()
    {
        return new[] {
            new NostrSubscriptionFilter() { Authors = ids, Kinds = new[]{ NostrKind.Metadata }, Limit = limitFilterData?.Limit, Since = limitFilterData?.Since, Until = limitFilterData?.Until }
        };
    }

    public static List<AccountDetailsSubscriptionFilter> CreateInBatch(string[] ids, int batchSize = 150, bool destroyOnEose = false, DateTimeOffset? destroyOn = null)
    {
        int index = 0;
        var segment = new ArraySegment<string>(ids);
        var ret = new List<AccountDetailsSubscriptionFilter>();
        while (index < ids.Length)
        {
            ret.Add(new AccountDetailsSubscriptionFilter(segment.Slice(index, Math.Min(batchSize, ids.Length - index)).ToArray())
            {
                DestroyOnEose = destroyOnEose,
                DestroyOn = destroyOn,
            });
            index += batchSize;
        }
        return ret;
    }

    public override SubscriptionFilter Clone()
    {
        return new AccountDetailsSubscriptionFilter(ids);
    }
}

