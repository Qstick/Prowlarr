using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Http;
using NzbDrone.Core.Indexers.Exceptions;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Indexers.Rarbg
{
    public class RarbgParser : IParseIndexerResponse
    {
        private static readonly Regex RegexGuid = new Regex(@"^magnet:\?xt=urn:btih:([a-f0-9]+)", RegexOptions.Compiled);

        private readonly IndexerCapabilities _capabilities;

        public RarbgParser(IndexerCapabilities capabilities)
        {
            _capabilities = capabilities;
        }

        public IList<ReleaseInfo> ParseResponse(IndexerResponse indexerResponse)
        {
            var results = new List<ReleaseInfo>();

            switch (indexerResponse.HttpResponse.StatusCode)
            {
                default:
                    if (indexerResponse.HttpResponse.StatusCode != HttpStatusCode.OK)
                    {
                        throw new IndexerException(indexerResponse, "Indexer API call returned an unexpected StatusCode [{0}]", indexerResponse.HttpResponse.StatusCode);
                    }

                    break;
            }

            var jsonResponse = new HttpResponse<RarbgResponse>(indexerResponse.HttpResponse);

            if (jsonResponse.Resource.error_code.HasValue)
            {
                if (jsonResponse.Resource.error_code == 20 || jsonResponse.Resource.error_code == 8
                    || jsonResponse.Resource.error_code == 9 || jsonResponse.Resource.error_code == 10)
                {
                    // No results or imdbid not found
                    return results;
                }

                throw new IndexerException(indexerResponse, "Indexer API call returned error {0}: {1}", jsonResponse.Resource.error_code, jsonResponse.Resource.error);
            }

            if (jsonResponse.Resource.torrent_results == null)
            {
                return results;
            }

            foreach (var torrent in jsonResponse.Resource.torrent_results)
            {
                var torrentInfo = new TorrentInfo();

                torrentInfo.Guid = GetGuid(torrent);
                torrentInfo.Categories = _capabilities.Categories.MapTrackerCatDescToNewznab(torrent.category);
                torrentInfo.Title = torrent.title;
                torrentInfo.Size = torrent.size;
                torrentInfo.DownloadUrl = torrent.download;
                torrentInfo.InfoUrl = $"{torrent.info_page}&app_id={BuildInfo.AppName}";
                torrentInfo.PublishDate = torrent.pubdate.ToUniversalTime();
                torrentInfo.Seeders = torrent.seeders;
                torrentInfo.Peers = torrent.leechers + torrent.seeders;
                torrentInfo.DownloadVolumeFactor = 0;
                torrentInfo.UploadVolumeFactor = 1;

                if (torrent.movie_info != null)
                {
                    if (torrent.movie_info.tvdb != null)
                    {
                        torrentInfo.TvdbId = torrent.movie_info.tvdb.Value;
                    }

                    if (torrent.movie_info.tvrage != null)
                    {
                        torrentInfo.TvRageId = torrent.movie_info.tvrage.Value;
                    }
                }

                results.Add(torrentInfo);
            }

            return results;
        }

        public Action<IDictionary<string, string>, DateTime?> CookiesUpdater { get; set; }

        private string GetGuid(RarbgTorrent torrent)
        {
            var match = RegexGuid.Match(torrent.download);

            if (match.Success)
            {
                return string.Format("rarbg-{0}", match.Groups[1].Value);
            }
            else
            {
                return string.Format("rarbg-{0}", torrent.download);
            }
        }
    }
}
