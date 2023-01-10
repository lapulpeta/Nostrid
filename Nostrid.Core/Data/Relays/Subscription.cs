using Nostrid.Misc;
using NNostr.Client;

namespace Nostrid.Data.Relays;

public class Subscription : IDisposable
{
    private bool disposedValue;
    private readonly NostrClient client;
    private readonly SubscriptionFilter filter;
    private readonly string id;

    private bool subscribed;

    public string SubscriptionId { get { return id; } }

    public Subscription(NostrClient client, SubscriptionFilter filter)
    {
        this.client = client;
        this.filter = filter;
        id = IdGenerator.Generate();
    }

    public SubscriptionFilter Filter { get { return filter; } }

    public async void Subscribe()
    {
        await client.CreateSubscription(SubscriptionId, filter.GetFilters());
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

