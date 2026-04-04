using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.DataAugmentation.Scene;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.IndexerSearchTests
{
    public class ReleaseSearchServiceFixture : CoreTest<ReleaseSearchService>
    {
        private Mock<IIndexer> _mockIndexer;
        private Series _xemSeries;
        private List<Episode> _xemEpisodes;

        [SetUp]
        public void SetUp()
        {
            _mockIndexer = Mocker.GetMock<IIndexer>();
            _mockIndexer.SetupGet(s => s.Definition).Returns(new IndexerDefinition { Id = 1 });
            _mockIndexer.SetupGet(s => s.SupportsSearch).Returns(true);

            Mocker.GetMock<IIndexerFactory>()
                  .Setup(s => s.AutomaticSearchEnabled(true))
                  .Returns(new List<IIndexer> { _mockIndexer.Object });

            Mocker.GetMock<IMakeDownloadDecision>()
                .Setup(s => s.GetSearchDecision(It.IsAny<List<Parser.Model.ReleaseInfo>>(), It.IsAny<SearchCriteriaBase>()))
                .Returns(new List<DownloadDecision>());

            _xemSeries = Builder<Series>.CreateNew()
                .With(v => v.UseSceneNumbering = true)
                .With(v => v.Monitored = true)
                .Build();

            _xemEpisodes = new List<Episode>();

            Mocker.GetMock<ISeriesService>()
                .Setup(v => v.GetSeries(_xemSeries.Id))
                .Returns(_xemSeries);

            Mocker.GetMock<IEpisodeService>()
                .Setup(v => v.GetEpisodesBySeason(_xemSeries.Id, It.IsAny<int>()))
                .Returns<int, int>((i, j) => _xemEpisodes.Where(d => d.SeasonNumber == j).ToList());

            Mocker.GetMock<ISceneMappingService>()
                  .Setup(s => s.FindByTvdbId(It.IsAny<int>()))
                  .Returns(new List<SceneMapping>());

            Mocker.GetMock<ISceneMappingService>()
                  .Setup(s => s.GetSceneNames(It.IsAny<int>(), It.IsAny<List<int>>(), It.IsAny<List<int>>()))
                  .Returns(new List<string>());
        }

        private void WithEpisode(int seasonNumber, int episodeNumber, int? sceneSeasonNumber, int? sceneEpisodeNumber, string airDate = null)
        {
            var episode = Builder<Episode>.CreateNew()
                .With(v => v.SeriesId == _xemSeries.Id)
                .With(v => v.Series == _xemSeries)
                .With(v => v.SeasonNumber, seasonNumber)
                .With(v => v.EpisodeNumber, episodeNumber)
                .With(v => v.SceneSeasonNumber, sceneSeasonNumber)
                .With(v => v.SceneEpisodeNumber, sceneEpisodeNumber)
                .With(v => v.AirDate = airDate ?? $"{2000 + seasonNumber}-{(episodeNumber % 12) + 1:00}-05")
                .With(v => v.AirDateUtc = DateTime.ParseExact(v.AirDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).ToUniversalTime())
                .With(v => v.Monitored = true)
                .Build();

            _xemEpisodes.Add(episode);
        }

        private void WithEpisodes()
        {
            // Season 1 maps to Scene Season 2 (one-to-one)
            WithEpisode(1, 12, 2, 3);
            WithEpisode(1, 13, 2, 4);

            // Season 2 maps to Scene Season 3 & 4 (one-to-one)
            WithEpisode(2, 1, 3, 11);
            WithEpisode(2, 2, 3, 12);
            WithEpisode(2, 3, 4, 11);
            WithEpisode(2, 4, 4, 12);

            // Season 3 maps to Scene Season 5 (partial)
            // Season 4 maps to Scene Season 5 & 6 (partial)
            WithEpisode(3, 1, 5, 11);
            WithEpisode(3, 2, 5, 12);
            WithEpisode(4, 1, 5, 13);
            WithEpisode(4, 2, 5, 14);
            WithEpisode(4, 3, 6, 11);
            WithEpisode(5, 1, 6, 12);

            // Season 7+ maps normally, so no mapping specified.
            WithEpisode(7, 1, null, null);
            WithEpisode(7, 2, null, null);
        }

        private List<SearchCriteriaBase> WatchForSearchCriteria()
        {
            var result = new List<SearchCriteriaBase>();

            _mockIndexer.Setup(v => v.Fetch(It.IsAny<SingleEpisodeSearchCriteria>()))
                .Callback<SingleEpisodeSearchCriteria>(s => result.Add(s))
                .Returns(Task.FromResult<IList<Parser.Model.ReleaseInfo>>(new List<Parser.Model.ReleaseInfo>()));

            _mockIndexer.Setup(v => v.Fetch(It.IsAny<SeasonSearchCriteria>()))
                .Callback<SeasonSearchCriteria>(s => result.Add(s))
                .Returns(Task.FromResult<IList<Parser.Model.ReleaseInfo>>(new List<Parser.Model.ReleaseInfo>()));

            _mockIndexer.Setup(v => v.Fetch(It.IsAny<DailyEpisodeSearchCriteria>()))
                .Callback<DailyEpisodeSearchCriteria>(s => result.Add(s))
                .Returns(Task.FromResult<IList<Parser.Model.ReleaseInfo>>(new List<Parser.Model.ReleaseInfo>()));

            _mockIndexer.Setup(v => v.Fetch(It.IsAny<DailySeasonSearchCriteria>()))
                .Callback<DailySeasonSearchCriteria>(s => result.Add(s))
                .Returns(Task.FromResult<IList<Parser.Model.ReleaseInfo>>(new List<Parser.Model.ReleaseInfo>()));

            _mockIndexer.Setup(v => v.Fetch(It.IsAny<AnimeEpisodeSearchCriteria>()))
                .Callback<AnimeEpisodeSearchCriteria>(s => result.Add(s))
                .Returns(Task.FromResult<IList<Parser.Model.ReleaseInfo>>(new List<Parser.Model.ReleaseInfo>()));

            _mockIndexer.Setup(v => v.Fetch(It.IsAny<AnimeSeasonSearchCriteria>()))
                .Callback<AnimeSeasonSearchCriteria>(s => result.Add(s))
                .Returns(Task.FromResult<IList<Parser.Model.ReleaseInfo>>(new List<Parser.Model.ReleaseInfo>()));

            _mockIndexer.Setup(v => v.Fetch(It.IsAny<SpecialEpisodeSearchCriteria>()))
                .Callback<SpecialEpisodeSearchCriteria>(s => result.Add(s))
                .Returns(Task.FromResult<IList<Parser.Model.ReleaseInfo>>(new List<Parser.Model.ReleaseInfo>()));

            return result;
        }

        [Test]
        public async Task Tags_IndexerTags_SeriesNoTags_IndexerNotIncluded()
        {
            _mockIndexer.SetupGet(s => s.Definition).Returns(new IndexerDefinition
            {
                Id = 1,
                Tags = new HashSet<int> { 3 }
            });

            WithEpisodes();

            var allCriteria = WatchForSearchCriteria();

            await Subject.EpisodeSearch(_xemEpisodes.First(), true, false);

            var criteria = allCriteria.OfType<SingleEpisodeSearchCriteria>().ToList();

            criteria.Count.Should().Be(0);
        }

        [Test]
        public async Task Tags_IndexerNoTags_SeriesTags_IndexerIncluded()
        {
            _mockIndexer.SetupGet(s => s.Definition).Returns(new IndexerDefinition
            {
                Id = 1
            });

            _xemSeries = Builder<Series>.CreateNew()
                .With(v => v.UseSceneNumbering = true)
                .With(v => v.Monitored = true)
                .With(v => v.Tags = new HashSet<int> { 3 })
                .Build();

            Mocker.GetMock<ISeriesService>()
                .Setup(v => v.GetSeries(_xemSeries.Id))
                .Returns(_xemSeries);

            WithEpisodes();

            var allCriteria = WatchForSearchCriteria();

            await Subject.EpisodeSearch(_xemEpisodes.First(), true, false);

            var criteria = allCriteria.OfType<SingleEpisodeSearchCriteria>().ToList();

            criteria.Count.Should().Be(1);
        }

        [Test]
        public async Task Tags_IndexerAndSeriesTagsMatch_IndexerIncluded()
        {
            _mockIndexer.SetupGet(s => s.Definition).Returns(new IndexerDefinition
            {
                Id = 1,
                Tags = new HashSet<int> { 1, 2, 3 }
            });

            _xemSeries = Builder<Series>.CreateNew()
                .With(v => v.UseSceneNumbering = true)
                .With(v => v.Monitored = true)
                .With(v => v.Tags = new HashSet<int> { 3, 4, 5 })
                .Build();

            Mocker.GetMock<ISeriesService>()
                .Setup(v => v.GetSeries(_xemSeries.Id))
                .Returns(_xemSeries);

            WithEpisodes();

            var allCriteria = WatchForSearchCriteria();

            await Subject.EpisodeSearch(_xemEpisodes.First(), true, false);

            var criteria = allCriteria.OfType<SingleEpisodeSearchCriteria>().ToList();

            criteria.Count.Should().Be(1);
        }

        [Test]
        public async Task Tags_IndexerAndSeriesTagsMismatch_IndexerNotIncluded()
        {
            _mockIndexer.SetupGet(s => s.Definition).Returns(new IndexerDefinition
            {
                Id = 1,
                Tags = new HashSet<int> { 1, 2, 3 }
            });

            _xemSeries = Builder<Series>.CreateNew()
                .With(v => v.UseSceneNumbering = true)
                .With(v => v.Monitored = true)
                .With(v => v.Tags = new HashSet<int> { 4, 5, 6 })
                .Build();

            Mocker.GetMock<ISeriesService>()
                .Setup(v => v.GetSeries(_xemSeries.Id))
                .Returns(_xemSeries);

            WithEpisodes();

            var allCriteria = WatchForSearchCriteria();

            await Subject.EpisodeSearch(_xemEpisodes.First(), true, false);

            var criteria = allCriteria.OfType<SingleEpisodeSearchCriteria>().ToList();

            criteria.Count.Should().Be(0);
        }

        [Test]
        public async Task scene_episodesearch()
        {
            WithEpisodes();

            var allCriteria = WatchForSearchCriteria();

            await Subject.EpisodeSearch(_xemEpisodes.First(), true, false);

            var criteria = allCriteria.OfType<SingleEpisodeSearchCriteria>().ToList();

            criteria.Count.Should().Be(1);
            criteria[0].SeasonNumber.Should().Be(2);
            criteria[0].EpisodeNumber.Should().Be(3);
        }

        [Test]
        public async Task scene_seasonsearch()
        {
            WithEpisodes();

            var allCriteria = WatchForSearchCriteria();

            await Subject.SeasonSearch(_xemSeries.Id, 1, false, false, true, false);

            var criteria = allCriteria.OfType<SeasonSearchCriteria>().ToList();

            criteria.Count.Should().Be(1);
            criteria[0].SeasonNumber.Should().Be(2);
        }

        [Test]
        public async Task scene_seasonsearch_should_search_multiple_seasons()
        {
            WithEpisodes();

            var allCriteria = WatchForSearchCriteria();

            await Subject.SeasonSearch(_xemSeries.Id, 2, false, false, true, false);

            var criteria = allCriteria.OfType<SeasonSearchCriteria>().ToList();

            criteria.Count.Should().Be(2);
            criteria[0].SeasonNumber.Should().Be(3);
            criteria[1].SeasonNumber.Should().Be(4);
        }

        [Test]
        public async Task scene_seasonsearch_should_search_single_episode_if_possible()
        {
            WithEpisodes();

            var allCriteria = WatchForSearchCriteria();

            await Subject.SeasonSearch(_xemSeries.Id, 4, false, false, true, false);

            var criteria1 = allCriteria.OfType<SeasonSearchCriteria>().ToList();
            var criteria2 = allCriteria.OfType<SingleEpisodeSearchCriteria>().ToList();

            criteria1.Count.Should().Be(1);
            criteria1[0].SeasonNumber.Should().Be(5);

            criteria2.Count.Should().Be(1);
            criteria2[0].SeasonNumber.Should().Be(6);
            criteria2[0].EpisodeNumber.Should().Be(11);
        }

        [Test]
        public async Task scene_seasonsearch_should_use_seasonnumber_if_no_scene_number_is_available()
        {
            WithEpisodes();

            var allCriteria = WatchForSearchCriteria();

            await Subject.SeasonSearch(_xemSeries.Id, 7, false, false, true, false);

            var criteria = allCriteria.OfType<SeasonSearchCriteria>().ToList();

            criteria.Count.Should().Be(1);
            criteria[0].SeasonNumber.Should().Be(7);
        }

        [Test]
        public async Task season_search_for_anime_should_search_for_each_monitored_episode()
        {
            WithEpisodes();
            _xemSeries.SeriesType = SeriesTypes.Anime;
            _xemEpisodes.ForEach(e => e.EpisodeFileId = 0);

            var seasonNumber = 1;
            var allCriteria = WatchForSearchCriteria();

            await Subject.SeasonSearch(_xemSeries.Id, seasonNumber, true, false, true, false);

            var criteria = allCriteria.OfType<AnimeEpisodeSearchCriteria>().ToList();

            criteria.Count.Should().Be(_xemEpisodes.Count(e => e.SeasonNumber == seasonNumber));
        }

        [Test]
        public async Task season_search_for_anime_should_not_search_for_unmonitored_episodes()
        {
            WithEpisodes();
            _xemSeries.SeriesType = SeriesTypes.Anime;
            _xemEpisodes.ForEach(e => e.Monitored = false);
            _xemEpisodes.ForEach(e => e.EpisodeFileId = 0);

            var seasonNumber = 1;
            var allCriteria = WatchForSearchCriteria();

            await Subject.SeasonSearch(_xemSeries.Id, seasonNumber, false, true, true, false);

            var criteria = allCriteria.OfType<AnimeEpisodeSearchCriteria>().ToList();

            criteria.Count.Should().Be(0);
        }

        [Test]
        public async Task season_search_for_anime_should_not_search_for_unaired_episodes()
        {
            WithEpisodes();
            _xemSeries.SeriesType = SeriesTypes.Anime;
            _xemEpisodes.ForEach(e => e.AirDateUtc = DateTime.UtcNow.AddDays(5));
            _xemEpisodes.ForEach(e => e.EpisodeFileId = 0);

            var seasonNumber = 1;
            var allCriteria = WatchForSearchCriteria();

            await Subject.SeasonSearch(_xemSeries.Id, seasonNumber, false, false, true, false);

            var criteria = allCriteria.OfType<AnimeEpisodeSearchCriteria>().ToList();

            criteria.Count.Should().Be(0);
        }

        [Test]
        public async Task season_search_for_anime_should_not_search_for_episodes_with_files()
        {
            WithEpisodes();
            _xemSeries.SeriesType = SeriesTypes.Anime;
            _xemEpisodes.ForEach(e => e.EpisodeFileId = 1);

            var seasonNumber = 1;
            var allCriteria = WatchForSearchCriteria();

            await Subject.SeasonSearch(_xemSeries.Id, seasonNumber, true, false, true, false);

            var criteria = allCriteria.OfType<AnimeEpisodeSearchCriteria>().ToList();

            criteria.Count.Should().Be(0);
        }

        [Test]
        public async Task season_search_for_anime_should_set_isSeasonSearch_flag()
        {
            WithEpisodes();
            _xemSeries.SeriesType = SeriesTypes.Anime;
            _xemEpisodes.ForEach(e => e.EpisodeFileId = 0);

            var seasonNumber = 1;
            var allCriteria = WatchForSearchCriteria();

            await Subject.SeasonSearch(_xemSeries.Id, seasonNumber, true, false, true, false);

            var criteria = allCriteria.OfType<AnimeEpisodeSearchCriteria>().ToList();

            criteria.Count.Should().Be(_xemEpisodes.Count(e => e.SeasonNumber == seasonNumber));
            criteria.ForEach(c => c.IsSeasonSearch.Should().BeTrue());
        }

        [Test]
        public async Task season_search_for_anime_should_search_for_each_monitored_season()
        {
            WithEpisodes();
            _xemSeries.SeriesType = SeriesTypes.Anime;
            _xemEpisodes.ForEach(e => e.EpisodeFileId = 0);

            var seasonNumber = 1;
            var allCriteria = WatchForSearchCriteria();

            await Subject.SeasonSearch(_xemSeries.Id, seasonNumber, true, false, true, false);

            var criteria = allCriteria.OfType<AnimeSeasonSearchCriteria>().ToList();

            var episodesForSeason1 = _xemEpisodes.Where(e => e.SeasonNumber == seasonNumber);
            criteria.Count.Should().Be(episodesForSeason1.Select(e => e.SeasonNumber).Distinct().Count());
        }

        [Test]
        public async Task season_search_for_anime_should_not_search_for_unmonitored_season()
        {
            WithEpisodes();
            _xemSeries.SeriesType = SeriesTypes.Anime;
            _xemEpisodes.ForEach(e => e.Monitored = false);
            _xemEpisodes.ForEach(e => e.EpisodeFileId = 0);

            var seasonNumber = 1;
            var allCriteria = WatchForSearchCriteria();

            await Subject.SeasonSearch(_xemSeries.Id, seasonNumber, false, true, true, false);

            var criteria = allCriteria.OfType<AnimeSeasonSearchCriteria>().ToList();

            criteria.Count.Should().Be(0);
        }

        [Test]
        public async Task season_search_for_anime_should_not_search_for_unaired_season()
        {
            WithEpisodes();
            _xemSeries.SeriesType = SeriesTypes.Anime;
            _xemEpisodes.ForEach(e => e.AirDateUtc = DateTime.UtcNow.AddDays(5));
            _xemEpisodes.ForEach(e => e.EpisodeFileId = 0);

            var seasonNumber = 1;
            var allCriteria = WatchForSearchCriteria();

            await Subject.SeasonSearch(_xemSeries.Id, seasonNumber, false, false, true, false);

            var criteria = allCriteria.OfType<AnimeSeasonSearchCriteria>().ToList();

            criteria.Count.Should().Be(0);
        }

        [Test]
        public async Task season_search_for_anime_should_not_search_for_season_with_files()
        {
            WithEpisodes();
            _xemSeries.SeriesType = SeriesTypes.Anime;
            _xemEpisodes.ForEach(e => e.EpisodeFileId = 1);

            var seasonNumber = 1;
            var allCriteria = WatchForSearchCriteria();

            await Subject.SeasonSearch(_xemSeries.Id, seasonNumber, true, false, true, false);

            var criteria = allCriteria.OfType<AnimeSeasonSearchCriteria>().ToList();

            criteria.Count.Should().Be(0);
        }

        [Test]
        public async Task season_search_for_daily_should_search_multiple_years()
        {
            WithEpisode(1, 1, null, null, "2005-12-30");
            WithEpisode(1, 2, null, null, "2005-12-31");
            WithEpisode(1, 3, null, null, "2006-01-01");
            WithEpisode(1, 4, null, null, "2006-01-02");
            _xemSeries.SeriesType = SeriesTypes.Daily;

            var allCriteria = WatchForSearchCriteria();

            await Subject.SeasonSearch(_xemSeries.Id, 1, false, false, true, false);

            var criteria = allCriteria.OfType<DailySeasonSearchCriteria>().ToList();

            criteria.Count.Should().Be(2);
            criteria[0].Year.Should().Be(2005);
            criteria[1].Year.Should().Be(2006);
        }

        [Test]
        public async Task season_search_for_daily_should_search_single_episode_if_possible()
        {
            WithEpisode(1, 1, null, null, "2005-12-30");
            WithEpisode(1, 2, null, null, "2005-12-31");
            WithEpisode(1, 3, null, null, "2006-01-01");
            _xemSeries.SeriesType = SeriesTypes.Daily;

            var allCriteria = WatchForSearchCriteria();

            await Subject.SeasonSearch(_xemSeries.Id, 1, false, false, true, false);

            var criteria1 = allCriteria.OfType<DailySeasonSearchCriteria>().ToList();
            var criteria2 = allCriteria.OfType<DailyEpisodeSearchCriteria>().ToList();

            criteria1.Count.Should().Be(1);
            criteria1[0].Year.Should().Be(2005);

            criteria2.Count.Should().Be(1);
            criteria2[0].AirDate.Should().Be(new DateTime(2006, 1, 1));
        }

        [Test]
        public async Task season_search_for_daily_should_not_search_for_unmonitored_episodes()
        {
            WithEpisode(1, 1, null, null, "2005-12-30");
            WithEpisode(1, 2, null, null, "2005-12-31");
            WithEpisode(1, 3, null, null, "2006-01-01");
            _xemSeries.SeriesType = SeriesTypes.Daily;
            _xemEpisodes[0].Monitored = false;

            var allCriteria = WatchForSearchCriteria();

            await Subject.SeasonSearch(_xemSeries.Id, 1, false, true, true, false);

            var criteria1 = allCriteria.OfType<DailySeasonSearchCriteria>().ToList();
            var criteria2 = allCriteria.OfType<DailyEpisodeSearchCriteria>().ToList();

            criteria1.Should().HaveCount(0);
            criteria2.Should().HaveCount(2);
        }

        [Test]
        public async Task getscenenames_should_use_seasonnumber_if_no_scene_seasonnumber_is_available()
        {
            WithEpisodes();

            var allCriteria = WatchForSearchCriteria();

            await Subject.SeasonSearch(_xemSeries.Id, 7, false, false, true, false);

            Mocker.GetMock<ISceneMappingService>()
                  .Verify(v => v.FindByTvdbId(_xemSeries.Id), Times.Once());

            allCriteria.Should().HaveCount(1);
            allCriteria.First().Should().BeOfType<SeasonSearchCriteria>();
            allCriteria.First().As<SeasonSearchCriteria>().SeasonNumber.Should().Be(7);
        }

        private void SetupAnimeSeasonWithApprovedDecisions(List<Episode> coveredEpisodes)
        {
            var remoteEpisode = new RemoteEpisode
            {
                Release = new ReleaseInfo { Guid = "test-season-pack-1", Title = "Test Season Pack" },
                Episodes = coveredEpisodes,
                Series = _xemSeries
            };

            var approvedDecision = new DownloadDecision(remoteEpisode);
            var approvedList = new List<DownloadDecision> { approvedDecision };

            Mocker.GetMock<IMakeDownloadDecision>()
                .Setup(s => s.GetSearchDecision(It.IsAny<List<ReleaseInfo>>(), It.IsAny<SearchCriteriaBase>()))
                .Returns<List<ReleaseInfo>, SearchCriteriaBase>((reports, criteria) =>
                    criteria is AnimeSeasonSearchCriteria ? approvedList : new List<DownloadDecision>());
        }

        private void WithAnimeEpisode(int id, int seasonNumber, int episodeNumber, int absoluteEpisodeNumber)
        {
            var episode = Builder<Episode>.CreateNew()
                .With(v => v.Id, id)
                .With(v => v.SeriesId, _xemSeries.Id)
                .With(v => v.Series, _xemSeries)
                .With(v => v.SeasonNumber, seasonNumber)
                .With(v => v.EpisodeNumber, episodeNumber)
                .With(v => v.AbsoluteEpisodeNumber, absoluteEpisodeNumber)
                .With(v => v.SceneSeasonNumber, (int?)null)
                .With(v => v.SceneEpisodeNumber, (int?)null)
                .With(v => v.SceneAbsoluteEpisodeNumber, (int?)null)
                .With(v => v.AirDate, $"{2000 + seasonNumber}-{(episodeNumber % 12) + 1:00}-05")
                .With(v => v.AirDateUtc, DateTime.UtcNow.AddDays(-30))
                .With(v => v.Monitored, true)
                .With(v => v.EpisodeFileId, 0)
                .Build();

            _xemEpisodes.Add(episode);
        }

        [Test]
        public async Task anime_season_search_should_skip_all_episodes_when_fully_covered()
        {
            _xemSeries.SeriesType = SeriesTypes.Anime;
            WithAnimeEpisode(101, 1, 1, 1);
            WithAnimeEpisode(102, 1, 2, 2);

            var seasonEpisodes = _xemEpisodes.Where(e => e.SeasonNumber == 1).ToList();
            SetupAnimeSeasonWithApprovedDecisions(seasonEpisodes);

            var allCriteria = WatchForSearchCriteria();

            await Subject.SeasonSearch(_xemSeries.Id, 1, true, false, true, false);

            var animeCriteria = allCriteria.OfType<AnimeEpisodeSearchCriteria>().ToList();
            animeCriteria.Count.Should().Be(0);
        }

        [Test]
        public async Task anime_season_search_should_search_all_episodes_when_none_covered()
        {
            _xemSeries.SeriesType = SeriesTypes.Anime;
            WithAnimeEpisode(201, 1, 1, 1);
            WithAnimeEpisode(202, 1, 2, 2);

            var allCriteria = WatchForSearchCriteria();

            await Subject.SeasonSearch(_xemSeries.Id, 1, true, false, true, false);

            var animeCriteria = allCriteria.OfType<AnimeEpisodeSearchCriteria>().ToList();
            animeCriteria.Count.Should().Be(2);
        }

        [Test]
        public async Task anime_season_search_should_only_search_uncovered_episodes()
        {
            _xemSeries.SeriesType = SeriesTypes.Anime;
            WithAnimeEpisode(301, 1, 1, 1);
            WithAnimeEpisode(302, 1, 2, 2);

            // Cover only the first episode
            SetupAnimeSeasonWithApprovedDecisions(new List<Episode> { _xemEpisodes.First() });

            var allCriteria = WatchForSearchCriteria();

            await Subject.SeasonSearch(_xemSeries.Id, 1, true, false, true, false);

            var animeCriteria = allCriteria.OfType<AnimeEpisodeSearchCriteria>().ToList();
            animeCriteria.Count.Should().Be(1);
        }

        [Test]
        public async Task anime_season_0_search_should_use_special_search_not_anime_season_search()
        {
            _xemSeries.SeriesType = SeriesTypes.Anime;
            WithAnimeEpisode(401, 0, 1, 0);

            var allCriteria = WatchForSearchCriteria();

            await Subject.SeasonSearch(_xemSeries.Id, 0, false, false, true, false);

            var animeCriteria = allCriteria.OfType<AnimeSeasonSearchCriteria>().ToList();
            animeCriteria.Count.Should().Be(0, "Season 0 should not use AnimeSeasonSearchCriteria");

            var specialCriteria = allCriteria.OfType<SpecialEpisodeSearchCriteria>().ToList();
            specialCriteria.Count.Should().Be(1, "Season 0 should use SpecialEpisodeSearchCriteria");
        }

        [Test]
        public async Task anime_season_0_search_should_not_affect_season_1_routing()
        {
            _xemSeries.SeriesType = SeriesTypes.Anime;
            WithAnimeEpisode(501, 1, 1, 1);
            WithAnimeEpisode(502, 1, 2, 2);

            var allCriteria = WatchForSearchCriteria();

            await Subject.SeasonSearch(_xemSeries.Id, 1, true, false, true, false);

            var animeCriteria = allCriteria.OfType<AnimeSeasonSearchCriteria>().ToList();
            animeCriteria.Count.Should().BeGreaterThan(0, "Season 1 should still use AnimeSeasonSearchCriteria");

            var specialCriteria = allCriteria.OfType<SpecialEpisodeSearchCriteria>().ToList();
            specialCriteria.Count.Should().Be(0, "Season 1 should not use SpecialEpisodeSearchCriteria");
        }

        [Test]
        public async Task multi_season_anime_search_should_accumulate_broad_queries_across_seasons()
        {
            _xemSeries.SeriesType = SeriesTypes.Anime;
            WithAnimeEpisode(101, 1, 1, 1);
            WithAnimeEpisode(102, 1, 2, 2);
            WithAnimeEpisode(201, 2, 1, 13);
            WithAnimeEpisode(202, 2, 2, 14);

            WatchForSearchCriteria();

            var broadEmitted = new HashSet<string>();
            await Subject.SeasonSearch(_xemSeries.Id, 1, true, false, true, false, broadEmitted);

            // After S1, broadEmitted should contain the series' scene titles
            broadEmitted.Should().NotBeEmpty();
            broadEmitted.Should().Contain(_xemSeries.Title);
        }

        [Test]
        public async Task multi_season_anime_search_should_pass_accumulated_broad_queries_to_later_seasons()
        {
            _xemSeries.SeriesType = SeriesTypes.Anime;
            WithAnimeEpisode(101, 1, 1, 1);
            WithAnimeEpisode(102, 1, 2, 2);
            WithAnimeEpisode(201, 2, 1, 13);
            WithAnimeEpisode(202, 2, 2, 14);

            // Capture BroadQueriesEmitted snapshots at the time of each Fetch
            var broadSnapshots = new List<string[]>();
            _mockIndexer.Setup(v => v.Fetch(It.IsAny<AnimeSeasonSearchCriteria>()))
                .Callback<AnimeSeasonSearchCriteria>(s =>
                    broadSnapshots.Add(s.BroadQueriesEmitted?.ToArray() ?? Array.Empty<string>()))
                .Returns(Task.FromResult<IList<ReleaseInfo>>(new List<ReleaseInfo>()));

            _mockIndexer.Setup(v => v.Fetch(It.IsAny<AnimeEpisodeSearchCriteria>()))
                .Returns(Task.FromResult<IList<ReleaseInfo>>(new List<ReleaseInfo>()));

            var broadEmitted = new HashSet<string>();
            await Subject.SeasonSearch(_xemSeries.Id, 1, true, false, true, false, broadEmitted);
            await Subject.SeasonSearch(_xemSeries.Id, 2, true, false, true, false, broadEmitted);

            broadSnapshots.Should().HaveCount(2);

            // S1 should receive empty emitted set (no prior seasons)
            broadSnapshots[0].Should().BeEmpty();

            // S2 should receive the titles that S1 added
            broadSnapshots[1].Should().NotBeEmpty();
            broadSnapshots[1].Should().Contain(_xemSeries.Title);
        }

        [Test]
        public async Task anime_season_0_should_not_pollute_cross_season_broad_query_state()
        {
            _xemSeries.SeriesType = SeriesTypes.Anime;
            WithAnimeEpisode(401, 0, 1, 0);

            WatchForSearchCriteria();

            var broadEmitted = new HashSet<string>();
            await Subject.SeasonSearch(_xemSeries.Id, 0, false, false, true, false, broadEmitted);

            // Season 0 uses SpecialEpisodeSearchCriteria, should not touch broadEmitted
            broadEmitted.Should().BeEmpty();
        }

        [Test]
        public async Task broad_query_state_should_deduplicate_scene_titles()
        {
            _xemSeries.SeriesType = SeriesTypes.Anime;
            _xemSeries.Title = "Test Anime";
            WithAnimeEpisode(101, 1, 1, 1);
            WithAnimeEpisode(102, 1, 2, 2);

            Mocker.GetMock<ISeriesService>()
                .Setup(v => v.GetSeries(_xemSeries.Id))
                .Returns(_xemSeries);

            // Scene mapping returns duplicate titles
            Mocker.GetMock<ISceneMappingService>()
                .Setup(s => s.GetSceneNames(It.IsAny<int>(), It.IsAny<List<int>>(), It.IsAny<List<int>>()))
                .Returns(new List<string> { "Test Anime", "Test Anime" });

            WatchForSearchCriteria();

            var broadEmitted = new HashSet<string>();
            await Subject.SeasonSearch(_xemSeries.Id, 1, true, false, true, false, broadEmitted);

            // HashSet should naturally deduplicate — "Test Anime" appears twice in SceneTitles
            // but UnionWith deduplicates
            broadEmitted.Should().HaveCount(1);
        }

        [Test]
        public async Task episode_search_should_use_all_available_numbering_from_services_and_xem()
        {
            WithEpisode(1, 12, 2, 3);

            Mocker.GetMock<ISceneMappingService>()
                .Setup(s => s.FindByTvdbId(It.IsAny<int>()))
                .Returns(new List<SceneMapping>
                {
                    new SceneMapping
                    {
                        TvdbId = _xemSeries.TvdbId,
                        SearchTerm = _xemSeries.Title,
                        ParseTerm = _xemSeries.Title,
                        FilterRegex = "(?i)-(BTN)$",
                        SeasonNumber = 1,
                        SceneSeasonNumber = 1,
                        SceneOrigin = "tvdb",
                        Type = "ServicesProvider"
                    }
                });

            var allCriteria = WatchForSearchCriteria();

            await Subject.EpisodeSearch(_xemEpisodes.First(), false, false);

            Mocker.GetMock<ISceneMappingService>()
                .Verify(v => v.FindByTvdbId(_xemSeries.Id), Times.Once());

            allCriteria.Should().HaveCount(2);

            allCriteria.First().Should().BeOfType<SingleEpisodeSearchCriteria>();
            allCriteria.First().As<SingleEpisodeSearchCriteria>().SeasonNumber.Should().Be(1);
            allCriteria.First().As<SingleEpisodeSearchCriteria>().EpisodeNumber.Should().Be(12);

            allCriteria.Last().Should().BeOfType<SingleEpisodeSearchCriteria>();
            allCriteria.Last().As<SingleEpisodeSearchCriteria>().SeasonNumber.Should().Be(2);
            allCriteria.Last().As<SingleEpisodeSearchCriteria>().EpisodeNumber.Should().Be(3);
        }

        [Test]
        public async Task episode_search_should_include_series_title_when_not_a_direct_title_match()
        {
            _xemSeries.Title = "Sonarr's Title";
            _xemSeries.CleanTitle = "sonarrstitle";

            WithEpisode(1, 12, 2, 3);

            Mocker.GetMock<ISceneMappingService>()
                .Setup(s => s.FindByTvdbId(It.IsAny<int>()))
                .Returns(new List<SceneMapping>
                {
                    new SceneMapping
                    {
                        TvdbId = _xemSeries.TvdbId,
                        SearchTerm = "Sonarrs Title",
                        ParseTerm = _xemSeries.CleanTitle,
                        SeasonNumber = 1,
                        SceneSeasonNumber = 1,
                        SceneOrigin = "tvdb",
                        Type = "ServicesProvider"
                    }
                });

            var allCriteria = WatchForSearchCriteria();

            await Subject.EpisodeSearch(_xemEpisodes.First(), false, false);

            Mocker.GetMock<ISceneMappingService>()
                .Verify(v => v.FindByTvdbId(_xemSeries.Id), Times.Once());

            allCriteria.Should().HaveCount(2);

            allCriteria.First().Should().BeOfType<SingleEpisodeSearchCriteria>();
            allCriteria.First().As<SingleEpisodeSearchCriteria>().SeasonNumber.Should().Be(1);
            allCriteria.First().As<SingleEpisodeSearchCriteria>().EpisodeNumber.Should().Be(12);

            allCriteria.Last().Should().BeOfType<SingleEpisodeSearchCriteria>();
            allCriteria.Last().As<SingleEpisodeSearchCriteria>().SeasonNumber.Should().Be(2);
            allCriteria.Last().As<SingleEpisodeSearchCriteria>().EpisodeNumber.Should().Be(3);
        }

        [Test]
        public async Task anime_season_search_should_exclude_cross_season_covered_episodes_from_fallback()
        {
            _xemSeries.SeriesType = SeriesTypes.Anime;
            WithAnimeEpisode(301, 1, 1, 1);
            WithAnimeEpisode(302, 1, 2, 2);

            // No approved results from season search (so within-season coverage is empty)
            // but episode 301 is already covered by cross-season grab
            var alreadyCovered = new HashSet<int> { 301 };

            var allCriteria = WatchForSearchCriteria();

            await Subject.SeasonSearch(_xemSeries.Id, 1, true, false, true, false, null, alreadyCovered);

            // Only the uncovered episode (302) should get an individual search
            var animeCriteria = allCriteria.OfType<AnimeEpisodeSearchCriteria>().ToList();
            animeCriteria.Count.Should().Be(1);
            animeCriteria[0].AbsoluteEpisodeNumber.Should().Be(2);
        }

        [Test]
        public async Task anime_season_search_should_skip_all_when_cross_season_covers_everything()
        {
            _xemSeries.SeriesType = SeriesTypes.Anime;
            WithAnimeEpisode(301, 1, 1, 1);
            WithAnimeEpisode(302, 1, 2, 2);

            // Both episodes already covered by cross-season grabs
            var alreadyCovered = new HashSet<int> { 301, 302 };

            var allCriteria = WatchForSearchCriteria();

            await Subject.SeasonSearch(_xemSeries.Id, 1, true, false, true, false, null, alreadyCovered);

            // No individual episode searches should happen
            var animeCriteria = allCriteria.OfType<AnimeEpisodeSearchCriteria>().ToList();
            animeCriteria.Count.Should().Be(0);
        }

        [Test]
        public async Task anime_season_search_should_combine_season_and_cross_season_coverage()
        {
            _xemSeries.SeriesType = SeriesTypes.Anime;
            WithAnimeEpisode(301, 1, 1, 1);
            WithAnimeEpisode(302, 1, 2, 2);
            WithAnimeEpisode(303, 1, 3, 3);

            // Episode 301 covered by cross-season grab
            var alreadyCovered = new HashSet<int> { 301 };

            // Episode 302 covered by approved season search result
            SetupAnimeSeasonWithApprovedDecisions(new List<Episode> { _xemEpisodes[1] });

            var allCriteria = WatchForSearchCriteria();

            await Subject.SeasonSearch(_xemSeries.Id, 1, true, false, true, false, null, alreadyCovered);

            // Only episode 303 (uncovered by both) should get individual search
            var animeCriteria = allCriteria.OfType<AnimeEpisodeSearchCriteria>().ToList();
            animeCriteria.Count.Should().Be(1);
            animeCriteria[0].AbsoluteEpisodeNumber.Should().Be(3);
        }

        [Test]
        public async Task anime_multi_episode_approved_result_should_only_suppress_mapped_episodes()
        {
            _xemSeries.SeriesType = SeriesTypes.Anime;
            WithAnimeEpisode(301, 1, 1, 1);
            WithAnimeEpisode(302, 1, 2, 2);
            WithAnimeEpisode(303, 1, 3, 3);

            // Approved result covers episodes 1 and 2 only (multi-episode release)
            var ep1 = _xemEpisodes[0];
            var ep2 = _xemEpisodes[1];
            SetupAnimeSeasonWithApprovedDecisions(new List<Episode> { ep1, ep2 });

            var allCriteria = WatchForSearchCriteria();

            await Subject.SeasonSearch(_xemSeries.Id, 1, true, false, true, false);

            // Only episode 3 should get individual search
            var animeCriteria = allCriteria.OfType<AnimeEpisodeSearchCriteria>().ToList();
            animeCriteria.Count.Should().Be(1);
            animeCriteria[0].AbsoluteEpisodeNumber.Should().Be(3);
        }

        [Test]
        public async Task standard_series_season_search_should_not_use_anime_cascade()
        {
            _xemSeries.SeriesType = SeriesTypes.Standard;
            WithEpisode(1, 1, null, null);
            WithEpisode(1, 2, null, null);

            var allCriteria = WatchForSearchCriteria();

            await Subject.SeasonSearch(_xemSeries.Id, 1, false, false, true, false);

            // Standard series should use SeasonSearchCriteria, not anime criteria
            var seasonCriteria = allCriteria.OfType<SeasonSearchCriteria>().ToList();
            seasonCriteria.Count.Should().BeGreaterThan(0);

            var animeCriteria = allCriteria.OfType<AnimeSeasonSearchCriteria>().ToList();
            animeCriteria.Count.Should().Be(0);

            var animeEpCriteria = allCriteria.OfType<AnimeEpisodeSearchCriteria>().ToList();
            animeEpCriteria.Count.Should().Be(0);
        }

        private void SetupSpecialSearchWithApprovedDecisions(List<Episode> coveredEpisodes)
        {
            var remoteEpisode = new RemoteEpisode
            {
                Release = new ReleaseInfo { Guid = "test-special-pack-1", Title = "Test Special Pack" },
                Episodes = coveredEpisodes,
                Series = _xemSeries
            };

            var approvedDecision = new DownloadDecision(remoteEpisode);
            var approvedList = new List<DownloadDecision> { approvedDecision };

            Mocker.GetMock<IMakeDownloadDecision>()
                .Setup(s => s.GetSearchDecision(It.IsAny<List<ReleaseInfo>>(), It.IsAny<SearchCriteriaBase>()))
                .Returns<List<ReleaseInfo>, SearchCriteriaBase>((reports, criteria) =>
                    criteria is SpecialEpisodeSearchCriteria ? approvedList : new List<DownloadDecision>());
        }

        [Test]
        public async Task special_search_should_skip_per_episode_fallback_when_fully_covered()
        {
            _xemSeries.SeriesType = SeriesTypes.Anime;
            WithAnimeEpisode(401, 0, 1, 0);
            WithAnimeEpisode(402, 0, 2, 0);

            var specialEpisodes = _xemEpisodes.Where(e => e.SeasonNumber == 0).ToList();
            SetupSpecialSearchWithApprovedDecisions(specialEpisodes);

            var allCriteria = WatchForSearchCriteria();

            await Subject.SeasonSearch(_xemSeries.Id, 0, false, false, true, false);

            // No SingleEpisodeSearchCriteria should fire — specials are fully covered
            var singleCriteria = allCriteria.OfType<SingleEpisodeSearchCriteria>().ToList();
            singleCriteria.Count.Should().Be(0);
        }

        [Test]
        public async Task special_search_should_only_search_uncovered_specials()
        {
            _xemSeries.SeriesType = SeriesTypes.Anime;
            WithAnimeEpisode(401, 0, 1, 0);
            WithAnimeEpisode(402, 0, 2, 0);

            // Only the first special is covered
            SetupSpecialSearchWithApprovedDecisions(new List<Episode> { _xemEpisodes[0] });

            var allCriteria = WatchForSearchCriteria();

            await Subject.SeasonSearch(_xemSeries.Id, 0, false, false, true, false);

            // Only the second special (uncovered) should get a per-episode search
            var singleCriteria = allCriteria.OfType<SingleEpisodeSearchCriteria>().ToList();
            singleCriteria.Count.Should().Be(1);
            singleCriteria[0].EpisodeNumber.Should().Be(2);
        }

        [Test]
        public async Task special_search_should_fall_through_all_when_none_covered()
        {
            _xemSeries.SeriesType = SeriesTypes.Anime;
            WithAnimeEpisode(401, 0, 1, 0);
            WithAnimeEpisode(402, 0, 2, 0);

            // No approved results for special search
            var allCriteria = WatchForSearchCriteria();

            await Subject.SeasonSearch(_xemSeries.Id, 0, false, false, true, false);

            // Both specials should get per-episode search
            var singleCriteria = allCriteria.OfType<SingleEpisodeSearchCriteria>().ToList();
            singleCriteria.Count.Should().Be(2);
        }

        [Test]
        public async Task special_search_should_not_search_unmonitored_specials_in_fallback()
        {
            _xemSeries.SeriesType = SeriesTypes.Anime;
            WithAnimeEpisode(401, 0, 1, 0);
            WithAnimeEpisode(402, 0, 2, 0);

            // Make second special unmonitored
            _xemEpisodes[1].Monitored = false;

            var allCriteria = WatchForSearchCriteria();

            await Subject.SeasonSearch(_xemSeries.Id, 0, false, true, true, false);

            // Only the monitored special should get a per-episode search
            var singleCriteria = allCriteria.OfType<SingleEpisodeSearchCriteria>().ToList();
            singleCriteria.Count.Should().Be(1);
            singleCriteria[0].EpisodeNumber.Should().Be(1);
        }
    }
}
