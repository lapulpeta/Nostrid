namespace Nostrid.Model;

public class NoteTreeNode
{
}

public class NoteTree : NoteTreeNode
{
    public NoteTree? Parent { get; set; }

    public Event Note { get; set; }

    public List<NoteTree> Children { get; set; } = new();


    public int RenderLevel => Parent == null ? 0 : // If no parent then level 0
        Parent.RenderLevel + (Parent.Children.Count > 1 ? 1 : 0); // Else we have one level more than the parent, unless we're only child

    public NoteTree(Event note, NoteTree? parent = null)
    {
        Note = note;
        Parent = parent;
    }

    public NoteTree? Find(string id)
    {
        return Find(id, out _);
    }

    public NoteTree? Find(string id, out bool exceededMax, int startChildIndex = 0, int? maxChildAllowed = null)
    {
        var isReplaceableId = EventExtension.IsReplaceableId(id);
        if ((!isReplaceableId && Note.Id == id) || (isReplaceableId && Note.ReplaceableId == id))
        {
            exceededMax = false;
            return this;
        }
        return Children.Find(id, out exceededMax, startChildIndex, maxChildAllowed);
    }

    public bool Exists(string id)
    {
		var isReplaceableId = EventExtension.IsReplaceableId(id);
		return (!isReplaceableId && Note.Id == id) || (isReplaceableId && Note.ReplaceableId == id) || Children.Exists(id);
    }

    public List<Event> AllNotes()
    {
        return new[] { Note }.Union(Children.AllNotes()).ToList();
    }
}

public static class NoteTreeExtensions
{
    public static bool Exists(this List<NoteTree> chain, string id)
    {
        return chain.Any(c => c.Exists(id));
    }

    public static NoteTree? Find(this List<NoteTree> chain, string id)
    {
        return Find(chain, id, out _);
    }

    public static NoteTree? Find(this List<NoteTree> chain, string id, out bool exceededMax, int startChildIndex = 0, int? maxChildAllowed = null)
    {
        exceededMax = false;
        for (int i = startChildIndex; i < chain.Count; i++)
        {
            var ret = chain[i].Find(id, out exceededMax);
            if (maxChildAllowed.HasValue)
                exceededMax |=  i - startChildIndex >= maxChildAllowed;
            if (ret != null) return ret;
        }
        return null;
    }

    public static List<Event> AllNotes(this List<NoteTree> chain)
    {
        return chain.SelectMany(child => child.AllNotes()).ToList();
    }
}