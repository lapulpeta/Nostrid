using Nostrid.Misc;
using System.IO;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Nostrid.Externals
{
    public partial class VoidCatMediaService : IMediaService
    {
        private static Regex _linkRegex = LinkRegex();

        public string Name => "void.cat";

        public int MaxSize { get => 5 * 1024 * 1024; }

        public async Task<Uri?> UploadFile(Stream data, string filename, string mimeType, Action<float> progress)
        {
            var sha256 = Convert.ToHexString(SHA256.HashData(data));
            data.Position = 0;

            using var httpClient = new HttpClient();
            using var progressStream = new ProgressStream(data);
            progressStream.UpdateProgress += (s, e) => progress(e);
            using var fileContent = new StreamContent(progressStream);
            fileContent.Headers.Add("V-Full-Digest", sha256);
            fileContent.Headers.Add("V-Filename", filename);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
            using var response = await httpClient.PostAsync("https://void.cat/upload?cli=true", fileContent);

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
            return null;
        }

        [GeneratedRegex("(http(s?):\\/\\/(?<link>[^ ]*))")]
        private static partial Regex LinkRegex();
    }
}
