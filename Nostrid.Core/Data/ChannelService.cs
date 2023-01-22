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

    public event EventHandler<(string channelId, ChannelDetails details)> ChannelDetailsChanged;
    public event EventHandler<string> ChannelReceivedMessage;

    public ChannelService(EventDatabase eventDatabase)
    {
        this.eventDatabase = eventDatabase;
    }

    public void MessageProcessed(Event e)
    {
        if (!e.NoteMetadata.ChannelId.IsNullOrEmpty())
        {
            ChannelReceivedMessage?.Invoke(this, e.NoteMetadata.ChannelId);
        }
    }

    public void HandleChannelCreationOrMetadata(Event eventToProcess)
    {
        Channel? channelChanged = null;
        lock (channelLock)
        {
            var isUpdate = eventToProcess.Kind == NostrKind.ChannelMetadata;
            if (!string.IsNullOrEmpty(eventToProcess.Content))
            {
                ChannelDetails? channelDetails = null;
                try
                {
                    channelDetails = JsonSerializer.Deserialize<ChannelDetails>(eventToProcess.Content);
                }
                catch (Exception)
                {
                }

                if (channelDetails != null)
                {
                    string? channelId = null;
                    if (isUpdate)
                    {
                        var tag = eventToProcess.Tags.FirstOrDefault(t => t.TagIdentifier == "e" && t.Data.Count > 0);
                        if (tag != null)
                            channelId = tag.Data[0];
                    }
                    else
                    {
                        channelId = eventToProcess.Id;
                    }
                    if (!channelId.IsNullOrEmpty())
                    {
                        var channel = eventDatabase.GetChannel(channelId);

                        bool ok = true;
                        if (channel.CreatorId.IsNullOrEmpty())
                        {
                            if (!isUpdate)
                            {
                                channel.CreatorId = eventToProcess.PublicKey; // Creator is whoever sent the creation message
                            }
                        }
                        else if (isUpdate && channel.CreatorId != eventToProcess.PublicKey) // Ignore metadata from another account
                        {
                            ok = false;
                        }

                        if (ok)
                        {
                            if (!eventToProcess.CreatedAt.HasValue || !channel.DetailsLastUpdate.HasValue ||
                                eventToProcess.CreatedAt.Value > channel.DetailsLastUpdate.Value)
                            {
                                channel.Details = channelDetails;
                                channel.DetailsLastUpdate = eventToProcess.CreatedAt ?? DateTimeOffset.UtcNow;
                                eventDatabase.SaveChannel(channel);
                                channelChanged = channel;
                            }
                        }
                    }
                }
            }

            eventToProcess.Processed = true;
            eventDatabase.SaveEvent(eventToProcess);
        }
        if (channelChanged != null)
        {
            ChannelDetailsChanged?.Invoke(this, (channelChanged.Id, channelChanged.Details));
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

    //public void HandleKindChannelMessage(Event eventToProcess)
    //{
    //    var messageMetadata = eventToProcess.MessageMetadata = new MessageMetadata();

    //    string replyToId, rootId, relayReplay, relayRoot;
    //    (replyToId, relayReplay) = eventToProcess.Tags
    //        .Where(t => t.TagIdentifier == "e" && t.Data.Count == 3 && t.Data[2] == "reply")
    //        .Select(t => (t.Data[0], t.Data[1]))
    //        .FirstOrDefault();
    //    (rootId, relayRoot) = eventToProcess.Tags
    //        .Where(t => t.TagIdentifier == "e" && t.Data.Count == 3 && t.Data[2] == "root")
    //        .Select(t => (t.Data[0], t.Data[1]))
    //        .FirstOrDefault();

    //    // It's not specified in NIP-28 but many clients do the same for channel messages that for kind=1 notes, so we copy the logic
    //    if (string.IsNullOrEmpty(replyToId))
    //    {
    //        var e = eventToProcess.Tags.Where(t => t.TagIdentifier == "e").ToList();
    //        switch (e.Count)
    //        {
    //            case 1:
    //                var edata = e.Last().Data;
    //                if (edata.Count > 0)
    //                {
    //                    replyToId = edata[0];
    //                    if (edata.Count > 1) relayReplay ??= edata[1];
    //                }
    //                break;
    //            case > 1:
    //                replyToId = e.Last().Data[0];
    //                rootId ??= e.First().Data[0];
    //                break;
    //        }
    //    }
    //    if (Utils.IsValidNostrId(replyToId))
    //    {
    //        messageMetadata.ReplyToId = replyToId;
    //    }
    //    if (Utils.IsValidNostrId(rootId))
    //    {
    //        messageMetadata.ChannelId = rootId;
    //    }
    //    //foreach (var relay in new[] { relayRoot, relayReplay })
    //    //{
    //    //    if (!string.IsNullOrEmpty(relay))
    //    //    {
    //    //        HandleRelayRecommendation(relay);
    //    //    }
    //    //}

    //    // Mentions
    //    // NIP-08: https://github.com/nostr-protocol/nips/blob/master/08.md
    //    //for (int index = 0; index < eventToProcess.Tags.Count; index++)
    //    //{
    //    //    var tag = eventToProcess.Tags[index];
    //    //    if (tag.Data.Count > 0)
    //    //    {
    //    //        switch (tag.TagIdentifier)
    //    //        {
    //    //            case "p":
    //    //                noteMetadata.AccountMentions[index] = tag.Data[0].ToLower(); break;
    //    //            case "e":
    //    //                noteMetadata.EventMentions[index] = tag.Data[0].ToLower(); break;
    //    //            case "t":
    //    //                var hashtag = tag.Data[0].ToLower();
    //    //                if (!noteMetadata.HashTags.Contains(hashtag))
    //    //                    noteMetadata.HashTags.Add(tag.Data[0].ToLower());
    //    //                break;
    //    //        }
    //    //    }
    //    //}

    //    eventToProcess.Processed = true;
    //    eventDatabase.SaveEvent(eventToProcess);
    //}
}

