namespace Nostrid.Model;

public class ChannelWithInfo : Channel
{
    public int MessageCount { get; set; }

    public ChannelWithInfo()
    {
    }

    public ChannelWithInfo(Channel channel)
    {
        Id = channel.Id;
        Details= channel.Details;
        DetailsLastUpdate = channel.DetailsLastUpdate;
    }
}

