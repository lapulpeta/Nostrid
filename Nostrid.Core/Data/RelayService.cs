using LinqKit;
using Newtonsoft.Json;
using NNostr.Client;
using Nostrid.Data.Relays;
using Nostrid.Model;
using System.Collections.Concurrent;
using System.Net.Http.Headers;

namespace Nostrid.Data;

public class RelaysMonitor
{
    private readonly Func<int> _pendingRelaysGetter;
    private readonly Func<int> _maxRelaysGetter;
    private readonly Func<bool> _isAutoGetter;

    public RelaysMonitor(Func<int> pendingRelaysGetter, Func<int> maxRelaysGetter, Func<bool> isAutoGetter)
    {
        _pendingRelaysGetter = pendingRelaysGetter;
        _maxRelaysGetter = maxRelaysGetter;
        _isAutoGetter = isAutoGetter;
    }

    public int PendingRelays => _pendingRelaysGetter();
    public int MaxRelays => _maxRelaysGetter();
    public bool IsAuto => _isAutoGetter();
}

public class RelayService
{
    private readonly string[] DefaultRelays = new[] { "wss://nostr.milou.lol", "wss://relay.snort.social", "wss://eden.nostr.land", "wss://nostr.developer.li", "wss://nostr-relay.alekberg.net", "wss://nostr.mom", "wss://relay.nostr.ch", "wss://nostr.sandwich.farm", "wss://nostr.oxtr.dev", "wss://nostr.zaprite.io", "wss://relay.minds.com/nostr/v1/ws", "wss://nostr.drss.io", "wss://nostr-verified.wellorder.net", "wss://nostr.semisol.dev", "wss://nostr-relay.untethr.me", "wss://nostr.onsats.org", "wss://nostr.cercatrova.me", "wss://nostr.swiss-enigma.ch", "wss://nostr-pub.semisol.dev", "wss://relay.nostr.info", "wss://nostr.zebedee.cloud", "wss://relay.damus.io", "wss://nostr-pub.wellorder.net" };
    private const int MinRelays = 8;
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
    private readonly ConcurrentDictionary<long, bool> connectedRelays = new();
    private readonly List<Task> runningTasks = new();

    private CancellationTokenSource clientThreadsCancellationTokenSource;
    private bool running;

    public int ConnectedRelays => connectedRelays.Count;
    public int RateLimitedRelays => relayRateLimited.Count;
    public int FiltersCount => filters.Count;

    public bool IsRestarting => clientThreadsCancellationTokenSource.IsCancellationRequested;

    public RelaysMonitor RelaysMonitor { get; private set; }

    public event EventHandler<(string filterId, IEnumerable<Event> events)> ReceivedEvents;
    public event EventHandler? ClientsStateChanged;
    public event EventHandler<(long relayId, bool connected)>? ClientStatusChanged;

    public RelayService(EventDatabase eventDatabase, ConfigService configService)
    {
        if (PriorityLowerBound > PriorityHigherBound)
            throw new Exception("MinPriority > MaxPriority");

        this.eventDatabase = eventDatabase;
        this.configService = configService;
        InitRelays();
        StartNostrClients();
    }

    public void StartNostrClients()
    {
        if (running)
            return;

        running = true;
        Task.Run(() =>
        {
            clientThreadsCancellationTokenSource = new CancellationTokenSource();
            if (configService.MainConfig.ManualRelayManagement)
            {
                var relaysToUse = eventDatabase.ListRelays().Where(r => r.Read || r.Write).ToList();
                RelaysMonitor = new RelaysMonitor(
                    () => 0,
                    () => relaysToUse.Count,
                    () => false);
                foreach (var relay in relaysToUse)
                {

                    runningTasks.Add(RunSpecificNostrClient(relay, clientThreadsCancellationTokenSource.Token));
                }
            }
            else
            {
                foreach (var index in Enumerable.Range(PriorityLowerBound, PriorityHigherBound - PriorityLowerBound + 1))
                {
                    pendingRelaysByPriority[PriorityHigherBound - PriorityLowerBound - index] = new();
                }
                foreach (var relay in eventDatabase.ListRelays().OrderBy(r => Random.Shared.Next()))
                {
                    pendingRelaysByPriority[PriorityHigherBound - PriorityLowerBound - relay.Priority].Add(relay);
                }
                var maxRelays = Math.Max(MinRelays, Environment.ProcessorCount);
                RelaysMonitor = new RelaysMonitor(
                    () => pendingRelaysByPriority.SelectMany(a => a).Count(),
                    () => maxRelays,
                    () => true);
                foreach (var index in Enumerable.Range(0, maxRelays))
                {
                    runningTasks.Add(RunAnyNostrClient(clientThreadsCancellationTokenSource.Token));
                }
            }
            ClientsStateChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    public void StopNostrClients()
    {
        running = false;
        clientThreadsCancellationTokenSource.Cancel();
        ClientsStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RestartNostrClients()
    {
        Task.Run(async () =>
        {
            StopNostrClients();
            await Task.WhenAll(runningTasks);
            runningTasks.Clear();
            StartNostrClients();
        });
    }

    public void ResetRelays()
    {
        eventDatabase.ClearRelays();
        InitRelays();
    }

    private void InitRelays()
    {
        if (eventDatabase.GetRelayCount() == 0)
        {
            foreach (var relay in DefaultRelays)
            {
                eventDatabase.SaveRelay(new Relay()
                {
                    Uri = relay,
                    Priority = (PriorityHigherBound + PriorityLowerBound) / 2, // Middle
                    Read = true,
                    Write = true,
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
        if (message.Contains("rate limit", StringComparison.CurrentCultureIgnoreCase) ||
            message.Contains("maximum concurrent", StringComparison.CurrentCultureIgnoreCase))
        {
            relayRateLimited[relay.Id] = DateTimeOffset.UtcNow.AddMinutes(1);
        }
    }

    private void EventReceived(Relay relay, string subscriptionId, NostrEvent[] events)
    {
        var newEvents = new HashSet<Event>();
        var destroyed = false;

        if (filterBySubscriptionId.TryGetValue(subscriptionId, out var filter))
        {
            foreach (var nostrEvent in events)
            {
                var ev = EventExtension.FromNostrEvent(nostrEvent);
                if (ev.Kind == NostrKind.Text && configService.MainConfig.MinDiffIncoming > 0)
                {
                    if (!ev.HasPow || ev.Difficulty < configService.MainConfig.MinDiffIncoming || !ev.CheckPowTarget(configService.MainConfig.StrictDiffCheck))
                        continue;
                }

                if (filter.DontSaveInLocalCache || eventDatabase.SaveNewEvent(ev, relay))
                {
                    newEvents.Add(ev);
                }
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

    public void AddFilters(IEnumerable<SubscriptionFilter> fls)
    {
        AddFilters((IEnumerable<SubscriptionFilter>)fls);
    }

    public void AddFilters(IEnumerable<SubscriptionFilter> fls)
    {
        lock (filters)
        {
            filters.AddRange(fls);
        }
        UpdateSubscriptions();
    }

    public void AddFilters(params SubscriptionFilter[] fls)
    {
        AddFilters((IEnumerable<SubscriptionFilter>)fls);
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

    public void DeleteFilters(params SubscriptionFilter?[] fls)
    {
        DeleteFilters((IEnumerable<SubscriptionFilter?>)fls);
    }

    public void DeleteFilters(IEnumerable<SubscriptionFilter?> fls)
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

    public bool AddNewRelayIfUnknown(string uri)
    {
        if (Uri.IsWellFormedUriString(uri, UriKind.Absolute))
        {
            return SaveRelay(new Relay()
            {
                Uri = uri,
                Read = true,
                Write = true
            });
        }
        return false;
    }

    public bool SaveRelay(Relay relay)
    {
        if (relay.Priority < PriorityLowerBound || relay.Priority > PriorityHigherBound)
            throw new Exception($"Priority should be between {PriorityLowerBound} and {PriorityHigherBound}");

        return eventDatabase.SaveRelay(relay);
    }

    public List<Relay> GetRelays()
    {
        return eventDatabase.ListRelays();
    }

    private async Task EventDispatcher(Relay relay)
    {
        if (!RelaysMonitor.IsAuto)
            return;

        if (!clientByRelay.TryGetValue(relay, out var client))
            return;

        foreach (var ev in eventDatabase.ListOwnEvents(relay.Id))
        {
            ev.Content ??= string.Empty;
            await client.PublishEvent(ev.ToNostrEvent());
            eventDatabase.AddSeenBy(ev.Id, relay.Id);
        }
    }

    private async Task RunSpecificNostrClient(Relay relay, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                await RunNostrClient(relay, cancellationToken);

                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunAnyNostrClient(CancellationToken cancellationToken)
    {
        Relay? relay = null;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                BlockingCollection<Relay>.TakeFromAny(pendingRelaysByPriority, out relay, cancellationToken);

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
                    pendingRelaysByPriority[PriorityHigherBound - PriorityLowerBound - relayToRequeue.Priority].Add(relayToRequeue); // Put back in queue
                }, cancellationToken);

                relay = null;

                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
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
            var nip11 = await TryNip11(relay.Uri, cancellationToken);
            if (nip11 != null && nip11.SupportedNips != null)
            {
                relay.SupportedNips = nip11.SupportedNips;
            }
            await client.ConnectAndWaitUntilConnected(cancellationToken);
            try
            {
                connectedRelays[relay.Id] = true;
                try
                {
                    ClientStatusChanged?.Invoke(this, (relay.Id, true));
                }
                catch { }
                UpdateSubscriptions(relay);
                _ = EventDispatcher(relay);
                await client.ListenForMessages();
            }
            finally
            {
                connectedRelays.TryRemove(relay.Id, out _);
                try
                {
                    ClientStatusChanged?.Invoke(this, (relay.Id, false));
                }
                catch { }
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

    public bool IsRelayConnected(long relayId)
    {
        return connectedRelays.ContainsKey(relayId);
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

    public void DeleteRelay(long relayId)
    {
        eventDatabase.DeleteRelay(relayId);
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
        if (!RelaysMonitor.IsAuto && !relay.Read)
            return;

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
            var supportedFilters = filters.Where(f => f.RequiredNips.All(rn => relay.SupportedNips.Contains(rn)));
            foreach (var f in supportedFilters)
            {
                if (!subs.Any(s => s.Filter == f))
                {
                    var sub = new Subscription(client, f);
                    filterBySubscriptionId[sub.SubscriptionId] = f;
                    sub.Subscribe();
                    subs.Add(sub);
                }
            }

            // Remove unused filters
            for (int i = subs.Count - 1; i >= 0; i--)
            {
                var sub = subs[i];
                if (!filters.Contains(sub.Filter))
                {
                    sub.Unsubscribe();
                    filterBySubscriptionId.TryRemove(sub.SubscriptionId, out _); // TODO: find out best way to cleanup this dictionary
                    subs.RemoveAt(i);
                }
            }
        }
    }

    public void SendEvent(NostrEvent nostrEvent)
    {
        eventDatabase.SaveOwnEvents(nostrEvent, RelaysMonitor.IsAuto);
        lock (clientByRelay)
        {
            foreach (var (relay, client) in clientByRelay)
            {
                if (RelaysMonitor.IsAuto || relay.Write)
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

    // NIP-11: https://github.com/nostr-protocol/nips/blob/master/11.md
    private static async Task<Nip11Response?> TryNip11(string uri, CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/nostr+json"));
            using var httpResponse = await httpClient.GetAsync(uri.Replace("wss://", "https://"), cancellationToken);
            if (httpResponse.IsSuccessStatusCode)
            {
                var response = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
                if (response.IsNotNullOrEmpty())
                {
                    return JsonConvert.DeserializeObject<Nip11Response>(response);
                }
            }
        }
        catch (Exception ex)
        {
        }
        return null;
    }
}

