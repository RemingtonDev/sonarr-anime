using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.DataAugmentation.Scene;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.Download.Aggregation;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.DecisionEngine
{
    public interface IMakeDownloadDecision
    {
        List<DownloadDecision> GetRssDecision(List<ReleaseInfo> reports, bool pushedRelease = false);
        List<DownloadDecision> GetSearchDecision(List<ReleaseInfo> reports, SearchCriteriaBase searchCriteriaBase);
    }

    public class DownloadDecisionMaker : IMakeDownloadDecision
    {
        private const string SafeToken =
            @"(?:" +
            @"Multi[\s-]?Dual[\s-]?Audio|Dual[\s-]?Audio|Multi[\s-]?Audio|Multi[\s-]?Subs?|Eng(?:lish)?[\s-]?Subs?" +
            @"|10[\s-]?bit|Hi10[Pp]?" +
            @"|Blu[\s-]?Ray|WEB[\s-]?(?:DL|Rip)|DVD[\s-]?Rip|BD[\s-]?Rip" +
            @"|[0-9]{3,4}[piPI]|4[Kk]" +
            @"|DVD|BD|WEB|HDTV" +
            @"|[xX]\.?26[45]|HEVC|AVC|AV1|VP9" +
            @"|AAC|OPUS|FLAC|AC3|DTS|MP3|TrueHD|Atmos" +
            @"|MKV|MP4|AVI" +
            @"|Batch|Complete" +
            @")";

        private static readonly Regex LeadingSubgroupRegex = new Regex(@"^\[.+?\][-_. ]*", RegexOptions.Compiled);
        private static readonly Regex TrailingBracketsRegex = new Regex(@"(?:\s*[\[\(][^\[\]()]*[\]\)])+\s*$", RegexOptions.Compiled);
        private static readonly Regex SpecialContentRegex = new Regex(@"\b(Movie|OVA|OVD|Special|Extras|NCED|NCOP|Bonus)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex SingleBlockRegex = new Regex(@"[\[\(]\s*([^\[\]()]+?)\s*[\]\)]", RegexOptions.Compiled);
        private static readonly Regex SafeBlockContentRegex = new Regex(
            @"^" + SafeToken + @"(?:[\s-]+" + SafeToken + @")*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex YearBlockRegex = new Regex(@"^\d{4}$", RegexOptions.Compiled);

        private static readonly Regex AnimeSpecialKeywordRegex = new Regex(
            @"\b(OVA|OAV|Special|Specials)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex NonEpisodeMarkerRegex = new Regex(
            @"\b(Extras|NCOP|NCED|Menu|Bonus)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex TrailingSeparatorsRegex = new Regex(
            @"[\s+\-_]+$", RegexOptions.Compiled);
        private static readonly Regex TrailingGroupSuffixRegex = new Regex(
            @"(?<=[\]\)])[-_]\w+$", RegexOptions.Compiled);
        private static readonly Regex InlineParenBlockRegex = new Regex(
            @"\([^)]+\)", RegexOptions.Compiled);

        private readonly IEnumerable<IDownloadDecisionEngineSpecification> _specifications;
        private readonly IParsingService _parsingService;
        private readonly ICustomFormatCalculationService _formatCalculator;
        private readonly IRemoteEpisodeAggregationService _aggregationService;
        private readonly ISceneMappingService _sceneMappingService;
        private readonly Logger _logger;

        public DownloadDecisionMaker(IEnumerable<IDownloadDecisionEngineSpecification> specifications,
                                     IParsingService parsingService,
                                     ICustomFormatCalculationService formatService,
                                     IRemoteEpisodeAggregationService aggregationService,
                                     ISceneMappingService sceneMappingService,
                                     Logger logger)
        {
            _specifications = specifications;
            _parsingService = parsingService;
            _formatCalculator = formatService;
            _aggregationService = aggregationService;
            _sceneMappingService = sceneMappingService;
            _logger = logger;
        }

        public List<DownloadDecision> GetRssDecision(List<ReleaseInfo> reports, bool pushedRelease = false)
        {
            return GetDecisions(reports, pushedRelease).ToList();
        }

        public List<DownloadDecision> GetSearchDecision(List<ReleaseInfo> reports, SearchCriteriaBase searchCriteriaBase)
        {
            return GetDecisions(reports, false, searchCriteriaBase).ToList();
        }

        private IEnumerable<DownloadDecision> GetDecisions(List<ReleaseInfo> reports, bool pushedRelease, SearchCriteriaBase searchCriteria = null)
        {
            if (reports.Any())
            {
                _logger.ProgressInfo("Processing {0} releases", reports.Count);
            }
            else
            {
                _logger.ProgressInfo("No results found");
            }

            var reportNumber = 1;

            foreach (var report in reports)
            {
                DownloadDecision decision = null;
                _logger.ProgressTrace("Processing release {0}/{1}", reportNumber, reports.Count);
                _logger.Debug("Processing release '{0}' from '{1}'", report.Title, report.Indexer);

                try
                {
                    var parsedEpisodeInfo = Parser.Parser.ParseTitle(report.Title);

                    if (parsedEpisodeInfo == null || parsedEpisodeInfo.IsPossibleSpecialEpisode)
                    {
                        var specialEpisodeInfo = _parsingService.ParseSpecialEpisodeTitle(parsedEpisodeInfo, report.Title, report.TvdbId, report.TvRageId, report.ImdbId, searchCriteria);

                        if (specialEpisodeInfo != null)
                        {
                            parsedEpisodeInfo = specialEpisodeInfo;
                        }
                    }

                    // Anime season search fallback: bare-title packs (no episode markers)
                    if (searchCriteria is AnimeSeasonSearchCriteria animeSeasonCriteria &&
                        (parsedEpisodeInfo == null || parsedEpisodeInfo.SeriesTitle.IsNullOrWhiteSpace()))
                    {
                        var syntheticInfo = TryBuildAnimeSeasonPackInfo(report, animeSeasonCriteria);

                        if (syntheticInfo != null)
                        {
                            parsedEpisodeInfo = syntheticInfo;
                        }
                    }

                    // Anime special search fallback: OVA/Special releases for a single requested special
                    if (searchCriteria is SpecialEpisodeSearchCriteria specialCriteria &&
                        (parsedEpisodeInfo == null || parsedEpisodeInfo.SeriesTitle.IsNullOrWhiteSpace()))
                    {
                        var syntheticInfo = TryBuildAnimeSpecialInfo(report, specialCriteria);

                        if (syntheticInfo != null)
                        {
                            parsedEpisodeInfo = syntheticInfo;
                        }
                    }

                    if (parsedEpisodeInfo != null && !parsedEpisodeInfo.SeriesTitle.IsNullOrWhiteSpace())
                    {
                        var remoteEpisode = _parsingService.Map(parsedEpisodeInfo, report.TvdbId, report.TvRageId, report.ImdbId, searchCriteria);
                        remoteEpisode.Release = report;

                        if (remoteEpisode.Series == null)
                        {
                            var matchingTvdbId = _sceneMappingService.FindTvdbId(parsedEpisodeInfo.SeriesTitle, parsedEpisodeInfo.ReleaseTitle, parsedEpisodeInfo.SeasonNumber);

                            if (matchingTvdbId.HasValue)
                            {
                                decision = new DownloadDecision(remoteEpisode, new DownloadRejection(DownloadRejectionReason.MatchesAnotherSeries, $"{parsedEpisodeInfo.SeriesTitle} matches an alias for series with TVDB ID: {matchingTvdbId}"));
                            }
                            else
                            {
                                decision = new DownloadDecision(remoteEpisode, new DownloadRejection(DownloadRejectionReason.UnknownSeries, "Unknown Series"));
                            }
                        }
                        else if (remoteEpisode.Episodes.Empty())
                        {
                            decision = new DownloadDecision(remoteEpisode, new DownloadRejection(DownloadRejectionReason.UnknownEpisode, "Unable to identify correct episode(s) using release name and scene mappings"));
                        }
                        else
                        {
                            _aggregationService.Augment(remoteEpisode);

                            remoteEpisode.CustomFormats = _formatCalculator.ParseCustomFormat(remoteEpisode, remoteEpisode.Release.Size);
                            remoteEpisode.CustomFormatScore = remoteEpisode?.Series?.QualityProfile?.Value.CalculateCustomFormatScore(remoteEpisode.CustomFormats) ?? 0;

                            _logger.Trace("Custom Format Score of '{0}' [{1}] calculated for '{2}'", remoteEpisode.CustomFormatScore, remoteEpisode.CustomFormats?.ConcatToString(), report.Title);

                            remoteEpisode.DownloadAllowed = remoteEpisode.Episodes.Any();
                            decision = GetDecisionForReport(remoteEpisode, searchCriteria);
                        }
                    }

                    if (searchCriteria != null)
                    {
                        if (parsedEpisodeInfo == null)
                        {
                            parsedEpisodeInfo = new ParsedEpisodeInfo
                            {
                                Languages = LanguageParser.ParseLanguages(report.Title),
                                Quality = QualityParser.ParseQuality(report.Title)
                            };
                        }

                        if (parsedEpisodeInfo.SeriesTitle.IsNullOrWhiteSpace())
                        {
                            var remoteEpisode = new RemoteEpisode
                            {
                                Release = report,
                                ParsedEpisodeInfo = parsedEpisodeInfo,
                                Languages = parsedEpisodeInfo.Languages
                            };

                            decision = new DownloadDecision(remoteEpisode, new DownloadRejection(DownloadRejectionReason.UnableToParse, "Unable to parse release"));
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.Error(e, "Couldn't process release.");

                    var remoteEpisode = new RemoteEpisode { Release = report };
                    decision = new DownloadDecision(remoteEpisode, new DownloadRejection(DownloadRejectionReason.Error, "Unexpected error processing release"));
                }

                reportNumber++;

                if (decision != null)
                {
                    var source = pushedRelease ? ReleaseSourceType.ReleasePush : ReleaseSourceType.Rss;

                    if (searchCriteria != null)
                    {
                        if (searchCriteria.InteractiveSearch)
                        {
                            source = ReleaseSourceType.InteractiveSearch;
                        }
                        else if (searchCriteria.UserInvokedSearch)
                        {
                            source = ReleaseSourceType.UserInvokedSearch;
                        }
                        else
                        {
                            source = ReleaseSourceType.Search;
                        }
                    }

                    decision.RemoteEpisode.ReleaseSource = source;

                    if (decision.Rejections.Any())
                    {
                        _logger.Debug("Release '{0}' from '{1}' rejected for the following reasons: {2}", report.Title, report.Indexer, string.Join(", ", decision.Rejections));
                    }
                    else
                    {
                        _logger.Debug("Release '{0}' from '{1}' accepted", report.Title, report.Indexer);
                    }

                    yield return decision;
                }
            }
        }

        private DownloadDecision GetDecisionForReport(RemoteEpisode remoteEpisode, SearchCriteriaBase searchCriteria = null)
        {
            var reasons = Array.Empty<DownloadRejection>();

            foreach (var specifications in _specifications.GroupBy(v => v.Priority).OrderBy(v => v.Key))
            {
                reasons = specifications.Select(c => EvaluateSpec(c, remoteEpisode, searchCriteria))
                                        .Where(c => c != null)
                                        .ToArray();

                if (reasons.Any())
                {
                    break;
                }
            }

            return new DownloadDecision(remoteEpisode, reasons.ToArray());
        }

        private DownloadRejection EvaluateSpec(IDownloadDecisionEngineSpecification spec, RemoteEpisode remoteEpisode, SearchCriteriaBase searchCriteriaBase = null)
        {
            try
            {
                var result = spec.IsSatisfiedBy(remoteEpisode, searchCriteriaBase);

                if (!result.Accepted)
                {
                    return new DownloadRejection(result.Reason, result.Message, spec.Type);
                }
            }
            catch (Exception e)
            {
                e.Data.Add("report", remoteEpisode.Release.ToJson());
                e.Data.Add("parsed", remoteEpisode.ParsedEpisodeInfo.ToJson());
                _logger.Error(e, "Couldn't evaluate decision on {0}", remoteEpisode.Release.Title);
                return new DownloadRejection(DownloadRejectionReason.DecisionError, $"{spec.GetType().Name}: {e.Message}");
            }

            return null;
        }

        private ParsedEpisodeInfo TryBuildAnimeSeasonPackInfo(ReleaseInfo report, AnimeSeasonSearchCriteria criteria)
        {
            // Season 0 is specials — never treat bare-title packs as season 0 packs
            if (criteria.SeasonNumber == 0)
            {
                return null;
            }

            // Strip leading [SubGroup]
            var stripped = LeadingSubgroupRegex.Replace(report.Title, "");
            stripped = stripped.Trim();

            if (stripped.IsNullOrWhiteSpace())
            {
                return null;
            }

            // Require at least one trailing bracket/paren block with safe metadata
            var blockMatch = TrailingBracketsRegex.Match(stripped);
            if (!blockMatch.Success)
            {
                return null;
            }

            var titlePart = stripped.Substring(0, blockMatch.Index).Trim();
            var blocks = SingleBlockRegex.Matches(blockMatch.Value);

            if (blocks.Count == 0)
            {
                return null;
            }

            foreach (Match block in blocks)
            {
                var blockValue = block.Groups[1].Value;

                if (!SafeBlockContentRegex.IsMatch(blockValue) && !YearBlockRegex.IsMatch(blockValue))
                {
                    return null;
                }
            }

            if (titlePart.IsNullOrWhiteSpace())
            {
                return null;
            }

            // Reject titles containing special-content keywords
            if (SpecialContentRegex.IsMatch(titlePart))
            {
                return null;
            }

            // Verify IDs don't contradict the searched series
            if (report.TvdbId > 0 && criteria.Series.TvdbId > 0 && report.TvdbId != criteria.Series.TvdbId)
            {
                return null;
            }

            if (report.TvRageId > 0 && criteria.Series.TvRageId > 0 && report.TvRageId != criteria.Series.TvRageId)
            {
                return null;
            }

            if (report.ImdbId.IsNotNullOrWhiteSpace() && criteria.Series.ImdbId.IsNotNullOrWhiteSpace() &&
                report.ImdbId != criteria.Series.ImdbId)
            {
                return null;
            }

            // Exact cleaned-title match against series title or scene titles
            var cleanedNormalized = titlePart.CleanSeriesTitle();
            var seriesClean = criteria.Series.Title.CleanSeriesTitle();

            var matched = cleanedNormalized == seriesClean;

            if (!matched && criteria.SceneTitles != null)
            {
                matched = criteria.SceneTitles.Any(st => st.CleanSeriesTitle() == cleanedNormalized);
            }

            if (!matched)
            {
                var withoutParens = InlineParenBlockRegex.Replace(titlePart, "");
                withoutParens = TrailingSeparatorsRegex.Replace(withoutParens, "");
                withoutParens = withoutParens.Trim();

                if (!withoutParens.IsNullOrWhiteSpace())
                {
                    cleanedNormalized = withoutParens.CleanSeriesTitle();
                    matched = cleanedNormalized == seriesClean;

                    if (!matched && criteria.SceneTitles != null)
                    {
                        matched = criteria.SceneTitles.Any(st => st.CleanSeriesTitle() == cleanedNormalized);
                    }
                }
            }

            if (!matched)
            {
                return null;
            }

            _logger.Debug(
                "Anime season search fallback: treating '{0}' as season {1} pack for '{2}'",
                report.Title,
                criteria.SeasonNumber,
                criteria.Series.Title);

            return new ParsedEpisodeInfo
            {
                ReleaseTitle = report.Title,
                SeriesTitle = criteria.Series.Title,
                SeasonNumber = criteria.SeasonNumber,
                FullSeason = true,
                EpisodeNumbers = Array.Empty<int>(),
                AbsoluteEpisodeNumbers = Array.Empty<int>(),
                Languages = LanguageParser.ParseLanguages(report.Title),
                Quality = QualityParser.ParseQuality(report.Title)
            };
        }

        private ParsedEpisodeInfo TryBuildAnimeSpecialInfo(ReleaseInfo report, SpecialEpisodeSearchCriteria criteria)
        {
            // Only for anime series searching for exactly one special episode
            if (criteria.Series?.SeriesType != SeriesTypes.Anime)
            {
                return null;
            }

            if (criteria.Episodes == null || criteria.Episodes.Count != 1)
            {
                return null;
            }

            var episode = criteria.Episodes[0];

            if (episode.SeasonNumber != 0)
            {
                return null;
            }

            // Must contain OVA/OAV/Special/Specials keyword
            if (!AnimeSpecialKeywordRegex.IsMatch(report.Title))
            {
                return null;
            }

            // Reject non-episode markers (Extras, NCOP, NCED, Menu, Bonus)
            if (NonEpisodeMarkerRegex.IsMatch(report.Title))
            {
                return null;
            }

            // Extract series title: strip [SubGroup], trailing group suffix, bracket blocks, and special keywords
            var stripped = LeadingSubgroupRegex.Replace(report.Title, "").Trim();

            if (stripped.IsNullOrWhiteSpace())
            {
                return null;
            }

            // Strip trailing release-group suffix after final bracket (e.g. "_Rokey", "-Group")
            stripped = TrailingGroupSuffixRegex.Replace(stripped, "");

            var blockMatch = TrailingBracketsRegex.Match(stripped);
            var titlePart = blockMatch.Success
                ? stripped.Substring(0, blockMatch.Index).Trim()
                : stripped;

            // Remove OVA/Special keywords and clean up separators
            titlePart = AnimeSpecialKeywordRegex.Replace(titlePart, "");
            titlePart = TrailingSeparatorsRegex.Replace(titlePart, "");
            titlePart = titlePart.Trim();

            if (titlePart.IsNullOrWhiteSpace())
            {
                return null;
            }

            // Exact cleaned-title match against series title or scene titles
            var cleanedNormalized = titlePart.CleanSeriesTitle();
            var seriesClean = criteria.Series.Title.CleanSeriesTitle();

            var matched = cleanedNormalized == seriesClean;

            if (!matched && criteria.SceneTitles != null)
            {
                matched = criteria.SceneTitles.Any(st => st.CleanSeriesTitle() == cleanedNormalized);
            }

            // Second pass: remove inline parenthesized alt-title blocks and retry
            if (!matched)
            {
                var withoutParens = InlineParenBlockRegex.Replace(titlePart, "");
                withoutParens = TrailingSeparatorsRegex.Replace(withoutParens, "");
                withoutParens = withoutParens.Trim();

                if (!withoutParens.IsNullOrWhiteSpace())
                {
                    cleanedNormalized = withoutParens.CleanSeriesTitle();
                    matched = cleanedNormalized == seriesClean;

                    if (!matched && criteria.SceneTitles != null)
                    {
                        matched = criteria.SceneTitles.Any(st => st.CleanSeriesTitle() == cleanedNormalized);
                    }
                }
            }

            if (!matched)
            {
                return null;
            }

            _logger.Debug(
                "Anime special search fallback: treating '{0}' as S00E{1:00} for '{2}'",
                report.Title,
                episode.EpisodeNumber,
                criteria.Series.Title);

            return new ParsedEpisodeInfo
            {
                ReleaseTitle = report.Title,
                SeriesTitle = criteria.Series.Title,
                SeasonNumber = 0,
                EpisodeNumbers = new[] { episode.EpisodeNumber },
                AbsoluteEpisodeNumbers = Array.Empty<int>(),
                Languages = LanguageParser.ParseLanguages(report.Title),
                Quality = QualityParser.ParseQuality(report.Title)
            };
        }
    }
}
