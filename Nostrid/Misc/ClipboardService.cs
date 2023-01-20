using Nostrid.Interfaces;

namespace Nostrid.Misc
{
    public class ClipboardService : IClipboardService
    {
        public async Task CopyAsync(string content)
        {
            await Clipboard.Default.SetTextAsync(content);
        }
    }
}
