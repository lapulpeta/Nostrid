using LinqKit;
using Microsoft.EntityFrameworkCore;
using NNostr.Client;
using Nostrid.Data.Relays;
using Nostrid.Model;
using Nostrid.Pages;
using SQLite;

namespace Nostrid.Data
{
    public class EventDatabase
    {
        private string _dbfile;

        public event EventHandler? DatabaseHasChanged;


        public void InitDatabase(string dbfile)
        {
            _dbfile = dbfile;
            using var db = new Context(dbfile);
            db.Database.Migrate();
        }

        public void SaveRelay(Relay relay)
        {
            using var db = new Context(_dbfile);
            db.Add(relay);
            db.SaveChanges();
        }

        public void DeleteRelay(long relayId)
        {
            using var db = new Context(_dbfile);
            db.Relays.Where(r => r.Id == relayId).ExecuteDelete();
        }

        public List<Relay> ListRelays()
        {
            using var db = new Context(_dbfile);
            return db.Relays.ToList();
        }

        public int GetRelayCount()
        {
            using var db = new Context(_dbfile);
            return db.Relays.Count();
        }

        public bool RelayExists(string uri)
        {
            using var db = new Context(_dbfile);
            return db.Relays.Any(r => r.Uri == uri);
        }

        public Account GetAccount(string id)
        {
            using var db = new Context(_dbfile);
            var account = db.Accounts
                .Include(a => a.Details)
                .FirstOrDefault(a => a.Id == id)
                ?? new Account() { Id = id };
            return account;
        }

        public AccountDetails GetAccountDetails(string accountId)
        {
            using var db = new Context(_dbfile);
            return db.AccountDetails.FirstOrDefault(ad => ad.Account.Id == accountId) ?? new AccountDetails() { Id = accountId };
        }

        public void SetAccountLastRead(string accountId, DateTime lastRead)
        {
            using var db = new Context(_dbfile);
            db.Accounts.Where(a => a.Id == accountId).ExecuteUpdate(a => a.SetProperty(a => a.LastNotificationRead, lastRead));
        }

        public void SaveAccount(Account account)
        {
            using var db = new Context(_dbfile);
            if (db.Accounts.Any(a => a.Id == account.Id))
            {
                db.Update(account);
            }
            else
            {
                db.Add(account);
            }
            db.SaveChanges();
            if (!string.IsNullOrEmpty(account.PrivKey))
            {
                DatabaseHasChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void SaveAccountDetails(AccountDetails accountDetails)
        {
            using var db = new Context(_dbfile);
            if (db.AccountDetails.Any(ad => ad.Id == accountDetails.Id))
            {
                db.Update(accountDetails);
            }
            else
            {
                if (!db.Accounts.Any(a => a.Id == accountDetails.Id))
                {
                    db.Add(new Account() { Id = accountDetails.Id });
                }
                db.Add(accountDetails);
            }
            db.SaveChanges();
        }

        public void MarkEventAsDeleted(string id, string ownerId)
        {
            using var db = new Context(_dbfile);
            db.Events
                .Where(e => e.Id == id && e.PublicKey == ownerId)
                .ExecuteUpdate(e => e
                    .SetProperty(e => e.Deleted, true)
                    .SetProperty(e => e.Broadcast, false)
                    .SetProperty(e => e.CanEcho, false)
                    .SetProperty(e => e.Content, string.Empty));
        }

        public void DeleteAccount(Account account)
        {
            using var db = new Context(_dbfile);

            db.Events.Where(e => e.PublicKey == account.Id).ExecuteDelete();
            db.AccountDetails.Where(a => a.Id == account.Id).ExecuteDelete();
            db.FeedSources.Where(f => f.OwnerId == account.Id).ExecuteDelete();
            db.Accounts.Where(a => a.Id == account.Id).ExecuteDelete();

            DatabaseHasChanged?.Invoke(this, EventArgs.Empty);
        }

        public List<string> GetAccountIdsWithPk()
        {
            using var db = new Context(_dbfile);
            return db.Accounts.Where(a => !string.IsNullOrEmpty(a.PrivKey)).Select(a => a.Id).ToList();
        }

        public Event GetEvent(string id)
        {
            return GetEventOrNull(id)
                ?? new Event() { Id = id };
        }

        public Event? GetEventOrNull(string id)
        {
            using var db = new Context(_dbfile);
            return db.Events
                .Include(e => e.Tags)
                .FirstOrDefault(e => e.Id == id);
        }

        public bool IsEventDeleted(string id)
        {
            using var db = new Context(_dbfile);
            return db.Events.Any(e => e.Id == id && e.Deleted);
        }

        public void SaveSeen(string eventId, long relayId, Context db)
        {
            if (db.EventSeen.Any(es => es.EventId == eventId && es.RelayId == relayId))
                return;

            db.Add(new EventSeen() { EventId = eventId, RelayId = relayId });
            try
            {
                db.SaveChanges();
            }
            catch (DbUpdateException ex)
            {
            }
        }

        public bool SaveNewEvent(Event ev, Relay relay)
        {
            using var db = new Context(_dbfile);

            try
            {
                if (db.Events.Any(e => e.Id == ev.Id && !e.CanEcho))
                    return false;

                if (db.Events.Any(e => e.Id == ev.Id && e.CanEcho))
                {
                    db.Events.Where(e => e.Id == ev.Id && e.CanEcho).ExecuteUpdate(e => e.SetProperty(e => e.CanEcho, false));
                    ev.CreatedAtCurated = ((DateTimeOffset)ev.CreatedAt.Value).ToUnixTimeSeconds();
                    return true;
                }

                var now = DateTimeOffset.UtcNow;
                ev.CreatedAtCurated = (!ev.CreatedAt.HasValue || ev.CreatedAt > now ? now : ev.CreatedAt.Value).ToUnixTimeSeconds();
                db.Add(ev);
                db.SaveChanges();
                return true;
            }
            catch (DbUpdateException ex)
            {
                return false;

            }
            finally
            {
                SaveSeen(ev.Id, relay.Id, db);
            }
        }

        public List<Event> ListNoteTree(string rootEventId, int downlevels, out bool maxReached)
        {
            using var db = new Context(_dbfile);
            var ret = ListAncestorNotesUntilRoot(rootEventId, db);
            ret.AddRange(ListChildrenNotes(rootEventId, downlevels, out maxReached, db));
            return ret;
        }

        public List<Event> ListNotes(int count)
        {
            using var db = new Context(_dbfile);
            return db.Events.Where(e => e.Kind == 1).OrderByDescending(e => e.CreatedAtCurated).Take(count).ToList();
        }

        public List<Event> ListNotes(NostrSubscriptionFilter[] filters, int count)
        {
            using var db = new Context(_dbfile);
            List<Event> notes = new();
            foreach (var filter in filters)
            {
                notes.AddRange(ApplyFilter(db, db.Events.OrderByDescending(n => n.CreatedAtCurated), filter).Take(count).Include(e => e.Tags).ToList());
            }
            return notes;
        }

        public int GetNotesCount(NostrSubscriptionFilter[] filters)
        {
            using var db = new Context(_dbfile);
            int count = 0;
            foreach (var filter in filters)
            {
                count += ApplyFilter(db, db.Events.Where(e => e.Kind == 1)/*.OrderByDescending(n => n.CreatedAtCurated)*/, filter).Count();
            }
            return count;
        }

        private IQueryable<Event> ApplyFilter(Context db, IQueryable<Event> notes, NostrSubscriptionFilter filter)
        {
            if (filter.PublicKey != null)
            {
                var tags = db.TagDatas.Where(filter.PublicKey.Aggregate(PredicateBuilder.New<TagData>(),
                    (current, temp) => current.Or(t => t.Data0 == "p" && t.Data1 == temp))).Select(t => t.Event.Id);
                notes = notes.Where(n => tags.Contains(n.Id));
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
                var tags = db.TagDatas.Where(filter.EventId.Aggregate(PredicateBuilder.New<TagData>(),
                    (current, temp) => current.Or(t => t.Data0 == "e" && t.Data1 == temp))).Select(t => t.Event.Id);
                notes = notes.Where(n => tags.Contains(n.Id));
            }
            if (filter.ExtensionData != null)
            {
                var filterTags = filter.GetAdditionalTagFilters()["t"].Select(t => t.ToLower()).ToList();
                var tags = db.TagDatas.Where(filterTags.Aggregate(PredicateBuilder.New<TagData>(),
                    (current, temp) => current.Or(t => t.Data0 == "t" && t.Data1 == temp))).Select(t => t.Event.Id);
                notes = notes.Where(n => tags.Contains(n.Id));
            }
            if (filter.Since.HasValue)
            {
                var since = filter.Since.Value.ToUnixTimeSeconds();
                notes = notes.Where(n => n.CreatedAtCurated > since);
            }
            if (filter.Until.HasValue)
            {
                var until = filter.Until.Value.ToUnixTimeSeconds();
                notes = notes.Where(n => n.CreatedAtCurated < until);
            }
            if (filter.Kinds != null)
            {
                notes = notes.Where(e => filter.Kinds.Contains(e.Kind));
            }
            return notes.Where(e => !e.Deleted);
        }

        private List<Event> ListAncestorNotesUntilRoot(string rootEventId, Context db) // That this tweet replies to
        {
            var ret = new List<Event>();
            while (rootEventId != null)
            {
                var tw = db.Events.Include(e => e.Tags).Where(tw => tw.Id == rootEventId).FirstOrDefault();
                if (tw == null) break;
                ret.Add(tw);
                rootEventId = tw.ReplyToId;
            }
            return ret;
        }

        private List<Event> ListChildrenNotes(string rootEventId, int levels, out bool maxReached, Context db) // That reply to this tweet
        {
            var ret = new List<Event>();

            maxReached = true;
            if (levels <= 0) return ret;
            var children = db.Events
                .Include(e => e.Tags)
                .Where(e => e.Kind == NostrKind.Text && e.Tags.Any(t => t.Data0 == "e" && t.Data1 == rootEventId))
                .ToList()
                .Where(e => e.ReplyToId == rootEventId) // TODO: move this to DB query
                .ToList();
            ret.AddRange(children);
            if (levels > 1)
            {
                maxReached = false;
                var childrenLevels = children.Count > 1 ? levels - 1 : levels; // Single replies are at level of parent
                foreach (var tww in children)
                {
                    ret.AddRange(ListChildrenNotes(tww.Id, childrenLevels, out maxReached, db));
                }
            }
            return ret;
        }

        public string? GetAccountName(string accountId)
        {
            using var db = new Context(_dbfile);
            return db.Accounts.Where(a => a.Id == accountId).Select(a => a.Details.Name).FirstOrDefault();
        }

        public List<Event> ListOwnEvents(long relayId)
        {
            using var db = new Context(_dbfile);
            return db.Events
                .Include(e => e.Tags)
                .Where(e => e.Broadcast && !db.EventSeen.Any(es => es.EventId == e.Id && es.RelayId == relayId))
                .ToList();
        }

        public void SaveOwnEvents(NostrEvent nostrEvent)
        {
            using var db = new Context(_dbfile);

            var ev = EventExtension.FromNostrEvent(nostrEvent);
            ev.Broadcast = true;
            ev.CanEcho = true;
            ev.CreatedAtCurated = nostrEvent.CreatedAt.Value.ToUnixTimeSeconds();

            db.Add(ev);
            db.SaveChanges();
            DatabaseHasChanged?.Invoke(this, EventArgs.Empty);
        }

        public void AddSeenBy(string ownEventId, long relayId)
        {
            using var db = new Context(_dbfile);

            SaveSeen(ownEventId, relayId, db);
        }

        public FeedSource? GetFeedSource(long id)
        {
            using var db = new Context(_dbfile);

            var feedSource = db.FeedSources.AsNoTracking().FirstOrDefault(f => f.Id == id);
            return feedSource;
        }

        public void SaveFeedSource(FeedSource feedSource)
        {
            using var db = new Context(_dbfile);

            if (db.FeedSources.Any(f => f.Id == feedSource.Id))
            {
                db.Update(feedSource);
            }
            else
            {
                db.Add(feedSource);
            }
            db.SaveChanges();
            DatabaseHasChanged?.Invoke(this, EventArgs.Empty);
        }

        public void DeleteFeedSource(long id)
        {
            using var db = new Context(_dbfile);
            db.FeedSources.Where(f => f.Id == id).ExecuteDelete();
            DatabaseHasChanged?.Invoke(this, EventArgs.Empty);
        }

        public List<FeedSource> ListFeedSources(string ownerId)
        {
            using var db = new Context(_dbfile);

            var feedSources = db.FeedSources
                .Where(f => f.OwnerId == ownerId)
                .AsNoTracking()
                .ToList();
            return feedSources;
        }

        public Config GetConfig()
        {
            using var db = new Context(_dbfile);

            return db.Configs.FirstOrDefault() ?? new Config();
        }

        public void SaveConfig(Config config)
        {
            using var db = new Context(_dbfile);

            db.Update(config);
            DatabaseHasChanged?.Invoke(this, EventArgs.Empty);
        }

        public List<ReactionGroup> ListReactionGroups(string eventId)
        {
            using var db = new Context(_dbfile);

            var reactions = db.TagDatas
                .Where(d => d.Data0 == "e" && d.Data1 == eventId && d.Event.Kind == NostrKind.Reaction)
                .GroupBy(d => d.Event.Content)
                .Select(g => new ReactionGroup()
                {
                    Reaction = g.Key,
                    Count = g.Count(),
                });
            return reactions.ToList();
        }
        
        public bool AccountReacted(string eventId, string accountId)
        {
            using var db = new Context(_dbfile);

            return db.TagDatas.Any(d => d.Data0 == "e" && d.Data1 == eventId && d.Event.Kind == NostrKind.Reaction &&
                d.Event.PublicKey == accountId);
        }

        public void SetNip05Validity(string id, bool valid)
        {
            using var db = new Context(_dbfile);
            db.AccountDetails.Where(ad => ad.Id == id).ExecuteUpdate(ad => ad.SetProperty(ad => ad.Nip05Valid, valid));
        }
    }
}
