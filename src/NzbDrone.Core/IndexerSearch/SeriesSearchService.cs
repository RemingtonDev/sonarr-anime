using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.Download;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.IndexerSearch
{
    public class SeriesSearchService : IExecute<SeriesSearchCommand>
    {
        private readonly ISeriesService _seriesService;
        private readonly IEpisodeService _episodeService;
        private readonly ISearchForReleases _releaseSearchService;
        private readonly IProcessDownloadDecisions _processDownloadDecisions;
        private readonly Logger _logger;

        public SeriesSearchService(ISeriesService seriesService,
                                   IEpisodeService episodeService,
                                   ISearchForReleases releaseSearchService,
                                   IProcessDownloadDecisions processDownloadDecisions,
                                   Logger logger)
        {
            _seriesService = seriesService;
            _episodeService = episodeService;
            _releaseSearchService = releaseSearchService;
            _processDownloadDecisions = processDownloadDecisions;
            _logger = logger;
        }

        public void Execute(SeriesSearchCommand message)
        {
            var series = _seriesService.GetSeries(message.SeriesId);
            var downloadedCount = 0;
            var userInvokedSearch = message.Trigger == CommandTrigger.Manual;

            if (series.Seasons.None(s => s.Monitored))
            {
                _logger.Debug("No seasons of {0} are monitored, searching for all monitored episodes", series.Title);

                var episodes = _episodeService.GetEpisodeBySeries(series.Id)
                    .Where(e => e.Monitored &&
                                !e.HasFile &&
                                e.AirDateUtc.HasValue &&
                                e.AirDateUtc.Value.Before(DateTime.UtcNow))
                    .ToList();

                foreach (var episode in episodes)
                {
                    var decisions = _releaseSearchService.EpisodeSearch(episode, userInvokedSearch, false).GetAwaiter().GetResult();
                    var processDecisions = _processDownloadDecisions.ProcessDecisions(decisions).GetAwaiter().GetResult();
                    downloadedCount += processDecisions.Grabbed.Count;
                }
            }
            else if (series.SeriesType == SeriesTypes.Anime)
            {
                var coveredEpisodeIds = new HashSet<int>();
                var broadQueriesEmitted = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

                // Search non-zero seasons first so a grabbed pack (e.g. S01+Specials)
                // can cover Season 0 specials before we search for them separately.
                foreach (var season in series.Seasons
                    .OrderBy(s => s.SeasonNumber == 0 ? 1 : 0)
                    .ThenBy(s => s.SeasonNumber))
                {
                    if (!season.Monitored)
                    {
                        _logger.Debug("Season {0} of {1} is not monitored, skipping search", season.SeasonNumber, series.Title);
                        continue;
                    }

                    // Check if all monitored, aired, missing episodes in this season are already covered
                    var seasonEpisodes = _episodeService.GetEpisodesBySeason(series.Id, season.SeasonNumber);
                    var wantedEpisodes = seasonEpisodes
                        .Where(e => e.Monitored &&
                                    !e.HasFile &&
                                    e.AirDateUtc.HasValue &&
                                    e.AirDateUtc.Value.Before(DateTime.UtcNow))
                        .ToList();

                    if (wantedEpisodes.Any() && wantedEpisodes.All(e => coveredEpisodeIds.Contains(e.Id)))
                    {
                        _logger.Debug(
                            "Anime cascade [{0} Season {1}]: all {2} wanted episodes already covered by earlier grabs, skipping season search",
                            series.Title,
                            season.SeasonNumber,
                            wantedEpisodes.Count);
                        continue;
                    }

                    if (wantedEpisodes.Any())
                    {
                        var alreadyCovered = wantedEpisodes.Count(e => coveredEpisodeIds.Contains(e.Id));

                        if (alreadyCovered > 0)
                        {
                            _logger.Debug(
                                "Anime cascade [{0} Season {1}]: {2}/{3} wanted episodes already covered by earlier grabs, searching season with partial coverage",
                                series.Title,
                                season.SeasonNumber,
                                alreadyCovered,
                                wantedEpisodes.Count);
                        }
                    }

                    var decisions = _releaseSearchService.SeasonSearch(
                        message.SeriesId,
                        season.SeasonNumber,
                        false,
                        true,
                        userInvokedSearch,
                        false,
                        broadQueriesEmitted,
                        coveredEpisodeIds).GetAwaiter().GetResult();
                    var processDecisions = _processDownloadDecisions.ProcessDecisions(decisions).GetAwaiter().GetResult();
                    downloadedCount += processDecisions.Grabbed.Count;

                    // Track episode IDs from grabbed results for cross-season skipping
                    foreach (var grabbed in processDecisions.Grabbed)
                    {
                        foreach (var ep in grabbed.RemoteEpisode.Episodes)
                        {
                            coveredEpisodeIds.Add(ep.Id);
                        }
                    }
                }
            }
            else
            {
                foreach (var season in series.Seasons.OrderBy(s => s.SeasonNumber))
                {
                    if (!season.Monitored)
                    {
                        _logger.Debug("Season {0} of {1} is not monitored, skipping search", season.SeasonNumber, series.Title);
                        continue;
                    }

                    var decisions = _releaseSearchService.SeasonSearch(message.SeriesId, season.SeasonNumber, false, true, userInvokedSearch, false).GetAwaiter().GetResult();
                    var processDecisions = _processDownloadDecisions.ProcessDecisions(decisions).GetAwaiter().GetResult();
                    downloadedCount += processDecisions.Grabbed.Count;
                }
            }

            _logger.ProgressInfo("Series search completed. {0} reports downloaded.", downloadedCount);
        }
    }
}
