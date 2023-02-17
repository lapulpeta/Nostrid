using NNostr.Client;
using Nostrid.Model;

namespace Nostrid.Data.Relays;

public class DmSubscriptionFilter : SubscriptionFilter, IDbFilter
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

    public IQueryable<Event> ApplyDbFilter(IQueryable<Event> events)
    {
        var query = events;
        if (LimitFilterData.Since.HasValue)
        {
            var since = LimitFilterData.Since.Value.ToUnixTimeMilliseconds();
            query = query.Where(e => e.CreatedAtCurated >= since);
        }
        if (LimitFilterData.Until.HasValue)
        {
            var until = LimitFilterData.Until.Value.ToUnixTimeMilliseconds();
            query = query.Where(e => e.CreatedAtCurated <= until);
        }
        if (account2 == null)
        {
            return query.Where(e => e.Kind == NostrKind.DM && (e.PublicKey == account1 || e.DmToId == account1));
        }
        return query.Where(e => e.Kind == NostrKind.DM && ((e.PublicKey == account1 && e.DmToId == account2) || (e.PublicKey == account2 && e.DmToId == account1)));
    }
}

