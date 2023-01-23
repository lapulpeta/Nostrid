using Nostrid.Model;
using LinqKit;
using LiteDB;
using NNostr.Client;

namespace Nostrid.Data
{
    public class EventDatabase
    {
        private LiteDatabase Database;
        private ILiteCollection<Relay> Relays;
        private ILiteCollection<Event> Events;
        private ILiteCollection<Account> Accounts;
        private ILiteCollection<FilterData> FilterDatas;
        private ILiteCollection<OwnEvent> OwnEvents;
        private ILiteCollection<FeedSource> FeedSources;
        private ILiteCollection<Config> Configs;

        public int EventsPending => Events.Query().Where(e => !e.Processed).Count();

        public event EventHandler? DatabaseHasChanged;

        public void InitDatabase(Stream storage)
        {
            InitDatabase(new LiteDatabase(storage));
        }

        public void InitDatabase(string filename)
        {
            InitDatabase(new LiteDatabase(filename));
        }

        private void InitDatabase(LiteDatabase database)
        {
            Database = database;
            Relays = Database.GetCollection<Relay>();
            Events = Database.GetCollection<Event>();
            Events.EnsureIndex(e => e.Processed);
            Events.EnsureIndex(e => e.CreatedAtCurated);
            Events.EnsureIndex(e => e.Kind);
            Events.EnsureIndex(e => e.NoteMetadata.ReplyToId);
            Events.EnsureIndex(e => e.NoteMetadata.HashTags);
            Events.EnsureIndex(e => e.PublicKey);
            Accounts = Database.GetCollection<Account>();
            FilterDatas = Database.GetCollection<FilterData>();
            OwnEvents = Database.GetCollection<OwnEvent>();
            OwnEvents.EnsureIndex(e => e.SeenByRelay);
            OwnEvents.EnsureIndex(e => e.Event.CreatedAt);
            FeedSources = Database.GetCollection<FeedSource>();
            FeedSources.EnsureIndex(e => e.OwnerId);
            Configs = Database.GetCollection<Config>();
        }

        public void SaveRelay(Relay relay)
        {
            Relays.Upsert(relay);
        }

        public void DeleteRelay(Relay relay)
        {
            Relays.Delete(relay.Id);
        }

        public List<Relay> ListRelays()
        {
            return Relays.Query().ToList();
        }

        public int GetRelayCount()
        {
            return Relays.Query().Count();
        }

        public bool RelayExists(string uri)
        {
            return Relays.Query().Where(r => r.Uri == uri).Count() > 0;
        }

        public Account GetAccount(string id)
        {
            return Accounts.FindById(id) ?? new Account() { Id = id };
        }

        public void SaveAccount(Account account)
        {
            Accounts.Upsert(account);
            if (!string.IsNullOrEmpty(account.PrivKey))
            {
                DatabaseHasChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void DeleteAccount(Account account)
        {
            Accounts.Delete(account.Id);
            Events.DeleteMany(e => e.PublicKey == account.Id);
            OwnEvents.DeleteMany(e => e.Event.PublicKey == account.Id);
            DatabaseHasChanged?.Invoke(this, EventArgs.Empty);
        }

        public List<string> GetAccountIdsWithPk()
        {
            return Accounts.Query().Where(a => !string.IsNullOrEmpty(a.PrivKey)).Select(a => a.Id).ToList();
        }

        public Event GetEvent(string id)
        {
            return Events.FindById(id) ?? new Event() { Id = id };
        }

        public Event GetEventOrNull(string id)
        {
            return Events.FindById(id);
        }

        public bool IsEventDeleted(string id)
        {
            return Events.Exists(e => e.Id == id && e.Deleted);
        }

        public void SaveEvent(Event ev)
        {
            Events.Update(ev);
        }

        public Event? SaveNewEvent(Event ev, Relay relay)
        {
            var existingEvent = Events.FindById(ev.Id);
            if (existingEvent != null)
            {
                if (!existingEvent.Deleted || existingEvent.PublicKey == ev.PublicKey)
                {
                    return !existingEvent.Processed ? existingEvent : null;
                }
                // This point can be reached only if event is marked as deleted but the recorded owner is not the real one,
                // so we have to processed it again
            }

            var now = DateTimeOffset.UtcNow;
            ev.CreatedAtCurated = !ev.CreatedAt.HasValue || ev.CreatedAt > now ? now : ev.CreatedAt.Value;
            Events.Upsert(ev);

            return !ev.Processed ? ev : null;
        }

        public List<Event> ListUnprocessedEvents(int? count = null)
        {
            var ret = Events.Query().Where(e => !e.Processed);
            return (count.HasValue ? ret.Limit(count.Value) : ret).ToList();
        }

        public List<Event> ListNoteTree(string rootEventId, int downlevels, out bool maxReached)
        {
            var ret = ListAncestorNotesUntilRoot(rootEventId);
            ret.AddRange(ListChildrenNotes(rootEventId, downlevels, out maxReached));
            return ret;
        }

        public List<Event> ListNotes(int count)
        {
            return Events.Query().Where(e => e.Kind == 1).OrderByDescending(e => e.CreatedAtCurated).Limit(count).ToList();
        }

        public List<Event> ListNotes(NostrSubscriptionFilter[] filters, int count)
        {
            List<Event> notes = new();
            foreach (var filter in filters)
            {
                notes.AddRange(ApplyFilter(Events.Query().OrderByDescending(n => n.CreatedAtCurated), filter).Limit(count).ToList());
            }
            return notes;
        }

        public int GetNotesCount(NostrSubscriptionFilter[] filters)
        {
            int count = 0;
            foreach (var filter in filters)
            {
                count += ApplyFilter(Events.Query().Where(e => e.Kind == 1).OrderByDescending(n => n.CreatedAtCurated), filter).Count();
            }
            return count;
        }

        private ILiteQueryable<Event> ApplyFilter(ILiteQueryable<Event> notes, NostrSubscriptionFilter filter)
        {
            if (filter.PublicKey != null)
            {
                notes = notes.Where(filter.PublicKey.Aggregate(PredicateBuilder.New<Event>(),
                        (current, temp) => current.Or(n => n.Tags.Where(t => t.TagIdentifier == "p" && t.Data[0] == temp).Any())));
            }
            if (filter.Authors != null)
            {
                notes = notes.Where(e => filter.Authors.Contains(e.PublicKey));
            }
            if (filter.Ids != null)
            {
                notes = notes.Where(e => filter.Ids.Contains(e.Id));
            }
            if (filter.EventId != null)
            {
                notes = notes.Where(e => filter.EventId.Contains(e.NoteMetadata.ReplyToId));
            }
            if (filter.ExtensionData != null)
            {
                var filterTags = filter.GetAdditionalTagFilters()["t"].Select(t => t.ToLower()).ToList();
                notes = notes.Where(filterTags.Aggregate(PredicateBuilder.New<Event>(),
                    (current, temp) => current.Or(n => n.NoteMetadata.HashTags.Contains(temp))));
            }
            if (filter.Since.HasValue)
            {
                var since = filter.Since.Value;
                notes = notes.Where(n => n.CreatedAtCurated > since);
            }
            if (filter.Until.HasValue)
            {
                var until = filter.Until.Value;
                notes = notes.Where(n => n.CreatedAtCurated < until);
            }
            if (filter.Kinds != null)
            {
                notes = notes.Where(e => filter.Kinds.Contains(e.Kind));
            }
            return notes.Where(e => !e.Deleted);
        }

        private List<Event> ListAncestorNotesUntilRoot(string rootEventId) // That this tweet replies to
        {
            var ret = new List<Event>();
            while (rootEventId != null)
            {
                var tw = Events.Query().Where(tw => tw.Id == rootEventId).FirstOrDefault();
                if (tw == null) break;
                ret.Add(tw);
                rootEventId = tw.NoteMetadata.ReplyToId;
            }
            return ret;
        }

        public string GetEventPubKey(string eventId)
        {
            return Events.FindById(eventId)?.PublicKey;
        }

        public List<Event> ListChildrenNotes(string rootEventId, int levels, out bool maxReached) // That reply to this tweet
        {
            var ret = new List<Event>();

            maxReached = true;
            if (levels <= 0) return ret;
            var children = Events.Query().Where(tw => tw.NoteMetadata.ReplyToId == rootEventId).ToList();
            ret.AddRange(children);
            if (levels > 1)
            {
                maxReached = false;
                var childrenLevels = children.Count > 1 ? levels - 1 : levels; // Single replies are at level of parent
                foreach (var tww in children)
                {
                    ret.AddRange(ListChildrenNotes(tww.Id, childrenLevels, out maxReached));
                }
            }
            return ret;
        }

        public string GetAccountName(string accountId)
        {
            return Accounts.Query().Where(a => a.Id == accountId).Select(a => a.Details.Name).FirstOrDefault();
        }

        public void UpdateFilterData(string paramsId, long relayId, DateTimeOffset oldest)
        {
            FilterDatas.Upsert(new FilterData()
            {
                Id = $"{paramsId}-{relayId}",
                OldestEvent = oldest,
            });
        }

        public DateTimeOffset? GetFilterData(string paramsId, long relayId)
        {
            var id = $"{paramsId}-{relayId}";
            return FilterDatas.FindById(id)?.OldestEvent ?? null;
        }

        public List<OwnEvent> ListOwnEvents(long relayId)
        {
            return OwnEvents.Query().Where(e => !e.SeenByRelay.Contains(relayId)).OrderBy(e => e.Event.CreatedAt).ToList();
        }

        public void SaveOwnEvents(OwnEvent nostrEvent)
        {
            OwnEvents.Upsert(nostrEvent);
            DatabaseHasChanged?.Invoke(this, EventArgs.Empty);
        }

        public void AddSeenBy(string ownEventId, long relayId)
        {
            var ev = OwnEvents.FindById(ownEventId);
            if (!ev.SeenByRelay.Contains(relayId))
            {
                ev.SeenByRelay.Add(relayId);
                OwnEvents.Update(ev);
            }
        }

        public FeedSource GetFeedSource(long id)
        {
            return FeedSources.FindById(id);
        }

        public void SaveFeedSource(FeedSource feedSource)
        {
            FeedSources.Upsert(feedSource);
            DatabaseHasChanged?.Invoke(this, EventArgs.Empty);
        }

        public void DeleteFeedSource(long id)
        {
            FeedSources.Delete(id);
            DatabaseHasChanged?.Invoke(this, EventArgs.Empty);
        }

        public List<FeedSource> ListFeedSources(string ownerId)
        {
            return FeedSources.Query().Where(f => f.OwnerId == ownerId).ToList();
        }

        public Config GetConfig()
        {
            return Configs.Query().FirstOrDefault() ?? new Config();
        }

        public void SaveConfig(Config config)
        {
            Configs.Upsert(config);
            DatabaseHasChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
