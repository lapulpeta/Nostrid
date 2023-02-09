using Nostrid.Misc;
using NNostr.Client;

namespace Nostrid.Data.Relays;

public abstract class SubscriptionFilter
{
    public string Id { get; private set; }

    public List<int> RequiredNips { get; private set; }

    public LimitFilterData LimitFilterData { get; } = new();

    public bool DestroyOnFirstEvent { get; set; }

    public bool DestroyOnEose { get; set; }

    public DateTimeOffset? DestroyOn { get; set; }

    public bool DontSaveInLocalCache { get; set; }

    protected SubscriptionFilter(params int[] requiredNips)
    {
        Id = IdGenerator.Generate();
        RequiredNips = new(requiredNips);
    }

    public abstract NostrSubscriptionFilter[] GetFilters();

    public abstract SubscriptionFilter Clone();
}

