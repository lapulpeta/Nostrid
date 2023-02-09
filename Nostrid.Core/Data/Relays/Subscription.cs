using NNostr.Client;
using Nostrid.Misc;

namespace Nostrid.Data.Relays;

public class Subscription : IDisposable
{
    private readonly NostrClient client;
    private readonly NostrSubscriptionFilter[] nostrFilters;
    private readonly string filterId;
    private readonly string id;

    private bool subscribed;
    private bool disposedValue;

    public string SubscriptionId => id;
    public string FilterId => filterId;

    public Subscription(NostrClient client, NostrSubscriptionFilter[] nostrFilters, string filterId)
    {
        this.client = client;
        this.nostrFilters = nostrFilters;
        this.filterId = filterId;
        id = IdGenerator.Generate();
    }

    public async void Subscribe()
    {
        await client.CreateSubscription(SubscriptionId, nostrFilters);
        subscribed = true;
    }

    public async void Unsubscribe()
    {
        if (subscribed)
        {
            await client.CloseSubscription(SubscriptionId);
            subscribed = false;
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                Unsubscribe();
            }

            // TODO: set large fields to null
            disposedValue = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}

