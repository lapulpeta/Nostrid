using Plugin.LocalNotification;

namespace Nostrid.Misc;

public class NotificationCounterForMobile : INotificationCounter
{
	public void SetNotificationCount(int count)
	{
		Task.Run(async () =>
		{
			if (!await LocalNotificationCenter.Current.AreNotificationsEnabled())
			{
				await LocalNotificationCenter.Current.RequestNotificationPermission();
			}

			LocalNotificationCenter.Current.Clear();

			if (count == 0)
				return;

			var notification = new NotificationRequest
			{
				NotificationId = 100,
				Title = "Nostrid",
				Description = $"You have {count} unread notification(s)",
			};
			await LocalNotificationCenter.Current.Show(notification);
		});
	}
}