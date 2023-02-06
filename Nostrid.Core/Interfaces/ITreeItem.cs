using Nostrid.Model;

namespace Nostrid.Interfaces
{
    public interface ITreeItem
    {
        NoteTree Tree { get; }

        IEnumerable<ITreeItem> GetTreeItems();

        Task<bool> IsVisibleAsync();
    }
}
