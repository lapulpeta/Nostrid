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

    public NoteTree? Find(string id, out bool exceededMax, int? maxChildAllowed = null)
    {
        if (Note.Id == id)
        {
            exceededMax = false;
            return this;
        }
        return Children.Find(id, out exceededMax, maxChildAllowed);
    }

    public bool Exists(string id)
    {
        return Note.Id == id || Children.Exists(id);
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

    public static NoteTree? Find(this List<NoteTree> chain, string id, out bool exceededMax, int? maxChildAllowed = null)
    {
        exceededMax = false;
        for (int i = 0; i < chain.Count; i++)
        {
            var ret = chain[i].Find(id, out exceededMax, maxChildAllowed);
            if (maxChildAllowed.HasValue)
                exceededMax |=  i >= maxChildAllowed;
            if (ret != null) return ret;
        }
        return null;
    }

    public static List<Event> AllNotes(this List<NoteTree> chain)
    {
        return chain.SelectMany(child => child.AllNotes()).ToList();
    }
}