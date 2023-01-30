using Newtonsoft.Json;
using Nostrid.Misc;
using System.Net.Http.Headers;

namespace Nostrid.Externals
{
    public class NostrBuildMediaService : IMediaService
    {
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
            var response = await httpClient.PostAsync("https://nostr.build/api/upload/nostrid.php", httpContent);
            if (response.IsSuccessStatusCode)
            {
                var responseText = await response.Content.ReadAsStringAsync();
                if (responseText.IsNotNullOrEmpty())
                {
                    var link = JsonConvert.DeserializeObject<string>(responseText);
                    if (Uri.IsWellFormedUriString(link, UriKind.Absolute))
                    {
                        return new Uri(link);
                    }
                }
            }
            return null;
        }
    }
}
