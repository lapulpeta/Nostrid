namespace Nostrid.Model;

public class Account
{
    public string Id { get; set; }

    public string? PrivKey { get; set; }

    public AccountDetails? Details { get; set; }

    public DateTimeOffset? FollowsLastUpdate { get; set; }

    public DateTimeOffset? MutesLastUpdate { get; set; }

    public DateTime LastNotificationRead { get; set; }
}

