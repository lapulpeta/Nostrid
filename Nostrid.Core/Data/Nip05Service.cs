using Nostrid.Model;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

namespace Nostrid.Data
{
    // NIP-05: https://github.com/nostr-protocol/nips/blob/master/05.md
    public partial class Nip05Service
    {
        private readonly Regex nip05Regex = Nip05Regex();

        public async Task<(string Domain, string Username, string Pubkey)?> IdToPubkey(string nip05id)
        {
            if (!string.IsNullOrEmpty(nip05id))
            {
                var match = nip05Regex.Match(nip05id);
                if (match.Success)
                {
                    var domain = match.Groups["domain"].Value;
                    var username = match.Groups["username"].Value;

                    var handler = new HttpClientHandler()
                    {
                        AllowAutoRedirect = false
                    };
                    using var client = new HttpClient(handler);
                    using var response = await client.GetAsync($"https://{domain}/.well-known/nostr.json?name={username}");

                    if (response.IsSuccessStatusCode)
                    {
                        try
                        {
                            var nipData = await response.Content.ReadFromJsonAsync<Nip05Root>();
                            if (nipData?.names?.TryGetValue(username, out var hex) ?? false)
                            {
                                return (domain, username, hex);
                            }
                        }
                        catch
                        {
                        }
                    }
                }
            }
            return null;
        }

        public async Task<bool> RefreshNip05(string accountId, AccountDetails details)
        {
            var nip05response = await IdToPubkey(details.Nip05Id);
            if (nip05response == null)
                return false;

            var nip05responseV = nip05response.Value;

            if (nip05responseV.Pubkey != accountId)
                return false;

            details.Nip05Data = new Nip05Data()
            {
                Username = nip05responseV.Username,
                Domain = nip05responseV.Domain
            };

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
