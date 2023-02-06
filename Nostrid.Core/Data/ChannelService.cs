using Nostrid.Data.Relays;
using Nostrid.Misc;
using Nostrid.Model;
using System.Text.Json;

namespace Nostrid.Data;

/// <summary>
/// Handles public chat aka channels as per NIP-28 https://github.com/nostr-protocol/nips/blob/master/28.md
/// </summary>
public class ChannelService
{
    private readonly EventDatabase eventDatabase;

    private readonly object channelLock = new();

    public event EventHandler<(string channelId, ChannelDetails details)>? ChannelDetailsChanged;
    public event EventHandler<string>? ChannelReceivedMessage;

    public ChannelService(EventDatabase eventDatabase)
    {
        this.eventDatabase = eventDatabase;
    }

    public void MessageProcessed(Event e)
    {
        if (!e.ChannelId.IsNullOrEmpty())
        {
            ChannelReceivedMessage?.Invoke(this, e.ChannelId);
        }
    }

    public void HandleChannelCreationOrMetadata(Event eventToProcess)
    {
        lock (channelLock)
        {
            var isUpdate = eventToProcess.Kind == NostrKind.ChannelMetadata;
            if (!string.IsNullOrEmpty(eventToProcess.Content))
            {
                ChannelDetails? channelDetailsReceived = null;
                try
                {
                    channelDetailsReceived = JsonSerializer.Deserialize<ChannelDetails>(eventToProcess.Content);
                }
                catch (Exception)
                {
                }

                if (channelDetailsReceived == null)
                    return;

                string? channelId = null;
                if (isUpdate)
                {
                    var tag = eventToProcess.Tags.FirstOrDefault(t => t.Data0 == "e" && t.DataCount > 1);
                    if (tag != null)
                        channelId = tag.Data1;
                }
                else
                {
                    channelId = eventToProcess.Id;
                }

                var channelDetails = eventDatabase.GetChannelDetails(channelId);

                if (!eventToProcess.CreatedAt.HasValue || eventToProcess.CreatedAt.Value > channelDetails.DetailsLastUpdate)
                {
                    channelDetails.About = channelDetailsReceived.About;
                    channelDetails.Name = channelDetailsReceived.Name;
                    channelDetails.PictureUrl = channelDetailsReceived.PictureUrl;
                    channelDetails.DetailsLastUpdate = eventToProcess.CreatedAt ?? DateTime.UtcNow;

                    eventDatabase.SaveChannelDetails(channelDetails);

                    ChannelDetailsChanged?.Invoke(this, (channelDetails.Id, channelDetails));
                }
            }
        }
    }

    public List<Channel> GetChannels()
    {
        return eventDatabase.ListChannels();
    }
    public List<ChannelWithInfo> GetChannelsWithInfo()
    {
        return eventDatabase.ListChannelsWithInfo();
    }

    public Channel GetChannel(string id)
    {
        return eventDatabase.GetChannel(id);
    }

    public int GetChannelMessagesInDb(string channelId)
    {
        return eventDatabase.GetChannelMessagesInDb(channelId);
    }
}

