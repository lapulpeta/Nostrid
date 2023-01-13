namespace Nostrid.Model;

public class NoteTree
{
    public Event Note { get; set; }

    public string AccountName { get; set; }

    public string PictureUrl { get; set; }

    public List<NoteTree> Children { get; set; } = new();

    public NoteTree(Event note)
    {
        Note = note;
    }

    public NoteTree Find(string id)
    {
        if (Note.Id == id) return this;
        return Children.Find(id);
    }

    public bool Exists(string id)
    {
        return Note.Id == id || Children.Exists(id);
    }
}

public static class NoteTreeExtensions
{
    public static bool Exists(this List<NoteTree> chain, string id)
    {
        return chain.Any(c => c.Exists(id));
    }

    public static NoteTree Find(this List<NoteTree> chain, string id)
    {
        foreach (var tw in chain)
        {
            var ret = tw.Find(id);
            if (ret != null) return ret;
        }
        return null;
    }
}