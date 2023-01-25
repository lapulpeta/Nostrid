using Nostrid.Misc;
using NNostr.Client;

namespace Nostrid.Data.Relays;

public abstract class SubscriptionFilter
{
    public string Id { get; private set; }

    public LimitFilterData limitFilterData { get; } = new();

    public bool DestroyOnFirstEvent { get; set; }

    public bool DestroyOnEose { get; set; }

    public DateTimeOffset? DestroyOn { get; set; }

    protected SubscriptionFilter()
    {
        Id = IdGenerator.Generate();
    }

    public abstract NostrSubscriptionFilter[] GetFilters();

    public abstract SubscriptionFilter Clone();
}

