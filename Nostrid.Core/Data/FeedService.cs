using NNostr.Client;
using Nostrid.Data.Relays;
using Nostrid.Misc;
using Nostrid.Model;
using System.Web;

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
        Task.Run(() => HandleEventsAsync(eventDatabase.ListUnprocessedEvents()));
    }

    private async Task HandleEventsAsync(IEnumerable<Event> events)
    {
        foreach (var ev in events)
            _ = Task.Run(() => HandleEvent(ev));
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
            case NostrKind.Repost:
                HandleKind6(eventToProcess);
                break;
            case NostrKind.Reaction:
                HandleKind7(eventToProcess);
                break;
            default:
                eventToProcess.Processed = true;
                eventDatabase.SaveEvent(eventToProcess);
                break;
        }
    }

    private void ReceivedEvents(object sender, (string filterId, IEnumerable<Event> events) data)
    {
        Task.Run(() => HandleEventsAsync(data.events));

        var notes = data.events.Where(ev => (ev.Kind == NostrKind.Text || ev.Kind == NostrKind.Repost) && !eventDatabase.IsEventDeleted(ev.Id));
        if (notes.Any())
        {
            ReceivedNotes?.Invoke(this, (data.filterId, notes));
        }
    }

    public void HandleKind1(Event eventToProcess)
    {
        var noteMetadata = eventToProcess.NoteMetadata = new NoteMetadata();

        // NIP-10 https://github.com/nostr-protocol/nips/blob/master/10.md
        string replyToId, rootId, relayReplay, relayRoot;
        (replyToId, relayReplay) = eventToProcess.Tags
            .Where(t => t.TagIdentifier == "e" && t.Data.Count == 3 && t.Data[2] == "reply")
            .Select(t => (t.Data[0], t.Data[1]))
            .FirstOrDefault();
        (rootId, relayRoot) = eventToProcess.Tags
            .Where(t => t.TagIdentifier == "e" && t.Data.Count == 3 && t.Data[2] == "root")
            .Select(t => (t.Data[0], t.Data[1]))
            .FirstOrDefault();
        if (string.IsNullOrEmpty(replyToId))
        {
            var e = eventToProcess.Tags.Where(t => t.TagIdentifier == "e").ToList();
            switch (e.Count)
            {
                case 1:
                    var edata = e.Last().Data;
                    if (edata.Count > 0)
                    {
                        replyToId = edata[0];
                        if (edata.Count > 1) relayReplay ??= edata[1];
                    }
                    break;
                case > 1:
                    replyToId = e.Last().Data[0];
                    rootId ??= e.First().Data[0];
                    break;
            }
        }
        if (Utils.IsValidNostrId(replyToId))
        {
            noteMetadata.ReplyToId = replyToId;
        }
        if (Utils.IsValidNostrId(rootId))
        {
            noteMetadata.ReplyToRootId = rootId;
        }
        foreach (var relay in new[] { relayRoot, relayReplay })
        {
            if (!string.IsNullOrEmpty(relay))
            {
                HandleRelayRecommendation(relay);
            }
        }

        // Mentions
        // NIP-08: https://github.com/nostr-protocol/nips/blob/master/08.md
        for (int index = 0; index < eventToProcess.Tags.Count; index++)
        {
            var tag = eventToProcess.Tags[index];
            if (tag.Data.Count > 0)
            {
                switch (tag.TagIdentifier)
                {
                    case "p":
                        noteMetadata.AccountMentions[index] = tag.Data[0].ToLower(); break;
                    case "e":
                        noteMetadata.EventMentions[index] = tag.Data[0].ToLower(); break;
                    case "t":
                        var hashtag = tag.Data[0].ToLower();
                        if (!noteMetadata.HashTags.Contains(hashtag))
                            noteMetadata.HashTags.Add(tag.Data[0].ToLower());
                        break;
                }
            }
        }

        eventToProcess.Processed = true;
        eventDatabase.SaveEvent(eventToProcess);

        NoteUpdated?.Invoke(this, eventToProcess);
        if (!string.IsNullOrEmpty(replyToId))
        {
            NoteReceivedChild?.Invoke(this, (replyToId, eventToProcess));
        }
    }

    public void HandleKind2(Event eventToProcess)
    {
        HandleRelayRecommendation(eventToProcess.Content);
        eventToProcess.Processed = true;
        eventDatabase.SaveEvent(eventToProcess);
    }

    public void HandleKind5(Event eventToProcess)
    {
        var e = eventToProcess.Tags.Where(t => t.TagIdentifier == "e" && t.Data.Count > 0).ToList();
        foreach (var tag in e)
        {
            var eventId = tag.Data[0];
            if (Utils.IsValidNostrId(eventId))
            {
                var pubKey = eventDatabase.GetEventPubKey(eventId);
                if (string.IsNullOrEmpty(pubKey))
                    return; // If event doesn't exist then keep this event unprocessed and retry later

                if (pubKey == eventToProcess.PublicKey)
                {
                    // A deleted empty event will replace the original event
                    var ev = new Event()
                    {
                        Id = eventId,
                        PublicKey = eventToProcess.PublicKey,
                        Deleted = true,
                        Kind = -1,
                        Processed = true,
                    };
                    eventDatabase.SaveEvent(ev);
                }
            }
        }
        eventToProcess.Processed = true;
        eventDatabase.SaveEvent(eventToProcess);

        NoteUpdated?.Invoke(this, eventToProcess);
    }

    public void HandleKind6(Event eventToProcess)
    {
        // NIP-18: https://github.com/nostr-protocol/nips/blob/master/18.md
        if (string.IsNullOrEmpty(eventToProcess.Content)) // As per the NIP, content must be empty
        {
            var e = eventToProcess.Tags.FirstOrDefault(t => t.TagIdentifier == "e" && t.Data.Count > 0);
            var p = eventToProcess.Tags.FirstOrDefault(t => t.TagIdentifier == "p" && t.Data.Count > 0);

            if (e != null && p != null)
            {
                var noteMetadata = eventToProcess.NoteMetadata = new NoteMetadata();
                noteMetadata.EventMentions[0] = noteMetadata.RepostEventId = e.Data[0].ToLower();
                noteMetadata.AccountMentions[1] = p.Data[0].ToLower();
                if (e.Data.Count > 1 && !string.IsNullOrEmpty(e.Data[1]))
                {
                    HandleRelayRecommendation(e.Data[1]);
                }
            }
        }

        eventToProcess.Processed = true;
        eventDatabase.SaveEvent(eventToProcess);
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
        Event? eventUpdated = null;

        // NIP-25: https://github.com/nostr-protocol/nips/blob/master/25.md
        string? e = null;
        var etag = eventToProcess.Tags.Where(t => t.TagIdentifier == "e").LastOrDefault();
        if (etag != null)
        {
            e = etag.Data[0];
        }
        if (Utils.IsValidNostrId(e))
        {
            var reaction = eventToProcess.Content;
            // According to NIP-25 the content should be either +, - or an emoji, but some clients send an empty content, so let's disable this check
            //if (!string.IsNullOrEmpty(reaction) && reaction.Length == 1)
            {
                var reactedNote = eventDatabase.GetEventOrNull(e);
                if (reactedNote == null || !reactedNote.Processed)
                    return; // If event doesn't exist/not processed then keep this event unprocessed and retry later

                // Only save if note is not deleted and reaction has not been recorded already
                if (!reactedNote.Deleted && !reactedNote.NoteMetadata.Reactions.Any(r => r.ReactorId == eventToProcess.PublicKey))
                {
                    reactedNote.NoteMetadata.Reactions.Add(
                        new Reaction()
                        {
                            ReactorId = eventToProcess.PublicKey,
                            Content = reaction,
                        });
                    eventDatabase.SaveEvent(reactedNote);
                    eventUpdated = reactedNote;
                }
            }
        }
        eventToProcess.Processed = true;
        eventDatabase.SaveEvent(eventToProcess);

        if (eventUpdated != null)
            NoteUpdated?.Invoke(this, eventUpdated);
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
            var root = rootTrees.Find(tw.NoteMetadata.ReplyToId);
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
            var subtree = rootTrees.Find(rootTree.Note.NoteMetadata.ReplyToId);
            if (subtree != null)
            {
                rootTrees.Remove(rootTree);
                subtree.Children.Add(rootTree);
            }
        }

        return rootTrees;
    }

    public void SendNote(string content, Event replyTo, Account sender)
    {
        var unescapedContent = content.Trim();

        var ps = new List<string>();
        var ers = new List<(string, string)>();

        // Process account mentions
        foreach (var pubKey in Utils.GetAccountPubKeyMentions(unescapedContent))
        {
            var index = ps.IndexOf(pubKey);
            if (index == -1)
            {
                index = ps.Count;
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
                    index = ps.Count;
                    ps.Add(pubKey);
                }
                unescapedContent = unescapedContent.Replace($"@{bech32}", $"#[{index}]");
            }
        }

        var nostrEvent = new NostrEvent()
        {
            CreatedAt = DateTimeOffset.UtcNow,
            Kind = 1,
            PublicKey = sender.Id,
            Tags = new(),
            Content = unescapedContent,
        };


        if (replyTo != null)
        {
            // Use preferred method as per NIP-10 https://github.com/nostr-protocol/nips/blob/master/10.md
            var replyToId = replyTo.Id;
            var rootId = replyTo.NoteMetadata.ReplyToRootId;
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
                    if (!string.IsNullOrEmpty(ev.NoteMetadata.ReplyToRootId))
                    {
                        rootId = ev.NoteMetadata.ReplyToRootId;
                        break;
                    }
                    if (string.IsNullOrEmpty(ev.NoteMetadata.ReplyToId))
                    {
                        break;
                    }
                    rootId = ev.NoteMetadata.ReplyToId;
                }
            }
            if (string.IsNullOrEmpty(rootId))
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
            foreach (var mention in replyTo.NoteMetadata.AccountMentions.Values.Union(new[] { replyTo.PublicKey }))
            {
                if (!ps.Contains(mention))
                    ps.Add(mention);
            }
        }

        // First p's
        foreach (var p in ps)
        {
            nostrEvent.Tags.Add(new NostrEventTag() { TagIdentifier = "p", Data = new[] { p }.ToList() });
        }

        // Then e's
        var relay = relayService.GetRecommendedRelayUri();
        foreach (var er in ers)
        {
            nostrEvent.Tags.Add(new NostrEventTag() { TagIdentifier = "e", Data = new[] { er.Item1, relay, er.Item2 }.ToList() });
        }

        // Then t's
        foreach (var t in Utils.GetHashTags(content))
        {
            nostrEvent.Tags.Add(new NostrEventTag() { TagIdentifier = "t", Data = new[] { t }.ToList() });
        }

        sender.ComputeIdAndSign(nostrEvent);
        relayService.SendEvent(nostrEvent);
    }

    public void SendReaction(string reaction, Event reactTo, Account sender)
    {
        // NIP-25: https://github.com/nostr-protocol/nips/blob/master/25.md
        var nostrEvent = new NostrEvent()
        {
            Content = reaction,
            CreatedAt = DateTimeOffset.UtcNow,
            Kind = 7,
            PublicKey = sender.Id,
            Tags = new(),
        };
        nostrEvent.Tags.Add(new NostrEventTag() { TagIdentifier = "e", Data = new[] { reactTo.Id }.ToList() });
        nostrEvent.Tags.Add(new NostrEventTag() { TagIdentifier = "p", Data = new[] { eventDatabase.GetEventPubKey(reactTo.Id) }.ToList() });
        sender.ComputeIdAndSign(nostrEvent);
        relayService.SendEvent(nostrEvent);
    }

    public void DeleteNote(Event eventToDelete, Account sender, string reason = null)
    {
        // NIP-09: https://github.com/nostr-protocol/nips/blob/master/09.md
        var nostrEvent = new NostrEvent()
        {
            Content = reason ?? string.Empty,
            CreatedAt = DateTimeOffset.UtcNow,
            Kind = 5,
            PublicKey = sender.Id,
            Tags = new(),
        };
        if (eventToDelete.PublicKey != sender.Id)
        {
            throw new Exception("Can only delete own events");
        }
        nostrEvent.Tags.Add(new NostrEventTag() { TagIdentifier = "e", Data = new[] { eventToDelete.Id }.ToList() });

        sender.ComputeIdAndSign(nostrEvent);
        relayService.SendEvent(nostrEvent);

        eventToDelete.Deleted = true;
        eventDatabase.SaveEvent(eventToDelete);
    }

    public void Repost(Event repost, Account sender)
    {
        // NIP-18: https://github.com/nostr-protocol/nips/blob/master/18.md
        var nostrEvent = new NostrEvent()
        {
            Content = string.Empty,
            CreatedAt = DateTimeOffset.UtcNow,
            Kind = NostrKind.Repost,
            PublicKey = sender.Id,
            Tags = new(),
        };
        nostrEvent.Tags.Add(new NostrEventTag() { TagIdentifier = "e", Data = new[] { repost.Id, relayService.GetRecommendedRelayUri() }.ToList() });
        nostrEvent.Tags.Add(new NostrEventTag() { TagIdentifier = "p", Data = new[] { repost.PublicKey }.ToList() });
        sender.ComputeIdAndSign(nostrEvent);
        relayService.SendEvent(nostrEvent);
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
}

