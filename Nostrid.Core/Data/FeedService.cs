using NNostr.Client;
using Nostrid.Data.Relays;
using Nostrid.Misc;
using Nostrid.Model;

namespace Nostrid.Data;

public class FeedService
{
    private readonly EventDatabase eventDatabase;
    private readonly RelayService relayService;
    private readonly AccountService accountService;

    public event EventHandler<(string filterId, IEnumerable<Event> notes)> ReceivedNotes;
    public event EventHandler<Event> NoteUpdated;
    public event EventHandler<(string EventId, Event Child)> NoteReceivedChild;

    public FeedService(EventDatabase eventDatabase, RelayService relayService, AccountService accountService)
    {
        this.eventDatabase = eventDatabase;
        this.relayService = relayService;
        this.accountService = accountService;
        this.relayService.ReceivedEvents += ReceivedEvents;
    }

    private void HandleEvent(Event eventToProcess)
    {
        switch (eventToProcess.Kind)
        {
            case NostrKind.Metadata:
                accountService.HandleKind0(eventToProcess);
                break;
            case NostrKind.Text:
                HandleKind1(eventToProcess);
                break;
            case NostrKind.Relay:
                HandleKind2(eventToProcess);
                break;
            case NostrKind.Contacts:
                accountService.HandleKind3(eventToProcess);
                break;
            case NostrKind.Deletion:
                HandleKind5(eventToProcess);
                break;
            case NostrKind.Reaction:
                HandleKind7(eventToProcess);
                break;
        }
    }

    private void ReceivedEvents(object sender, (string filterId, IEnumerable<Event> events) data)
    {
        foreach (var ev in data.events)
            HandleEvent(ev);

        var notes = data.events.Where(ev => !eventDatabase.IsEventDeleted(ev.Id));
        if (notes.Any())
        {
            ReceivedNotes?.Invoke(this, (data.filterId, notes));
        }
    }

    public void HandleKind1(Event eventToProcess)
    {
        if (eventToProcess.ReplyToId.IsNotNullOrEmpty())
        {
            NoteReceivedChild?.Invoke(this, (eventToProcess.ReplyToId, eventToProcess));
        }
    }

    public void HandleKind2(Event eventToProcess)
    {
        HandleRelayRecommendation(eventToProcess.Content);
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
            }
        }

        NoteUpdated?.Invoke(this, eventToProcess);
    }

    private void HandleRelayRecommendation(string uri)
    {
        if (Uri.IsWellFormedUriString(uri, UriKind.Absolute))
        {
            relayService.AddNewRelayIfUnknown(uri.ToLower());
        }
    }

    public void HandleKind7(Event eventToProcess)
    {
        // NIP-25: https://github.com/nostr-protocol/nips/blob/master/25.md
        var etag = eventToProcess.Tags.Where(t => t.Data0 == "e" && t.Data1 != null).LastOrDefault();
        if (Utils.IsValidNostrId(etag.Data1))
        {
            var reactedNote = eventDatabase.GetEventOrNull(etag.Data1);
            if (reactedNote != null)
                NoteUpdated?.Invoke(this, reactedNote);
        }
    }

    public List<Event> GetGlobalFeed(int count)
    {
        return eventDatabase.ListNotes(count).ToList();
    }

    public List<Event> GetNotesFeed(NostrSubscriptionFilter[] filters, int count)
    {
        return eventDatabase.ListNotes(filters, count);
    }

    public List<Event> GetNotesThread(string eventId, int downLevels, out bool maxReached)
    {
        return eventDatabase.ListNoteTree(eventId, downLevels, out maxReached).ToList();
    }

    public List<NoteTree> GetTreesFromNotes(IEnumerable<Event> tws)
    {
        List<NoteTree> rootTrees = new();

        foreach (var tw in tws.ToList())
        {
            if (rootTrees.Exists(tw.Id)) continue;
            var root = rootTrees.Find(tw.ReplyToId);
            var newTree = new NoteTree(tw);
            newTree.Details = accountService.GetAccountDetails(tw.PublicKey);
            if (root != null)
            {
                root.Children.Add(newTree);
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
                subtree.Children.Add(rootTree);
            }
        }

        return rootTrees;
    }

    public async Task<bool> SendNoteWithPow(string content, Event replyTo, int diff, CancellationToken cancellationToken)
    {
        var unsignedNote = AssembleNote(content, replyTo);

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

    private NostrEvent AssembleNote(string content, Event replyTo)
    {
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
            Kind = 1,
            PublicKey = accountService.MainAccount.Id,
            Tags = new(),
            Content = unescapedContent,
        };

        if (replyTo != null)
        {
            // Use preferred method as per NIP-10 https://github.com/nostr-protocol/nips/blob/master/10.md
            var replyToId = replyTo.Id;
            var rootId = replyTo.ReplyToRootId;
            if (string.IsNullOrEmpty(rootId))
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
                    if (!string.IsNullOrEmpty(ev.ReplyToRootId))
                    {
                        rootId = ev.ReplyToRootId;
                        break;
                    }
                    if (string.IsNullOrEmpty(ev.ReplyToId))
                    {
                        break;
                    }
                    rootId = ev.ReplyToId;
                }
            }
            if (string.IsNullOrEmpty(rootId) || replyToId == rootId)
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
            foreach (var mention in replyTo.GetMentions().Where(m => m.Type == 'p').Select(m => m.MentionId).Union(new[] { replyTo.PublicKey }))
            {
                if (!ps.Contains(mention) && !prs.Contains(mention))
                    prs.Add(mention);
            }
        }

        // First p's (mentions) (indexed)
        foreach (var p in ps)
        {
            nostrEvent.Tags.Add(new NostrEventTag() { TagIdentifier = "p", Data = new[] { p }.ToList() });
        }

        // Then e's (mentions) (indexed)
        var relay = relayService.GetRecommendedRelayUri();
        foreach (var e in es)
        {
            nostrEvent.Tags.Add(new NostrEventTag() { TagIdentifier = "e", Data = new[] { e, relay, "mention" }.ToList() });
        }

        // Then e's (reply/root) (non-indexed)
        foreach (var er in ers)
        {
            nostrEvent.Tags.Add(new NostrEventTag() { TagIdentifier = "e", Data = new[] { er.Item1, relay, er.Item2 }.ToList() });
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
        nostrEvent.Tags.Add(new NostrEventTag() { TagIdentifier = "e", Data = new[] { reactTo.Id }.ToList() });
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
        mentionsFilter.limitFilterData.Since = accountService.MainAccount.LastNotificationRead;
        return eventDatabase.GetNotesCount(mentionsFilter.GetFilters());
    }

    public List<Reaction> GetReactions(string eventId)
    {
        return eventDatabase.ListReactions(eventId);
    }
}

