using System.IO;
using System.Linq;
using System.Collections.Generic;
//using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Entities;
using MediaBrowser.Controller.Entities.TV;

namespace Jellyfin.Plugin.YoutubeMetadata.Providers
{
    public class YoutubeLocalImageProvider : ILocalImageProvider, IHasOrder
    {
        private readonly IFileSystem _fileSystem;
        private readonly ILogger<YoutubeLocalImageProvider> _logger;
        private readonly IImageProcessor _imageProcessor;

        public YoutubeLocalImageProvider(IServerConfigurationManager config, IFileSystem fileSystem, ILogger<YoutubeLocalImageProvider> logger, IImageProcessor imageProcessor)
        {
            _fileSystem = fileSystem;
            _logger = logger;
            _imageProcessor = imageProcessor;
        }

        public string Name => "YouTube Metadata";
        public int Order => 1;

        //public const string YTDL_THUMBNAILS_RE_STR = @"(_[0-9])?\.(jpe?g|webp|png|gif|tiff?)$";
        //private Regex _thumbnailsRe = new Regex(YTDL_THUMBNAILS_RE_STR, RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.ExplicitCapture);

        public List<LocalImageInfo> GetImages(BaseItem item, IDirectoryService directoryService)
        {
            _logger.LogInformation(item.Path);
            var list = new List<LocalImageInfo>();

            if (item?.ContainingFolderPath is null) {
                return list;
            }

            var files = _fileSystem.GetFiles(item.ContainingFolderPath);

            LocalImageInfo largestLocalImg = null;
            ImageDimensions largestSize = new ImageDimensions(0, 0);

            //_logger.LogDebug("item.FileNameWithoutExtension: {0}", item.FileNameWithoutExtension);
            foreach (FileSystemMetadata file in files) {
                //_logger.LogDebug("file.Name: {0}", file.Name);

                if (file.Name.StartsWith(item.FileNameWithoutExtension)) {
                    //_logger.LogDebug("File is related!");
                    //_logger.LogDebug(file.Extension);

                    //if (_thumbnailsRe.IsMatch(file.Name)) {
                    if (_imageProcessor.SupportedInputFormats.Contains(file.Extension.ToLower().TrimStart('.'))) {
                        //_logger.LogDebug("Regex matches!");
                        //_logger.LogDebug("Is image!");

                        ImageDimensions size = _imageProcessor.GetImageDimensions(file.FullName);

                        if (largestSize.Width < size.Width) {
                            //_logger.LogDebug("Image is larger!");
                            largestLocalImg = new LocalImageInfo {
                                FileInfo = file,
                                Type = ImageType.Primary
                            };
                            largestSize = size;
                        }
                    }
                }
            }

            if (!(largestLocalImg is null)) {
                _logger.LogDebug("Picking {0} with dimensions {1}x{2}", largestLocalImg.FileInfo.Name, largestSize.Width, largestSize.Height);
                list.Add(largestLocalImg);
            }

            return list;
        }

        public bool Supports(BaseItem item)
            => item is Movie || item is MusicVideo || item is Episode || item is Trailer;
    }
}
