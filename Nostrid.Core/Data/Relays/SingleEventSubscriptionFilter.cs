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
            new NostrSubscriptionFilter() { Ids = new[]{id }, Kinds = new[]{ NostrKind.Text }, Limit = limitFilterData?.Limit, Since = limitFilterData?.Since, Until = limitFilterData?.Until },
        };
    }

    public override SubscriptionFilter Clone()
    {
        return new SingleEventSubscriptionFilter(id);
    }
}

