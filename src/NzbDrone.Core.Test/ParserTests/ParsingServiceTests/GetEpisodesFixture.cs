using System;
using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.DataAugmentation.Scene;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.ParserTests.ParsingServiceTests
{
    [TestFixture]
    public class GetEpisodesFixture : TestBase<ParsingService>
    {
        private Series _series;
        private List<Episode> _episodes;
        private ParsedEpisodeInfo _parsedEpisodeInfo;
        private SingleEpisodeSearchCriteria _singleEpisodeSearchCriteria;

        [SetUp]
        public void Setup()
        {
            _series = Builder<Series>.CreateNew()
                .With(s => s.Title = "30 Stone")
                .With(s => s.CleanTitle = "stone")
                .Build();

            _episodes = Builder<Episode>.CreateListOfSize(1)
                                        .All()
                                        .With(e => e.AirDate = DateTime.Today.ToString(Episode.AIR_DATE_FORMAT))
                                        .Build()
                                        .ToList();

            _parsedEpisodeInfo = new ParsedEpisodeInfo
            {
                SeriesTitle = _series.Title,
                SeasonNumber = 1,
                EpisodeNumbers = new[] { 1 },
                AbsoluteEpisodeNumbers = Array.Empty<int>(),
                Languages = new List<Language> { Language.English }
            };

            _singleEpisodeSearchCriteria = new SingleEpisodeSearchCriteria
            {
                Series = _series,
                EpisodeNumber = _episodes.First().EpisodeNumber,
                SeasonNumber = _episodes.First().SeasonNumber,
                Episodes = _episodes
            };

            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.FindByTitle(It.IsAny<string>()))
                  .Returns(_series);
        }

        private void GivenDailySeries()
        {
            _series.SeriesType = SeriesTypes.Daily;
        }

        private void GivenDailyParseResult()
        {
            _parsedEpisodeInfo.AirDate = DateTime.Today.ToString(Episode.AIR_DATE_FORMAT);
        }

        private void GivenSceneNumberingSeries()
        {
            _series.UseSceneNumbering = true;
        }

        private void GivenAbsoluteNumberingSeries()
        {
            _parsedEpisodeInfo.AbsoluteEpisodeNumbers = new[] { 1 };
        }

        private void GivenFullSeason()
        {
            _parsedEpisodeInfo.FullSeason = true;
            _parsedEpisodeInfo.EpisodeNumbers = Array.Empty<int>();
        }

        [Test]
        public void should_get_daily_episode_episode_when_search_criteria_is_null()
        {
            GivenDailySeries();
            GivenDailyParseResult();

            Subject.Map(_parsedEpisodeInfo, _series.TvdbId, _series.TvRageId, _series.ImdbId);

            Mocker.GetMock<IEpisodeService>()
                .Verify(v => v.FindEpisode(It.IsAny<int>(), It.IsAny<string>(), null), Times.Once());
        }

        [Test]
        public void should_use_search_criteria_episode_when_it_matches_daily()
        {
            GivenDailySeries();
            GivenDailyParseResult();

            Subject.Map(_parsedEpisodeInfo, _series.TvdbId, _series.TvRageId, _series.ImdbId, _singleEpisodeSearchCriteria);

            Mocker.GetMock<IEpisodeService>()
                .Verify(v => v.FindEpisode(It.IsAny<int>(), It.IsAny<string>(), null), Times.Never());
        }

        [Test]
        public void should_fallback_to_daily_episode_lookup_when_search_criteria_episode_doesnt_match()
        {
            GivenDailySeries();
            _parsedEpisodeInfo.AirDate = DateTime.Today.AddDays(-5).ToString(Episode.AIR_DATE_FORMAT);

            Subject.Map(_parsedEpisodeInfo, _series.TvdbId, _series.TvRageId, _series.ImdbId, _singleEpisodeSearchCriteria);

            Mocker.GetMock<IEpisodeService>()
                .Verify(v => v.FindEpisode(It.IsAny<int>(), It.IsAny<string>(), null), Times.Once());
        }

        [Test]
        public void should_get_daily_episode_episode_should_lookup_including_daily_part()
        {
            GivenDailySeries();
            GivenDailyParseResult();
            _parsedEpisodeInfo.DailyPart = 1;

            Subject.Map(_parsedEpisodeInfo, _series.TvdbId, _series.TvRageId, _series.ImdbId);

            Mocker.GetMock<IEpisodeService>()
                  .Verify(v => v.FindEpisode(It.IsAny<int>(), It.IsAny<string>(), 1), Times.Once());
        }

        [Test]
        public void should_use_search_criteria_episode_when_it_matches_absolute()
        {
            GivenAbsoluteNumberingSeries();

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.FindEpisodesBySceneNumbering(It.IsAny<int>(), It.IsAny<int>()))
                  .Returns(new List<Episode>());

            Subject.Map(_parsedEpisodeInfo, _series.TvdbId, _series.TvRageId, _series.ImdbId, _singleEpisodeSearchCriteria);

            Mocker.GetMock<IEpisodeService>()
                .Verify(v => v.FindEpisode(It.IsAny<int>(), It.IsAny<string>(), null), Times.Never());
        }

        [Test]
        public void should_use_scene_numbering_when_series_uses_scene_numbering()
        {
            GivenSceneNumberingSeries();

            Subject.Map(_parsedEpisodeInfo, _series.TvdbId, _series.TvRageId, _series.ImdbId);

            Mocker.GetMock<IEpisodeService>()
                .Verify(v => v.FindEpisodesBySceneNumbering(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Once());
        }

        [Test]
        public void should_match_search_criteria_by_scene_numbering()
        {
            GivenSceneNumberingSeries();

            Subject.Map(_parsedEpisodeInfo, _series.TvdbId, _series.TvRageId, _series.ImdbId, _singleEpisodeSearchCriteria);

            Mocker.GetMock<IEpisodeService>()
                .Verify(v => v.FindEpisodesBySceneNumbering(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never());
        }

        [Test]
        public void should_fallback_to_findEpisode_when_search_criteria_match_fails_for_scene_numbering()
        {
            GivenSceneNumberingSeries();
            _episodes.First().SceneEpisodeNumber = 10;

            Subject.Map(_parsedEpisodeInfo, _series.TvdbId, _series.TvRageId, _series.ImdbId, _singleEpisodeSearchCriteria);

            Mocker.GetMock<IEpisodeService>()
                .Verify(v => v.FindEpisodesBySceneNumbering(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Once());
        }

        [Test]
        public void should_find_episode()
        {
            Subject.Map(_parsedEpisodeInfo, _series.TvdbId, _series.TvRageId, _series.ImdbId);

            Mocker.GetMock<IEpisodeService>()
                .Verify(v => v.FindEpisode(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Once());
        }

        [Test]
        public void should_match_episode_with_search_criteria()
        {
            Subject.Map(_parsedEpisodeInfo, _series.TvdbId, _series.TvRageId, _series.ImdbId, _singleEpisodeSearchCriteria);

            Mocker.GetMock<IEpisodeService>()
                .Verify(v => v.FindEpisode(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never());
        }

        [Test]
        public void should_fallback_to_findEpisode_when_search_criteria_match_fails()
        {
            _episodes.First().EpisodeNumber = 10;

            Subject.Map(_parsedEpisodeInfo, _series.TvdbId, _series.TvRageId, _series.ImdbId, _singleEpisodeSearchCriteria);

            Mocker.GetMock<IEpisodeService>()
                .Verify(v => v.FindEpisode(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Once());
        }

        [Test]
        public void should_look_for_episode_in_season_zero_if_absolute_special()
        {
            GivenAbsoluteNumberingSeries();

            _parsedEpisodeInfo.Special = true;

            Subject.GetEpisodes(_parsedEpisodeInfo, _series, true, null);

            Mocker.GetMock<IEpisodeService>()
                  .Verify(v => v.FindEpisodesBySceneNumbering(It.IsAny<int>(), 0, It.IsAny<int>()), Times.Never());

            Mocker.GetMock<IEpisodeService>()
                  .Verify(v => v.FindEpisode(It.IsAny<int>(), 0, It.IsAny<int>()), Times.Once());
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void should_use_scene_numbering_when_scene_season_number_has_value(int seasonNumber)
        {
            GivenAbsoluteNumberingSeries();

            Mocker.GetMock<ISceneMappingService>()
                  .Setup(s => s.GetSceneSeasonNumber(_parsedEpisodeInfo.SeriesTitle, It.IsAny<string>()))
                  .Returns(seasonNumber);

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.FindEpisodesBySceneNumbering(It.IsAny<int>(), seasonNumber, It.IsAny<int>()))
                  .Returns(new List<Episode>());

            Subject.GetEpisodes(_parsedEpisodeInfo, _series, true, null);

            Mocker.GetMock<IEpisodeService>()
                  .Verify(v => v.FindEpisodesBySceneNumbering(It.IsAny<int>(), seasonNumber, It.IsAny<int>()), Times.Once());

            Mocker.GetMock<IEpisodeService>()
                  .Verify(v => v.FindEpisode(It.IsAny<int>(), seasonNumber, It.IsAny<int>()), Times.Once());
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void should_find_episode_by_season_and_scene_absolute_episode_number(int seasonNumber)
        {
            GivenAbsoluteNumberingSeries();

            Mocker.GetMock<ISceneMappingService>()
                  .Setup(s => s.GetSceneSeasonNumber(_parsedEpisodeInfo.SeriesTitle, It.IsAny<string>()))
                  .Returns(seasonNumber);

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.FindEpisodesBySceneNumbering(It.IsAny<int>(), seasonNumber, It.IsAny<int>()))
                  .Returns(new List<Episode> { _episodes.First() });

            Subject.GetEpisodes(_parsedEpisodeInfo, _series, true, null);

            Mocker.GetMock<IEpisodeService>()
                  .Verify(v => v.FindEpisodesBySceneNumbering(It.IsAny<int>(), seasonNumber, It.IsAny<int>()), Times.Once());

            Mocker.GetMock<IEpisodeService>()
                  .Verify(v => v.FindEpisode(It.IsAny<int>(), seasonNumber, It.IsAny<int>()), Times.Never());
        }

        [TestCase(2)]
        [TestCase(20)]
        public void should_find_episode_by_parsed_season_and_absolute_episode_number_when_season_number_is_2_or_higher(int seasonNumber)
        {
            GivenAbsoluteNumberingSeries();
            _parsedEpisodeInfo.SeasonNumber = seasonNumber;
            _parsedEpisodeInfo.EpisodeNumbers = Array.Empty<int>();

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.FindEpisodesBySceneNumbering(It.IsAny<int>(), seasonNumber, It.IsAny<int>()))
                  .Returns(new List<Episode> { _episodes.First() });

            Subject.GetEpisodes(_parsedEpisodeInfo, _series, true, null);

            Mocker.GetMock<IEpisodeService>()
                  .Verify(v => v.FindEpisodesBySceneNumbering(It.IsAny<int>(), seasonNumber, It.IsAny<int>()), Times.Once());

            Mocker.GetMock<IEpisodeService>()
                  .Verify(v => v.FindEpisode(It.IsAny<int>(), seasonNumber, It.IsAny<int>()), Times.Never());
        }

        [TestCase(2)]
        [TestCase(20)]
        public void should_find_episode_by_parsed_season_and_absolute_episode_number_when_season_number_is_2_or_higher_and_scene_season_number_lookup_failed(int seasonNumber)
        {
            GivenAbsoluteNumberingSeries();
            _parsedEpisodeInfo.SeasonNumber = seasonNumber;
            _parsedEpisodeInfo.EpisodeNumbers = Array.Empty<int>();

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.FindEpisodesBySceneNumbering(It.IsAny<int>(), seasonNumber, It.IsAny<int>()))
                  .Returns(new List<Episode>());

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.FindEpisode(It.IsAny<int>(), seasonNumber, It.IsAny<int>()))
                  .Returns(_episodes.First());

            Subject.GetEpisodes(_parsedEpisodeInfo, _series, true, null);

            Mocker.GetMock<IEpisodeService>()
                  .Verify(v => v.FindEpisodesBySceneNumbering(It.IsAny<int>(), seasonNumber, It.IsAny<int>()), Times.Once());

            Mocker.GetMock<IEpisodeService>()
                  .Verify(v => v.FindEpisode(It.IsAny<int>(), seasonNumber, It.IsAny<int>()), Times.Once());
        }

        [TestCase(2)]
        [TestCase(20)]
        public void should_not_find_episode_by_parsed_season_and_absolute_episode_number_when_season_number_is_2_or_higher_and_a_episode_number_was_parsed(int seasonNumber)
        {
            GivenAbsoluteNumberingSeries();
            _parsedEpisodeInfo.SeasonNumber = seasonNumber;
            _parsedEpisodeInfo.EpisodeNumbers = new[] { 1 };

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.FindEpisodesBySceneNumbering(It.IsAny<int>(), It.IsAny<int>()))
                  .Returns(new List<Episode> { _episodes.First() });

            Subject.GetEpisodes(_parsedEpisodeInfo, _series, true, null);

            Mocker.GetMock<IEpisodeService>()
                  .Verify(v => v.FindEpisodesBySceneNumbering(It.IsAny<int>(), seasonNumber, It.IsAny<int>()), Times.Never());

            Mocker.GetMock<IEpisodeService>()
                  .Verify(v => v.FindEpisode(It.IsAny<int>(), seasonNumber, It.IsAny<int>()), Times.Never());
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void should_return_episodes_when_scene_absolute_episode_number_returns_multiple_results(int seasonNumber)
        {
            GivenAbsoluteNumberingSeries();

            Mocker.GetMock<ISceneMappingService>()
                  .Setup(s => s.GetSceneSeasonNumber(_parsedEpisodeInfo.SeriesTitle, It.IsAny<string>()))
                  .Returns(seasonNumber);

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.FindEpisodesBySceneNumbering(It.IsAny<int>(), seasonNumber, It.IsAny<int>()))
                  .Returns(Builder<Episode>.CreateListOfSize(5).Build().ToList());

            var result = Subject.GetEpisodes(_parsedEpisodeInfo, _series, true, null);

            result.Should().HaveCount(5);

            Mocker.GetMock<IEpisodeService>()
                  .Verify(v => v.FindEpisodesBySceneNumbering(It.IsAny<int>(), seasonNumber, It.IsAny<int>()), Times.Once());

            Mocker.GetMock<IEpisodeService>()
                  .Verify(v => v.FindEpisode(It.IsAny<int>(), seasonNumber, It.IsAny<int>()), Times.Never());
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void should_find_episode_by_season_and_absolute_episode_number_when_scene_absolute_episode_number_returns_no_results(int seasonNumber)
        {
            GivenAbsoluteNumberingSeries();

            Mocker.GetMock<ISceneMappingService>()
                  .Setup(s => s.GetSceneSeasonNumber(_parsedEpisodeInfo.SeriesTitle, It.IsAny<string>()))
                  .Returns(seasonNumber);

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.FindEpisodesBySceneNumbering(It.IsAny<int>(), seasonNumber, It.IsAny<int>()))
                  .Returns(new List<Episode>());

            Subject.GetEpisodes(_parsedEpisodeInfo, _series, true, null);

            Mocker.GetMock<IEpisodeService>()
                  .Verify(v => v.FindEpisodesBySceneNumbering(It.IsAny<int>(), seasonNumber, It.IsAny<int>()), Times.Once());

            Mocker.GetMock<IEpisodeService>()
                  .Verify(v => v.FindEpisode(It.IsAny<int>(), seasonNumber, It.IsAny<int>()), Times.Once());
        }

        [Test]
        public void should_use_tvdb_season_number_when_available_and_a_scene_source()
        {
            const int tvdbSeasonNumber = 5;

            Mocker.GetMock<ISceneMappingService>()
                  .Setup(v => v.FindSceneMapping(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
                  .Returns<string, string, int>((s, r, sn) => new SceneMapping { SceneSeasonNumber = 1, SeasonNumber = tvdbSeasonNumber });

            Subject.GetEpisodes(_parsedEpisodeInfo, _series, true, null);

            Mocker.GetMock<IEpisodeService>()
                .Verify(v => v.FindEpisode(_series.Id, _parsedEpisodeInfo.SeasonNumber, _parsedEpisodeInfo.EpisodeNumbers.First()), Times.Never());

            Mocker.GetMock<IEpisodeService>()
                .Verify(v => v.FindEpisode(_series.Id, tvdbSeasonNumber, _parsedEpisodeInfo.EpisodeNumbers.First()), Times.Once());
        }

        [Test]
        public void should_not_use_tvdb_season_number_when_available_for_a_different_season_and_a_scene_source()
        {
            const int tvdbSeasonNumber = 5;

            Mocker.GetMock<ISceneMappingService>()
                  .Setup(v => v.FindSceneMapping(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
                  .Returns<string, string, int>((s, r, sn) => new SceneMapping { SceneSeasonNumber = 101, SeasonNumber = tvdbSeasonNumber });

            Subject.GetEpisodes(_parsedEpisodeInfo, _series, true, null);

            Mocker.GetMock<IEpisodeService>()
                .Verify(v => v.FindEpisode(_series.Id, tvdbSeasonNumber, _parsedEpisodeInfo.EpisodeNumbers.First()), Times.Never());

            Mocker.GetMock<IEpisodeService>()
                .Verify(v => v.FindEpisode(_series.Id, _parsedEpisodeInfo.SeasonNumber, _parsedEpisodeInfo.EpisodeNumbers.First()), Times.Once());
        }

        [Test]
        public void should_not_use_tvdb_season_when_not_a_scene_source()
        {
            const int tvdbSeasonNumber = 5;

            Subject.GetEpisodes(_parsedEpisodeInfo, _series, false, null);

            Mocker.GetMock<IEpisodeService>()
                .Verify(v => v.FindEpisode(_series.Id, tvdbSeasonNumber, _parsedEpisodeInfo.EpisodeNumbers.First()), Times.Never());

            Mocker.GetMock<IEpisodeService>()
                .Verify(v => v.FindEpisode(_series.Id, _parsedEpisodeInfo.SeasonNumber, _parsedEpisodeInfo.EpisodeNumbers.First()), Times.Once());
        }

        [Test]
        public void should_not_use_tvdb_season_when_tvdb_season_number_is_less_than_zero()
        {
            const int tvdbSeasonNumber = -1;

            Mocker.GetMock<ISceneMappingService>()
                  .Setup(s => s.FindSceneMapping(_parsedEpisodeInfo.SeriesTitle, It.IsAny<string>(), It.IsAny<int>()))
                  .Returns(new SceneMapping { SeasonNumber = tvdbSeasonNumber, SceneSeasonNumber = _parsedEpisodeInfo.SeasonNumber });

            Subject.GetEpisodes(_parsedEpisodeInfo, _series, true, null);

            Mocker.GetMock<IEpisodeService>()
                .Verify(v => v.FindEpisode(_series.Id, tvdbSeasonNumber, _parsedEpisodeInfo.EpisodeNumbers.First()), Times.Never());

            Mocker.GetMock<IEpisodeService>()
                .Verify(v => v.FindEpisode(_series.Id, _parsedEpisodeInfo.SeasonNumber, _parsedEpisodeInfo.EpisodeNumbers.First()), Times.Once());
        }

        [Test]
        public void should_lookup_full_season_by_season_number_if_series_does_not_use_scene_numbering()
        {
            GivenFullSeason();

            Mocker.GetMock<IEpisodeService>()
                .Setup(s => s.GetEpisodesBySeason(_series.Id, _parsedEpisodeInfo.SeasonNumber))
                .Returns(_episodes);

            Subject.GetEpisodes(_parsedEpisodeInfo, _series, true, null);

            Mocker.GetMock<IEpisodeService>()
                .Verify(v => v.GetEpisodesBySeason(It.IsAny<int>(), It.IsAny<int>()), Times.Once);

            Mocker.GetMock<IEpisodeService>()
                .Verify(v => v.GetEpisodesBySceneSeason(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Test]
        public void should_lookup_full_season_by_scene_season_number_if_series_uses_scene_numbering()
        {
            GivenSceneNumberingSeries();
            GivenFullSeason();

            Mocker.GetMock<IEpisodeService>()
                .Setup(s => s.GetEpisodesBySceneSeason(_series.Id, _parsedEpisodeInfo.SeasonNumber))
                .Returns(_episodes);

            Subject.GetEpisodes(_parsedEpisodeInfo, _series, true, null);

            Mocker.GetMock<IEpisodeService>()
                .Verify(v => v.GetEpisodesBySeason(It.IsAny<int>(), It.IsAny<int>()), Times.Never);

            Mocker.GetMock<IEpisodeService>()
                .Verify(v => v.GetEpisodesBySceneSeason(It.IsAny<int>(), It.IsAny<int>()), Times.Once);
        }

        [Test]
        public void should_fallback_to_lookup_full_season_by_season_number_if_series_uses_scene_numbering_and_no_epsiodes_are_found_by_scene_season_number()
        {
            GivenSceneNumberingSeries();
            GivenFullSeason();

            Mocker.GetMock<IEpisodeService>()
                .Setup(s => s.GetEpisodesBySceneSeason(_series.Id, _parsedEpisodeInfo.SeasonNumber))
                .Returns(new List<Episode>());

            Mocker.GetMock<IEpisodeService>()
                .Setup(s => s.GetEpisodesBySeason(_series.Id, _parsedEpisodeInfo.SeasonNumber))
                .Returns(_episodes);

            Subject.GetEpisodes(_parsedEpisodeInfo, _series, true, null);

            Mocker.GetMock<IEpisodeService>()
                .Verify(v => v.GetEpisodesBySeason(It.IsAny<int>(), It.IsAny<int>()), Times.Once);

            Mocker.GetMock<IEpisodeService>()
                .Verify(v => v.GetEpisodesBySceneSeason(It.IsAny<int>(), It.IsAny<int>()), Times.Once);
        }

        [Test]
        public void should_lookup_anime_multi_season_by_scene_season_number_if_series_uses_scene_numbering()
        {
            GivenSceneNumberingSeries();
            GivenFullSeason();
            _series.SeriesType = SeriesTypes.Anime;
            _parsedEpisodeInfo.IsMultiSeason = true;
            _parsedEpisodeInfo.SeasonNumbers = new[] { 1, 2 };

            var season1Episodes = new List<Episode>
            {
                Builder<Episode>.CreateNew()
                    .With(e => e.Id = 101)
                    .With(e => e.SeasonNumber = 1)
                    .Build()
            };
            var season2Episodes = new List<Episode>
            {
                Builder<Episode>.CreateNew()
                    .With(e => e.Id = 202)
                    .With(e => e.SeasonNumber = 2)
                    .Build()
            };

            Mocker.GetMock<IEpisodeService>()
                .Setup(s => s.GetEpisodesBySceneSeason(_series.Id, 1))
                .Returns(season1Episodes);
            Mocker.GetMock<IEpisodeService>()
                .Setup(s => s.GetEpisodesBySceneSeason(_series.Id, 2))
                .Returns(season2Episodes);

            var result = Subject.GetEpisodes(_parsedEpisodeInfo, _series, true, null);

            result.Select(e => e.Id).Should().BeEquivalentTo(new[] { 101, 202 });
            Mocker.GetMock<IEpisodeService>()
                .Verify(v => v.GetEpisodesBySceneSeason(_series.Id, 1), Times.Once);
            Mocker.GetMock<IEpisodeService>()
                .Verify(v => v.GetEpisodesBySceneSeason(_series.Id, 2), Times.Once);
            Mocker.GetMock<IEpisodeService>()
                .Verify(v => v.GetEpisodesBySeason(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Test]
        public void should_fallback_to_tvdb_season_for_anime_multi_season_when_scene_lookup_is_empty()
        {
            GivenSceneNumberingSeries();
            GivenFullSeason();
            _series.SeriesType = SeriesTypes.Anime;
            _parsedEpisodeInfo.IsMultiSeason = true;
            _parsedEpisodeInfo.SeasonNumbers = new[] { 1, 2 };

            var season2Episodes = new List<Episode>
            {
                Builder<Episode>.CreateNew()
                    .With(e => e.Id = 202)
                    .With(e => e.SeasonNumber = 2)
                    .Build()
            };

            Mocker.GetMock<IEpisodeService>()
                .Setup(s => s.GetEpisodesBySceneSeason(_series.Id, 1))
                .Returns(new List<Episode>());
            Mocker.GetMock<IEpisodeService>()
                .Setup(s => s.GetEpisodesBySceneSeason(_series.Id, 2))
                .Returns(season2Episodes);
            Mocker.GetMock<IEpisodeService>()
                .Setup(s => s.GetEpisodesBySeason(_series.Id, 1))
                .Returns(new List<Episode>
                {
                    Builder<Episode>.CreateNew()
                        .With(e => e.Id = 101)
                        .With(e => e.SeasonNumber = 1)
                        .Build()
                });

            var result = Subject.GetEpisodes(_parsedEpisodeInfo, _series, true, null);

            result.Select(e => e.Id).Should().BeEquivalentTo(new[] { 101, 202 });
            Mocker.GetMock<IEpisodeService>()
                .Verify(v => v.GetEpisodesBySeason(_series.Id, 1), Times.Once);
        }

        [Test]
        public void should_return_episodes_for_anime_multi_season_pack_with_specials_metadata()
        {
            _series.SeriesType = SeriesTypes.Anime;

            // Simulates a parsed anime multi-season pack whose trailing metadata
            // contains "Specials" (e.g. [Anime Time] SAO (S01+S02+S03+S04+Movies+Specials+OVAs)).
            // With the parser fix, FullSeason stays true and Special stays false.
            _parsedEpisodeInfo.FullSeason = true;
            _parsedEpisodeInfo.IsMultiSeason = true;
            _parsedEpisodeInfo.Special = false;
            _parsedEpisodeInfo.SeasonNumber = 1;
            _parsedEpisodeInfo.SeasonNumbers = new[] { 1, 2 };
            _parsedEpisodeInfo.EpisodeNumbers = Array.Empty<int>();

            var season1Episodes = new List<Episode>
            {
                Builder<Episode>.CreateNew()
                    .With(e => e.Id = 101)
                    .With(e => e.SeasonNumber = 1)
                    .Build()
            };
            var season2Episodes = new List<Episode>
            {
                Builder<Episode>.CreateNew()
                    .With(e => e.Id = 202)
                    .With(e => e.SeasonNumber = 2)
                    .Build()
            };

            Mocker.GetMock<IEpisodeService>()
                .Setup(s => s.GetEpisodesBySeason(_series.Id, 1))
                .Returns(season1Episodes);
            Mocker.GetMock<IEpisodeService>()
                .Setup(s => s.GetEpisodesBySeason(_series.Id, 2))
                .Returns(season2Episodes);

            var result = Subject.GetEpisodes(_parsedEpisodeInfo, _series, false, null);

            result.Should().HaveCount(2);
            result.Select(e => e.Id).Should().BeEquivalentTo(new[] { 101, 202 });
        }

        [Test]
        public void should_map_real_anime_multi_season_title_with_specials_metadata()
        {
            _series.SeriesType = SeriesTypes.Anime;

            var parsedEpisodeInfo = Parser.Parser.ParseTitle("[Anime Time] Sword Art Online (S01+S02+S03+S04+Movies+Specials+OVAs) [BD][1080p][HEVC 10bit x265][AAC][Eng Sub]");

            parsedEpisodeInfo.Should().NotBeNull();
            parsedEpisodeInfo.FullSeason.Should().BeTrue();
            parsedEpisodeInfo.Special.Should().BeFalse();
            parsedEpisodeInfo.IsMultiSeason.Should().BeTrue();
            parsedEpisodeInfo.SeasonNumbers.Should().BeEquivalentTo(new[] { 1, 2, 3, 4 });

            Mocker.GetMock<IEpisodeService>()
                .Setup(s => s.GetEpisodesBySeason(_series.Id, 1))
                .Returns(new List<Episode>
                {
                    Builder<Episode>.CreateNew().With(e => e.Id = 101).With(e => e.SeasonNumber = 1).Build()
                });
            Mocker.GetMock<IEpisodeService>()
                .Setup(s => s.GetEpisodesBySeason(_series.Id, 2))
                .Returns(new List<Episode>
                {
                    Builder<Episode>.CreateNew().With(e => e.Id = 202).With(e => e.SeasonNumber = 2).Build()
                });
            Mocker.GetMock<IEpisodeService>()
                .Setup(s => s.GetEpisodesBySeason(_series.Id, 3))
                .Returns(new List<Episode>
                {
                    Builder<Episode>.CreateNew().With(e => e.Id = 303).With(e => e.SeasonNumber = 3).Build()
                });
            Mocker.GetMock<IEpisodeService>()
                .Setup(s => s.GetEpisodesBySeason(_series.Id, 4))
                .Returns(new List<Episode>
                {
                    Builder<Episode>.CreateNew().With(e => e.Id = 404).With(e => e.SeasonNumber = 4).Build()
                });

            var remoteEpisode = Subject.Map(parsedEpisodeInfo, _series);

            remoteEpisode.Episodes.Select(e => e.Id).Should().BeEquivalentTo(new[] { 101, 202, 303, 404 });
        }

        [Test]
        public void should_use_season_zero_when_looking_up_is_partial_special_episode_found_by_title()
        {
            _series.UseSceneNumbering = false;
            _parsedEpisodeInfo.SeasonNumber = 1;
            _parsedEpisodeInfo.EpisodeNumbers = new int[] { 0 };
            _parsedEpisodeInfo.ReleaseTitle = "Series.Title.S01E00.My.Special.Episode.1080p.AMZN.WEB-DL.DDP5.1.H264-TEPES";

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.FindEpisodeByTitle(_series.TvdbId, 0, _parsedEpisodeInfo.ReleaseTitle))
                  .Returns(
                      Builder<Episode>.CreateNew()
                                      .With(e => e.SeasonNumber = 0)
                                      .With(e => e.EpisodeNumber = 1)
                                      .Build());

            Subject.Map(_parsedEpisodeInfo, _series.TvdbId, _series.TvRageId, _series.ImdbId);

            Mocker.GetMock<IEpisodeService>()
                  .Verify(v => v.FindEpisode(_series.TvdbId, 0, 1), Times.Once());
        }

        [Test]
        public void should_use_original_parse_result_when_special_episode_lookup_by_title_fails()
        {
            _series.UseSceneNumbering = false;
            _parsedEpisodeInfo.SeasonNumber = 1;
            _parsedEpisodeInfo.EpisodeNumbers = new int[] { 0 };
            _parsedEpisodeInfo.ReleaseTitle = "Series.Title.S01E00.My.Special.Episode.1080p.AMZN.WEB-DL.DDP5.1.H264-TEPES";

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.FindEpisodeByTitle(_series.TvdbId, 0, _parsedEpisodeInfo.ReleaseTitle))
                  .Returns((Episode)null);

            Subject.Map(_parsedEpisodeInfo, _series.TvdbId, _series.TvRageId, _series.ImdbId);

            Mocker.GetMock<IEpisodeService>()
                  .Verify(v => v.FindEpisode(_series.TvdbId, _parsedEpisodeInfo.SeasonNumber, _parsedEpisodeInfo.EpisodeNumbers.First()), Times.Once());
        }

        [Test]
        public void should_cocover_single_wanted_season0_special_for_anime_pack_with_special_keyword()
        {
            _series.SeriesType = SeriesTypes.Anime;
            _parsedEpisodeInfo.FullSeason = true;
            _parsedEpisodeInfo.EpisodeNumbers = Array.Empty<int>();
            _parsedEpisodeInfo.SeasonNumber = 1;
            _parsedEpisodeInfo.ReleaseTitle = "[SubGroup] Grimgar S01+Specials [1080p]";

            var season1Episodes = new List<Episode>
            {
                Builder<Episode>.CreateNew()
                    .With(e => e.Id = 101)
                    .With(e => e.SeasonNumber = 1)
                    .Build()
            };

            var season0Special = Builder<Episode>.CreateNew()
                .With(e => e.Id = 901)
                .With(e => e.SeasonNumber = 0)
                .With(e => e.Monitored = true)
                .With(e => e.EpisodeFileId = 0)
                .With(e => e.AirDateUtc = DateTime.UtcNow.AddDays(-30))
                .Build();

            Mocker.GetMock<IEpisodeService>()
                .Setup(s => s.GetEpisodesBySeason(_series.Id, 1))
                .Returns(season1Episodes);
            Mocker.GetMock<IEpisodeService>()
                .Setup(s => s.GetEpisodesBySeason(_series.Id, 0))
                .Returns(new List<Episode> { season0Special });

            var result = Subject.GetEpisodes(_parsedEpisodeInfo, _series, false, null);

            result.Should().HaveCount(2);
            result.Select(e => e.Id).Should().BeEquivalentTo(new[] { 101, 901 });
        }

        [TestCase("[SubGroup] Grimgar S01+OVA [1080p]")]
        [TestCase("[SubGroup] Grimgar S01+OAV [1080p]")]
        [TestCase("[SubGroup] Grimgar S01+Special [1080p]")]
        [TestCase("[Trix] Grimgar: Ashes and Illusions S01+OVA (Batch) [BDRip 1080p AV1 Opus] (Dual Audio, Multi Subs, VOSTFR) | Hai to Gensou no Grimgar 灰と幻想のグリムガル Season S1 OAV Specials")]
        public void should_cocover_single_wanted_season0_special_for_anime_pack_with_ova_keyword(string releaseTitle)
        {
            _series.SeriesType = SeriesTypes.Anime;
            _parsedEpisodeInfo.FullSeason = true;
            _parsedEpisodeInfo.EpisodeNumbers = Array.Empty<int>();
            _parsedEpisodeInfo.SeasonNumber = 1;
            _parsedEpisodeInfo.ReleaseTitle = releaseTitle;

            var season1Episodes = new List<Episode>
            {
                Builder<Episode>.CreateNew()
                    .With(e => e.Id = 101)
                    .With(e => e.SeasonNumber = 1)
                    .Build()
            };

            var season0Special = Builder<Episode>.CreateNew()
                .With(e => e.Id = 901)
                .With(e => e.SeasonNumber = 0)
                .With(e => e.Monitored = true)
                .With(e => e.EpisodeFileId = 0)
                .With(e => e.AirDateUtc = DateTime.UtcNow.AddDays(-30))
                .Build();

            Mocker.GetMock<IEpisodeService>()
                .Setup(s => s.GetEpisodesBySeason(_series.Id, 1))
                .Returns(season1Episodes);
            Mocker.GetMock<IEpisodeService>()
                .Setup(s => s.GetEpisodesBySeason(_series.Id, 0))
                .Returns(new List<Episode> { season0Special });

            var result = Subject.GetEpisodes(_parsedEpisodeInfo, _series, false, null);

            result.Should().HaveCount(2);
            result.Select(e => e.Id).Should().BeEquivalentTo(new[] { 101, 901 });
        }

        [Test]
        public void should_not_cocover_season0_when_multiple_wanted_specials()
        {
            _series.SeriesType = SeriesTypes.Anime;
            _parsedEpisodeInfo.FullSeason = true;
            _parsedEpisodeInfo.EpisodeNumbers = Array.Empty<int>();
            _parsedEpisodeInfo.SeasonNumber = 1;
            _parsedEpisodeInfo.ReleaseTitle = "[SubGroup] Grimgar S01+Specials [1080p]";

            var season1Episodes = new List<Episode>
            {
                Builder<Episode>.CreateNew()
                    .With(e => e.Id = 101)
                    .With(e => e.SeasonNumber = 1)
                    .Build()
            };

            var season0Specials = new List<Episode>
            {
                Builder<Episode>.CreateNew()
                    .With(e => e.Id = 901)
                    .With(e => e.SeasonNumber = 0)
                    .With(e => e.Monitored = true)
                    .With(e => e.EpisodeFileId = 0)
                    .With(e => e.AirDateUtc = DateTime.UtcNow.AddDays(-30))
                    .Build(),
                Builder<Episode>.CreateNew()
                    .With(e => e.Id = 902)
                    .With(e => e.SeasonNumber = 0)
                    .With(e => e.Monitored = true)
                    .With(e => e.EpisodeFileId = 0)
                    .With(e => e.AirDateUtc = DateTime.UtcNow.AddDays(-20))
                    .Build()
            };

            Mocker.GetMock<IEpisodeService>()
                .Setup(s => s.GetEpisodesBySeason(_series.Id, 1))
                .Returns(season1Episodes);
            Mocker.GetMock<IEpisodeService>()
                .Setup(s => s.GetEpisodesBySeason(_series.Id, 0))
                .Returns(season0Specials);

            var result = Subject.GetEpisodes(_parsedEpisodeInfo, _series, false, null);

            result.Should().HaveCount(1);
            result.Select(e => e.Id).Should().BeEquivalentTo(new[] { 101 });
        }

        [TestCase("[SubGroup] Grimgar S01+Extras [1080p]")]
        [TestCase("[SubGroup] Grimgar S01+NCOP [1080p]")]
        [TestCase("[SubGroup] Grimgar S01+NCED [1080p]")]
        [TestCase("[SubGroup] Grimgar S01+Menu [1080p]")]
        [TestCase("[SubGroup] Grimgar S01+Bonus [1080p]")]
        public void should_not_cocover_season0_when_title_has_non_episode_markers(string releaseTitle)
        {
            _series.SeriesType = SeriesTypes.Anime;
            _parsedEpisodeInfo.FullSeason = true;
            _parsedEpisodeInfo.EpisodeNumbers = Array.Empty<int>();
            _parsedEpisodeInfo.SeasonNumber = 1;
            _parsedEpisodeInfo.ReleaseTitle = releaseTitle;

            var season1Episodes = new List<Episode>
            {
                Builder<Episode>.CreateNew()
                    .With(e => e.Id = 101)
                    .With(e => e.SeasonNumber = 1)
                    .Build()
            };

            Mocker.GetMock<IEpisodeService>()
                .Setup(s => s.GetEpisodesBySeason(_series.Id, 1))
                .Returns(season1Episodes);

            var result = Subject.GetEpisodes(_parsedEpisodeInfo, _series, false, null);

            result.Should().HaveCount(1);
            result.Select(e => e.Id).Should().BeEquivalentTo(new[] { 101 });

            // Should NOT query season 0 episodes at all
            Mocker.GetMock<IEpisodeService>()
                .Verify(v => v.GetEpisodesBySeason(_series.Id, 0), Times.Never);
        }

        [Test]
        public void should_not_cocover_season0_for_standard_series()
        {
            _series.SeriesType = SeriesTypes.Standard;
            _parsedEpisodeInfo.FullSeason = true;
            _parsedEpisodeInfo.EpisodeNumbers = Array.Empty<int>();
            _parsedEpisodeInfo.SeasonNumber = 1;
            _parsedEpisodeInfo.ReleaseTitle = "Series Title S01+Specials 1080p";

            var season1Episodes = new List<Episode>
            {
                Builder<Episode>.CreateNew()
                    .With(e => e.Id = 101)
                    .With(e => e.SeasonNumber = 1)
                    .Build()
            };

            Mocker.GetMock<IEpisodeService>()
                .Setup(s => s.GetEpisodesBySeason(_series.Id, 1))
                .Returns(season1Episodes);

            var result = Subject.GetEpisodes(_parsedEpisodeInfo, _series, false, null);

            result.Should().HaveCount(1);
            Mocker.GetMock<IEpisodeService>()
                .Verify(v => v.GetEpisodesBySeason(_series.Id, 0), Times.Never);
        }
    }
}
