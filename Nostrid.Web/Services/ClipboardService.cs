using Microsoft.JSInterop;
using Nostrid.Interfaces;

namespace Nostrid.Web.Services
{
    public class ClipboardService : IClipboardService
    {
        private readonly IJSRuntime jsInterop;

        public ClipboardService(IJSRuntime jsInterop)
        {
            this.jsInterop = jsInterop;
        }

        public async Task CopyAsync(string content)
        {
            await jsInterop.InvokeVoidAsync("navigator.clipboard.writeText", content);
        }
    }
}