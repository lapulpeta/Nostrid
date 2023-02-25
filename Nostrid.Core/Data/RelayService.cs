using LinqKit;
using Newtonsoft.Json;
using NNostr.Client;
using Nostrid.Data.Relays;
using Nostrid.Misc;
using Nostrid.Model;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Threading.Channels;
using Channel = System.Threading.Channels.Channel;

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
    private readonly string[] DefaultFreeRelays = new[] { "wss://relay.snort.social", "wss://nostr.developer.li", "wss://nostr-relay.alekberg.net", "wss://nostr.mom", "wss://relay.nostr.ch", "wss://nostr.sandwich.farm", "wss://nostr.oxtr.dev", "wss://nostr.zaprite.io", "wss://relay.minds.com/nostr/v1/ws", "wss://nostr.drss.io", "wss://nostr-verified.wellorder.net", "wss://nostr.semisol.dev", "wss://nostr-relay.untethr.me", "wss://nostr.onsats.org", "wss://nostr.cercatrova.me", "wss://nostr.swiss-enigma.ch", "wss://nostr-pub.semisol.dev", "wss://relay.nostr.info", "wss://nostr.zebedee.cloud", "wss://relay.damus.io", "wss://nostr-pub.wellorder.net" };
    private readonly string[] DefaultPaidRelays = new[] { "wss://eden.nostr.land", "wss://nostr.milou.lol", "wss://puravida.nostr.land", "wss://relay.nostr.com.au", "wss://relay.orangepill.dev", "wss://nostr.wine", "wss://nostr.inosta.cc", "wss://relay.nostrati.com", "wss://atlas.nostr.land", "wss://nostr.plebchain.org", "wss://relay.nostriches.org", "wss://relay.nostrich.land", "wss://nostr.decentony.com", "wss://bitcoiner.social", "wss://private.red.gb.net", "wss://nostr.gives.africa", "wss://nostr.uselessshit.co", "wss://nostr.ownscale.org", "wss://nostr.howtobitcoin.shop", "wss://nostr.bitcoinpuertori.co", "wss://paid.spore.ws", "wss://nostr.bitcoinplebs.de", "wss://nostr.naut.social", "wss://lightningrelay.com", "wss://relay.stonez.me", "wss://relay.orange-crush.com", "wss://relay.nostrview.com", "wss://nostr.easify.de", "wss://relay.nostr.distrl.net", "wss://nostream.denizenid.com", "wss://nostrsatva.net", "wss://at.nostrworks.com", "wss://relay.alien.blue", "wss://nostream.nostrly.io", "wss://bitcoinmaximalists.online", "wss://relay.nostr.nu", "wss://nostr.sovbit.host", "wss://nostr01.vida.dev", "wss://nostr.1sat.org" };

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
    private readonly Channel<SubscriptionFilter> filtersToAdd = Channel.CreateUnbounded<SubscriptionFilter>();
    private readonly Channel<SubscriptionFilter> filtersToDelete = Channel.CreateUnbounded<SubscriptionFilter>();

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

    private static object lockObj = new();

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
        clientThreadsCancellationTokenSource = new CancellationTokenSource();
        Task.Run(() =>
        {
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
                var maxRelays = configService.MainConfig.MaxAutoRelays;
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

        Task.Run(RunAddFilters);
        Task.Run(RunDeleteFilters);
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
        configService.MainConfig.MaxAutoRelays = 0;
        InitRelays();
    }

    private void InitRelays()
    {
        if (configService.MainConfig.MaxAutoRelays == 0)
        {
            configService.MainConfig.MaxAutoRelays = int.Max(MinRelays, Environment.ProcessorCount);
            configService.Save();
        }

        if (eventDatabase.GetRelayCount() == 0)
        {
            foreach (var relay in DefaultFreeRelays)
            {
                eventDatabase.SaveRelay(new Relay()
                {
                    Uri = relay,
                    Priority = (PriorityHigherBound + PriorityLowerBound) / 2, // Middle
                    Read = true,
                    Write = true,
                    IsPaid = false,
                });
            }
            foreach (var relay in DefaultPaidRelays)
            {
                eventDatabase.SaveRelay(new Relay()
                {
                    Uri = relay,
                    Priority = (PriorityHigherBound + PriorityLowerBound) / 2, // Middle
                    Read = true,
                    Write = true,
                    IsPaid = true,
                });
            }
        }
        else
        {
            using var db = eventDatabase.CreateContext();
            foreach (var relay in db.Relays.Where(r => !r.IsPaid))
            {
                if (DefaultPaidRelays.Contains(relay.Uri))
                {
                    relay.IsPaid = true;
                }
            }
            db.SaveChanges();
        }
    }

    private void EoseReceived(Relay relay, string subscriptionId)
    {
        if (filterBySubscriptionId.TryGetValue(subscriptionId, out var filter) && filter.DestroyOnEose)
        {
            lock (lockObj)
            {
                if (!clientByRelay.TryGetValue(relay, out var client))
                    return;
                if (!subscriptionsByClient.TryGetValue(client, out var subs))
                    return;
                subs.Where(s => s.SubscriptionId == subscriptionId).ForEach(s => s.Unsubscribe());
                subs.RemoveAll(s => s.SubscriptionId == subscriptionId);
                if (!subs.Any())
                {
                    DeleteFilters(filter);
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

                if (filter.DontSaveInLocalCache || eventDatabase.SaveNewEvent(ev, relay) || filter.Handler != null)
                {
                    newEvents.Add(ev);
                }

                //Check repost
                if (ev.Kind == NostrKind.Repost && TryGetEventFromRepost(ev, out var repostedEvent))
                {
                    if (filter.DontSaveInLocalCache || eventDatabase.SaveNewEvent(repostedEvent, relay))
                    {
                        newEvents.Add(repostedEvent);
                    }
                }
            }

            // Optimization: re-check filter as it may have been deleted since we checked it at the beginning
            if (filterBySubscriptionId.ContainsKey(subscriptionId))
            {
                if (newEvents.Count > 0)
                {
                    filter.Handler?.Invoke(newEvents);
                    ReceivedEvents?.Invoke(this, (filter.Id, newEvents));
                    if (filter.DestroyOnFirstEvent)
                    {
                        DeleteFilters(filter);
                        destroyed = true;
                    }
                }
                if (!destroyed && filter.DestroyOn.HasValue && filter.DestroyOn < DateTimeOffset.UtcNow)
                {
                    DeleteFilters(filter);
                }
            }
        }
    }

    private static bool TryGetEventFromRepost(Event repostEvent, [NotNullWhen(true)] out Event? ev)
    {
        try
        {
            if (repostEvent.Content.IsNotNullOrEmpty())
            {
                var repostedEvent = JsonConvert.DeserializeObject<NostrEvent>(repostEvent.Content);
                if (repostedEvent != null && repostedEvent.Verify())
                {
                    ev = repostedEvent.FromNostrEvent();
                    return true;
                }
            }
        }
        catch
        {
            // Do nothing
        }
        ev = null;
        return false;
    }

    public void AddFilters(IEnumerable<SubscriptionFilter> fls)
    {
        foreach (var filter in fls)
        {
            filtersToAdd.Writer.TryWrite(filter);
        }
    }

    public void AddFilters(params SubscriptionFilter[] fls)
    {
        AddFilters((IEnumerable<SubscriptionFilter>)fls);
    }

    public void RefreshFilters(params SubscriptionFilter[] fls)
    {
        DeleteFilters(fls);
        AddFilters(fls);
    }

    public void DeleteFilters(params SubscriptionFilter?[] fls)
    {
        DeleteFilters((IEnumerable<SubscriptionFilter?>)fls);
    }

    public void DeleteFilters(IEnumerable<SubscriptionFilter?> fls)
    {
        foreach (var filter in fls)
        {
            if (filter != null)
            {
                filtersToDelete.Writer.TryWrite(filter);
            }
        }
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

    public async Task EventDispatcherAsync(long relayId, string accountId)
    {
        Relay? relay;
        lock (lockObj)
        {
            relay = clientByRelay.Keys.FirstOrDefault(r => r.Id == relayId);
        }
        if (relay == null)
        {
            return;
        }

        if (!clientByRelay.TryGetValue(relay, out var client))
            return;

        foreach (var ev in eventDatabase.ListOwnEvents(accountId))
        {
            ev.Content ??= string.Empty;
            if (await client.PublishEvent(ev.ToNostrEvent(), clientThreadsCancellationTokenSource.Token))
            {
                eventDatabase.AddSeenBy(ev.Id, relay.Id);
            }
        }
    }

    private async Task EventDispatcherAsync(Relay relay, CancellationToken cancellationToken)
    {
        if (!RelaysMonitor.IsAuto)
            return;

        if (!clientByRelay.TryGetValue(relay, out var client))
            return;

        foreach (var ev in eventDatabase.ListOwnEvents(relay.Id))
        {
            ev.Content ??= string.Empty;
            if (await client.PublishEvent(ev.ToNostrEvent(), cancellationToken))
            {
                eventDatabase.AddSeenBy(ev.Id, relay.Id);
            }
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
        lock (lockObj)
        {
            if (clientByRelay.ContainsKey(relay)) return;
            var proxy = Utils.IsWasm() ? null : HttpClient.DefaultProxy;
            client = new NostrClient(new Uri(relay.Uri), proxy);
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
                SendAllFiltersToNewClient(relay, client);
                _ = Task.Run(() => EventDispatcherAsync(relay, cancellationToken));
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
        lock (lockObj)
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

    private void SendAllFiltersToNewClient(Relay relay, NostrClient client)
    {
        lock (lockObj)
        {
            var subs = subscriptionsByClient.GetOrAdd(client, _ => new());

            // Add all filters
            foreach (var f in filters)
            {
                SendFilterToClient(relay, client, subs, f);
            }
        }
    }

    private void SendFilterToClient(Relay relay, NostrClient client, List<Subscription> subs, SubscriptionFilter f)
    {
        // Auto mode uses per-relay filters if supported
        var nostrFilters = RelaysMonitor.IsAuto && f is IRelayFilter relayFilter ? relayFilter.GetFiltersForRelay(relay.Id) : f.GetFilters();
        var supportedNostrFilters = nostrFilters.Where(f => f.RequiredNips.All(rn => relay.SupportedNips.Contains(rn)));
        if (supportedNostrFilters.Any())
        {
            var sub = new Subscription(client, supportedNostrFilters.ToArray(), f.Id);
            filterBySubscriptionId[sub.SubscriptionId] = f;
            sub.Subscribe();
            subs.Add(sub);
        }
    }

    private async Task RunAddFilters()
    {
        while (!clientThreadsCancellationTokenSource.IsCancellationRequested)
        {
            try
            {
                var filterToAdd = await filtersToAdd.Reader.ReadAsync(clientThreadsCancellationTokenSource.Token);
                lock (lockObj)
                {
                    filters.Add(filterToAdd);
                    foreach (var (relay, client) in clientByRelay)
                    {
                        if (!RelaysMonitor.IsAuto && !relay.Read)
                            continue;

                        if (relayRateLimited.TryGetValue(relay.Id, out var rateLimited))
                        {
                            if (rateLimited > DateTimeOffset.UtcNow)
                            {
                                continue;
                            }
                            relayRateLimited.TryRemove(relay.Id, out _);
                        }

                        var subs = subscriptionsByClient.GetOrAdd(client, _ => new());

                        if (!subs.Any(s => s.FilterId == filterToAdd.Id))
                        {
                            SendFilterToClient(relay, client, subs, filterToAdd);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                // Retry
            }
        }
    }

    private async Task RunDeleteFilters()
    {
        while (!clientThreadsCancellationTokenSource.IsCancellationRequested)
        {
            try
            {
                var filterToRemove = await filtersToDelete.Reader.ReadAsync(clientThreadsCancellationTokenSource.Token);
                lock (lockObj)
                {
                    filters.Remove(filterToRemove);
                    foreach (var (relay, client) in clientByRelay)
                    {
                        if (!RelaysMonitor.IsAuto && !relay.Read)
                            continue;

                        //if (relayRateLimited.TryGetValue(relay.Id, out var rateLimited))
                        //{
                        //    if (rateLimited > DateTimeOffset.UtcNow)
                        //    {
                        //        continue;
                        //    }
                        //    relayRateLimited.TryRemove(relay.Id, out _);
                        //}

                        var subs = subscriptionsByClient.GetOrAdd(client, _ => new());

                        // Remove unused filters
                        for (int i = subs.Count - 1; i >= 0; i--)
                        {
                            var sub = subs[i];
                            if (!filters.Any(f => f.Id == sub.FilterId))
                            {
                                sub.Unsubscribe();
                                filterBySubscriptionId.TryRemove(sub.SubscriptionId, out _); // TODO: find out best way to cleanup this dictionary
                                subs.RemoveAt(i);
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                // Retry
            }
        }
    }

    public void SendEvent(NostrEvent nostrEvent, bool save = true)
    {
        if (save)
        {
            eventDatabase.SaveOwnEvents(nostrEvent, RelaysMonitor.IsAuto);
        }
        lock (lockObj)
        {
            foreach (var (relay, client) in clientByRelay)
            {
                if (RelaysMonitor.IsAuto || relay.Write)
                {
                    try
                    {
                        _ = Task.Run(async () =>
                        {
                            if (await client.PublishEvent(nostrEvent))
                            {
                                eventDatabase.AddSeenBy(nostrEvent.Id, relay.Id);
                            }
                        });
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
            using var httpResponse = await httpClient.GetAsync(Utils.GetRelayMainAddress(uri), cancellationToken);
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

