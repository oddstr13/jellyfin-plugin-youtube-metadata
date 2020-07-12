using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using System.Linq;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.YoutubeMetadata.Providers
{
    public class InfoJson
    {
        public string id { get; set; }
        public string extractor_key { get; set; }
        // Human name
        public string uploader { get; set; }
        public int timestamp { get; set; }
        public string uploader_id { get; set; }
        public string upload_date { get; set; }
        public string release_date { get; set; }
        // https://github.com/ytdl-org/youtube-dl/issues/1806
        public string title { get; set; }
        public string fulltitle { get; set; }
        public string description { get; set; }
        public string thumbnail { get; set; }
        // Name for use in API?
        public string channel_id { get; set; }
        //public string
        public int? season_number { get; set; }
        public int? episode_number { get; set; }
        public int? age_limit { get; set; }
        public string series { get; set; }
        public List<string> tags { get; set; }
        public List<string> categories { get; set; }
        public string playlist_id { get; set; }
    }

    public class YoutubeLocalProvider : IHasItemChangeMonitor,
        ILocalMetadataProvider<Movie>,
        ILocalMetadataProvider<Episode>
    {
        private readonly ILogger<YoutubeLocalProvider> _logger;
        private readonly IJsonSerializer _json;
        private readonly IFileSystem _fileSystem;
        public readonly Dictionary<string, string> extractorKeyMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            {"NRKTV", "NRK"},
        };

        public YoutubeLocalProvider(IFileSystem fileSystem, IJsonSerializer json, ILogger<YoutubeLocalProvider> logger)
        {
            _fileSystem = fileSystem;
            _logger = logger;
            _json = json;
        }

        public string Name => "YouTube Metadata";

        private FileSystemMetadata GetInfoJsonFile(string path)
        {
            var fileInfo = _fileSystem.GetFileSystemInfo(path);
            var directoryInfo = fileInfo.IsDirectory ? fileInfo : _fileSystem.GetDirectoryInfo(Path.GetDirectoryName(path));
            var directoryPath = directoryInfo.FullName;
            var specificFile = Path.Combine(directoryPath, Path.GetFileNameWithoutExtension(path) + ".info.json");
            var file = _fileSystem.GetFileInfo(specificFile);
            return file;
        }

        public bool HasChanged(BaseItem item, IDirectoryService directoryService)
        {
            var infoJson = GetInfoJsonFile(item.Path);
            var result = infoJson.Exists && _fileSystem.GetLastWriteTimeUtc(infoJson) > item.DateLastSaved;
            return result;
        }

        private void UpdateItemMetadata(BaseItem item, InfoJson infoJson) {
            var extractor = extractorKeyMapping.GetValueOrDefault(infoJson.extractor_key, infoJson.extractor_key);

            item.ProviderIds = new Dictionary<string, string> {
                { extractor, infoJson.id}
            };

            // Bug in the NRK extractors - playlist_id is what is needed to produce a valid URL.
            if (extractor == "NRK") {
                item.ProviderIds[extractor] = infoJson.playlist_id;
            }

            item.Name = infoJson.fulltitle;
            item.Overview = infoJson.description;

            if (!(infoJson.release_date is null) || !(infoJson.upload_date is null)) {
                var date = DateTime.ParseExact(infoJson.release_date ?? infoJson.upload_date, "yyyyMMdd", null);
                item.ProductionYear = date.Year;
                item.PremiereDate = date;
            } else if (infoJson.timestamp > 0) {
                var date = DateTime.UnixEpoch.AddSeconds(infoJson.timestamp);
                item.ProductionYear = date.Year;
                item.PremiereDate = date;
            }

            foreach (string tag in infoJson.tags ?? Enumerable.Empty<string>()) {
                if (!string.IsNullOrWhiteSpace(tag)) {
                    item.AddTag(tag.Trim());
                }
            }

            foreach (string category in infoJson.categories ?? Enumerable.Empty<string>()) {
                if (!string.IsNullOrWhiteSpace(category)) {
                    item.AddGenre(category.Trim());
                }
            }
        }

        private void AddPersons<T>(MetadataResult<T> result, InfoJson infoJson) where T: IHasProviderIds {
            var extractor = extractorKeyMapping.GetValueOrDefault(infoJson.extractor_key, infoJson.extractor_key);

            if (!(infoJson.channel_id is null) || !(infoJson.uploader_id is null) || !(infoJson.uploader is null)) {
                var name = infoJson.uploader ?? infoJson.uploader_id;
                var id = infoJson.channel_id ?? infoJson.uploader_id ?? infoJson.uploader;
                var uploader = new PersonInfo
                {
                    Name = name,
                    Type = PersonType.Director,
                    ProviderIds = new Dictionary<string, string> {
                        { extractor, id },
                        { string.Format("ytdl:{0}", extractor), id },
                        { "ytdl",  string.Format("{0}:{1}", extractor, id) },
                    },
                };

                result.AddPerson(uploader);
            }
        }

        public Task<MetadataResult<Movie>> GetMetadata(ItemInfo info, IDirectoryService directoryService, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Movie>();
            try
            {
                var infoJsonFile = GetInfoJsonFile(info.Path);

                var infoJson = ReadInfoJson(infoJsonFile.FullName, cancellationToken);
                if (!(infoJson.series is null)) {
                    result.HasMetadata = false;
                    return Task.FromResult(result);
                }

                result.HasMetadata = true;
                var item = new Movie();
                result.Item = item;

                UpdateItemMetadata(item, infoJson);
                AddPersons<Movie>(result, infoJson);

                return Task.FromResult(result);
            }
            catch (FileNotFoundException)
            {
                _logger.LogInformation("Could not find {0}", info.Path);
                result.HasMetadata = false;
                return Task.FromResult(result);
            }
        }

        Task<MetadataResult<Episode>> ILocalMetadataProvider<Episode>.GetMetadata(ItemInfo info, IDirectoryService directoryService, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Episode>();
            try
            {
                var infoJsonFile = GetInfoJsonFile(info.Path);

                var infoJson = ReadInfoJson(infoJsonFile.FullName, cancellationToken);
                if (infoJson.series is null) {
                    result.HasMetadata = false;
                    return Task.FromResult(result);
                }

                result.HasMetadata = true;
                var item = new Episode();
                result.Item = item;

                item.SeriesName = infoJson.series;
                item.IndexNumber = infoJson.episode_number;
                item.ParentIndexNumber = infoJson.season_number;

                UpdateItemMetadata(item, infoJson);
                AddPersons<Episode>(result, infoJson);

            }
            catch (FileNotFoundException)
            {
                _logger.LogInformation("Could not find {0}", info.Path);
                result.HasMetadata = false;
            }

            return Task.FromResult(result);
        }

        private InfoJson ReadInfoJson(string metaFile, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return _json.DeserializeFromFile<InfoJson>(metaFile);
        }
    }
}
