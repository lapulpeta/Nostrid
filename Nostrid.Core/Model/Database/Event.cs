namespace Nostrid.Model;

public class Event
{
    public string Id { get; set; }

    public string PublicKey { get; set; }

    public DateTime? CreatedAt { get; set; }

    public int Kind { get; set; }

    public string? Content { get; set; }

    public List<TagData> Tags { get; set; } = new();

    public string Signature { get; set; }

    public bool Deleted { get; set; }

    public long CreatedAtCurated { get; set; }

    public int Difficulty { get; set; }

    public bool HasPow { get; set; }

    public bool Broadcast { get; set; }

    public bool CanEcho { get; set; }

    public string? ReplyToId { get; set; }

    public string? ReplyToRootId { get; set; }

    public string? RepostEventId { get; set; }

    public string? ChannelId { get; set; }

    #region Properties (move to extension when supported by C#)

    private Lazy<List<Mention>>? _mentions = null;

    public List<string> GetMentionsIds(char type)
    {
        return GetMentions().Where(m => m.Type == type).Select(m => m.MentionId).ToList();
    }

    public List<string> GetMentionsIdsPresentInContent(char type)
    {
        return GetMentions().Where(m => m.Type == type && Content?.IndexOf($"#[{m.Index}]") > -1).Select(m => m.MentionId).ToList();
    }

    public List<Mention> GetMentions()
    {
        _mentions ??= new(() =>
        {
            // NIP-08: https://github.com/nostr-protocol/nips/blob/master/08.md
            var mentions = new List<Mention>();
            for (int index = 0; index < Tags.Count; index++)
            {
                var tag = Tags[index];
                if (tag.DataCount > 1)
                {
                    switch (tag.Data0)
                    {
                        case "e":
                        case "p":
                            mentions.Add(new Mention() { Type = tag.Data0[0], Index = index, MentionId = tag.Data1.ToLower() });
                            break;
                    }
                }
            }
            return mentions;

        });
        return _mentions.Value;
    }
    #endregion

    public override bool Equals(object obj)
    {
        return obj is Event @event &&
               Id == @event.Id;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id);
    }
}

