using Nostrid.Data.Relays;
using Nostrid.Misc;
using Nostrid.Model;
using NNostr.Client;
using System.Text.Json;
using System.Web;

namespace Nostrid.Data;

public class AccountService
{
    private readonly EventDatabase eventDatabase;
    private readonly RelayService relayService;

    private SubscriptionFilter[] mainFilters;

    public event EventHandler MainAccountChanged;
    public event EventHandler<(string accountId, AccountDetails details)> AccountDetailsChanged;
    public event EventHandler<(string accountId, List<string> follows)> AccountFollowsChanged;
    public event EventHandler MentionsUpdated;

    public SubscriptionFilter MainAccountMentionsFilter { get; private set; }

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
    }

    private Account mainAccount;

    public Account MainAccount
    {
        get => mainAccount;
        set
        {
            mainAccount = value;
            MainAccountChanged?.Invoke(this, EventArgs.Empty);
            if (mainFilters != null)
            {
                relayService.DeleteFilters(mainFilters);
            }
            MainAccountMentionsFilter = new MentionSubscriptionFilter(mainAccount.Id);
            MainAccountMentionsFilter.limitFilterData.Limit = 1;
			mainFilters = new[] { new MainAccountSubscriptionFilter(mainAccount.Id), MainAccountMentionsFilter };
            relayService.AddFilters(mainFilters);
        }
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

    public void FollowUnfollow(string otherAccountId, bool unfollow)
    {
        lock (eventDatabase)
        {
            if ((unfollow && !mainAccount.FollowList.Contains(otherAccountId)) ||
                (!unfollow && mainAccount.FollowList.Contains(otherAccountId)))
                return;

            eventDatabase.RunInTransaction(() =>
            {
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

                return true;
            });

            SendContactList(mainAccount);
        }
    }

    public void SendContactList(Account account)
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

        account.ComputeIdAndSign(nostrEvent);
        relayService.SendEvent(nostrEvent);
    }

    // NIP-01: https://github.com/nostr-protocol/nips/blob/master/01.md
    public void HandleKind0(Event eventToProcess)
    {
        Account accountChanged = null;
        lock (eventDatabase)
        {
            eventDatabase.RunInTransaction(() =>
            {
                if (!string.IsNullOrEmpty(eventToProcess.Content))
                {
                    try
                    {
                        var accountDetails = JsonSerializer.Deserialize<AccountDetails>(Utils.JavaScriptStringDecode(eventToProcess.Content, false));
                        if (accountDetails != null)
                        {
                            var account = eventDatabase.GetAccount(eventToProcess.PublicKey);

                            if (!eventToProcess.CreatedAt.HasValue || !account.DetailsLastUpdate.HasValue ||
                                eventToProcess.CreatedAt.Value > account.DetailsLastUpdate.Value)
                            {
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

                return true;
            });
        }
        if (accountChanged != null)
            AccountDetailsChanged?.Invoke(this, (accountChanged.Id, accountChanged.Details));
    }

    // NIP-02: https://github.com/nostr-protocol/nips/blob/master/02.md
    public void HandleKind3(Event eventToProcess)
    {
        Account accountChanged = null;

        lock (eventDatabase)
        {
            var newFollowList = eventToProcess.Tags.Where(t => t.TagIdentifier == "p" && t.Data?.Count > 0).Select(t => t.Data[0]).Distinct().ToList();

            eventDatabase.RunInTransaction(() =>
            {
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
                        MainAccount = account;
                    }

                    if (addedFollows.Any() || removedFollows.Any())
                    {
                        accountChanged = account;
                    }
                }

                eventToProcess.Processed = true;
                eventDatabase.SaveEvent(eventToProcess);

                return true;
            });
        }

        if (accountChanged != null)
            AccountFollowsChanged?.Invoke(this, (accountChanged.Id, accountChanged.FollowList));
    }

    public string GetAccountName(string accountId)
    {
        return eventDatabase.GetAccountName(accountId) ?? ByteTools.PubkeyToNpub(accountId, true);
    }

    public (string Name, string ProfileUrl) GetAccountNamePictureUrl(string accountId)
    {
        var accountDetails = eventDatabase.GetAccount(accountId)?.Details;
        return (accountDetails?.Name ?? ByteTools.PubkeyToNpub(accountId, true), accountDetails?.PictureUrl);
    }

    public AccountDetails GetAccountDetails(string accountId)
    {
        return eventDatabase.GetAccount(accountId)?.Details ?? new AccountDetails();
    }

    public Account GetAccount(string accountId)
    {
        return eventDatabase.GetAccount(accountId);
    }

    public void SaveAccountDetails(AccountDetails details)
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

            // NNostr doesn't escape the content when calculating id and signature but serializer escapes it when sending it
            // to the network. So we escape it here, run the calculations and unescape it back so it is escaped during serialization
            Content = HttpUtility.JavaScriptStringEncode(unescapedContent),
        };

        account.ComputeIdAndSign(nostrEvent);
        nostrEvent.Content = unescapedContent;

        relayService.SendEvent(nostrEvent);
    }
}

