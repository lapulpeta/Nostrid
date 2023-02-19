using NNostr.Client;
using Nostrid.Model;
using System.Linq;

namespace Nostrid.Data.Relays;

public class AllSubscriptionFilter : SubscriptionFilter, IRelayFilter, IDbFilter
{
    private readonly int[] validKinds = new[] { NostrKind.Text, NostrKind.Relay, NostrKind.Deletion, NostrKind.Repost, NostrKind.Reaction };
    
    private readonly EventDatabase eventDatabase;

    public AllSubscriptionFilter(EventDatabase eventDatabase)
    {
        this.eventDatabase = eventDatabase;
    }

    public override NostrSubscriptionFilter[] GetFilters()
    {
        return new[] { new NostrSubscriptionFilter() { Kinds = validKinds, Limit = LimitFilterData?.Limit, Since = LimitFilterData?.Since, Until = LimitFilterData?.Until } };
    }

    public override SubscriptionFilter Clone()
    {
        return new AllSubscriptionFilter(eventDatabase);
    }

    public NostrSubscriptionFilter[] GetFiltersForRelay(long relayId)
    {
        using var db = eventDatabase.CreateContext();
        if (db.Relays.Any(r => r.Id == relayId && r.IsPaid))
        {
            return new[] { new NostrSubscriptionFilter() { Kinds = validKinds, Limit = LimitFilterData?.Limit, Since = LimitFilterData?.Since, Until = LimitFilterData?.Until } };
        }
        return Array.Empty<NostrSubscriptionFilter>();
    }

    public IQueryable<Event> ApplyDbFilter(IQueryable<Event> events)
    {
        var query = events.Where(e => validKinds.Contains(e.Kind));
        if (LimitFilterData.Since.HasValue)
        {
            var since = LimitFilterData.Since.Value.ToUnixTimeSeconds();
            query = query.Where(e => e.CreatedAtCurated >= since);
        }
        if (LimitFilterData.Until.HasValue)
        {
            var until = LimitFilterData.Until.Value.ToUnixTimeSeconds();
            query = query.Where(e => e.CreatedAtCurated <= until);
        }
        return query;
    }
}

