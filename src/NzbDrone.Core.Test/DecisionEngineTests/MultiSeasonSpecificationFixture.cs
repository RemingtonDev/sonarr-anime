using System;
using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.DecisionEngineTests
{
    [TestFixture]
    public class MultiSeasonSpecificationFixture : CoreTest<MultiSeasonSpecification>
    {
        private RemoteEpisode _remoteEpisode;

        [SetUp]
        public void Setup()
        {
            var series = Builder<Series>.CreateNew()
                .With(s => s.Id = 1234)
                .With(s => s.SeriesType = SeriesTypes.Standard)
                .Build();

            _remoteEpisode = new RemoteEpisode
            {
                ParsedEpisodeInfo = new ParsedEpisodeInfo
                {
                    FullSeason = true,
                    IsMultiSeason = true,
                    SeasonNumbers = new[] { 1, 2, 3, 4 }
                },
                Episodes = Builder<Episode>.CreateListOfSize(3)
                                           .All()
                                           .With(s => s.SeriesId = series.Id)
                                           .BuildList(),
                Series = series,
                Release = new ReleaseInfo
                {
                    Title = "Series.Title.S01-05.720p.BluRay.X264-RlsGrp"
                }
            };

            Mocker.GetMock<IEpisodeService>().Setup(s => s.EpisodesBetweenDates(It.IsAny<DateTime>(), It.IsAny<DateTime>(), false))
                                             .Returns(new List<Episode>());
        }

        [Test]
        public void should_return_true_if_is_not_a_multi_season_release()
        {
            _remoteEpisode.ParsedEpisodeInfo.IsMultiSeason = false;
            _remoteEpisode.Episodes.Last().AirDateUtc = DateTime.UtcNow.AddDays(+2);
            Subject.IsSatisfiedBy(_remoteEpisode, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_return_false_if_is_a_multi_season_release()
        {
            Subject.IsSatisfiedBy(_remoteEpisode, null).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_return_false_for_non_anime_multi_season_with_anime_search()
        {
            var searchCriteria = new AnimeSeasonSearchCriteria { SeasonNumber = 1 };
            Subject.IsSatisfiedBy(_remoteEpisode, searchCriteria).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_return_true_for_anime_multi_season_with_anime_search()
        {
            _remoteEpisode.Series.SeriesType = SeriesTypes.Anime;
            var searchCriteria = new AnimeSeasonSearchCriteria { SeasonNumber = 1 };
            Subject.IsSatisfiedBy(_remoteEpisode, searchCriteria).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_return_false_for_anime_multi_season_without_anime_search()
        {
            _remoteEpisode.Series.SeriesType = SeriesTypes.Anime;
            Subject.IsSatisfiedBy(_remoteEpisode, null).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_reject_anime_multi_season_when_searched_season_is_not_covered()
        {
            _remoteEpisode.Series.SeriesType = SeriesTypes.Anime;
            _remoteEpisode.ParsedEpisodeInfo.SeasonNumbers = new[] { 1, 2, 3, 4 };
            var searchCriteria = new AnimeSeasonSearchCriteria { SeasonNumber = 5 };
            Subject.IsSatisfiedBy(_remoteEpisode, searchCriteria).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_accept_anime_multi_season_when_searched_season_is_covered()
        {
            _remoteEpisode.Series.SeriesType = SeriesTypes.Anime;
            _remoteEpisode.ParsedEpisodeInfo.SeasonNumbers = new[] { 1, 2, 3, 4 };
            var searchCriteria = new AnimeSeasonSearchCriteria { SeasonNumber = 3 };
            Subject.IsSatisfiedBy(_remoteEpisode, searchCriteria).Accepted.Should().BeTrue();
        }
    }
}
