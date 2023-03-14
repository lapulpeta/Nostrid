﻿using LinqKit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NNostr.Client;
using Nostrid.Data.Relays;
using Nostrid.Model;

namespace Nostrid.Data
{
	public class EventDatabase
	{
		private string _dbfile;
		private bool _optimizing;

		public event EventHandler? DatabaseHasChanged;
		public event EventHandler? OptimizationComplete;

		public Context CreateContext()
		{
			return new Context(_dbfile);
		}

		public void InitDatabase(string dbfile)
		{
			_dbfile = dbfile;
			using var db = new Context(dbfile);
			var path = Path.GetDirectoryName(dbfile);
			if (path.IsNotNullOrEmpty() && !Directory.Exists(path))
			{
				Directory.CreateDirectory(path);
			}
			if (db.Database.SqlQuery<bool>($"SELECT 1 AS [Value] FROM sqlite_master WHERE type='table' AND name='__EFMigrationsHistory'").FirstOrDefault())
			{
				db.Database.ExecuteSql($"UPDATE __EFMigrationsHistory SET MigrationId='20230209075431_ProxyConfig' WHERE MigrationId='20230208180012_RelayFilters'");
			}
			db.Database.Migrate();
		}

		public void Optimize()
		{
			_optimizing = true;
			Task.Run(async () =>
			{
				using var db = new Context(_dbfile);
				await db.Database.ExecuteSqlAsync($"VACUUM");
				await db.Database.ExecuteSqlAsync($"ANALYZE");
			})
			.ContinueWith((_) =>
			{
				_optimizing = false;
				OptimizationComplete?.Invoke(this, EventArgs.Empty);
			});
		}

		public void Clean()
		{
			_optimizing = true;
			Task.Run(async () =>
			{
				using var db = new Context(_dbfile);
				await db.AccountDetails.ExecuteDeleteAsync();
				await db.Follows.ExecuteDeleteAsync();
				await db.Mutes.ExecuteDeleteAsync();
				await db.Accounts.Where(a => a.PrivKey == null).ExecuteDeleteAsync();
				await db.Accounts.Where(a => a.PrivKey != null).ExecuteUpdateAsync(a => a.SetProperty(a => a.FollowsLastUpdate, (DateTime?)null));
				await db.Accounts.Where(a => a.PrivKey != null).ExecuteUpdateAsync(a => a.SetProperty(a => a.MutesLastUpdate, (DateTime?)null));
				await db.EventSeen.ExecuteDeleteAsync();
				await db.TagDatas.ExecuteDeleteAsync();
				await db.Events.ExecuteDeleteAsync();
				await db.ChannelDetails.ExecuteDeleteAsync();
				await db.Channels.ExecuteDeleteAsync();
				await db.Database.ExecuteSqlAsync($"VACUUM");
				await db.Database.ExecuteSqlAsync($"ANALYZE");
				DatabaseHasChanged?.Invoke(this, EventArgs.Empty);
			})
			.ContinueWith((_) =>
			{
				_optimizing = false;
				OptimizationComplete?.Invoke(this, EventArgs.Empty);
			});
		}

		public bool IsOptimizing => _optimizing;

		public bool SaveRelay(Relay relay)
		{
			using var db = new Context(_dbfile);
			if (db.Relays.Any(r => r.Id == relay.Id))
			{
				db.Update(relay);
			}
			else
			{
				if (db.Relays.Any(r => r.Uri == relay.Uri))
					return false;
				db.Add(relay);
			}
			db.SaveChanges();
			return true;
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

		public void ClearRelays()
		{
			using var db = new Context(_dbfile);
			db.Relays.ExecuteDelete();
			db.EventSeen.ExecuteDelete();
		}

		public int GetRelayCount()
		{
			using var db = new Context(_dbfile);
			return db.Relays.Count();
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
				else if (accountDetails.Account != null)
				{
					db.Attach(accountDetails.Account);
				}
				else
				{
					db.Attach(new Account() { Id = accountDetails.Id });
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

			db.TagDatas.Where(t => t.Event.PublicKey == account.Id).ExecuteDelete();
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
			if (id.IsReplaceableId())
			{
                return db.Events
                    .Include(e => e.Tags)
                    .FirstOrDefault(e => e.ReplaceableId == id);
            }
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
			db.EventSeen
				.Upsert(new EventSeen() { EventId = eventId, RelayId = relayId })
				.On(es => new { es.EventId, es.RelayId })
				.NoUpdate()
				.Run();
		}

		public bool SaveNewEvent(Event ev, Relay? relay = null)
		{
			IDbContextTransaction? tx = null;

			using var db = new Context(_dbfile);

			try
			{
				if (db.Events.Any(e => e.Id == ev.Id && !e.CanEcho))
					return false;

				if (db.Events.Any(e => e.Id == ev.Id && e.CanEcho))
				{
					db.Events.Where(e => e.Id == ev.Id && e.CanEcho).ExecuteUpdate(e => e.SetProperty(e => e.CanEcho, false));
					return true;
				}

				if (ev.KindClass != NostrKindClass.Other)
				{
					// Deletion and insertion should be atomic
					tx = db.Database.BeginTransaction();

					// All older events are deleted. If there are any newer then the UNIQUE constraint will
					// cause the insert to fail
					db.Events
						.Where(e => e.CreatedAt < ev.CreatedAt && e.ReplaceableId == ev.ReplaceableId)
						.ExecuteDelete();
				}

				db.Add(ev);
				db.SaveChanges();

				tx?.Commit();

				return true;
			}
			catch (DbUpdateException ex)
			{
				return false;
			}
			finally
			{
				if (relay != null)
				{
					SaveSeen(ev.Id, relay.Id, db);
				}
			}
		}

		public List<Event> ListNotes(int count)
		{
			using var db = new Context(_dbfile);
			return db.Events.Where(e => e.Kind == 1).OrderByDescending(e => e.CreatedAtCurated).Take(count).ToList();
		}

		public List<Event> ListNotes(NostrSubscriptionFilter[] filters, int[]? kinds, int count)
		{
			using var db = new Context(_dbfile);
			List<Event> notes = new();
			foreach (var filter in filters)
			{
				var query = ApplyFilter(db.Events.OrderByDescending(n => n.CreatedAtCurated), filter);
				if (kinds != null)
				{
					query = query.Where(n => kinds.Contains(n.Kind));
				}
				var newNotes = query.Take(count).Include(e => e.Tags);
				notes.AddRange(newNotes);
			}
			return notes;
		}

		public List<Event> ListNotes(IDbFilter dbFilter, int[]? kinds, int count)
		{
			using var db = new Context(_dbfile);
			var query = dbFilter.ApplyDbFilter(db.Events);
			if (kinds != null)
			{
				query = query.Where(n => kinds.Contains(n.Kind));
			}
			query = query.OrderByDescending(n => n.CreatedAtCurated).Take(count);
			return query.Include(e => e.Tags).ToList();
		}

		public int GetNotesCount(NostrSubscriptionFilter[] filters, string? excludeMutesFromId = null)
		{
			using var db = new Context(_dbfile);
			int count = 0;
			var events = db.Events.Where(e => e.Kind == 1);
			if (excludeMutesFromId.IsNotNullOrEmpty())
			{
				events = events.Where(e => !db.Mutes.Any(m => m.AccountId == excludeMutesFromId && m.MuteId == e.PublicKey));
			}
            foreach (var filter in filters)
			{
				count += ApplyFilter(db.Events.Where(e => e.Kind == 1), filter).Count();
			}
			return count;
		}

		public IEnumerable<Event> ApplyFilters(IQueryable<Event> notes, NostrSubscriptionFilter[] filters)
		{
			List<Event> filtered = new();
			foreach (var filter in filters)
			{
				var query = ApplyFilter(notes, filter);
				filtered.AddRange(query);
			}
			return filtered;
		}

		private static IQueryable<Event> ApplyFilter(IQueryable<Event> notes, NostrSubscriptionFilter filter)
		{
			if (filter.PublicKey != null)
			{
				notes = notes.Where(e => e.Tags.Any(t => t.Data0 == "p" && filter.PublicKey.Contains(t.Data1)));
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
				notes = notes.Where(e => e.Tags.Any(t => t.Data0 == "e" && filter.EventId.Contains(t.Data1)));
			}
			if (filter.ExtensionData != null)
			{
				foreach (var tagfilter in filter.GetAdditionalTagFilters())
				{
					var filterTags = tagfilter.Value.Select(t => t.ToLower()).ToList();
					notes = notes.Where(e => e.Tags.Any(t => t.Data0 == tagfilter.Key && filterTags.Contains(t.Data1)));
				}
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
			if (filter.Search.IsNotNullOrEmpty())
			{
				var words = filter.Search.Split(" ");
				notes = notes.Where(words.Aggregate(PredicateBuilder.New<Event>(),
					(current, temp) => current.Or(e => e.Content!.Contains(temp))));
			}
			return notes.Where(e => !e.Deleted);
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

		public List<Event> ListOwnEvents(string accountId)
		{
			using var db = new Context(_dbfile);
			return db.Events
				.Include(e => e.Tags)
				.Where(e => e.PublicKey == accountId)
				.ToList();
		}

		public void SaveOwnEvents(NostrEvent nostrEvent, bool broadcast)
		{
			using var db = new Context(_dbfile);

			var ev = EventExtension.FromNostrEvent(nostrEvent);
			ev.Broadcast = broadcast;
			ev.CanEcho = true;

			if (SaveNewEvent(ev))
			{
				DatabaseHasChanged?.Invoke(this, EventArgs.Empty);
			}
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

			if (db.Configs.Any(c => c.Id == config.Id))
			{
				db.Update(config);
			}
			else
			{
				db.Add(config);
			}
			db.SaveChanges();
			DatabaseHasChanged?.Invoke(this, EventArgs.Empty);
		}

		public EventDetailsCount GetEventDetailsCount(string eventId)
		{
			using var db = new Context(_dbfile);

			var validKinds = new int[] { NostrKind.Reaction, NostrKind.Repost, NostrKind.Zap };

            var tag = eventId.IsReplaceableId() ? "a" : "e";

            var counts = db.TagDatas
				.Where(d => d.Data0 == tag && d.Data1 == eventId && validKinds.Contains(d.Event.Kind))
				.GroupBy(d => new { d.Event.Kind, ReactionType = d.Event.Kind == NostrKind.Reaction ? d.Event.Content ?? string.Empty : string.Empty })
				.Select(d => new { d.Key.Kind, d.Key.ReactionType, Count = d.Count() });
			return new()
			{
				ReactionGroups = new(counts
					.Where(c => c.Kind == NostrKind.Reaction)
					.ToDictionary(c => c.ReactionType, c => c.Count)),
				Reposts = counts.Where(c => c.Kind == NostrKind.Repost).Sum(c => c.Count),
				Zaps = counts.Where(c => c.Kind == NostrKind.Zap).Sum(c => c.Count),
			};
		}

        public List<Reaction> GetReactionList(string eventId, int index, int count)
        {
            using var db = new Context(_dbfile);

            var tag = eventId.IsReplaceableId() ? "a" : "e";
            return db.Events
                .Where(e => e.Tags.Any(d => d.Data0 == tag && d.Data1 == eventId) && e.Kind == NostrKind.Reaction)
                .Skip(index)
                .Take(count)
                .Select(e => new Reaction() { Content = e.Content, ReactorId = e.PublicKey })
                .ToList();
        }

        public bool AccountReacted(string eventId, string accountId)
        {
            using var db = new Context(_dbfile);

			var tag = eventId.IsReplaceableId() ? "a" : "e";
			return db.TagDatas.Any(d => d.Data0 == tag && d.Data1 == eventId && d.Event.Kind == NostrKind.Reaction &&
				d.Event.PublicKey == accountId);
		}

		public void SetNip05Validity(string id, bool valid)
		{
			using var db = new Context(_dbfile);
			db.AccountDetails.Where(ad => ad.Id == id).ExecuteUpdate(ad => ad.SetProperty(ad => ad.Nip05Valid, valid));
		}

		public List<string> GetAccountIdsThatRequireUpdate(IEnumerable<string> sourceList, TimeSpan validity)
		{
			using var db = new Context(_dbfile);
			var expireOn = DateTimeOffset.UtcNow.Subtract(validity).ToUnixTimeSeconds();

			var updatedAccountsQuery = db.AccountDetails
				.Where(ad => sourceList.Contains(ad.Id) && ad.DetailsLastReceived >= expireOn)
				.Select(ad => ad.Id);

			var inter = sourceList.Except(updatedAccountsQuery);

			return inter.ToList();
		}

		public bool TryDetermineHexType(string id, out IdType type)
		{
			using var db = new Context(_dbfile);
			if (db.Accounts.Any(a => a.Id == id) || db.AccountDetails.Any(a => a.Id == id))
			{
				type = IdType.Account;
				return true;
			}
			if (db.Channels.Any(c => c.Id == id) || db.ChannelDetails.Any(c => c.Id == id) ||
				db.Events.Any(e => e.ChannelId == id))
			{
				type = IdType.Channel;
				return true;
			}
			if (db.Events.Any(e => e.Id == id && (e.Kind == NostrKind.Text || e.Kind == NostrKind.Repost)))
			{
				type = IdType.Event;
				return true;
			}
			type = IdType.Unknown;
			return false;
		}

		public void AddFollow(string accountId, string followId)
		{
			using var db = new Context(_dbfile);
			db.Add(new Follow() { AccountId = accountId, FollowId = followId });
			db.SaveChanges();
		}

        public void RemoveFollow(string accountId, string followId)
        {
            using var db = new Context(_dbfile);
            db.Follows.Where(f => f.AccountId == accountId && f.FollowId == followId).ExecuteDelete();
        }

        public void SetFollows(string accountId, IEnumerable<string> followIds)
		{
			using var db = new Context(_dbfile);
			db.Follows.Where(f => f.AccountId == accountId).ExecuteDelete();
			foreach (string id in followIds)
			{
				db.Add(new Follow() { AccountId = accountId, FollowId = id });
			}
			db.SaveChanges();
		}

        public void ClearFollows(string accountId)
		{
			using var db = new Context(_dbfile);
			db.Events.Where(e => e.Kind == NostrKind.Contacts && e.PublicKey == accountId).ExecuteDelete();
			db.Follows.Where(f => f.AccountId == accountId).ExecuteDelete();
			db.Accounts.Where(a => a.Id == accountId).ExecuteUpdate(a => a.SetProperty(a => a.FollowsLastUpdate, (DateTimeOffset?)null));
		}

		public List<string> GetFollowIds(string accountId)
		{
			using var db = new Context(_dbfile);
			return db.Follows.Where(f => f.AccountId == accountId).Select(f => f.FollowId).ToList();
		}

		public List<string> GetFollowerIds(string accountId)
		{
			using var db = new Context(_dbfile);
			return db.Follows.Where(f => f.FollowId == accountId).Select(f => f.AccountId).ToList();
		}

		public bool IsFollowing(string accountId, string followId)
		{
			using var db = new Context(_dbfile);
			return db.Follows.Any(f => f.AccountId == accountId && f.FollowId == followId);
		}

		public int GetFollowCount(string accountId)
		{
			using var db = new Context(_dbfile);
			return db.Follows.Count(f => f.AccountId == accountId);
		}

		public bool HasFollows(string accountId)
		{
			using var db = new Context(_dbfile);
			return db.Follows.Any(f => f.AccountId == accountId);
		}

		public int GetFollowerCount(string accountId)
		{
			using var db = new Context(_dbfile);
			return db.Follows.Count(f => f.FollowId == accountId);
		}

        public void AddMute(string accountId, string muteId, bool priv)
        {
            using var db = new Context(_dbfile);
            db.Add(new Mute() { AccountId = accountId, MuteId = muteId, IsPrivate = priv });
            db.SaveChanges();
        }

        public void RemoveMute(string accountId, string muteId, bool priv)
        {
            using var db = new Context(_dbfile);
            db.Mutes.Where(m => m.AccountId == accountId && m.MuteId == muteId && m.IsPrivate == priv).ExecuteDelete();
        }

        public void SetMutes(string accountId, IEnumerable<string> publicList, IEnumerable<string> privateList)
        {
            using var db = new Context(_dbfile);
            using var tx = db.Database.BeginTransaction();
            db.Mutes.Where(f => f.AccountId == accountId).ExecuteDelete();
            db.AddRange(publicList.Select(id => new Mute() { AccountId = accountId, MuteId = id }));
            db.AddRange(privateList.Select(id => new Mute() { AccountId = accountId, MuteId = id, IsPrivate = true }));
            db.SaveChanges();
            tx.Commit();
        }

        public bool IsMuting(string accountId, string muteId, bool? priv = null)
        {
            using var db = new Context(_dbfile);
			if (priv.HasValue)
			{
				return db.Mutes.Any(m => m.AccountId == accountId && m.MuteId == muteId && m.IsPrivate == priv.Value);
			}
			else
			{
                return db.Mutes.Any(m => m.AccountId == accountId && m.MuteId == muteId);
            }
        }

        public List<(string MuteId, bool IsPrivate)> GetMuteIds(string accountId)
        {
            using var db = new Context(_dbfile);
			return db.Mutes
				.Where(f => f.AccountId == accountId)
				.Select(f => new { f.MuteId, f.IsPrivate })
				.AsEnumerable()
				.Select(f => (f.MuteId, f.IsPrivate))
				.ToList();
        }

        public bool HasMutes(string accountId)
        {
            using var db = new Context(_dbfile);
            return db.Mutes.Any(f => f.AccountId == accountId);
        }

        public Channel GetChannel(string id)
		{
			using var db = new Context(_dbfile);
			return db.Channels.Include(c => c.Details).FirstOrDefault(c => c.Id == id) ?? new Channel() { Id = id };
		}

		public ChannelDetails GetChannelDetails(string channelId)
		{
			using var db = new Context(_dbfile);
			return db.ChannelDetails.FirstOrDefault(cd => cd.Channel.Id == channelId) ?? new ChannelDetails() { Id = channelId };
		}

		public void SaveChannelDetails(ChannelDetails channelDetails)
		{
			using var db = new Context(_dbfile);
			if (db.ChannelDetails.Any(ad => ad.Id == channelDetails.Id))
			{
				db.Update(channelDetails);
			}
			else
			{
				if (!db.Channels.Any(a => a.Id == channelDetails.Id))
				{
					db.Add(new Channel() { Id = channelDetails.Id });
				}
				else if (channelDetails.Channel != null)
				{
					db.Attach(channelDetails.Channel);
				}
				else
				{
					db.Attach(new Channel() { Id = channelDetails.Id });
				}
				db.Add(channelDetails);
			}
			db.SaveChanges();
		}

		public void SaveChannel(Channel channel)
		{
			using var db = new Context(_dbfile);
			if (db.Channels.Any(c => c.Id == channel.Id))
			{
				db.Update(channel);
			}
			else
			{
				db.Add(channel);
			}
			db.SaveChanges();
		}

		public List<Channel> ListChannels()
		{
			using var db = new Context(_dbfile);
			return db.Channels.Include(c => c.Details).ToList();
		}

		public int GetChannelMessagesInDb(string channelId)
		{
			using var db = new Context(_dbfile);
			return db.Events.Count(e =>
				e.Kind == NostrKind.ChannelMessage && e.ChannelId == channelId);
		}

		public List<ChannelWithInfo> ListChannelsWithInfo()
		{
			using var db = new Context(_dbfile);

			var channelsMessageCount = db.Events
				.Where(e => e.Kind == NostrKind.ChannelMessage && !string.IsNullOrEmpty(e.ChannelId))
				.GroupBy(e => e.ChannelId ?? string.Empty)
				.Select(g => new
				{
					Id = g.Key,
					MessageCount = g.Count(),
				})
				.ToDictionary(c => c.Id);

			// Get all channels with details and populate with previous data
			var channelsWithInfo = db.Channels
				.Include(c => c.Details)
				.AsEnumerable()
				.Select(c =>
					new ChannelWithInfo(c)
					{
						MessageCount = channelsMessageCount.TryGetValue(c.Id, out var channelInfo) ? channelInfo.MessageCount : 0
					})
				.ToList();

			// Add channels with count but without details
			foreach (var (channelId, channelInfo) in channelsMessageCount)
			{
				if (!channelsWithInfo.Any(c => c.Id == channelId))
				{
					channelsWithInfo.Add(new ChannelWithInfo() { Id = channelId, MessageCount = channelInfo.MessageCount });
				}
			}

			return channelsWithInfo;
		}
	}
}
