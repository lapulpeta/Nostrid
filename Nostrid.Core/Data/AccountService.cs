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
    private bool detailsNeededIdsChanged;

    private readonly ConcurrentDictionary<string, ISigner> knownSigners = new();
    private readonly ConcurrentDictionary<string, DateTime> detailsNeededIds = new();
    private readonly ConcurrentDictionary<string, string> followerRequestFilters = new();
    private readonly ConcurrentDictionary<(string, string, bool), bool> mutingCache = new();

    public event EventHandler? MainAccountChanged;
    public event EventHandler<(string accountId, AccountDetails details)>? AccountDetailsChanged;
    public event EventHandler<(string accountId, List<string> follows)>? AccountFollowsChanged;
    public event EventHandler<(string accountId, List<string> mutes)>? AccountMutesChanged;
    public event EventHandler<string>? AccountFollowersChanged;
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
            MainAccountMentionsFilter.LimitFilterData.Limit = 1;
            mainFilters = new[] { new MainAccountSubscriptionFilter(mainAccount.Id), MainAccountMentionsFilter, new DmSubscriptionFilter(mainAccount.Id) };
            relayService.AddFilters(mainFilters);
        }
        MainAccountChanged?.Invoke(this, EventArgs.Empty);
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
        return eventDatabase.IsFollowing(MainAccount.Id, accountToCheckId);
    }

    public async Task SetFollows(IEnumerable<string> follows)
    {
        if (MainAccount == null)
            return;

        lock (eventDatabase)
        {
            eventDatabase.SetFollows(MainAccount.Id, follows);
        }
        await SendContactList();
    }

    public async Task FollowUnfollow(string otherAccountId, bool unfollow)
    {
        lock (eventDatabase)
        {
            if ((unfollow && !IsFollowing(otherAccountId)) ||
                (!unfollow && IsFollowing(otherAccountId)))
                return;

            if (unfollow)
                eventDatabase.RemoveFollow(MainAccount.Id, otherAccountId);
            else
                eventDatabase.AddFollow(MainAccount.Id, otherAccountId);
        }
        await SendContactList();
    }

    public async Task<bool> SendContactList()
    {
        var accountId = MainAccount.Id;
        var nostrEvent = new NostrEvent()
        {
            CreatedAt = DateTimeOffset.UtcNow,
            Kind = 3,
            PublicKey = accountId,
            Tags = new(),
            Content = "{}",
        };

        foreach (var follow in eventDatabase.GetFollowIds(accountId))
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

                lock (detailsNeededIds)
                    detailsNeededIds.TryRemove(eventToProcess.PublicKey, out _);

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
                        accountDetails.Nip05Valid = accountDetails.Nip05Id.IsNotNullOrEmpty() && await Nip05.RefreshNip05(accountDetails.Id, accountDetails.Nip05Id);
                        eventDatabase.SetNip05Validity(accountDetails.Id, accountDetails.Nip05Valid);
                        AccountDetailsChanged?.Invoke(this, (accountDetails.Id, accountDetails));
                    });
                }

            }
        }
    }

    public void RegisterFollowerRequestFilter(string filterId, string accountId)
    {
        followerRequestFilters[filterId] = accountId;
    }

    public void UnregisterFollowerRequestFilter(string filterId)
    {
        followerRequestFilters.TryRemove(filterId, out _);
    }

    public List<string> GetFollowsFromEvent(Event ev)
    {
        var followList = ev.Tags.Where(t => t.Data0 == "p" && t.Data1.IsNotNullOrEmpty()).Select(t => t.Data1).Distinct().ToList();
        return followList!;
    }

    // NIP-02: https://github.com/nostr-protocol/nips/blob/master/02.md
    public void HandleKind3(Event eventToProcess, string filterId)
    {
        Action? update = null;

        lock (eventDatabase)
        {
            var newFollowList = eventToProcess.Tags.Where(t => t.Data0 == "p" && t.Data1.IsNotNullOrEmpty()).Select(t => t.Data1).Distinct().ToList();
            var account = eventDatabase.GetAccount(eventToProcess.PublicKey);

            if (!eventToProcess.CreatedAt.HasValue || !account.FollowsLastUpdate.HasValue ||
                eventToProcess.CreatedAt.Value > account.FollowsLastUpdate.Value)
            {
                if (followerRequestFilters.TryGetValue(filterId, out var requesterId))
                {
                    // If we received this because someone is requesting his followers then we don't save all the list, just this single follow
                    if (newFollowList.Contains(requesterId) && !eventDatabase.IsFollowing(account.Id, requesterId))
                    {
                        eventDatabase.AddFollow(account.Id, requesterId);

                        update = () =>
                        {
                            AccountFollowersChanged?.Invoke(this, requesterId);
                        };
                    }
                }
                else
                {
                    eventDatabase.SetFollows(account.Id, newFollowList);

                    account.FollowsLastUpdate = eventToProcess.CreatedAt ?? DateTimeOffset.UtcNow;

                    eventDatabase.SaveAccount(account);

                    if (account.Id == mainAccount?.Id)
                    {
                        SetMainAccount(account);
                    }

                    update = () =>
                    {
                        AccountFollowsChanged?.Invoke(this, (account.Id, newFollowList));
                        foreach (var follow in newFollowList)
                            AccountFollowersChanged?.Invoke(this, follow);
                    };
                }
            }
        }

        update?.Invoke();
    }

    #region Muting // TODO: combine muting with following

    public async Task MuteUnmute(string muteId, bool unmute, bool? priv = null)
    {
        lock (eventDatabase)
        {
            if ((unmute && !IsMuting(muteId, priv)) ||
                (!unmute && IsMuting(muteId, priv)))
                return;

            if (unmute)
            {
                if (priv.HasValue)
                {
                    eventDatabase.RemoveMute(MainAccount.Id, muteId, priv.Value);
                }
                else
                {
                    eventDatabase.RemoveMute(MainAccount.Id, muteId, true);
                    eventDatabase.RemoveMute(MainAccount.Id, muteId, false);
                }
            }
            else
            {
                if (priv.HasValue)
                {
                    eventDatabase.AddMute(MainAccount.Id, muteId, priv.Value);
                }
                else
                {
                    eventDatabase.AddMute(MainAccount.Id, muteId, true);
                    eventDatabase.AddMute(MainAccount.Id, muteId, false);
                }
            }
        }
        await SendMuteList();
    }

    public bool IsMuting(string accountToCheckId, bool? priv = null)
    {
        if (MainAccount == null)
        {
            return false;
        }
        if (priv == null)
        {
            return IsMuting(accountToCheckId, true) || IsMuting(accountToCheckId, false);
        }
        return mutingCache.GetOrAdd((MainAccount.Id, accountToCheckId, priv.Value), (_) => eventDatabase.IsMuting(MainAccount.Id, accountToCheckId, priv.Value));
    }

    public async Task<bool> SendMuteList()
    {
        var accountId = MainAccount!.Id;
        var nostrEvent = new NostrEvent()
        {
            CreatedAt = DateTimeOffset.UtcNow,
            Kind = NostrKind.Mutes,
            PublicKey = accountId,
            Tags = new(),
        };

        var privateMuteList = new List<string>();
        foreach (var (muteId, priv) in eventDatabase.GetMuteIds(accountId))
        {
            if (priv)
            {
                privateMuteList.Add(muteId);
            }
            else
            {
                nostrEvent.Tags.Add(new NostrEventTag() { TagIdentifier = "p", Data = { muteId } });
            }
        }
        nostrEvent.Content = await EncryptMuteList(accountId, privateMuteList);

        if (!await MainAccountSigner.Sign(nostrEvent))
            return false;
        relayService.SendEvent(nostrEvent);
        mutingCache.Clear();
        return true;
    }

    private async Task<List<string>> DecryptMuteList(string pubkey, string content)
    {
        try
        {
            var decryptedContent = await MainAccountSigner!.DecryptNip04(pubkey, content);
            if (decryptedContent.IsNotNullOrEmpty())
            {
                var decryptedTags = JsonSerializer.Deserialize<List<List<string>>>(decryptedContent);
                if (decryptedTags != null)
                {
                    return decryptedTags.Where(pair => pair.FirstOrDefault() == "p" && pair.Count > 1).Select(pair => pair[1]).ToList();
                }
            }
        }
        catch
        {
        }
        return new();
    }

    private async Task<string> EncryptMuteList(string pubkey, List<string> mutes)
    {
        try
        {
            var decryptedContentList = mutes.Select(m => new List<string>() { "p", m });
            var decryptedContent = JsonSerializer.Serialize(decryptedContentList) ?? string.Empty;

            return await MainAccountSigner!.EncryptNip04(pubkey, decryptedContent) ?? string.Empty;
        }
        catch
        {
        }
        return string.Empty;
    }

    // NIP-51: https://github.com/nostr-protocol/nips/blob/master/51.md
    public async Task HandleMuteList(Event eventToProcess)
    {
        if (eventToProcess.PublicKey != mainAccount?.Id) // Only save own mute list
        {
            return;
        }

        Action? update = null;

        // Decrypt private list
        List<string> newPrivateMuteList = new();
        if (eventToProcess.Content.IsNotNullOrEmpty())
        {
            newPrivateMuteList = await DecryptMuteList(eventToProcess.PublicKey, eventToProcess.Content);
        }

        var newPublicMuteList = eventToProcess.Tags.Where(t => t.Data0 == "p" && t.Data1.IsNotNullOrEmpty()).Select(t => t.Data1!).Distinct().ToList();

        lock (eventDatabase)
        {
            var account = eventDatabase.GetAccount(eventToProcess.PublicKey);

            if (Utils.MustUpdate(eventToProcess.CreatedAt, account.MutesLastUpdate))
            {
                eventDatabase.SetMutes(account.Id, newPublicMuteList, newPrivateMuteList);

                mutingCache.Clear();

                account.MutesLastUpdate = eventToProcess.CreatedAt ?? DateTimeOffset.UtcNow;

                eventDatabase.SaveAccount(account);

                if (account.Id == mainAccount?.Id)
                {
                    SetMainAccount(account);
                }

                update = () =>
                {
                    AccountMutesChanged?.Invoke(this, (account.Id, newPublicMuteList.Union(newPrivateMuteList).ToList()));
                };
            }
        }

        update?.Invoke();
    }

    #endregion

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
        if (details.Id.IsNullOrEmpty())
        {
            details.Id = details.Account.Id;
        }
        eventDatabase.SaveAccountDetails(details);

        var unescapedContent = JsonSerializer.Serialize(details);
        var nostrEvent = new NostrEvent()
        {
            CreatedAt = DateTimeOffset.UtcNow,
            Kind = 0,
            PublicKey = details.Id,
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
        var expireOn = DateTime.UtcNow.AddSeconds(TtlSeconds);
        lock (detailsNeededIds)
        {
            detailsNeededIdsChanged = true;
            detailsNeededIds[accountId] = expireOn;
        }
    }

    public void AddDetailsNeeded(IEnumerable<string> accountIds)
    {
        var expireOn = DateTime.UtcNow.AddSeconds(TtlSeconds);
        lock (detailsNeededIds)
        {
            detailsNeededIdsChanged = true;
            foreach (var accountId in accountIds)
                detailsNeededIds[accountId] = expireOn;
        }
    }

    private const int SecondsForDetailsFilters = 5;
    private const int DetailsValidityMinutes = 30;
    private const int TtlSeconds = 60;
    public async Task QueryDetails(CancellationToken cancellationToken)
    {
        List<string> willQuery = new();
        List<AccountDetailsSubscriptionFilter> filters = new();
        bool mustUpdate;
        while (!cancellationToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            lock (detailsNeededIds)
            {
                if (detailsNeededIdsChanged)
                {
                    detailsNeededIdsChanged = false;
                    detailsNeededIds.Where(d => d.Value < now).Select(d => d.Key).ToList().ForEach(id => detailsNeededIds.TryRemove(id, out _));
                    var oldWillQuery = new List<string>(willQuery);
                    willQuery = eventDatabase.GetAccountIdsThatRequireUpdate(detailsNeededIds.Keys, TimeSpan.FromMinutes(DetailsValidityMinutes));
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
                filters = AccountDetailsSubscriptionFilter.CreateInBatch(willQuery.ToArray(), destroyOnEose: true);
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

