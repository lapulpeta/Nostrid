namespace Nostrid.Model;

public class NoteTreeShowMore : NoteTreeNode
{
    public int ShowMoreChildIndex { get; set; }

    public string ParentEventId { get; set; }

    public bool Start { get; set; }

    public NoteTreeShowMore(int showMoreChildIndex, bool start, string parentEventId)
    {
        ShowMoreChildIndex = showMoreChildIndex;
        Start = start;
        ParentEventId = parentEventId;
    }
}
