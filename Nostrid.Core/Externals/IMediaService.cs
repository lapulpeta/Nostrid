namespace Nostrid.Externals
{
    public interface IMediaService
    {
        public Task<Uri?> UploadFile(byte[] data, string filename, string mimeType);
    }
}
