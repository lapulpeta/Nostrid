using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Nostrid.Externals
{
    public partial class VoidCatMediaService : IMediaService
    {
        private static Regex _linkRegex = LinkRegex();

        public async Task<Uri?> UploadFile(byte[] data, string filename, string mimeType)
        {
            try
            {
                using var httpClient = new HttpClient();
                using var fileContent = new ByteArrayContent(data);
                var sha256 = Convert.ToHexString(SHA256.HashData(data));
                fileContent.Headers.Add("V-Full-Digest", sha256);
                fileContent.Headers.Add("V-Filename", filename);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
                var response = await httpClient.PostAsync("https://void.cat/upload?cli=true", fileContent);


                if (response.IsSuccessStatusCode)
                {
                    var responseText = await response.Content.ReadAsStringAsync();
                    if (responseText.IsNotNullOrEmpty())
                    {
                        var match = _linkRegex.Match(responseText);
                        if (match.Success)
                        {
                            var link = $"https://{match.Groups["link"].Value}";
                            if (Uri.IsWellFormedUriString(link, UriKind.Absolute))
                            {
                                return new Uri(link);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {

            }
            return null;
        }

        [GeneratedRegex("(http(s?):\\/\\/(?<link>[^ ]*))")]
        private static partial Regex LinkRegex();
    }
}
