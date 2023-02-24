using Nostrid.Interfaces;
using System.Threading.Tasks;

internal class ClipboardService : IClipboardService
{
    public async Task CopyAsync(string content)
    {
        await TextCopy.ClipboardService.SetTextAsync(content);
    }
}