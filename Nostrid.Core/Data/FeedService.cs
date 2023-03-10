using Microsoft.EntityFrameworkCore;
using NNostr.Client;
using Nostrid.Data.Relays;
using Nostrid.Misc;
using Nostrid.Model;
using System.Collections.Concurrent;

namespace Nostrid.Data;

public class FeedService
{
    private readonly EventDatabase eventDatabase;
    private readonly RelayService relayService;
    private readonly AccountService accountService;
    private readonly ChannelService channelService;
    private readonly DmService dmService;
    private readonly ConcurrentDictionary<string, DateTime> detailsNeededIds = new();

    private bool running;
    private CancellationTokenSource clientThreadsCancellationTokenSource;
    private bool detailsNeededIdsChanged;
    private bool detailsNeededForceClear;

    public event EventHandler<(string filterId, IEnumerable<Event> notes)> NotesReceived;
    public event EventHandler<(string eventId, EventDetailsCount delta)> NoteCountChanged;
    public event EventHandler<(string eventId, string reactorId)> NoteReacted;
    public event EventHandler<string> NoteDeleted;
    public event EventHandler<(string EventId, Event Child)> NoteReceivedChild;

    public FeedService(EventDatabase eventDatabase, RelayService relayService, AccountService accountService, ChannelService channelService, DmService dmService)
    {
        this.eventDatabase = eventDatabase;
        this.relayService = relayService;
        this.accountService = accountService;
        this.channelService = channelService;
        this.dmService = dmService;
        this.relayService.ReceivedEvents += ReceivedEvents;
        StartDetailsUpdater();
    }

    private void HandleEvent(Event eventToProcess, string filterId)
    {
        switch (eventToProcess.Kind)
        {
            case NostrKind.Metadata:
                accountService.HandleKind0(eventToProcess);
                break;
            case NostrKind.ChannelMessage:
                channelService.MessageProcessed(eventToProcess);
                goto case NostrKind.Text;
            case NostrKind.Text:
                HandleMessage(eventToProcess);
                break;
            case NostrKind.Relay:
                HandleKind2(eventToProcess);
                break;
            case NostrKind.Contacts:
                accountService.HandleKind3(eventToProcess, filterId);
                break;
            case NostrKind.DM:
                dmService.HandleDm(eventToProcess);
                break;
            case NostrKind.Deletion:
                HandleKind5(eventToProcess);
                break;
            case NostrKind.Zap:
            case NostrKind.Reaction:
            case NostrKind.Repost:
                HandleRepostReactionOrZap(eventToProcess);
                break;
            case NostrKind.ChannelCreation:
            case NostrKind.ChannelMetadata:
                channelService.HandleChannelCreationOrMetadata(eventToProcess);
                break;
            case NostrKind.Mutes:
                Task.Run(() => accountService.HandleMuteList(eventToProcess));
                break;
        }
    }

    private void ReceivedEvents(object sender, (string filterId, IEnumerable<Event> events) data)
    {
        foreach (var ev in data.events)
            HandleEvent(ev, data.filterId);

        var notes = data.events.Where(ev => !eventDatabase.IsEventDeleted(ev.Id));
        if (notes.Any())
        {
            NotesReceived?.Invoke(this, (data.filterId, notes));
        }
    }

    public void HandleMessage(Event eventToProcess)
    {
        if (eventToProcess.ReplyToId.IsNotNullOrEmpty())
        {
            NoteReceivedChild?.Invoke(this, (eventToProcess.ReplyToId, eventToProcess));
        }
    }

    public void HandleKind2(Event eventToProcess)
    {
        if (relayService.RelaysMonitor.IsAuto)
        {
            HandleRelayRecommendation(eventToProcess.Content);
        }
    }

    public void HandleKind5(Event eventToProcess)
    {
        var e = eventToProcess.Tags.Where(t => t.Data0 == "e" && t.DataCount > 1).ToList();
        foreach (var tag in e)
        {
            var eventId = tag.Data1;
            if (Utils.IsValidNostrId(eventId))
            {
                eventDatabase.MarkEventAsDeleted(eventId, eventToProcess.PublicKey);
                NoteDeleted?.Invoke(this, eventId);
            }
        }
    }

    private void HandleRelayRecommendation(string uri)
    {
        relayService.AddNewRelayIfUnknown(uri.ToLower());
    }

    public void HandleRepostReactionOrZap(Event eventToProcess)
    {
        // NIP-25: https://github.com/nostr-protocol/nips/blob/master/25.md
        // NIP-57: https://github.com/nostr-protocol/nips/blob/master/57.md
        var tag = eventToProcess.Tags.Where(t => (t.Data0 == "e" || t.Data0 == "a") && t.Data1 != null).LastOrDefault();
        if (tag == null)
        {
            return;
        }

        if ((tag.Data0 != "e" || !Utils.IsValidNostrId(tag.Data1)) &&
            (tag.Data0 != "a" || !tag.Data1!.IsReplaceableIdStrict()))
        {
            return;
        }

        EventDetailsCount delta = eventToProcess.Kind switch
        {
            NostrKind.Repost => new() { Reposts = 1 },
            NostrKind.Zap => new() { Zaps = 1 },
            NostrKind.Reaction => new() { ReactionGroups = new() { [eventToProcess.Content ?? string.Empty] = 1 } },
            _ => new()
        };
        NoteCountChanged?.Invoke(this, (tag.Data1!, delta));
        if (eventToProcess.Kind == NostrKind.Reaction)
        {
            NoteReacted?.Invoke(this, (tag.Data1!, eventToProcess.PublicKey));
        }
    }

    public List<Event> GetGlobalFeed(int count)
    {
        return eventDatabase.ListNotes(count).ToList();
    }

    public IEnumerable<Event> FilterList(SubscriptionFilter filter, IEnumerable<Event> events, int[]? kinds = null)
    {
        if (kinds != null)
        {
            events = events.Where(e => kinds.Contains(e.Kind));
        }
        if (filter is IDbFilter dbFilter)
        {
            return dbFilter.ApplyDbFilter(events.AsQueryable());
        }
        return eventDatabase.ApplyFilters(events.AsQueryable(), filter.GetFilters());
    }

    public List<Event> GetNotesFeed(SubscriptionFilter filter, int count, int[]? kinds = null)
    {
        if (filter is IDbFilter dbFilter)
        {
            return eventDatabase.ListNotes(dbFilter, kinds, count);
        }
        return eventDatabase.ListNotes(filter.GetFilters(), kinds, count);
    }

    public List<Event> GetNotesThread(string eventId, out string? standardRootId, out string? replaceableRootId)
    {
        int[] validRootKinds = new[] { NostrKind.Text, NostrKind.LongContent, NostrKind.Badge };
        int[] validChildKinds = new[] { NostrKind.Text };

        using var db = eventDatabase.CreateContext();
        var note = db.Events.AsNoTracking().FirstOrDefault(e => validRootKinds.Contains(e.Kind) && (e.Id == eventId || e.ReplaceableId == eventId));
        note ??= db.Events.AsNoTracking().FirstOrDefault(e => validChildKinds.Contains(e.Kind) && (e.ReplyToRootId == eventId || e.ReplyToId == eventId));
        if (note == null)
        {
            standardRootId = null;
            replaceableRootId = null;
            return new();
        }
        if (note.ReplyToRootId.IsNullOrEmpty())
        {
            standardRootId = note.Id;
            replaceableRootId = note.ReplaceableId;
        }
        else if (note.ReplyToRootId.IsReplaceableId())
        {
            standardRootId = null;
            replaceableRootId = note.ReplyToRootId;
        }
        else
        {
            standardRootId = note.ReplyToRootId;
            replaceableRootId = null;
        }

        List<Event> events = new();
        if (standardRootId.IsNotNullOrEmpty())
        {
            var queryRootId = standardRootId;
            events = db.Events
                .AsNoTracking()
                .Include(e => e.Tags)
                .Where(e => (validRootKinds.Contains(e.Kind) && e.Id == queryRootId) || (validChildKinds.Contains(e.Kind) && e.ReplyToRootId == queryRootId))
                .ToList();
        }
        
        if (replaceableRootId.IsNotNullOrEmpty())
        {
            var queryRootId = replaceableRootId;
            var alreadyLoadedEvents = events.Select(e => e.Id).ToList();
            events.AddRange(db.Events
                .AsNoTracking()
                .Include(e => e.Tags)
                .Where(e => !alreadyLoadedEvents.Contains(e.Id))
                .Where(e => (validRootKinds.Contains(e.Kind) && e.ReplaceableId == queryRootId) || (validChildKinds.Contains(e.Kind) && e.ReplyToRootId == queryRootId))
                );
        }

        return events;
    }

    public List<NoteTree> GetTreesFromNotesNoGrouping(IEnumerable<Event> evs)
    {
        return evs.Select(ev => new NoteTree(ev)).ToList();
    }

    public List<NoteTree> GetTreesFromNotes(IEnumerable<Event> tws, List<NoteTree>? rootTrees = null)
    {
        if (rootTrees == null)
        {
            rootTrees = new();
        }

        foreach (var tw in tws.ToList())
        {
            if (rootTrees.Exists(tw.Id)) continue;
            var root = rootTrees.Find(tw.ReplyToId);
            var newTree = new NoteTree(tw, root);
            if (root != null)
            {
                root.Children.Add(newTree);
                // Internal nodes are sorted newest on top
                root.Children.Sort((a, b) => b.Note.CreatedAtCurated.CompareTo(a.Note.CreatedAtCurated));
            }
            else
            {
                rootTrees.Add(newTree);
            }
        }

        for (int i = rootTrees.Count - 1; i >= 0; i--)
        {
            var rootTree = rootTrees[i];
            var subtree = rootTrees.Find(rootTree.Note.ReplyToId);
            if (subtree != null)
            {
                rootTrees.Remove(rootTree);
                rootTree.Parent = subtree;
                subtree.Children.Add(rootTree);
                // Internal nodes are sorted newest on top
                subtree.Children.Sort((a, b) => b.Note.CreatedAtCurated.CompareTo(a.Note.CreatedAtCurated));
            }
        }

        // Root nodes are sorted oldest on top (original first)
        rootTrees.Sort((a, b) => a.Note.CreatedAtCurated.CompareTo(b.Note.CreatedAtCurated));

        return rootTrees;
    }

    public async Task<bool> SendNoteWithPow(string content, int kind, string? replyToId, string? rootId, IEnumerable<string>? accountMentionIds, int diff, CancellationToken cancellationToken)
    {
        NostrEvent? unsignedNote;

        if (kind == NostrKind.DM)
        {
            unsignedNote = await AssembleDm(content, replyToId, accountMentionIds.First());
        }
        else
        {
            unsignedNote = AssembleNote(content, kind == NostrKind.ChannelMessage, replyToId, rootId, accountMentionIds);
        }
        if (unsignedNote == null)
        {
            return false;
        }

        if (diff > 0)
        {
            // Create placeholders for real values
            var nonceMarker = IdGenerator.Generate();
            var randomLong = (long)(Random.Shared.Next() << 32) + Random.Shared.Next();
            unsignedNote.CreatedAt = DateTimeOffset.FromUnixTimeSeconds(randomLong);
            var createdMarker = randomLong.ToString();

            var nonceTag = new NostrEventTag()
            {
                TagIdentifier = "nonce",
                Data = { nonceMarker, diff.ToString() }
            };
            unsignedNote.Tags.Add(nonceTag);

            var eventJson = unsignedNote.ToJson(true);

            int maxTasks = Environment.ProcessorCount;

            ulong? foundNonce = null;
            DateTimeOffset? foundCreated = null;
            await Task.WhenAll(
                Enumerable.Range(0, maxTasks).Select(taskIndex => Task.Run(() =>
                {
                    ulong nonce = (uint)taskIndex;
                    while (!foundNonce.HasValue)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var created = DateTimeOffset.UtcNow;
                        var eventJsonWithNonce = eventJson
                            .Replace(nonceMarker, nonce.ToString())
                            .Replace(createdMarker, created.ToUnixTimeSeconds().ToString());
                        var id = eventJsonWithNonce.ComputeEventId();
                        if (EventExtension.CalculateDifficulty(id) >= diff)
                        {
                            foundNonce = nonce;
                            foundCreated = created;
                            return;
                        }
                        nonce += (uint)maxTasks;
                    }
                }, cancellationToken))
            );

            if (!foundNonce.HasValue) // Sanity
                throw new Exception("Nonce not found!");

            nonceTag.Data[0] = foundNonce.ToString();
            unsignedNote.CreatedAt = foundCreated;
        }

        if (!await accountService.MainAccountSigner.Sign(unsignedNote))
            return false;
        relayService.SendEvent(unsignedNote);
        return true;
    }

    private async Task<NostrEvent> AssembleDm(string content, string? replyToId, string dmWith)
    {
        var encryptedContent = await accountService.MainAccountSigner.EncryptNip04(dmWith, content);
        if (encryptedContent == null)
        {
            return null;
        }
        var nostrEvent = new NostrEvent()
        {
            CreatedAt = DateTimeOffset.UtcNow,
            Kind = NostrKind.DM,
            PublicKey = accountService.MainAccount.Id,
            Tags = new(),
            Content = encryptedContent,
        };

        if (replyToId.IsNotNullOrEmpty())
        {
            nostrEvent.Tags.Add(new NostrEventTag() { TagIdentifier = "e", Data = new() { replyToId } });
        }
        nostrEvent.Tags.Add(new NostrEventTag() { TagIdentifier = "p", Data = new() { dmWith } });

        return nostrEvent;
    }

    private NostrEvent AssembleNote(string content, bool inChannel, string? replyToId, string? rootId, IEnumerable<string>? accountMentionIds)
    {
        if (accountService.MainAccount == null)
        {
            throw new Exception("MainAccount not loaded");
        }

        var unescapedContent = content.Trim();

        var ps = new List<string>();
        var prs = new List<string>();
        var es = new List<string>();
        var ers = new List<(string, string)>();

        // Process account mentions
        foreach (var pubKey in Utils.GetAccountPubKeyMentions(unescapedContent))
        {
            var index = ps.IndexOf(pubKey);
            if (index == -1)
            {
                index = ps.Count; // Start at 0 index
                ps.Add(pubKey);
            }
            unescapedContent = unescapedContent.Replace($"@{pubKey}", $"#[{index}]");
        }
        foreach (var bech32 in Utils.GetAccountNpubMentions(unescapedContent))
        {
            var (prefix, pubKey) = ByteTools.DecodeBech32(bech32);
            if (prefix == "npub" && Utils.IsValidNostrId(pubKey))
            {
                var index = ps.IndexOf(pubKey);
                if (index == -1)
                {
                    index = ps.Count; // Continues after previous p's
                    ps.Add(pubKey);
                }
                unescapedContent = unescapedContent.Replace($"@{bech32}", $"#[{index}]");
            }
        }
        foreach (var bech32 in Utils.GetNoteMentions(unescapedContent))
        {
            var (prefix, pubKey) = ByteTools.DecodeBech32(bech32);
            if (prefix == "note" && Utils.IsValidNostrId(pubKey))
            {
                var index = es.IndexOf(pubKey);
                if (index == -1)
                {
                    index = es.Count;
                    es.Add(pubKey);
                }
                index += ps.Count; // Continues after both p's
                unescapedContent = unescapedContent.Replace($"{bech32}", $"#[{index}]");
            }
        }

        var nostrEvent = new NostrEvent()
        {
            CreatedAt = DateTimeOffset.UtcNow,
            Kind = inChannel ? NostrKind.ChannelMessage : NostrKind.Text,
            PublicKey = accountService.MainAccount.Id,
            Tags = new(),
            Content = unescapedContent,
        };

        if (!replyToId.IsNullOrEmpty())
        {
            // Use preferred method as per NIP-10 https://github.com/nostr-protocol/nips/blob/master/10.md
            if (rootId.IsNullOrEmpty())
            {
                // RootId missing maybe because of a faulty/deprecated client. Let's try to find the rootId
                rootId = replyToId;
                while (true)
                {
                    var ev = eventDatabase.GetEvent(rootId);
                    if (ev == null)
                    {
                        rootId = null;
                        break;
                    }
                    if (!ev.ReplyToRootId.IsNullOrEmpty())
                    {
                        rootId = ev.ReplyToRootId;
                        break;
                    }
                    if (ev.ReplyToId.IsNullOrEmpty())
                    {
                        break;
                    }
                    rootId = ev.ReplyToId;
                }
            }
            if (rootId.IsNullOrEmpty() || replyToId == rootId)
            {
                // A direct reply to the root of a thread should have a single marked "e" tag of type "root".
                ers.Add((replyToId, "root"));
            }
            else
            {
                ers.Add((rootId, "root"));
                ers.Add((replyToId, "reply"));
            }

            // When replying to a text event E the reply event's "p" tags should contain all of E's "p" tags as well as the "pubkey" of the event being replied to.
            if (accountMentionIds != null)
            {
                foreach (var mention in accountMentionIds)
                {
                    if (!ps.Contains(mention) && !prs.Contains(mention))
                        prs.Add(mention);
                }
            }
        }
        else if (inChannel)
        {
            ers.Add((rootId, "root"));
        }

        // First p's (mentions) (indexed)
        foreach (var p in ps)
        {
            nostrEvent.Tags.Add(new NostrEventTag() { TagIdentifier = "p", Data = new[] { p }.ToList() });
        }

        // Then e's (mentions) (indexed) (use "a" tag if it's a replaceable event)
        var relay = relayService.GetRecommendedRelayUri();
        foreach (var e in es)
        {
            var ev = eventDatabase.GetEventOrNull(e);
            (string tag, string tagdata) =
                ev != null && ev.ReplaceableId.IsNotNullOrEmpty() ?
                ("a", ev.ReplaceableId) :
                ("e", e);
            nostrEvent.Tags.Add(new NostrEventTag() { TagIdentifier = tag, Data = new[] { tagdata, relay, "mention" }.ToList() });
        }

        // Then e's (reply/root) (non-indexed) (use "a" tag if it's a replaceable event)
        foreach (var er in ers)
        {
            var ev = eventDatabase.GetEventOrNull(er.Item1);
            (string tag, string tagdata) =
                ev != null && ev.ReplaceableId.IsNotNullOrEmpty() ?
                ("a", ev.ReplaceableId) :
                ("e", er.Item1);
            nostrEvent.Tags.Add(new NostrEventTag() { TagIdentifier = tag, Data = new[] { tagdata, relay, er.Item2 }.ToList() });
        }

        // Then t's (non-indexed)
        foreach (var t in Utils.GetHashTags(content))
        {
            nostrEvent.Tags.Add(new NostrEventTag() { TagIdentifier = "t", Data = new[] { t }.ToList() });
        }

        // Then p's (reply/root) (non-indexed)
        foreach (var p in prs)
        {
            nostrEvent.Tags.Add(new NostrEventTag() { TagIdentifier = "p", Data = new[] { p }.ToList() });
        }

        return nostrEvent;
    }

    public async Task<bool> SendReaction(string reaction, Event reactTo)
    {
        // NIP-25: https://github.com/nostr-protocol/nips/blob/master/25.md
        var nostrEvent = new NostrEvent()
        {
            Content = reaction,
            CreatedAt = DateTimeOffset.UtcNow,
            Kind = 7,
            PublicKey = accountService.MainAccount.Id,
            Tags = new(),
        };
        (string tag, string tagdata) =
            reactTo.ReplaceableId.IsNotNullOrEmpty() ?
            ("a", reactTo.ReplaceableId) :
            ("e", reactTo.Id);
        nostrEvent.Tags.Add(new NostrEventTag() { TagIdentifier = tag, Data = new[] { tagdata }.ToList() });
        nostrEvent.Tags.Add(new NostrEventTag() { TagIdentifier = "p", Data = new[] { reactTo.PublicKey }.ToList() });
        if (!await accountService.MainAccountSigner.Sign(nostrEvent))
            return false;
        relayService.SendEvent(nostrEvent);
        return true;
    }

    public async Task<bool> DeleteNote(Event eventToDelete, string? reason = null)
    {
        // NIP-09: https://github.com/nostr-protocol/nips/blob/master/09.md
        var nostrEvent = new NostrEvent()
        {
            Content = reason ?? string.Empty,
            CreatedAt = DateTimeOffset.UtcNow,
            Kind = 5,
            PublicKey = accountService.MainAccount.Id,
            Tags = new(),
        };
        if (eventToDelete.PublicKey != accountService.MainAccount.Id)
        {
            throw new Exception("Can only delete own events");
        }
        nostrEvent.Tags.Add(new NostrEventTag() { TagIdentifier = "e", Data = new[] { eventToDelete.Id }.ToList() });

        if (!await accountService.MainAccountSigner.Sign(nostrEvent))
            return false;
        relayService.SendEvent(nostrEvent);

        eventDatabase.MarkEventAsDeleted(eventToDelete.Id, accountService.MainAccount.Id);

        return true;
    }

    public async Task<bool> Repost(Event repost)
    {
        // NIP-18: https://github.com/nostr-protocol/nips/blob/master/18.md
        var nostrEvent = new NostrEvent()
        {
            Content = string.Empty,
            CreatedAt = DateTimeOffset.UtcNow,
            Kind = NostrKind.Repost,
            PublicKey = accountService.MainAccount.Id,
            Tags = new(),
        };
        nostrEvent.Tags.Add(new NostrEventTag() { TagIdentifier = "e", Data = new[] { repost.Id, relayService.GetRecommendedRelayUri() }.ToList() });
        nostrEvent.Tags.Add(new NostrEventTag() { TagIdentifier = "p", Data = new[] { repost.PublicKey }.ToList() });
        if (!await accountService.MainAccountSigner.Sign(nostrEvent))
            return false;
        relayService.SendEvent(nostrEvent);
        return true;
    }

    public void ResendExistingEvent(Event? ev)
    {
        if (ev != null)
        {
            relayService.SendEvent(ev.ToNostrEvent(), false);
        }
    }

    public FeedSource GetFeedSource(long id)
    {
        return eventDatabase.GetFeedSource(id);
    }

    public List<FeedSource> GetFeedSources(string ownerId)
    {
        return eventDatabase.ListFeedSources(ownerId);
    }

    public void SaveFeedSource(FeedSource feedSource)
    {
        eventDatabase.SaveFeedSource(feedSource);
    }

    public void DeleteFeedSource(long id)
    {
        eventDatabase.DeleteFeedSource(id);
    }

    public int GetUnreadMentionsCount()
    {
        if (accountService.MainAccount == null)
            return 0;

        var mentionsFilter = accountService.MainAccountMentionsFilter.Clone();
        mentionsFilter.LimitFilterData.Since = accountService.MainAccount.LastNotificationRead.ToUniversalTime();
        return eventDatabase.GetNotesCount(mentionsFilter.GetFilters(), accountService.MainAccount.Id);
    }

    public EventDetailsCount GetEventDetailsCount(string eventId)
    {
        return eventDatabase.GetEventDetailsCount(eventId);
    }

    public bool AccountReacted(string eventId, string accountId)
    {
        return eventDatabase.AccountReacted(eventId, accountId);
    }

    public void StartDetailsUpdater()
    {
        if (running)
            return;

        running = true;
        clientThreadsCancellationTokenSource = new CancellationTokenSource();
        Task.Run(async () => await QueryDetails(clientThreadsCancellationTokenSource.Token));
    }

    public void StopDetailsUpdater()
    {
        running = false;
        clientThreadsCancellationTokenSource.Cancel();
    }

    public void AddDetailsNeeded(params string?[] eventIds)
    {
        AddDetailsNeeded((IEnumerable<string?>)eventIds);
    }

    public void RemoveDetailsNeeded(params string?[] eventIds)
    {
        RemoveDetailsNeeded((IEnumerable<string?>)eventIds);
    }

    public void AddDetailsNeeded(IEnumerable<string?> eventIds)
    {
        var expireOn = DateTime.MaxValue; // For now request doesn't expire and it has to manually be closed
        lock (detailsNeededIds)
        {
            foreach (var eventId in eventIds)
            {
                if (eventId.IsNotNullOrEmpty())
                {
                    detailsNeededIds[eventId] = expireOn;
                    detailsNeededIdsChanged = true;
                }
            }
        }
    }

    public void RemoveDetailsNeeded(IEnumerable<string?> eventIds)
    {
        lock (detailsNeededIds)
        {
            foreach (var eventId in eventIds)
            {
                if (eventId.IsNotNullOrEmpty())
                {
                    detailsNeededIds.TryRemove(eventId, out _);
                    detailsNeededIdsChanged = true;
                }
            }
        }
    }

    public void ClearDetailsNeeded()
    {
        lock (detailsNeededIds)
        {
            detailsNeededIds.Clear();
            detailsNeededForceClear = true;
        }
    }

    private const int SecondsForDetailsFilters = 10;
    public async Task QueryDetails(CancellationToken cancellationToken)
    {
        List<string> willQuery = new();
        List<SubscriptionFilter> filters = new();
        bool mustUpdate;
        while (!cancellationToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            lock (detailsNeededIds)
            {
                if (detailsNeededForceClear)
                {
                    detailsNeededForceClear = false;
                    detailsNeededIdsChanged = true;
                    willQuery = new();
                }
                
                if (detailsNeededIdsChanged)
                {
                    detailsNeededIdsChanged = false;
                    detailsNeededIds.Where(d => d.Value < now).Select(d => d.Key).ToList().ForEach(id => detailsNeededIds.TryRemove(id, out _));
                    var oldWillQuery = new List<string>(willQuery);
                    willQuery = new(detailsNeededIds.Keys);
                    mustUpdate = willQuery.Except(oldWillQuery).Any();
                    if (!mustUpdate)
                        willQuery = oldWillQuery;
                }
                else
                {
                    mustUpdate = false;
                }
            }

            if (mustUpdate && willQuery.Count > 0)
            {
                relayService.DeleteFilters(filters);
                filters = new();
                var rids = willQuery.Where(id => id.Contains(':')).ToArray();
                var eids = willQuery.Except(rids).ToArray();
                // TODO: optimize this so they are checked separately (eg. we don't need to recreate eids if only rids changed)
                if (eids.Any())
                {
                    filters.Add(new EventSubscriptionFilter(eids));
                }
				if (rids.Any())
				{
					filters.Add(new ReplaceableEventSubscriptionFilter(rids));
				}
				relayService.AddFilters(filters);
                await Task.Delay(TimeSpan.FromSeconds(SecondsForDetailsFilters), cancellationToken);
            }
            else
            {
                await Task.Delay(TimeSpan.FromSeconds(SecondsForDetailsFilters), cancellationToken);
            }
        }

        relayService.DeleteFilters(filters);
    }

}

