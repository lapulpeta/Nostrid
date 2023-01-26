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
	private SubscriptionFilter[]? mainFilters;
    private Account? mainAccount;
    private bool running;
    private CancellationTokenSource clientThreadsCancellationTokenSource;

    private readonly ConcurrentDictionary<string, ISigner> knownSigners = new();
    private readonly List<string> detailsNeededIds = new();

    public event EventHandler? MainAccountChanged;
	public event EventHandler<(string accountId, AccountDetails details)>? AccountDetailsChanged;
	public event EventHandler<(string accountId, List<string> follows)>? AccountFollowsChanged;
	public event EventHandler? MentionsUpdated;


    public SubscriptionFilter? MainAccountMentionsFilter { get; private set; }

	public AccountService(EventDatabase eventDatabase, RelayService relayService)
	{
		this.eventDatabase = eventDatabase;
		this.relayService = relayService;
		relayService.ReceivedEvents += (_, data) =>
		{
			if (data.filterId != MainAccountMentionsFilter?.Id)
				return;

			MentionsUpdated?.Invoke(this, EventArgs.Empty);
		};
		StartDetailsUpdater();
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

	public void SetLastRead(DateTime lastRead)
	{
		mainAccount.LastNotificationRead = lastRead;
		eventDatabase.SetAccountLastRead(mainAccount.Id, lastRead);
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
		lock (eventDatabase)
		{
			if (!string.IsNullOrEmpty(eventToProcess.Content))
			{
				AccountDetails? accountDetailsReceived = null;

                try
				{
					accountDetailsReceived = JsonSerializer.Deserialize<AccountDetails>(eventToProcess.Content);
                }
                catch (Exception ex)
                {
                }

				if (accountDetailsReceived == null)
					return;
				
				var accountDetails = eventDatabase.GetAccountDetails(eventToProcess.PublicKey);

				if (!eventToProcess.CreatedAt.HasValue || eventToProcess.CreatedAt.Value > accountDetails.DetailsLastUpdate)
				{
					accountDetails.About = accountDetailsReceived.About;
					accountDetails.Name = accountDetailsReceived.Name;
					accountDetails.PictureUrl = accountDetailsReceived.PictureUrl;
					accountDetails.Nip05Id = accountDetailsReceived.Nip05Id;
					accountDetails.Lud16Id = accountDetailsReceived.Lud16Id;
					accountDetails.Lud06Url = accountDetailsReceived.Lud06Url;
					accountDetails.DetailsLastUpdate = eventToProcess.CreatedAt ?? DateTime.UtcNow;
					accountDetails.DetailsLastReceived = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

					eventDatabase.SaveAccountDetails(accountDetails);

					if (eventToProcess.PublicKey == mainAccount?.Id)
					{
                        mainAccount.Details = accountDetailsReceived;
                        SetMainAccount(mainAccount);
                    }

                    Task.Run(async () =>
                    {
                        accountDetails.Nip05Valid = accountDetails.Nip05Id.IsNotNullOrEmpty() && await Nip05.RefreshNip05(mainAccount.Id, accountDetails.Nip05Id);
                        eventDatabase.SetNip05Validity(accountDetails.Id, accountDetails.Nip05Valid);
                        AccountDetailsChanged?.Invoke(this, (accountDetails.Id, accountDetails));
                    });
                }

			}
		}
	}

	// NIP-02: https://github.com/nostr-protocol/nips/blob/master/02.md
	public void HandleKind3(Event eventToProcess)
	{
		Account accountChanged = null;

		lock (eventDatabase)
		{
			var newFollowList = eventToProcess.Tags.Where(t => t.Data0 == "p" && t.DataCount > 1).Select(t => t.Data1).Distinct().ToList();
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
        return eventDatabase.GetAccountDetails(accountId);
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

	public void AddDetailsNeeded(string accountId)
	{
        lock (detailsNeededIds)
            detailsNeededIds.Add(accountId);
    }

    public void AddDetailsNeeded(IEnumerable<string> accountIds)
    {
        lock (detailsNeededIds)
            detailsNeededIds.AddRange(accountIds);
    }

    private const int SecondsForDetailsFilters = 5;
    private const int DetailsValidityMinutes = 30;
    public async Task QueryDetails(CancellationToken cancellationToken)
	{
		List<string> willQuery;

		while (!cancellationToken.IsCancellationRequested)
		{
			lock (detailsNeededIds)
			{
                willQuery = eventDatabase.GetAccountIdsThatRequireUpdate(detailsNeededIds, TimeSpan.FromMinutes(DetailsValidityMinutes));
				detailsNeededIds.Clear();
			}

			if (willQuery.Count > 0)
			{
				var filters = AccountDetailsSubscriptionFilter.CreateInBatch(willQuery.ToArray(), destroyOnEose: true);
				relayService.AddFilters(filters);
				await Task.Delay(TimeSpan.FromSeconds(SecondsForDetailsFilters), cancellationToken);
				relayService.DeleteFilters(filters);
			}
			else
			{
                await Task.Delay(TimeSpan.FromSeconds(SecondsForDetailsFilters), cancellationToken);
            }
        }
	}
}

