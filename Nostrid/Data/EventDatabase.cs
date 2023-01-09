using Nostrid.Model;
using LinqKit;
using LiteDB;
using NNostr.Client;

namespace Nostrid.Data
{
    public class EventDatabase
    {
        private readonly LiteDatabase Database;
        private readonly ILiteCollection<Relay> Relays;
        private readonly ILiteCollection<Event> Events;
        private readonly ILiteCollection<Account> Accounts;
        private readonly ILiteCollection<FilterData> FilterDatas;
        private readonly ILiteCollection<OwnEvent> OwnEvents;
        private readonly ILiteCollection<FeedSource> FeedSources;

        public int EventsPending => Events.Query().Where(e => !e.Processed).Count();

        public EventDatabase()
        {
            Database = new LiteDatabase(DbConstants.DatabasePath);
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
        }

        public void DeleteAccount(Account account)
        {
            Accounts.Delete(account.Id);
        }

        public List<string> GetAccountIdsWithPk()
        {
            return Accounts.Query().Where(a => !string.IsNullOrEmpty(a.PrivKey)).Select(a => a.Id).ToList();
        }

        public Event GetEvent(string id)
        {
            return Events.FindById(id) ?? new Event() { Id = id };
        }

        public bool IsEventDeleted(string id)
        {
            return Events.Exists(e => e.Id == id && e.Deleted);
        }

        public void SaveEvent(Event ev)
        {
            Events.Update(ev);
        }

        public Event SaveNostrEvent(NostrEvent ev, Relay relay)
        {
            var now = DateTimeOffset.UtcNow;
            Event eventData = null;

            RunInTransaction(() =>
            {
                eventData = Events.FindById(ev.Id);
                if (eventData != null)
                {
                    if (eventData.Deleted && eventData.PublicKey == ev.PublicKey) // If recorded event is deleted and owners match, ignore, otherwise overwrite
                    {
                        return true;
                    }
                    else if (eventData.Processed && (
                        eventData.Kind == 7 ||
                        eventData.Kind == 5)) // If we receive a reaction or a delete let's process it again, just in case we missed it the last time
                    {
                        eventData.Processed = false;
                        Events.Update(eventData);

                        return true;
                    }
                }

                eventData = new Event(ev)
                {
                    CreatedAtCurated = !ev.CreatedAt.HasValue || ev.CreatedAt > now ? now : ev.CreatedAt.Value
                };
                Events.Upsert(eventData);

                return true;
            });

            return eventData == null ? null : (!eventData.Processed ? eventData : null);
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
                notes.AddRange(ApplyFilter(Events.Query().Where(e => e.Kind == 1).OrderByDescending(n => n.CreatedAtCurated), filter).Limit(count).ToList());
            }
            return notes;
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

        private static readonly object txLock = new();
        public void RunInTransaction(Func<bool> action)
        {
            lock (txLock)
            {
                try
                {
                    if (!Database.BeginTrans()) throw new Exception("BeginTrans failed!");

                    if (action())
                    {
                        if (!Database.Commit()) throw new Exception("Commit failed!");
                    }
                    else
                    {
                        if (!Database.Rollback()) throw new Exception("Rollback failed!");
                    }
                    Database.Checkpoint();
                }
                catch (Exception ex)
                {
                    if (!Database.Rollback()) throw new Exception("Rollback2 failed!");
                    throw;
                }
            }
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
        }

        public void DeleteFeedSource(long id)
        {
            FeedSources.Delete(id);
        }

        public List<FeedSource> ListFeedSources(string ownerId)
        {
            return FeedSources.Query().Where(f => f.OwnerId == ownerId).ToList();
        }
    }
}
