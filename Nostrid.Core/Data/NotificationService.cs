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

        public event EventHandler<int> NotificationNumberChanged;

        public NotificationService(INotificationCounter notificationCounter, AccountService accountService, FeedService feedService)
        {
            this.notificationCounter = notificationCounter;
            this.accountService = accountService;
            this.feedService = feedService;
            accountService.MentionsUpdated += MentionsUpdated;
        }

        private void MentionsUpdated(object sender, EventArgs e)
        {
            int mentionsCount;
            if (accountService.MainAccount != null)
            {
                mentionsCount = feedService.GetUnreadMentionsCount();
            }
            else
            {
                mentionsCount = 0;
            }
            notificationCounter.SetNotificationCount(mentionsCount);
            NotificationNumberChanged?.Invoke(this, mentionsCount);
		}
    }
}
