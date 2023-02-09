using NNostr.Client;
using Nostrid.Model;

namespace Nostrid.Data.Relays;

public class SingleEventSubscriptionFilter : SubscriptionFilter
{
    private readonly string id;

    public SingleEventSubscriptionFilter(string id)
    {
        this.id = id;
    }

    public override NostrSubscriptionFilter[] GetFilters()
    {
        return new[] {
            new NostrSubscriptionFilter() { Ids = new[]{id }, Kinds = new[]{ NostrKind.Text }, Limit = LimitFilterData?.Limit, Since = LimitFilterData?.Since, Until = LimitFilterData?.Until },
        };
    }

    public override SubscriptionFilter Clone()
    {
        return new SingleEventSubscriptionFilter(id);
    }
}

