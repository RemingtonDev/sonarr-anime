using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Queue;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.IndexerSearchTests
{
    [TestFixture]
    public class SeriesSearchServiceFixture : CoreTest<SeriesSearchService>
    {
        private Series _series;

        [SetUp]
        public void Setup()
        {
            _series = new Series
                      {
                          Id = 1,
                          Title = "Title",
                          Seasons = new List<Season>()
                      };

            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.GetSeries(It.IsAny<int>()))
                  .Returns(_series);

            Mocker.GetMock<ISearchForReleases>()
                  .Setup(s => s.SeasonSearch(_series.Id, It.IsAny<int>(), false, false, true, false))
                  .Returns(Task.FromResult(new List<DownloadDecision>()));

            Mocker.GetMock<ISearchForReleases>()
                  .Setup(s => s.SeasonSearch(_series.Id, It.IsAny<int>(), false, true, true, false, It.IsAny<HashSet<string>>(), It.IsAny<HashSet<int>>()))
                  .Returns(Task.FromResult(new List<DownloadDecision>()));

            Mocker.GetMock<IProcessDownloadDecisions>()
                  .Setup(s => s.ProcessDecisions(It.IsAny<List<DownloadDecision>>()))
                  .Returns(Task.FromResult(new ProcessedDecisions(new List<DownloadDecision>(), new List<DownloadDecision>(), new List<DownloadDecision>())));

            Mocker.GetMock<IQueueService>()
                  .Setup(s => s.GetQueue())
                  .Returns(new List<Queue.Queue>());
        }

        [Test]
        public void should_only_include_monitored_seasons()
        {
            _series.Seasons = new List<Season>
                              {
                                  new Season { SeasonNumber = 0, Monitored = false },
                                  new Season { SeasonNumber = 1, Monitored = true }
                              };

            Subject.Execute(new SeriesSearchCommand { SeriesId = _series.Id, Trigger = CommandTrigger.Manual });

            Mocker.GetMock<ISearchForReleases>()
                .Verify(v => v.SeasonSearch(_series.Id, It.IsAny<int>(), false, true, true, false), Times.Exactly(_series.Seasons.Count(s => s.Monitored)));
        }

        [Test]
        public void should_start_with_lower_seasons_first()
        {
            var seasonOrder = new List<int>();

            _series.Seasons = new List<Season>
                              {
                                  new Season { SeasonNumber = 3, Monitored = true },
                                  new Season { SeasonNumber = 1, Monitored = true },
                                  new Season { SeasonNumber = 2, Monitored = true }
                              };

            Mocker.GetMock<ISearchForReleases>()
                  .Setup(s => s.SeasonSearch(_series.Id, It.IsAny<int>(), false, true, true, false))
                  .Returns(Task.FromResult(new List<DownloadDecision>()))
                  .Callback<int, int, bool, bool, bool, bool>((seriesId, seasonNumber, missingOnly, monitoredOnly, userInvokedSearch, interactiveSearch) => seasonOrder.Add(seasonNumber));

            Subject.Execute(new SeriesSearchCommand { SeriesId = _series.Id, Trigger = CommandTrigger.Manual });

            seasonOrder.First().Should().Be(_series.Seasons.OrderBy(s => s.SeasonNumber).First().SeasonNumber);
        }

        [Test]
        public void should_skip_later_anime_season_when_earlier_grab_already_covers_it()
        {
            var seasonSearches = new List<int>();
            var season1Episode = Builder<Episode>.CreateNew()
                .With(e => e.Id = 11)
                .With(e => e.SeriesId = _series.Id)
                .With(e => e.SeasonNumber = 1)
                .With(e => e.Monitored = true)
                .With(e => e.EpisodeFileId = 0)
                .With(e => e.AirDateUtc = System.DateTime.UtcNow.AddDays(-1))
                .Build();
            var season2Episode = Builder<Episode>.CreateNew()
                .With(e => e.Id = 22)
                .With(e => e.SeriesId = _series.Id)
                .With(e => e.SeasonNumber = 2)
                .With(e => e.Monitored = true)
                .With(e => e.EpisodeFileId = 0)
                .With(e => e.AirDateUtc = System.DateTime.UtcNow.AddDays(-1))
                .Build();

            _series.SeriesType = SeriesTypes.Anime;
            _series.Seasons = new List<Season>
            {
                new Season { SeasonNumber = 1, Monitored = true },
                new Season { SeasonNumber = 2, Monitored = true }
            };

            Mocker.GetMock<IEpisodeService>()
                .Setup(s => s.GetEpisodesBySeason(_series.Id, 1))
                .Returns(new List<Episode> { season1Episode });

            Mocker.GetMock<IEpisodeService>()
                .Setup(s => s.GetEpisodesBySeason(_series.Id, 2))
                .Returns(new List<Episode> { season2Episode });

            Mocker.GetMock<ISearchForReleases>()
                .Setup(s => s.SeasonSearch(_series.Id, It.IsAny<int>(), false, true, true, false, It.IsAny<HashSet<string>>(), It.IsAny<HashSet<int>>()))
                .Returns(Task.FromResult(new List<DownloadDecision>()))
                .Callback<int, int, bool, bool, bool, bool, HashSet<string>, HashSet<int>>((seriesId, seasonNumber, missingOnly, monitoredOnly, userInvokedSearch, interactiveSearch, broadQueries, coveredIds) => seasonSearches.Add(seasonNumber));

            var grabbedDecision = new DownloadDecision(new RemoteEpisode
            {
                Episodes = new List<Episode> { season1Episode, season2Episode },
                ParsedEpisodeInfo = new ParsedEpisodeInfo()
            });

            Mocker.GetMock<IProcessDownloadDecisions>()
                .SetupSequence(s => s.ProcessDecisions(It.IsAny<List<DownloadDecision>>()))
                .Returns(Task.FromResult(new ProcessedDecisions(new List<DownloadDecision> { grabbedDecision }, new List<DownloadDecision>(), new List<DownloadDecision>())));

            Subject.Execute(new SeriesSearchCommand { SeriesId = _series.Id, Trigger = CommandTrigger.Manual });

            seasonSearches.Should().Equal(1);
        }

        [Test]
        public void should_search_anime_non_zero_seasons_before_season_zero()
        {
            var seasonSearches = new List<int>();

            _series.SeriesType = SeriesTypes.Anime;
            _series.Seasons = new List<Season>
            {
                new Season { SeasonNumber = 0, Monitored = true },
                new Season { SeasonNumber = 1, Monitored = true },
                new Season { SeasonNumber = 2, Monitored = true }
            };

            Mocker.GetMock<IEpisodeService>()
                .Setup(s => s.GetEpisodesBySeason(_series.Id, It.IsAny<int>()))
                .Returns<int, int>((seriesId, seasonNumber) => new List<Episode>
                {
                    Builder<Episode>.CreateNew()
                        .With(e => e.Id = (seasonNumber * 10) + 1)
                        .With(e => e.SeriesId = seriesId)
                        .With(e => e.SeasonNumber = seasonNumber)
                        .With(e => e.Monitored = true)
                        .With(e => e.EpisodeFileId = 0)
                        .With(e => e.AirDateUtc = System.DateTime.UtcNow.AddDays(-1))
                        .Build()
                });

            Mocker.GetMock<ISearchForReleases>()
                .Setup(s => s.SeasonSearch(_series.Id, It.IsAny<int>(), false, true, true, false, It.IsAny<HashSet<string>>(), It.IsAny<HashSet<int>>()))
                .Returns(Task.FromResult(new List<DownloadDecision>()))
                .Callback<int, int, bool, bool, bool, bool, HashSet<string>, HashSet<int>>((seriesId, seasonNumber, missingOnly, monitoredOnly, userInvokedSearch, interactiveSearch, broadQueries, coveredIds) => seasonSearches.Add(seasonNumber));

            Subject.Execute(new SeriesSearchCommand { SeriesId = _series.Id, Trigger = CommandTrigger.Manual });

            seasonSearches.Should().Equal(1, 2, 0);
        }

        [Test]
        public void should_not_reorder_standard_series_seasons()
        {
            var seasonSearches = new List<int>();

            _series.SeriesType = SeriesTypes.Standard;
            _series.Seasons = new List<Season>
            {
                new Season { SeasonNumber = 0, Monitored = true },
                new Season { SeasonNumber = 1, Monitored = true },
                new Season { SeasonNumber = 2, Monitored = true }
            };

            Mocker.GetMock<ISearchForReleases>()
                .Setup(s => s.SeasonSearch(_series.Id, It.IsAny<int>(), false, true, true, false))
                .Returns(Task.FromResult(new List<DownloadDecision>()))
                .Callback<int, int, bool, bool, bool, bool>((seriesId, seasonNumber, missingOnly, monitoredOnly, userInvokedSearch, interactiveSearch) => seasonSearches.Add(seasonNumber));

            Subject.Execute(new SeriesSearchCommand { SeriesId = _series.Id, Trigger = CommandTrigger.Manual });

            seasonSearches.Should().Equal(0, 1, 2);
        }

        [Test]
        public void should_skip_season_zero_when_earlier_grab_covers_wanted_special()
        {
            var seasonSearches = new List<int>();
            var season1Episode = Builder<Episode>.CreateNew()
                .With(e => e.Id = 11)
                .With(e => e.SeriesId = _series.Id)
                .With(e => e.SeasonNumber = 1)
                .With(e => e.Monitored = true)
                .With(e => e.EpisodeFileId = 0)
                .With(e => e.AirDateUtc = System.DateTime.UtcNow.AddDays(-1))
                .Build();
            var season0Special = Builder<Episode>.CreateNew()
                .With(e => e.Id = 99)
                .With(e => e.SeriesId = _series.Id)
                .With(e => e.SeasonNumber = 0)
                .With(e => e.Monitored = true)
                .With(e => e.EpisodeFileId = 0)
                .With(e => e.AirDateUtc = System.DateTime.UtcNow.AddDays(-1))
                .Build();

            _series.SeriesType = SeriesTypes.Anime;
            _series.Seasons = new List<Season>
            {
                new Season { SeasonNumber = 0, Monitored = true },
                new Season { SeasonNumber = 1, Monitored = true }
            };

            Mocker.GetMock<IEpisodeService>()
                .Setup(s => s.GetEpisodesBySeason(_series.Id, 0))
                .Returns(new List<Episode> { season0Special });
            Mocker.GetMock<IEpisodeService>()
                .Setup(s => s.GetEpisodesBySeason(_series.Id, 1))
                .Returns(new List<Episode> { season1Episode });

            Mocker.GetMock<ISearchForReleases>()
                .Setup(s => s.SeasonSearch(_series.Id, It.IsAny<int>(), false, true, true, false, It.IsAny<HashSet<string>>(), It.IsAny<HashSet<int>>()))
                .Returns(Task.FromResult(new List<DownloadDecision>()))
                .Callback<int, int, bool, bool, bool, bool, HashSet<string>, HashSet<int>>((seriesId, seasonNumber, missingOnly, monitoredOnly, userInvokedSearch, interactiveSearch, broadQueries, coveredIds) => seasonSearches.Add(seasonNumber));

            // The S01 grab covers both S01 episode and S00 special (S01+Specials pack)
            var grabbedDecision = new DownloadDecision(new RemoteEpisode
            {
                Episodes = new List<Episode> { season1Episode, season0Special },
                ParsedEpisodeInfo = new ParsedEpisodeInfo()
            });

            Mocker.GetMock<IProcessDownloadDecisions>()
                .SetupSequence(s => s.ProcessDecisions(It.IsAny<List<DownloadDecision>>()))
                .Returns(Task.FromResult(new ProcessedDecisions(new List<DownloadDecision> { grabbedDecision }, new List<DownloadDecision>(), new List<DownloadDecision>())));

            Subject.Execute(new SeriesSearchCommand { SeriesId = _series.Id, Trigger = CommandTrigger.Manual });

            // S01 is searched, S00 is skipped because the grab covered the special
            seasonSearches.Should().Equal(1);
        }

        [Test]
        public void should_not_skip_later_anime_season_on_partial_coverage()
        {
            var seasonSearches = new List<int>();
            var season1Episode = Builder<Episode>.CreateNew()
                .With(e => e.Id = 11)
                .With(e => e.SeriesId = _series.Id)
                .With(e => e.SeasonNumber = 1)
                .With(e => e.Monitored = true)
                .With(e => e.EpisodeFileId = 0)
                .With(e => e.AirDateUtc = System.DateTime.UtcNow.AddDays(-1))
                .Build();
            var season2Episode1 = Builder<Episode>.CreateNew()
                .With(e => e.Id = 21)
                .With(e => e.SeriesId = _series.Id)
                .With(e => e.SeasonNumber = 2)
                .With(e => e.Monitored = true)
                .With(e => e.EpisodeFileId = 0)
                .With(e => e.AirDateUtc = System.DateTime.UtcNow.AddDays(-1))
                .Build();
            var season2Episode2 = Builder<Episode>.CreateNew()
                .With(e => e.Id = 22)
                .With(e => e.SeriesId = _series.Id)
                .With(e => e.SeasonNumber = 2)
                .With(e => e.Monitored = true)
                .With(e => e.EpisodeFileId = 0)
                .With(e => e.AirDateUtc = System.DateTime.UtcNow.AddDays(-1))
                .Build();

            _series.SeriesType = SeriesTypes.Anime;
            _series.Seasons = new List<Season>
            {
                new Season { SeasonNumber = 1, Monitored = true },
                new Season { SeasonNumber = 2, Monitored = true }
            };

            Mocker.GetMock<IEpisodeService>()
                .Setup(s => s.GetEpisodesBySeason(_series.Id, 1))
                .Returns(new List<Episode> { season1Episode });

            Mocker.GetMock<IEpisodeService>()
                .Setup(s => s.GetEpisodesBySeason(_series.Id, 2))
                .Returns(new List<Episode> { season2Episode1, season2Episode2 });

            Mocker.GetMock<ISearchForReleases>()
                .Setup(s => s.SeasonSearch(_series.Id, It.IsAny<int>(), false, true, true, false, It.IsAny<HashSet<string>>(), It.IsAny<HashSet<int>>()))
                .Returns(Task.FromResult(new List<DownloadDecision>()))
                .Callback<int, int, bool, bool, bool, bool, HashSet<string>, HashSet<int>>((seriesId, seasonNumber, missingOnly, monitoredOnly, userInvokedSearch, interactiveSearch, broadQueries, coveredIds) => seasonSearches.Add(seasonNumber));

            // S1 grab covers S1 episode and only ONE of the two S2 episodes (partial coverage)
            var grabbedDecision = new DownloadDecision(new RemoteEpisode
            {
                Episodes = new List<Episode> { season1Episode, season2Episode1 },
                ParsedEpisodeInfo = new ParsedEpisodeInfo()
            });

            Mocker.GetMock<IProcessDownloadDecisions>()
                .SetupSequence(s => s.ProcessDecisions(It.IsAny<List<DownloadDecision>>()))
                .Returns(Task.FromResult(new ProcessedDecisions(new List<DownloadDecision> { grabbedDecision }, new List<DownloadDecision>(), new List<DownloadDecision>())))
                .Returns(Task.FromResult(new ProcessedDecisions(new List<DownloadDecision>(), new List<DownloadDecision>(), new List<DownloadDecision>())));

            Subject.Execute(new SeriesSearchCommand { SeriesId = _series.Id, Trigger = CommandTrigger.Manual });

            // S2 must still be searched because only 1 of 2 wanted episodes is covered
            seasonSearches.Should().Equal(1, 2);
        }

        [Test]
        public void should_pass_covered_episode_ids_to_later_season_search()
        {
            HashSet<int> capturedCoveredIds = null;
            var season1Episode = Builder<Episode>.CreateNew()
                .With(e => e.Id = 11)
                .With(e => e.SeriesId = _series.Id)
                .With(e => e.SeasonNumber = 1)
                .With(e => e.Monitored = true)
                .With(e => e.EpisodeFileId = 0)
                .With(e => e.AirDateUtc = System.DateTime.UtcNow.AddDays(-1))
                .Build();
            var season2Episode1 = Builder<Episode>.CreateNew()
                .With(e => e.Id = 21)
                .With(e => e.SeriesId = _series.Id)
                .With(e => e.SeasonNumber = 2)
                .With(e => e.Monitored = true)
                .With(e => e.EpisodeFileId = 0)
                .With(e => e.AirDateUtc = System.DateTime.UtcNow.AddDays(-1))
                .Build();
            var season2Episode2 = Builder<Episode>.CreateNew()
                .With(e => e.Id = 22)
                .With(e => e.SeriesId = _series.Id)
                .With(e => e.SeasonNumber = 2)
                .With(e => e.Monitored = true)
                .With(e => e.EpisodeFileId = 0)
                .With(e => e.AirDateUtc = System.DateTime.UtcNow.AddDays(-1))
                .Build();

            _series.SeriesType = SeriesTypes.Anime;
            _series.Seasons = new List<Season>
            {
                new Season { SeasonNumber = 1, Monitored = true },
                new Season { SeasonNumber = 2, Monitored = true }
            };

            Mocker.GetMock<IEpisodeService>()
                .Setup(s => s.GetEpisodesBySeason(_series.Id, 1))
                .Returns(new List<Episode> { season1Episode });

            Mocker.GetMock<IEpisodeService>()
                .Setup(s => s.GetEpisodesBySeason(_series.Id, 2))
                .Returns(new List<Episode> { season2Episode1, season2Episode2 });

            Mocker.GetMock<ISearchForReleases>()
                .Setup(s => s.SeasonSearch(_series.Id, It.IsAny<int>(), false, true, true, false, It.IsAny<HashSet<string>>(), It.IsAny<HashSet<int>>()))
                .Returns(Task.FromResult(new List<DownloadDecision>()))
                .Callback<int, int, bool, bool, bool, bool, HashSet<string>, HashSet<int>>((seriesId, seasonNumber, missingOnly, monitoredOnly, userInvokedSearch, interactiveSearch, broadQueries, coveredIds) =>
                {
                    if (seasonNumber == 2)
                    {
                        capturedCoveredIds = coveredIds != null ? new HashSet<int>(coveredIds) : null;
                    }
                });

            // S1 grab covers S1 episode and one S2 episode
            var grabbedDecision = new DownloadDecision(new RemoteEpisode
            {
                Episodes = new List<Episode> { season1Episode, season2Episode1 },
                ParsedEpisodeInfo = new ParsedEpisodeInfo()
            });

            Mocker.GetMock<IProcessDownloadDecisions>()
                .SetupSequence(s => s.ProcessDecisions(It.IsAny<List<DownloadDecision>>()))
                .Returns(Task.FromResult(new ProcessedDecisions(new List<DownloadDecision> { grabbedDecision }, new List<DownloadDecision>(), new List<DownloadDecision>())))
                .Returns(Task.FromResult(new ProcessedDecisions(new List<DownloadDecision>(), new List<DownloadDecision>(), new List<DownloadDecision>())));

            Subject.Execute(new SeriesSearchCommand { SeriesId = _series.Id, Trigger = CommandTrigger.Manual });

            // The covered IDs passed to S2 should contain both grabbed episode IDs
            capturedCoveredIds.Should().NotBeNull();
            capturedCoveredIds.Should().Contain(11);
            capturedCoveredIds.Should().Contain(21);
        }

        [Test]
        public void should_skip_anime_season_when_wanted_episodes_are_already_covered_by_queue()
        {
            var seasonSearches = new List<int>();
            var season1Episode1 = Builder<Episode>.CreateNew()
                .With(e => e.Id = 11)
                .With(e => e.SeriesId = _series.Id)
                .With(e => e.SeasonNumber = 1)
                .With(e => e.Monitored = true)
                .With(e => e.EpisodeFileId = 0)
                .With(e => e.AirDateUtc = System.DateTime.UtcNow.AddDays(-1))
                .Build();
            var season1Episode2 = Builder<Episode>.CreateNew()
                .With(e => e.Id = 12)
                .With(e => e.SeriesId = _series.Id)
                .With(e => e.SeasonNumber = 1)
                .With(e => e.Monitored = true)
                .With(e => e.EpisodeFileId = 0)
                .With(e => e.AirDateUtc = System.DateTime.UtcNow.AddDays(-1))
                .Build();

            _series.SeriesType = SeriesTypes.Anime;
            _series.Seasons = new List<Season>
            {
                new Season { SeasonNumber = 1, Monitored = true }
            };

            Mocker.GetMock<IEpisodeService>()
                .Setup(s => s.GetEpisodesBySeason(_series.Id, 1))
                .Returns(new List<Episode> { season1Episode1, season1Episode2 });

            Mocker.GetMock<IQueueService>()
                .Setup(s => s.GetQueue())
                .Returns(new List<Queue.Queue>
                {
                    new Queue.Queue
                    {
                        Series = _series,
                        Episode = season1Episode1,
                        TrackedDownloadState = TrackedDownloadState.Downloading
                    },
                    new Queue.Queue
                    {
                        Series = _series,
                        Episode = season1Episode2,
                        TrackedDownloadState = TrackedDownloadState.Downloading
                    }
                });

            Mocker.GetMock<ISearchForReleases>()
                .Setup(s => s.SeasonSearch(_series.Id, It.IsAny<int>(), false, true, true, false, It.IsAny<HashSet<string>>(), It.IsAny<HashSet<int>>()))
                .Returns(Task.FromResult(new List<DownloadDecision>()))
                .Callback<int, int, bool, bool, bool, bool, HashSet<string>, HashSet<int>>((seriesId, seasonNumber, missingOnly, monitoredOnly, userInvokedSearch, interactiveSearch, broadQueries, coveredIds) => seasonSearches.Add(seasonNumber));

            Subject.Execute(new SeriesSearchCommand { SeriesId = _series.Id, Trigger = CommandTrigger.Manual });

            seasonSearches.Should().BeEmpty();
        }

        [Test]
        public void should_seed_anime_covered_episode_ids_from_queue()
        {
            HashSet<int> capturedCoveredIds = null;
            var season1Episode1 = Builder<Episode>.CreateNew()
                .With(e => e.Id = 11)
                .With(e => e.SeriesId = _series.Id)
                .With(e => e.SeasonNumber = 1)
                .With(e => e.Monitored = true)
                .With(e => e.EpisodeFileId = 0)
                .With(e => e.AirDateUtc = System.DateTime.UtcNow.AddDays(-1))
                .Build();
            var season1Episode2 = Builder<Episode>.CreateNew()
                .With(e => e.Id = 12)
                .With(e => e.SeriesId = _series.Id)
                .With(e => e.SeasonNumber = 1)
                .With(e => e.Monitored = true)
                .With(e => e.EpisodeFileId = 0)
                .With(e => e.AirDateUtc = System.DateTime.UtcNow.AddDays(-1))
                .Build();

            _series.SeriesType = SeriesTypes.Anime;
            _series.Seasons = new List<Season>
            {
                new Season { SeasonNumber = 1, Monitored = true }
            };

            Mocker.GetMock<IEpisodeService>()
                .Setup(s => s.GetEpisodesBySeason(_series.Id, 1))
                .Returns(new List<Episode> { season1Episode1, season1Episode2 });

            Mocker.GetMock<IQueueService>()
                .Setup(s => s.GetQueue())
                .Returns(new List<Queue.Queue>
                {
                    new Queue.Queue
                    {
                        Series = _series,
                        Episode = season1Episode1,
                        TrackedDownloadState = TrackedDownloadState.Downloading
                    },
                    new Queue.Queue
                    {
                        Series = _series,
                        Episode = season1Episode2,
                        TrackedDownloadState = TrackedDownloadState.FailedPending
                    }
                });

            Mocker.GetMock<ISearchForReleases>()
                .Setup(s => s.SeasonSearch(_series.Id, 1, false, true, true, false, It.IsAny<HashSet<string>>(), It.IsAny<HashSet<int>>()))
                .Returns(Task.FromResult(new List<DownloadDecision>()))
                .Callback<int, int, bool, bool, bool, bool, HashSet<string>, HashSet<int>>((seriesId, seasonNumber, missingOnly, monitoredOnly, userInvokedSearch, interactiveSearch, broadQueries, coveredIds) =>
                {
                    capturedCoveredIds = coveredIds != null ? new HashSet<int>(coveredIds) : null;
                });

            Subject.Execute(new SeriesSearchCommand { SeriesId = _series.Id, Trigger = CommandTrigger.Manual });

            capturedCoveredIds.Should().NotBeNull();
            capturedCoveredIds.Should().Contain(11);
            capturedCoveredIds.Should().NotContain(12);
        }

        [Test]
        public void should_not_skip_or_reorder_standard_series_with_anime_skip_logic()
        {
            var seasonSearches = new List<int>();

            _series.SeriesType = SeriesTypes.Standard;
            _series.Seasons = new List<Season>
            {
                new Season { SeasonNumber = 0, Monitored = true },
                new Season { SeasonNumber = 1, Monitored = true },
                new Season { SeasonNumber = 2, Monitored = true }
            };

            Mocker.GetMock<ISearchForReleases>()
                .Setup(s => s.SeasonSearch(_series.Id, It.IsAny<int>(), false, true, true, false))
                .Returns(Task.FromResult(new List<DownloadDecision>()))
                .Callback<int, int, bool, bool, bool, bool>((seriesId, seasonNumber, missingOnly, monitoredOnly, userInvokedSearch, interactiveSearch) => seasonSearches.Add(seasonNumber));

            Subject.Execute(new SeriesSearchCommand { SeriesId = _series.Id, Trigger = CommandTrigger.Manual });

            // Standard series: normal order, no skipping, no anime overload called
            seasonSearches.Should().Equal(0, 1, 2);

            // Verify the anime-specific overload with broadQueries is never called
            Mocker.GetMock<ISearchForReleases>()
                .Verify(s => s.SeasonSearch(_series.Id, It.IsAny<int>(), false, true, true, false, It.IsAny<HashSet<string>>(), It.IsAny<HashSet<int>>()), Times.Never());
        }
    }
}
