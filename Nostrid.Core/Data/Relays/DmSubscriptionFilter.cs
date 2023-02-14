using NNostr.Client;
using Nostrid.Model;

namespace Nostrid.Data.Relays;

public class DmSubscriptionFilter : SubscriptionFilter
{
    private readonly string account1;
    private readonly string? account2;

    public DmSubscriptionFilter(string account1, string? account2)
    {
        this.account1 = account1;
        this.account2 = account2;
    }

    public DmSubscriptionFilter(string account1)
    {
        this.account1 = account1;
    }

    public override NostrSubscriptionFilter[] GetFilters()
    {
        if (account2 == null)
        {
            return new[] {
                new NostrSubscriptionFilter() { Authors = new[]{ account1 }, Kinds = new[]{ NostrKind.DM }, Limit = LimitFilterData?.Limit, Since = LimitFilterData?.Since, Until = LimitFilterData?.Until },
                new NostrSubscriptionFilter() { PublicKey = new[]{ account1 } , Kinds = new[]{ NostrKind.DM }, Limit = LimitFilterData?.Limit, Since = LimitFilterData?.Since, Until = LimitFilterData?.Until },
            };
        }

        return new[] {
            new NostrSubscriptionFilter() { Authors = new[]{ account1 }, PublicKey = new[]{ account2 } , Kinds = new[]{ NostrKind.DM }, Limit = LimitFilterData?.Limit, Since = LimitFilterData?.Since, Until = LimitFilterData?.Until },
            new NostrSubscriptionFilter() { Authors = new[]{ account2 }, PublicKey = new[]{ account1 } , Kinds = new[]{ NostrKind.DM }, Limit = LimitFilterData?.Limit, Since = LimitFilterData?.Since, Until = LimitFilterData?.Until },
        };
    }

    public override SubscriptionFilter Clone()
    {
        return new DmSubscriptionFilter(account1, account2);
    }
}

