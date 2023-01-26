namespace Nostrid.Externals
{
    public class MediaServiceProvider
    {
        private List<IMediaService> _mediaServices = new();

        public MediaServiceProvider(IEnumerable<IMediaService> mediaServices)
        {
            _mediaServices = new(mediaServices);
        }

        public void RegisterMediaService(IMediaService service)
        {
            _mediaServices.Add(service);
        }

        public IEnumerable<IMediaService> GetMediaServices()
        {
            return _mediaServices.AsReadOnly();
        }
    }
}
