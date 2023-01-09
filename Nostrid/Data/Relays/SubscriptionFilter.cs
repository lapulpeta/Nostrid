using Nostrid.Misc;
using NNostr.Client;

namespace Nostrid.Data.Relays;

public abstract class SubscriptionFilter
{
    public string Id { get; private set; }
    public LimitFilterData limitFilterData { get; } = new();

    public bool PreserveOldest { get; set; }

    public string ParamsId { get; protected set; }

    protected SubscriptionFilter()
    {
        Id = IdGenerator.Generate();
    }

    public abstract NostrSubscriptionFilter[] GetFilters();

    public abstract SubscriptionFilter Clone();
}

