using Nostrid.Misc;
using NNostr.Client;
using Nostrid.Model;

namespace Nostrid.Data.Relays;

public abstract class SubscriptionFilter
{
    public string Id { get; private set; }

    public LimitFilterData LimitFilterData { get; } = new();

    public bool DestroyOnFirstEvent { get; set; }

    public bool DestroyOnEose { get; set; }

    public DateTimeOffset? DestroyOn { get; set; }

    public bool DontSaveInLocalCache { get; set; }

    public Action<IEnumerable<Event>>? Handler { get; set; }

    protected SubscriptionFilter()
    {
        Id = IdGenerator.Generate();
    }

    public abstract NostrSubscriptionFilter[] GetFilters();

    public abstract SubscriptionFilter Clone();
}

