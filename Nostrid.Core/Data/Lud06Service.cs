using Nostrid.Misc;
using Nostrid.Model;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

namespace Nostrid.Data
{
    // LUD-16: https://github.com/lnurl/luds/blob/luds/16.md
    // LUD-06: https://github.com/lnurl/luds/blob/luds/06.md
    public partial class Lud06Service
    {
        private readonly Regex lud16Regex = Lud16Regex();

        public bool IsValidLud16(string? lud16id)
        {
            if (lud16id.IsNotNullOrEmpty())
            {
                var match = lud16Regex.Match(lud16id);
                return match.Success;
            }
            return false;
        }

        public bool TryLud06UrlToUrl(string? lud06, out string? url)
        {
            if (lud06.IsNotNullOrEmpty())
            {
                try
                {
                    url = ByteTools.Bech32ToString(lud06.ToLower(), "lnurl");
                    if (url.IsNotNullOrEmpty() && Uri.IsWellFormedUriString(url, UriKind.Absolute))
                    {
                        return true;
                    }
                }
                catch (FormatException)
                {
                }
            }
            url = null;
            return false;
        }

        public async Task<Lud06Data?> Query(string? ln)
        {
            if (IsValidLud16(ln))
            {
                return await QueryLud16(ln);
            }
            else if (TryLud06UrlToUrl(ln, out var url))
            {
                return await QueryUrl(url);
            }
            return null;
        }

        public async Task<Lud06Data?> QueryUrl(string? url)
        {
            if (!string.IsNullOrEmpty(url) && Uri.IsWellFormedUriString(url, UriKind.Absolute))
            {
                var handler = new HttpClientHandler()
                {
                    AllowAutoRedirect = false
                };
                using var client = new HttpClient(handler);
                using var response = await client.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        var ludData = await response.Content.ReadFromJsonAsync<Lud16Root>();
                        if (ludData?.tag == "payRequest")
                        {
                            return new Lud06Data()
                            {
                                Callback = ludData.callback,
                                MinSendableMsat = ludData.minSendable,
                                MaxSendableMsat = ludData.maxSendable,
                            };
                        }
                    }
                    catch
                    {
                    }
                }
            }
            return null;
        }

        public async Task<Lud06Data?> QueryLud16(string? lud16id)
        {
            if (!string.IsNullOrEmpty(lud16id))
            {
                var match = lud16Regex.Match(lud16id);
                if (match.Success)
                {
                    var domain = match.Groups["domain"].Value;
                    var username = match.Groups["username"].Value;

                    var scheme = domain.EndsWith("onion") ? "http" : "https";
                    return await QueryUrl($"{scheme}://{domain}/.well-known/lnurlp/{username}");

                }
            }
            return null;
        }

        public async Task<string?> GetPayReq(Lud06Data ludData, long amountMsat)
        {
            if (ludData != null)
            {
                using var client = new HttpClient();
                var separator = ludData.Callback.Contains('?') ? "&" : "?";
                using var response = await client.GetAsync($"{ludData.Callback}{separator}amount={amountMsat}");

                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        var prData = await response.Content.ReadFromJsonAsync<Lud06Root>();
                        return prData?.pr; // TODO: check it's a valid pay req
                    }
                    catch
                    {
                    }
                }
            }
            return null;
        }

        public class Lud16Root
        {
            public string callback { get; set; }
            public long maxSendable { get; set; }
            public int minSendable { get; set; }
            public string metadata { get; set; }
            public int commentAllowed { get; set; }
            public string tag { get; set; }
        }

        public class Lud06Root
        {
            public string pr { get; set; }
        }

        [GeneratedRegex("^(?<username>[a-z0-9-_.]+)@(?<domain>([\\w-]+\\.)+[\\w-]{2,4}$)", RegexOptions.Compiled)]
        private static partial Regex Lud16Regex();
    }
}
