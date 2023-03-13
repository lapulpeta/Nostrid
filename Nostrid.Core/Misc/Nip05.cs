using Nostrid.Misc;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

namespace Nostrid.Data
{
    // NIP-05: https://github.com/nostr-protocol/nips/blob/master/05.md
    public static partial class Nip05
    {
        private static readonly Regex _nip05Regex = Nip05Regex();

        public static bool IsValidNip05(string nip05id)
        {
            if (!string.IsNullOrEmpty(nip05id))
            {
                var match = _nip05Regex.Match(nip05id);
                return match.Success;
            }
            return false;
        }

        public static bool DecodeNip05(string nip05id, out string? domain, out string? username)
        {
            if (!string.IsNullOrEmpty(nip05id))
            {
                var match = _nip05Regex.Match(nip05id);
                if (match.Success)
                {
                    domain = match.Groups["domain"].Value;
                    username = match.Groups["username"].Value;
                    return true;
                }
            }
            domain = username = null;
            return false;
        }

        public static async Task<(string? Domain, string? Username, string? Pubkey)> IdToPubkey(string nip05id)
        {
            if (DecodeNip05(nip05id, out var domain, out var username) && username.IsNotNullOrEmpty())
            {
                var handler = new HttpClientHandler()
                {
                    AllowAutoRedirect = false
                };
                using var client = new HttpClient(handler);
                try
                {
                    using var response = await client.GetAsync($"https://{domain}/.well-known/nostr.json?name={username}");
                    if (response.IsSuccessStatusCode)
                    {
                        var nipData = await response.Content.ReadFromJsonAsync<Nip05Root>();
                        if ((nipData?.names?.TryGetValue(username, out var hex) ?? false) && Utils.IsValidNostrId(hex))
                        {
                            return (domain, username, hex);
                        }
                    }
                }
                catch
                {
                }
            }
            return (null, null, null);
        }

        public static async Task<bool> RefreshNip05(string accountId, string nip05Id)
        {
            var (_, _, pubkey) = await IdToPubkey(nip05Id);
            if (pubkey != accountId)
                return false;

            //details.Nip05Data = new Nip05Data()
            //{
            //    Username = nip05responseV.Username,
            //    Domain = nip05responseV.Domain
            //};

            return true;
        }

        public class Nip05Root
        {
            public Dictionary<string, string> names { get; set; }
            public Dictionary<string, string> relays { get; set; }
        }

        [GeneratedRegex("^(?<username>[\\w-\\.]+)@(?<domain>([\\w-]+\\.)+[\\w-]{2,4}$)", RegexOptions.Compiled)]
        private static partial Regex Nip05Regex();
    }
}
