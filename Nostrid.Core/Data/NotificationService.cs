using Nostrid.Data;

namespace Nostrid
{
	public interface INotificationCounter
	{
		void SetNotificationCount(int count);
	}

	public class NotificationService
	{
		private readonly INotificationCounter notificationCounter;
		private readonly AccountService accountService;
		private readonly FeedService feedService;
		private readonly DmService dmService;
		private readonly HashSet<(string, string)> blockedDmNotifications = new();

		public event EventHandler<(int, int)>? NotificationNumberChanged;

		private int mentionsCount, unreadDmCount;

		public NotificationService(INotificationCounter notificationCounter, AccountService accountService, FeedService feedService, DmService dmService)
		{
			this.notificationCounter = notificationCounter;
			this.accountService = accountService;
			this.feedService = feedService;
			this.dmService = dmService;
			accountService.MentionsUpdated += (_, _) => SendUpdate(true, false);
			accountService.MainAccountChanged += (_, _) => SendUpdate(true, true);
			dmService.NewDm += NewDm;
			dmService.LastReadUpdated += NewDm;
		}

		public void Update()
		{
			SendUpdate(true, true);
		}

		private void SendUpdate(bool mentions, bool dms)
		{
			int newMentionsCount, newUnreadDmCount;

			if (accountService.MainAccount == null)
			{
				newMentionsCount = 0;
				newUnreadDmCount = 0;
			}
			else
			{
				newMentionsCount = mentions ? feedService.GetUnreadMentionsCount() : mentionsCount;
				newUnreadDmCount = dms ? dmService.GetUnreadCount(accountService.MainAccount.Id) : unreadDmCount;
			}
			if (mentionsCount != newMentionsCount || unreadDmCount != newUnreadDmCount)
			{
				mentionsCount = newMentionsCount;
				unreadDmCount = newUnreadDmCount;
				notificationCounter.SetNotificationCount(mentionsCount + unreadDmCount);
				NotificationNumberChanged?.Invoke(this, (mentionsCount, unreadDmCount));
			}
		}

		private void NewDm(object? sender, (string senderId, string receiverId) data)
		{
			if (blockedDmNotifications.Contains((data.senderId, data.receiverId)) || blockedDmNotifications.Contains((data.receiverId, data.senderId)))
			{
				return;
			}
			if (data.senderId == accountService.MainAccount?.Id || data.receiverId == accountService.MainAccount?.Id)
			{
				SendUpdate(false, true);
			}
		}

		public void AddDmBlock(string account1, string account2)
		{
			blockedDmNotifications.Add((account1, account2));
        }

        public void RemoveDmBlock(string account1, string account2)
        {
            blockedDmNotifications.Remove((account1, account2));
        }

		public (int, int) GetNotificationNumber()
		{
			return (mentionsCount, unreadDmCount);
		}
	}
}
