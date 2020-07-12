using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.YoutubeMetadata
{
    public class YoutubeExternalId : IExternalId
    {
        /// <inheritdoc />
        public string ProviderName => "YouTube";

        /// <inheritdoc />
        public string Key => "Youtube";

        /// <inheritdoc />
        public ExternalIdMediaType? Type => null;

        /// <inheritdoc />
        public string UrlFormatString => "https://www.youtube.com/watch?v={0}";

        /// <inheritdoc />
        public bool Supports(IHasProviderIds item)
        {
            return item is Movie || item is MusicVideo || item is Episode || item is Trailer;
        }
    }

    public class NrkExternalId : IExternalId
    {
        /// <inheritdoc />
        public string ProviderName => "NRK";

        /// <inheritdoc />
        public string Key => "NRK";

        /// <inheritdoc />
        public ExternalIdMediaType? Type => null;

        /// <inheritdoc />
        public string UrlFormatString => "https://tv.nrk.no/program/{0}";

        /// <inheritdoc />
        public bool Supports(IHasProviderIds item)
        {
            return item is Movie || item is MusicVideo || item is Episode || item is Trailer;
        }
    }
}
