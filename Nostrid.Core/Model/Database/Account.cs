namespace Nostrid.Model;

public class Account
{
    public string Id { get; set; }

    public string? PrivKey { get; set; }

    public AccountDetails? Details { get; set; }

    public List<string> FollowList { get; set; } = new();

    public List<string> FollowerList { get; set; } = new();

    public DateTimeOffset? FollowsLastUpdate { get; set; }

    public DateTime LastNotificationRead { get; set; }
}

