using NNostr.Client;

namespace Nostrid.Data
{
    public interface ISigner
    {
        public Task<string?> GetPubKey();

        public Task<bool> Sign(NostrEvent ev);

        public Task<string?> EncryptNip04(string pubkey, string content);

        public Task<string?> DecryptNip04(string pubkey, string content);
    }
}
