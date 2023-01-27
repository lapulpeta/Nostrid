using Nostrid.Misc;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace Nostrid.Externals
{
    public partial class NostrBuildMediaService : IMediaService
    {
        private static Regex _linkRegex = LinkRegex();

        public string Name => "nostr.build";
        
        public int MaxSize { get => 50 * 1024 * 1024; }

        public async Task<Uri?> UploadFile(Stream data, string filename, string mimeType, Action<float> progress)
        {
            using var httpClient = new HttpClient();
            using var progressStream = new ProgressStream(data);
            progressStream.UpdateProgress += (s, e) => progress(e);
            using var httpContent = new MultipartFormDataContent();
            using var fileContent = new StreamContent(progressStream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
            httpContent.Add(fileContent, "fileToUpload", filename);
            httpContent.Add(new StringContent("Upload Image"), "submit");
            var response = await httpClient.PostAsync("https://nostr.build/upload.php", httpContent);
            if (response.IsSuccessStatusCode)
            {
                var responseText = await response.Content.ReadAsStringAsync();
                if (responseText.IsNotNullOrEmpty())
                {
                    var match = _linkRegex.Match(responseText);
                    if (match.Success)
                    {
                        var link = match.Groups["link"].Value;
                        if (Uri.IsWellFormedUriString(link, UriKind.Absolute))
                        {
                            return new Uri(link);
                        }
                    }
                }
            }
            return null;
        }

        [GeneratedRegex(".*(?<link>https://nostr.build/i/nostr.build_([0-9a-f]+).([0-9a-zA-Z]+)).*")]
        private static partial Regex LinkRegex();
    }
}
