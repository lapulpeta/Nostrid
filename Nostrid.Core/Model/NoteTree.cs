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
        if (Note.Id == id) return this;
        return Children.Find(id);
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
        foreach (var tw in chain)
        {
            var ret = tw.Find(id);
            if (ret != null) return ret;
        }
        return null;
    }

    public static List<Event> AllNotes(this List<NoteTree> chain)
    {
        return chain.SelectMany(child => child.AllNotes()).ToList();
    }
}