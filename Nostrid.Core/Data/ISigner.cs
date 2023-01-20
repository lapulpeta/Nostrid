using NNostr.Client;

namespace Nostrid.Data
{
    public interface ISigner
    {
        public Task<string?> GetPubKey();

        public Task<bool> Sign(NostrEvent ev);
    }
}
