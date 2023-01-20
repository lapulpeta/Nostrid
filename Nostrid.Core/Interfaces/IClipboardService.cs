namespace Nostrid.Interfaces
{
    public interface IClipboardService
    {
        Task CopyAsync(string content);
    }
}
