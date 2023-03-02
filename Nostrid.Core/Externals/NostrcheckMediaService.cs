using Newtonsoft.Json;
using Nostrid.Misc;

namespace Nostrid.Externals
{
    public partial class NostrcheckMediaService : IMediaService
    {
        public string Name => "nostrcheck.me";

        public int MaxSize => 5 * 1024 * 1024;

        public async Task<Uri?> UploadFile(Stream data, string filename, string mimeType, Action<float> progress)
        {
            using var progressStream = new ProgressStream(data);
            progressStream.UpdateProgress += (s, e) => progress(e);

            using var httpContent = new MultipartFormDataContent
            {
                { new StreamContent(progressStream), "publicgallery", filename },
                { new StringContent("26d075787d261660682fb9d20dbffa538c708b1eda921d0efa2be95fbef4910a"), "apikey" }
            };

            using var httpClient = new HttpClient();
            var response = await httpClient.PostAsync("https://nostrcheck.me/api/media.php", httpContent);

            if (response.IsSuccessStatusCode)
            {
                var responseText = await response.Content.ReadAsStringAsync();
                if (responseText.IsNotNullOrEmpty())
                {
                    var responseObj = JsonConvert.DeserializeObject<NostrcheckRoot>(responseText);
                    if (responseObj != null && responseObj.Status && Uri.IsWellFormedUriString(responseObj.Url, UriKind.Absolute))
                    {
                        return new Uri(responseObj.Url);
                    }
                }
            }
            return null;
        }

        private class NostrcheckRoot
        {
            [JsonProperty("apikey")]
            public string? ApiKey { get; set; }

            [JsonProperty("request")]
            public string? Request { get; set; }

            [JsonProperty("filesize")]
            public int FileSize { get; set; }

            [JsonProperty("message")]
            public string? Message { get; set; }

            [JsonProperty("status")]
            public bool Status { get; set; }

            [JsonProperty("URL")]
            public string? Url { get; set; }
        }
    }
}
