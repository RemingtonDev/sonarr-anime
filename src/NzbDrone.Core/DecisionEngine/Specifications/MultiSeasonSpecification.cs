using System.Linq;
using NLog;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.DecisionEngine.Specifications
{
    public class MultiSeasonSpecification : IDownloadDecisionEngineSpecification
    {
        private readonly Logger _logger;

        public MultiSeasonSpecification(Logger logger)
        {
            _logger = logger;
        }

        public SpecificationPriority Priority => SpecificationPriority.Default;
        public RejectionType Type => RejectionType.Permanent;

        public virtual DownloadSpecDecision IsSatisfiedBy(RemoteEpisode subject, SearchCriteriaBase searchCriteria)
        {
            if (subject.ParsedEpisodeInfo.IsMultiSeason)
            {
                // Allow anime multi-season packs during anime season search, but only if the pack covers the searched season
                if (subject.Series.SeriesType == SeriesTypes.Anime &&
                    searchCriteria is AnimeSeasonSearchCriteria animeSeasonCriteria)
                {
                    if (subject.ParsedEpisodeInfo.SeasonNumbers.Length > 0 &&
                        !subject.ParsedEpisodeInfo.SeasonNumbers.Contains(animeSeasonCriteria.SeasonNumber))
                    {
                        _logger.Debug("Anime multi-season pack does not cover searched season {0}, skipping.", animeSeasonCriteria.SeasonNumber);
                        return DownloadSpecDecision.Reject(DownloadRejectionReason.WrongSeason, "Wrong season");
                    }

                    _logger.Debug("Anime multi-season release {0} accepted for anime season search", subject.Release.Title);
                    return DownloadSpecDecision.Accept();
                }

                _logger.Debug("Multi-season release {0} rejected. Not supported", subject.Release.Title);
                return DownloadSpecDecision.Reject(DownloadRejectionReason.MultiSeason, "Multi-season releases are not supported");
            }

            return DownloadSpecDecision.Accept();
        }
    }
}
