using Nostrid.Misc;
using NNostr.Client;
using Nostrid.Model;

namespace Nostrid.Data.Relays;

public abstract class SubscriptionFilter
{
    public string Id { get; private set; }

    public LimitFilterData LimitFilterData { get; } = new();

    /// <summary>
    /// Destroys the whole filter when one event is received
    /// </summary>
    public bool DestroyOnFirstEvent { get; set; }

    /// <summary>
    /// Destroy the subscription (not the whole filter) when EOSE is received
    /// </summary>
    public bool DestroyOnEose { get; set; }

    /// <summary>
    /// Destroys the whole filter at a given moment
    /// </summary>
    public DateTimeOffset? DestroyOn { get; set; }

    /// <summary>
    /// Event is not stored in DB
    /// </summary>
    public bool DontSaveInLocalCache { get; set; }

    /// <summary>
    /// Method that will be notified when one or more events are received
    /// </summary>
    public Action<IEnumerable<Event>>? Handler { get; set; }

    protected SubscriptionFilter()
    {
        Id = IdGenerator.Generate();
    }

    public abstract NostrSubscriptionFilter[] GetFilters();

    public abstract SubscriptionFilter Clone();
}

