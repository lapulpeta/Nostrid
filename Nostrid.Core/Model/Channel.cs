namespace Nostrid.Model;

public class Channel
{
    public string Id { get; set; }

    public ChannelDetails Details { get; set; }

    public string? CreatorId { get; set; }

    public DateTimeOffset? DetailsLastUpdate { get; set; }

}

