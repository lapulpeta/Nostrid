namespace Nostrid.Model;

public class NoteMetadata
{
    public string ReplyToId { get; set; }

    public string ReplyToRootId { get; set; }

    public List<Reaction> Reactions { get; set; } = new();

    public List<string> HashTags { get; set; } = new();

    public Dictionary<int, string> AccountMentions { get; set; } = new();

    public Dictionary<int, string> EventMentions { get; set; } = new();

	public string RepostEventId { get; set; }
}

