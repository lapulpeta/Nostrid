using NNostr.Client;
using Nostrid.Model;

namespace Nostrid.Data.Relays;

public class LongContentSubscriptionFilter : SubscriptionFilter
{
    public override NostrSubscriptionFilter[] GetFilters()
    {
        return new[] {
            new NostrSubscriptionFilter() { Kinds = new[]{ NostrKind.LongContent }, Limit = LimitFilterData?.Limit, Since = LimitFilterData?.Since, Until = LimitFilterData?.Until },
        };
    }

    public override SubscriptionFilter Clone()
    {
        return new LongContentSubscriptionFilter();
    }
}

