using Newtonsoft.Json;
using Nostrid.Misc;
using System.Net.Http.Headers;

namespace Nostrid.Externals
{
    public class NostrImgMediaService : IMediaService
    {
        public string Name => "nostrimg.com";

        public int MaxSize { get => 5 * 1024 * 1024; }

        public async Task<Uri?> UploadFile(Stream data, string filename, string mimeType, Action<float> progress)
        {
            using var httpClient = new HttpClient();
            using var progressStream = new ProgressStream(data);
            progressStream.UpdateProgress += (s, e) => progress(e);
            using var httpContent = new MultipartFormDataContent();
            using var fileContent = new StreamContent(progressStream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
            httpContent.Add(fileContent, "image", filename);
            var response = await httpClient.PostAsync("https://nostrimg.com/api/upload", httpContent);
            if (response.IsSuccessStatusCode)
            {
                var responseText = await response.Content.ReadAsStringAsync();
                if (responseText.IsNotNullOrEmpty())
                {
                    var responseObj = JsonConvert.DeserializeObject<NostrImgRoot>(responseText);
                    if (responseObj != null && responseObj.Success && Uri.IsWellFormedUriString(responseObj.ImageUrl, UriKind.Absolute))
                    {
                        return new Uri(responseObj.ImageUrl);
                    }
                }
            }
            return null;
        }
    }

    public class NostrImgData
    {
        [JsonProperty("link")]
        public string Link { get; set; }
    }

    public class NostrImgRoot
    {
        [JsonProperty("data")]
        public NostrImgData Data { get; set; }

        [JsonProperty("route")]
        public string Route { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("imageUrl")]
        public string ImageUrl { get; set; }

        [JsonProperty("fileName")]
        public string FileName { get; set; }

        [JsonProperty("fileID")]
        public string FileID { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("lightningDestination")]
        public string LightningDestination { get; set; }

        [JsonProperty("lightningPaymentLink")]
        public string LightningPaymentLink { get; set; }

        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("status")]
        public int Status { get; set; }
    }
}
