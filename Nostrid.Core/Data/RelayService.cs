using LinqKit;
using NNostr.Client;
using Nostrid.Data.Relays;
using Nostrid.Model;
using System.Collections.Concurrent;

namespace Nostrid.Data;

public class RelayService
{
    private readonly string[] DefaultHighPriorityRelays = new[] { "wss://relay.damus.io", "wss://relay.nostr.info", "wss://nostr-pub.wellorder.net", "wss://nostr.onsats.org", "wss://nostr-pub.semisol.dev", "wss://nostr.walletofsatoshi", "wss://nostr-relay.wlvs.space", "wss://nostr.bitcoiner.social", "wss://nostr.zebedee.cloud" };
    private readonly string[] DefaultLowPriorityRelays = new[] { "wss://nostr.openchain.fr", "wss://nostr.sandwich.farm", "wss://nostr.ono.re", "wss://nostr.rocks", "wss://nostr-relay.untethr.me", "wss://nostr.mom", "wss://relayer.fiatjaf.com", "wss://expensive-relay.fiatjaf.com", "wss://freedom-relay.herokuapp.com/ws", "wss://nostr-relay.freeberty.net", "wss://relay.nostr.ch", "wss://nostr.zaprite.io", "wss://nostr.delo.software", "wss://nostr-relay.untethr.me", "wss://nostr.semisol.dev", "wss://nostr-verified.wellorder.net", "wss://nostr.drss.io", "wss://nostr.unknown.place", "wss://nostr.oxtr.dev", "wss://relay.grunch.dev", "wss://relay.cynsar.foundation", "wss://nostr-2.zebedee.cloud", "wss://nostr-relay.digitalmob.ro", "wss://no.str.cr" };
    private const int MinRelays = 6;
    private const int PriorityLowerBound = 0;
    private const int PriorityHigherBound = 10;

    private readonly EventDatabase eventDatabase;
    private readonly ConfigService configService;
    private readonly List<SubscriptionFilter> filters = new();
    private readonly ConcurrentDictionary<NostrClient, List<Subscription>> subscriptionsByClient = new();
    private readonly ConcurrentDictionary<Relay, NostrClient> clientByRelay = new();
    private readonly ConcurrentDictionary<string, SubscriptionFilter> filterBySubscriptionId = new();
    private readonly BlockingCollection<Relay>[] pendingRelaysByPriority = new BlockingCollection<Relay>[PriorityHigherBound - PriorityLowerBound + 1];
    private readonly ConcurrentDictionary<long, DateTimeOffset> relayRateLimited = new();

    private CancellationTokenSource clientThreadsCancellationTokenSource;

    private bool running;
    private int connectedClients;
    private List<Task> runningTasks = new();

    public int ConnectedRelays => connectedClients;
    public int PendingRelays => pendingRelaysByPriority.SelectMany(a => a).Count();
    public int RateLimitedRelays => relayRateLimited.Count;
    public int MaxRelays => Math.Max(MinRelays, Environment.ProcessorCount);
    public int FiltersCount => filters.Count;

    public event EventHandler<(string filterId, IEnumerable<Event> events)> ReceivedEvents;

    // This method starts one client per relay in a separate thread
    public RelayService(EventDatabase eventDatabase, ConfigService configService)
    {
        if (PriorityLowerBound > PriorityHigherBound)
            throw new Exception("MinPriority > MaxPriority");

        this.eventDatabase = eventDatabase;
        this.configService = configService;
        InitRelays();
        foreach (var index in Enumerable.Range(PriorityLowerBound, PriorityHigherBound - PriorityLowerBound + 1))
        {
            pendingRelaysByPriority[index] = new();
        }

        foreach (var relay in eventDatabase.ListRelays().OrderBy(r => Random.Shared.Next()))
        {
            pendingRelaysByPriority[relay.Priority].Add(relay);
        }
        StartNostrClients();
    }

    public void StartNostrClients()
    {
        if (running)
            return;

        running = true;
        _ = Task.Run(() =>
        {
            clientThreadsCancellationTokenSource = new CancellationTokenSource();
            for (int i = 0; i < MaxRelays; i++)
            {
                runningTasks.Add(RunAnyNostrClient(clientThreadsCancellationTokenSource.Token));
            }
        });
    }

    public void StopNostrClients()
    {
        running = false;
        clientThreadsCancellationTokenSource.Cancel();
    }

    public void RestartNostrClients()
    {
        _ = Task.Run(async () =>
        {
            StopNostrClients();
            await Task.WhenAll(runningTasks);
            runningTasks.Clear();
            StartNostrClients();
        });
    }

    private void InitRelays()
    {
        if (eventDatabase.GetRelayCount() == 0)
        {
            foreach (var relay in DefaultHighPriorityRelays)
            {
                eventDatabase.SaveRelay(new Relay()
                {
                    Uri = relay,
                    Priority = PriorityLowerBound // Max priority
                });
            }
            foreach (var relay in DefaultLowPriorityRelays)
            {
                eventDatabase.SaveRelay(new Relay()
                {
                    Uri = relay,
                    Priority = (PriorityHigherBound + PriorityLowerBound) / 2 // Middle
                });
            }
        }
    }

    private void EoseReceived(Relay relay, string subscriptionId)
    {
        if (filterBySubscriptionId.TryGetValue(subscriptionId, out var filter) && filter.DestroyOnEose)
        {
            lock (filters)
            {
                if (!clientByRelay.TryGetValue(relay, out var client))
                    return;
                if (!subscriptionsByClient.TryGetValue(client, out var subs))
                    return;
                subs.Where(s => s.SubscriptionId == subscriptionId).ForEach(s => s.Unsubscribe());
                subs.RemoveAll(s => s.SubscriptionId == subscriptionId);
                if (!subs.Any())
                {
                    DeleteFilter(filter);
                }
            }
        }
    }

    private void NoticeReceived(Relay relay, string message)
    {
        if (message.Contains("rate limit", StringComparison.CurrentCultureIgnoreCase))
        {
            relayRateLimited[relay.Id] = DateTimeOffset.UtcNow.AddMinutes(1);
        }
    }

    private void EventReceived(Relay relay, string subscriptionId, NostrEvent[] events)
    {
        var oldest = DateTimeOffset.UtcNow;
        var newEvents = new HashSet<Event>();
        var destroyed = false;

        foreach (var nostrEvent in events)
        {
            var ev = new Event(nostrEvent);
            if (ev.Kind == NostrKind.Text && configService.MainConfig.MinDiffIncoming > 0)
            {
                if (!ev.HasPow || ev.Difficulty < configService.MainConfig.MinDiffIncoming || !ev.CheckPowTarget(configService.MainConfig.StrictDiffCheck))
                    continue;
            }

            if (ev.CreatedAt.HasValue && ev.CreatedAt < oldest)
            {
                oldest = ev.CreatedAt.Value;
            }
            var newEvent = eventDatabase.SaveNewEvent(ev, relay);
            if (newEvent != null)
            {
                newEvents.Add(newEvent);
            }
        }
        if (filterBySubscriptionId.TryGetValue(subscriptionId, out var filter))
        {
            if (filter.PreserveOldest)
            {
                eventDatabase.UpdateFilterData(filter.ParamsId, relay.Id, oldest);
            }
            if (newEvents.Count > 0)
            {
                ReceivedEvents?.Invoke(this, (filter.Id, newEvents));
                if (filter.DestroyOnFirstEvent)
                {
                    DeleteFilter(filter);
                    destroyed = true;
                }
            }
            if (!destroyed && filter.DestroyOn.HasValue && filter.DestroyOn < DateTimeOffset.UtcNow)
            {
                DeleteFilter(filter);
            }
        }
    }

    public void AddFilter(SubscriptionFilter filter)
    {
        lock (filters)
        {
            filters.Add(filter);
        }
        UpdateSubscriptions();
    }

    public void AddFilters(params SubscriptionFilter[] fls)
    {
        lock (filters)
        {
            filters.AddRange(fls);
        }
        UpdateSubscriptions();
    }

    public void RefreshFilters(params SubscriptionFilter[] fls)
    {
        lock (filters)
        {
            filters.RemoveAll(f => fls.Contains(f));
        }
        UpdateSubscriptions();
        lock (filters)
        {
            filters.AddRange(fls);
        }
        UpdateSubscriptions();
    }

    public void DeleteFilter(SubscriptionFilter filter)
    {
        lock (filters)
        {
            filters.Remove(filter);
        }
        UpdateSubscriptions();
    }

    public void DeleteFilters(params SubscriptionFilter[] fls)
    {
        DeleteFilters((IEnumerable<SubscriptionFilter>)fls);
    }

    public void DeleteFilters(IEnumerable<SubscriptionFilter> fls)
    {
        lock (filters)
        {
            filters.RemoveAll(f => fls.Contains(f));
        }
        UpdateSubscriptions();
    }

    public string GetRecommendedRelayUri()
    {
        // TODO: improve this
        return eventDatabase.ListRelays().FirstOrDefault()?.Uri;
    }

    public void AddNewRelayIfUnknown(string uri)
    {
        if (!eventDatabase.RelayExists(uri))
        {
            AddRelay(new Relay()
            {
                Uri = uri,
            });
        }
    }

    public void AddRelay(Relay relay)
    {
        if (relay.Priority < PriorityLowerBound || relay.Priority > PriorityHigherBound)
            throw new Exception($"Priority should be between {PriorityLowerBound} and {PriorityHigherBound}");

        eventDatabase.SaveRelay(relay);
        pendingRelaysByPriority[relay.Priority].Add(relay);
    }

    private async Task EventDispatcher(Relay relay)
    {
        if (!clientByRelay.TryGetValue(relay, out var client))
            return;

        foreach (var ev in eventDatabase.ListOwnEvents(relay.Id))
        {
            ev.Event.Content ??= string.Empty; // TODO: LiteDb stores string.Empty as null
            await client.PublishEvent(ev.Event);
            eventDatabase.AddSeenBy(ev.Id, relay.Id);
        }
    }

    private static readonly object atomicSorting = new();
    private async Task RunAnyNostrClient(CancellationToken cancellationToken)
    {
        Relay? relay = null;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                BlockingCollection<Relay>.TakeFromAny(pendingRelaysByPriority, out relay, cancellationToken);

                if (!eventDatabase.RelayExists(relay.Uri))
                {
                    continue;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                await RunNostrClient(relay, cancellationToken);

                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var relayToRequeue = relay;
                _ = Task.Run(async () =>
                {
                    // Requeue after 30 seconds
                    await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                    pendingRelaysByPriority[relayToRequeue.Priority].Add(relayToRequeue); // Put back in queue
                }, cancellationToken);

                relay = null;

                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
        if (relay != null)
        {
            pendingRelaysByPriority[relay.Priority].Add(relay); // Put back in queue
        }
    }

    // This method starts a client and it is expected to run in a separate thread
    private async Task RunNostrClient(Relay relay, CancellationToken cancellationToken)
    {
        NostrClient client;
        lock (clientByRelay)
        {
            if (clientByRelay.ContainsKey(relay)) return;
            client = new NostrClient(new Uri(relay.Uri));
            clientByRelay[relay] = client;
        }
        client.NoticeReceived += (_, message) => NoticeReceived(relay, message);
        client.EventsReceived += (_, data) => EventReceived(relay, data.subscriptionId, data.events);
        client.EoseReceived += (_, subscriptionId) => EoseReceived(relay, subscriptionId);

        try
        {
            await client.ConnectAndWaitUntilConnected(cancellationToken);
            try
            {
                Interlocked.Increment(ref connectedClients);
                UpdateSubscriptions(relay);
                _ = EventDispatcher(relay);
                await client.ListenForMessages();
            }
            finally
            {
                Interlocked.Decrement(ref connectedClients);
            }
        }
        catch (Exception ex)
        {
        }
        finally
        {
            try
            {
                CleanupRelay(relay);
            }
            catch (Exception ex)
            {
            }
        }
    }

    private void CleanupRelay(Relay relay)
    {
        lock (clientByRelay)
        {
            relayRateLimited.TryRemove(relay.Id, out _);
            if (!clientByRelay.TryRemove(relay, out var client)) return;
            if (!subscriptionsByClient.TryRemove(client, out var subscriptions)) return;
            subscriptions.ForEach(s => s.Unsubscribe());
            client.Dispose();
        }
    }

    public void DeleteRelay(Relay relay)
    {
        CleanupRelay(relay);
        eventDatabase.DeleteRelay(relay);
    }

    private void UpdateSubscriptions()
    {
        lock (clientByRelay)
        {
            foreach (var relay in clientByRelay.Keys)
            {
                UpdateSubscriptions(relay);
            }
        }
    }

    private void UpdateSubscriptions(Relay relay)
    {
        var client = clientByRelay[relay];
        var subs = subscriptionsByClient.GetOrAdd(client, _ => new());

        if (relayRateLimited.TryGetValue(relay.Id, out var rateLimited))
        {
            if (rateLimited > DateTimeOffset.UtcNow)
            {
                return;
            }
            relayRateLimited.TryRemove(relay.Id, out _);
        }

        lock (filters)
        //lock (clientByRelay)
        {
            // Add new filters
            foreach (var f in filters)
            {
                if (!subs.Any(s => s.Filter == f))
                {
                    var sub = new Subscription(client, f);
                    if (f.PreserveOldest)
                    {
                        DateTimeOffset? oldest = eventDatabase.GetFilterData(f.ParamsId, relay.Id);
                        if (oldest.HasValue)
                            f.limitFilterData.Until = oldest.Value;
                    }
                    sub.Subscribe();
                    subs.Add(sub);
                    filterBySubscriptionId[sub.SubscriptionId] = f;
                }
            }

            // Remove unused filters
            for (int i = subs.Count - 1; i >= 0; i--)
            {
                var sub = subs[i];
                if (!filters.Contains(sub.Filter))
                {
                    sub.Unsubscribe();
                    filterBySubscriptionId.TryRemove(sub.SubscriptionId, out _);
                    subs.RemoveAt(i);
                }
            }
        }
    }

    public void SendEvent(NostrEvent nostrEvent)
    {
        eventDatabase.SaveOwnEvents(new OwnEvent(nostrEvent));
        lock (clientByRelay)
        {
            foreach (var (relay, client) in clientByRelay)
            {
                try
                {
                    _ = client.PublishEvent(nostrEvent);
                    eventDatabase.AddSeenBy(nostrEvent.Id, relay.Id);
                }
                catch
                {
                }
            }
        }
    }
}

