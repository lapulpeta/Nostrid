namespace Nostrid.Externals
{
    public interface IMediaService
    {
        public string Name { get; }

        public int MaxSize { get; }

        public Task<Uri?> UploadFile(Stream data, string filename, string mimeType);

        public event EventHandler<float>? UpdateProgress;
    }
}
