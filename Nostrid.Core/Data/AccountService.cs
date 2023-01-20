using NNostr.Client;
using Nostrid.Data.Relays;
using Nostrid.Misc;
using Nostrid.Model;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Nostrid.Data;

public class AccountService
{
	private readonly EventDatabase eventDatabase;
	private readonly RelayService relayService;
	private readonly Nip05Service nip05Service;
	private SubscriptionFilter[]? mainFilters;

	public event EventHandler? MainAccountChanged;
	public event EventHandler<(string accountId, AccountDetails details)>? AccountDetailsChanged;
	public event EventHandler<(string accountId, List<string> follows)>? AccountFollowsChanged;
	public event EventHandler? MentionsUpdated;

	public SubscriptionFilter? MainAccountMentionsFilter { get; private set; }

	public AccountService(EventDatabase eventDatabase, RelayService relayService, Nip05Service nip05Service)
	{
		this.eventDatabase = eventDatabase;
		this.relayService = relayService;
		this.nip05Service = nip05Service;
		relayService.ReceivedEvents += (_, data) =>
		{
			if (data.filterId != MainAccountMentionsFilter?.Id)
				return;

			MentionsUpdated?.Invoke(this, EventArgs.Empty);
		};
	}

	private Account? mainAccount;

	private ConcurrentDictionary<string, ISigner> knownSigners = new();

	public void SetMainAccount(Account? mainAccount, ISigner? signer = null)
	{
		this.mainAccount = mainAccount;

		if (mainAccount != null && signer != null)
		{
			knownSigners[mainAccount.Id] = signer;
		}

		MainAccountChanged?.Invoke(this, EventArgs.Empty);
		if (mainFilters != null)
		{
			relayService.DeleteFilters(mainFilters);
		}
		if (mainAccount == null)
		{
			MainAccountMentionsFilter = null;
			mainFilters = null;
		}
		else
		{
			MainAccountMentionsFilter = new MentionSubscriptionFilter(mainAccount.Id);
			MainAccountMentionsFilter.limitFilterData.Limit = 1;
			mainFilters = new[] { new MainAccountSubscriptionFilter(mainAccount.Id), MainAccountMentionsFilter };
			relayService.AddFilters(mainFilters);
		}
		MentionsUpdated?.Invoke(this, EventArgs.Empty);
	}

	public Account? MainAccount
	{
		get => mainAccount;
	}

	public ISigner? MainAccountSigner
	{
		get => knownSigners.TryGetValue(MainAccount.Id, out var signer) ? signer : null;
	}

	public List<string> GetAccountsWithSigners()
	{
		return knownSigners.Keys.ToList();
	}

	public async Task AddSigner(ISigner signer)
	{
		var pubkey = await signer.GetPubKey();
		if (pubkey != null)
			knownSigners[pubkey] = signer;
	}

	public void RemoveSigner(string pubkey)
	{
		knownSigners.TryRemove(pubkey, out var _);
	}

	public bool HasSigner(string pubkey)
	{
		return knownSigners.ContainsKey(pubkey);
	}

	public void SetLastRead(DateTimeOffset lastRead)
	{
		mainAccount.LastNotificationRead = lastRead;
		eventDatabase.SaveAccount(mainAccount);
		MentionsUpdated?.Invoke(this, EventArgs.Empty);
	}

	public bool IsFollowing(string accountToCheckId)
	{
		return mainAccount.FollowList.Contains(accountToCheckId);
	}

	public async Task FollowUnfollow(string otherAccountId, bool unfollow)
	{
		lock (eventDatabase)
		{
			if ((unfollow && !mainAccount.FollowList.Contains(otherAccountId)) ||
				(!unfollow && mainAccount.FollowList.Contains(otherAccountId)))
				return;

			if (unfollow)
				mainAccount.FollowList.Remove(otherAccountId);
			else
				mainAccount.FollowList.Add(otherAccountId);
			var otherAccount = eventDatabase.GetAccount(otherAccountId);
			if (unfollow)
			{
				otherAccount.FollowerList.RemoveAll(f => f == mainAccount.Id);
			}
			else
			{
				if (!otherAccount.FollowerList.Contains(mainAccount.Id))
					otherAccount.FollowerList.Add(mainAccount.Id);
			}

			eventDatabase.SaveAccount(mainAccount);
			eventDatabase.SaveAccount(otherAccount);
		}
		await SendContactList(mainAccount);
	}

	public async Task<bool> SendContactList(Account account)
	{
		var nostrEvent = new NostrEvent()
		{
			CreatedAt = DateTimeOffset.UtcNow,
			Kind = 3,
			PublicKey = account.Id,
			Tags = new(),
			Content = "{}",
		};

		foreach (var follow in account.FollowList)
		{
			nostrEvent.Tags.Add(new NostrEventTag() { TagIdentifier = "p", Data = { follow, relayService.GetRecommendedRelayUri(), "" } });
		}

		if (!await MainAccountSigner.Sign(nostrEvent))
			return false;
		relayService.SendEvent(nostrEvent);
		return true;
	}

	// NIP-01: https://github.com/nostr-protocol/nips/blob/master/01.md
	public void HandleKind0(Event eventToProcess)
	{
		Account accountChanged = null;
		lock (eventDatabase)
		{
			if (!string.IsNullOrEmpty(eventToProcess.Content))
			{
				try
				{
					var accountDetails = JsonSerializer.Deserialize<AccountDetails>(eventToProcess.Content);
					if (accountDetails != null)
					{
						var account = eventDatabase.GetAccount(eventToProcess.PublicKey);

						if (!eventToProcess.CreatedAt.HasValue || !account.DetailsLastUpdate.HasValue ||
							eventToProcess.CreatedAt.Value > account.DetailsLastUpdate.Value)
						{
							if (string.IsNullOrEmpty(accountDetails.Nip05Id))
							{
								accountDetails.Nip05Data = null;
							}
							account.Details = accountDetails;
							account.DetailsLastUpdate = eventToProcess.CreatedAt ?? DateTimeOffset.UtcNow;
							eventDatabase.SaveAccount(account);
							accountChanged = account;
						}
					}
				}
				catch (Exception ex)
				{
				}
			}

			eventToProcess.Processed = true;
			eventDatabase.SaveEvent(eventToProcess);
		}
		if (accountChanged != null)
		{
			if (accountChanged.Id == mainAccount?.Id)
			{
				SetMainAccount(accountChanged);
			}
			if (string.IsNullOrEmpty(accountChanged.Details.Nip05Id))
			{
				AccountDetailsChanged?.Invoke(this, (accountChanged.Id, accountChanged.Details));
			}
			else
			{
				Task.Run(async () =>
				{
					var details = accountChanged.Details;
					if (await nip05Service.RefreshNip05(accountChanged.Id, details))
					{
						// Refresh from DB just in case it changed
						accountChanged = eventDatabase.GetAccount(accountChanged.Id);
						accountChanged.Details = details;
						eventDatabase.SaveAccount(accountChanged);
						if (accountChanged.Id == mainAccount?.Id)
						{
							SetMainAccount(accountChanged);
						}
					}
					AccountDetailsChanged?.Invoke(this, (accountChanged.Id, accountChanged.Details));
				});
			}
		}
	}

	// NIP-02: https://github.com/nostr-protocol/nips/blob/master/02.md
	public void HandleKind3(Event eventToProcess)
	{
		Account accountChanged = null;

		lock (eventDatabase)
		{
			var newFollowList = eventToProcess.Tags.Where(t => t.TagIdentifier == "p" && t.Data?.Count > 0).Select(t => t.Data[0]).Distinct().ToList();
			var account = eventDatabase.GetAccount(eventToProcess.PublicKey);

			if (!eventToProcess.CreatedAt.HasValue || !account.FollowsLastUpdate.HasValue ||
				eventToProcess.CreatedAt.Value > account.FollowsLastUpdate.Value)
			{
				var existingFollows = account.FollowList;

				var removedFollows = existingFollows.Where(f => !newFollowList.Contains(f)).ToList();
				var addedFollows = newFollowList.Where(f => !existingFollows.Contains(f)).ToList();

				// Remove followed by
				foreach (var removedFollowId in removedFollows)
				{
					var removedFollow = eventDatabase.GetAccount(removedFollowId);
					removedFollow.FollowerList.RemoveAll(f => f == account.Id);
					eventDatabase.SaveAccount(removedFollow);
				}

				// Add followed by
				foreach (var addedFollowId in addedFollows)
				{
					var addedFollow = eventDatabase.GetAccount(addedFollowId);
					if (!addedFollow.FollowerList.Contains(account.Id))
						addedFollow.FollowerList.Add(account.Id);
					eventDatabase.SaveAccount(addedFollow);
				}

				// Save new list
				account.FollowList = newFollowList;

				account.FollowsLastUpdate = eventToProcess.CreatedAt ?? DateTimeOffset.UtcNow;

				eventDatabase.SaveAccount(account);

				if (account.Id == mainAccount?.Id)
				{
					SetMainAccount(account);
				}

				if (addedFollows.Any() || removedFollows.Any())
				{
					accountChanged = account;
				}
			}

			eventToProcess.Processed = true;
			eventDatabase.SaveEvent(eventToProcess);
		}

		if (accountChanged != null)
			AccountFollowsChanged?.Invoke(this, (accountChanged.Id, accountChanged.FollowList));
	}

	public string GetAccountName(string accountId)
	{
		return eventDatabase.GetAccountName(accountId) ?? ByteTools.PubkeyToNpub(accountId, true);
	}

	public AccountDetails GetAccountDetails(string accountId)
	{
		return eventDatabase.GetAccount(accountId)?.Details ?? new AccountDetails();
	}

	public Account GetAccount(string accountId)
	{
		return eventDatabase.GetAccount(accountId);
	}

	public async Task<bool> SaveAccountDetails(AccountDetails details)
	{
		var account = eventDatabase.GetAccount(mainAccount.Id);
		account.Details = details;
		eventDatabase.SaveAccount(account);

		var unescapedContent = JsonSerializer.Serialize(details);
		var nostrEvent = new NostrEvent()
		{
			CreatedAt = DateTimeOffset.UtcNow,
			Kind = 0,
			PublicKey = account.Id,
			Tags = new(),
			Content = unescapedContent,
		};
		if (!await MainAccountSigner.Sign(nostrEvent))
			return false;
		relayService.SendEvent(nostrEvent);
		return true;
	}
}

