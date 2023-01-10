namespace Nostrid.Model;

public class FeedSource
{
    public long Id { get; set; }

    public string OwnerId { get; set; }

    public List<string> Hashtags { get; set; } = new();
}

