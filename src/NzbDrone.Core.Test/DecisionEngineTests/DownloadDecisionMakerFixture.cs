using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.DataAugmentation.Scene;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.DecisionEngineTests
{
    [TestFixture]
    public class DownloadDecisionMakerFixture : CoreTest<DownloadDecisionMaker>
    {
        private List<ReleaseInfo> _reports;
        private RemoteEpisode _remoteEpisode;

        private Mock<IDownloadDecisionEngineSpecification> _pass1;
        private Mock<IDownloadDecisionEngineSpecification> _pass2;
        private Mock<IDownloadDecisionEngineSpecification> _pass3;

        private Mock<IDownloadDecisionEngineSpecification> _fail1;
        private Mock<IDownloadDecisionEngineSpecification> _fail2;
        private Mock<IDownloadDecisionEngineSpecification> _fail3;

        private Mock<IDownloadDecisionEngineSpecification> _failDelayed1;

        [SetUp]
        public void Setup()
        {
            _pass1 = new Mock<IDownloadDecisionEngineSpecification>();
            _pass2 = new Mock<IDownloadDecisionEngineSpecification>();
            _pass3 = new Mock<IDownloadDecisionEngineSpecification>();

            _fail1 = new Mock<IDownloadDecisionEngineSpecification>();
            _fail2 = new Mock<IDownloadDecisionEngineSpecification>();
            _fail3 = new Mock<IDownloadDecisionEngineSpecification>();

            _failDelayed1 = new Mock<IDownloadDecisionEngineSpecification>();

            _pass1.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), null)).Returns(DownloadSpecDecision.Accept);
            _pass2.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), null)).Returns(DownloadSpecDecision.Accept);
            _pass3.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), null)).Returns(DownloadSpecDecision.Accept);

            _fail1.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), null)).Returns(DownloadSpecDecision.Reject(DownloadRejectionReason.Unknown, "fail1"));
            _fail2.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), null)).Returns(DownloadSpecDecision.Reject(DownloadRejectionReason.Unknown, "fail2"));
            _fail3.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), null)).Returns(DownloadSpecDecision.Reject(DownloadRejectionReason.Unknown, "fail3"));

            _failDelayed1.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), null)).Returns(DownloadSpecDecision.Reject(DownloadRejectionReason.MinimumAgeDelay, "failDelayed1"));
            _failDelayed1.SetupGet(c => c.Priority).Returns(SpecificationPriority.Disk);

            _reports = new List<ReleaseInfo> { new ReleaseInfo { Title = "The.Office.S03E115.DVDRip.XviD-OSiTV" } };
            _remoteEpisode = new RemoteEpisode
            {
                Series = new Series(),
                Episodes = new List<Episode> { new Episode() }
            };

            Mocker.GetMock<IParsingService>()
                  .Setup(c => c.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<SearchCriteriaBase>()))
                  .Returns(_remoteEpisode);
        }

        private void GivenSpecifications(params Mock<IDownloadDecisionEngineSpecification>[] mocks)
        {
            Mocker.SetConstant<IEnumerable<IDownloadDecisionEngineSpecification>>(mocks.Select(c => c.Object));
        }

        [Test]
        public void should_call_all_specifications()
        {
            GivenSpecifications(_pass1, _pass2, _pass3, _fail1, _fail2, _fail3);

            Subject.GetRssDecision(_reports).ToList();

            _fail1.Verify(c => c.IsSatisfiedBy(_remoteEpisode, null), Times.Once());
            _fail2.Verify(c => c.IsSatisfiedBy(_remoteEpisode, null), Times.Once());
            _fail3.Verify(c => c.IsSatisfiedBy(_remoteEpisode, null), Times.Once());
            _pass1.Verify(c => c.IsSatisfiedBy(_remoteEpisode, null), Times.Once());
            _pass2.Verify(c => c.IsSatisfiedBy(_remoteEpisode, null), Times.Once());
            _pass3.Verify(c => c.IsSatisfiedBy(_remoteEpisode, null), Times.Once());
        }

        [Test]
        public void should_call_delayed_specifications_if_non_delayed_passed()
        {
            GivenSpecifications(_pass1, _failDelayed1);

            Subject.GetRssDecision(_reports).ToList();
            _failDelayed1.Verify(c => c.IsSatisfiedBy(_remoteEpisode, null), Times.Once());
        }

        [Test]
        public void should_not_call_delayed_specifications_if_non_delayed_failed()
        {
            GivenSpecifications(_fail1, _failDelayed1);

            Subject.GetRssDecision(_reports).ToList();

            _failDelayed1.Verify(c => c.IsSatisfiedBy(_remoteEpisode, null), Times.Never());
        }

        [Test]
        public void should_return_rejected_if_single_specs_fail()
        {
            GivenSpecifications(_fail1);

            var result = Subject.GetRssDecision(_reports);

            result.Single().Approved.Should().BeFalse();
        }

        [Test]
        public void should_return_rejected_if_one_of_specs_fail()
        {
            GivenSpecifications(_pass1, _fail1, _pass2, _pass3);

            var result = Subject.GetRssDecision(_reports);

            result.Single().Approved.Should().BeFalse();
        }

        [Test]
        public void should_return_pass_if_all_specs_pass()
        {
            GivenSpecifications(_pass1, _pass2, _pass3);

            var result = Subject.GetRssDecision(_reports);

            result.Single().Approved.Should().BeTrue();
        }

        [Test]
        public void should_have_same_number_of_rejections_as_specs_that_failed()
        {
            GivenSpecifications(_pass1, _pass2, _pass3, _fail1, _fail2, _fail3);

            var result = Subject.GetRssDecision(_reports);
            result.Single().Rejections.Should().HaveCount(3);
        }

        [Test]
        public void should_not_attempt_to_map_episode_if_not_parsable()
        {
            GivenSpecifications(_pass1, _pass2, _pass3);
            _reports[0].Title = "Not parsable";

            Subject.GetRssDecision(_reports).ToList();

            Mocker.GetMock<IParsingService>().Verify(c => c.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<SearchCriteriaBase>()), Times.Never());

            _pass1.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), null), Times.Never());
            _pass2.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), null), Times.Never());
            _pass3.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), null), Times.Never());
        }

        [Test]
        public void should_not_attempt_to_map_episode_if_series_title_is_blank()
        {
            GivenSpecifications(_pass1, _pass2, _pass3);
            _reports[0].Title = "1937 - Snow White and the Seven Dwarves";

            var results = Subject.GetRssDecision(_reports).ToList();

            Mocker.GetMock<IParsingService>().Verify(c => c.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<SearchCriteriaBase>()), Times.Never());

            _pass1.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), null), Times.Never());
            _pass2.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), null), Times.Never());
            _pass3.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), null), Times.Never());

            results.Should().BeEmpty();
        }

        [Test]
        public void should_return_rejected_result_for_unparsable_search()
        {
            GivenSpecifications(_pass1, _pass2, _pass3);
            _reports[0].Title = "1937 - Snow White and the Seven Dwarves";

            Subject.GetSearchDecision(_reports, new SingleEpisodeSearchCriteria()).ToList();

            Mocker.GetMock<IParsingService>().Verify(c => c.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<SearchCriteriaBase>()), Times.Never());

            _pass1.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), null), Times.Never());
            _pass2.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), null), Times.Never());
            _pass3.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), null), Times.Never());
        }

        [Test]
        public void should_not_attempt_to_make_decision_if_series_is_unknown()
        {
            GivenSpecifications(_pass1, _pass2, _pass3);

            _remoteEpisode.Series = null;

            Subject.GetRssDecision(_reports);

            _pass1.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), null), Times.Never());
            _pass2.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), null), Times.Never());
            _pass3.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), null), Times.Never());
        }

        [Test]
        public void broken_report_shouldnt_blowup_the_process()
        {
            GivenSpecifications(_pass1);

            Mocker.GetMock<IParsingService>().Setup(c => c.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<SearchCriteriaBase>()))
                     .Throws<TestException>();

            _reports = new List<ReleaseInfo>
                {
                    new ReleaseInfo { Title = "The.Office.S03E115.DVDRip.XviD-OSiTV" },
                    new ReleaseInfo { Title = "The.Office.S03E115.DVDRip.XviD-OSiTV" },
                    new ReleaseInfo { Title = "The.Office.S03E115.DVDRip.XviD-OSiTV" }
                };

            Subject.GetRssDecision(_reports);

            Mocker.GetMock<IParsingService>().Verify(c => c.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<SearchCriteriaBase>()), Times.Exactly(_reports.Count));

            ExceptionVerification.ExpectedErrors(3);
        }

        [Test]
        public void should_return_unknown_series_rejection_if_series_is_unknown()
        {
            GivenSpecifications(_pass1, _pass2, _pass3);

            _remoteEpisode.Series = null;

            var result = Subject.GetRssDecision(_reports);

            result.Should().HaveCount(1);
        }

        [Test]
        public void should_only_include_reports_for_requested_episodes()
        {
            var series = Builder<Series>.CreateNew().Build();

            var episodes = Builder<Episode>.CreateListOfSize(2)
                .All()
                .With(v => v.SeriesId, series.Id)
                .With(v => v.Series, series)
                .With(v => v.SeasonNumber, 1)
                .With(v => v.SceneSeasonNumber, 2)
                .BuildList();

            var criteria = new SeasonSearchCriteria { Episodes = episodes.Take(1).ToList(), SeasonNumber = 1 };

            var reports = episodes.Select(v =>
                new ReleaseInfo()
                {
                    Title = string.Format("{0}.S{1:00}E{2:00}.720p.WEB-DL-DRONE", series.Title, v.SceneSeasonNumber, v.SceneEpisodeNumber)
                }).ToList();

            Mocker.GetMock<IParsingService>()
                .Setup(v => v.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<SearchCriteriaBase>()))
                .Returns<ParsedEpisodeInfo, int, int, string, SearchCriteriaBase>((p, _, _, _, _) =>
                    new RemoteEpisode
                    {
                        DownloadAllowed = true,
                        ParsedEpisodeInfo = p,
                        Series = series,
                        Episodes = episodes.Where(v => v.SceneEpisodeNumber == p.EpisodeNumbers.First()).ToList()
                    });

            Mocker.SetConstant<IEnumerable<IDownloadDecisionEngineSpecification>>(new List<IDownloadDecisionEngineSpecification>
            {
                Mocker.Resolve<NzbDrone.Core.DecisionEngine.Specifications.Search.EpisodeRequestedSpecification>()
            });

            var decisions = Subject.GetSearchDecision(reports, criteria);

            var approvedDecisions = decisions.Where(v => v.Approved).ToList();

            approvedDecisions.Count.Should().Be(1);
        }

        [Test]
        public void should_not_allow_download_if_series_is_unknown()
        {
            GivenSpecifications(_pass1, _pass2, _pass3);

            _remoteEpisode.Series = null;

            var result = Subject.GetRssDecision(_reports);

            result.Should().HaveCount(1);

            result.First().RemoteEpisode.DownloadAllowed.Should().BeFalse();
        }

        [Test]
        public void should_not_allow_download_if_no_episodes_found()
        {
            GivenSpecifications(_pass1, _pass2, _pass3);

            _remoteEpisode.Episodes = new List<Episode>();

            var result = Subject.GetRssDecision(_reports);

            result.Should().HaveCount(1);

            result.First().RemoteEpisode.DownloadAllowed.Should().BeFalse();
        }

        [Test]
        public void should_return_a_decision_when_exception_is_caught()
        {
            GivenSpecifications(_pass1);

            Mocker.GetMock<IParsingService>().Setup(c => c.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<SearchCriteriaBase>()))
                     .Throws<TestException>();

            _reports = new List<ReleaseInfo>
                {
                    new ReleaseInfo { Title = "The.Office.S03E115.DVDRip.XviD-OSiTV" },
                };

            Subject.GetRssDecision(_reports).Should().HaveCount(1);

            ExceptionVerification.ExpectedErrors(1);
        }

        [Test]
        public void should_return_unknown_series_rejection_if_series_title_is_an_alias_for_another_series()
        {
            GivenSpecifications(_pass1, _pass2, _pass3);

            Mocker.GetMock<ISceneMappingService>()
                  .Setup(s => s.FindTvdbId(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
                  .Returns(12345);

            _remoteEpisode.Series = null;

            var result = Subject.GetRssDecision(_reports);

            result.Should().HaveCount(1);
            result.First().Rejections.First().Message.Should().Contain("12345");
        }

        [TestCase("[SavI0r] El Cazador de la Bruja [DVD][480p][AV1][OPUS][Dual Audio]")]
        [TestCase("[Exiled-Destiny] El Cazador De La Bruja [Dual Audio]")]
        [TestCase("El Cazador de la Bruja ( 480p ) [ Eng Subs ]")]
        public void should_treat_bare_title_as_season_pack_during_anime_season_search(string releaseTitle)
        {
            var series = Builder<Series>.CreateNew()
                .With(s => s.Title = "El Cazador de la Bruja")
                .With(s => s.TvdbId = 79099)
                .Build();

            var episodes = new List<Episode> { Builder<Episode>.CreateNew().With(e => e.SeasonNumber = 1).Build() };

            var criteria = new AnimeSeasonSearchCriteria
            {
                Series = series,
                SeasonNumber = 1,
                SceneTitles = new List<string> { "El Cazador de la Bruja" },
                Episodes = episodes
            };

            _reports = new List<ReleaseInfo> { new ReleaseInfo { Title = releaseTitle } };

            var remoteEpisode = new RemoteEpisode
            {
                Series = series,
                Episodes = episodes
            };

            Mocker.GetMock<IParsingService>()
                .Setup(c => c.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<SearchCriteriaBase>()))
                .Returns(remoteEpisode);

            _pass1.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<SearchCriteriaBase>())).Returns(DownloadSpecDecision.Accept);
            _pass2.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<SearchCriteriaBase>())).Returns(DownloadSpecDecision.Accept);
            _pass3.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<SearchCriteriaBase>())).Returns(DownloadSpecDecision.Accept);
            GivenSpecifications(_pass1, _pass2, _pass3);

            var result = Subject.GetSearchDecision(_reports, criteria).ToList();

            result.Should().HaveCount(1);
            result.First().Approved.Should().BeTrue();

            Mocker.GetMock<IParsingService>()
                .Verify(
                    c => c.Map(
                        It.Is<ParsedEpisodeInfo>(p => p.FullSeason && p.SeasonNumber == 1 && p.SeriesTitle == "El Cazador de la Bruja"),
                        It.IsAny<int>(),
                        It.IsAny<int>(),
                        It.IsAny<string>(),
                        It.IsAny<SearchCriteriaBase>()),
                    Times.Once());
        }

        [Test]
        public void should_treat_single_season_alias_only_bare_title_as_season_pack_during_anime_season_search()
        {
            var series = Builder<Series>.CreateNew()
                .With(s => s.Title = "Example Anime Title")
                .With(s => s.TvdbId = 123456)
                .Build();

            var episodes = new List<Episode> { Builder<Episode>.CreateNew().With(e => e.SeasonNumber = 1).Build() };

            var criteria = new AnimeSeasonSearchCriteria
            {
                Series = series,
                SeasonNumber = 1,
                SceneTitles = new List<string> { "Example Anime", "Example Anime Title" },
                Episodes = episodes
            };

            _reports = new List<ReleaseInfo> { new ReleaseInfo { Title = "[Group] Example Anime [1080p]" } };

            var remoteEpisode = new RemoteEpisode
            {
                Series = series,
                Episodes = episodes
            };

            Mocker.GetMock<IParsingService>()
                .Setup(c => c.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<SearchCriteriaBase>()))
                .Returns(remoteEpisode);

            _pass1.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<SearchCriteriaBase>())).Returns(DownloadSpecDecision.Accept);
            _pass2.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<SearchCriteriaBase>())).Returns(DownloadSpecDecision.Accept);
            _pass3.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<SearchCriteriaBase>())).Returns(DownloadSpecDecision.Accept);
            GivenSpecifications(_pass1, _pass2, _pass3);

            var result = Subject.GetSearchDecision(_reports, criteria).ToList();

            result.Should().HaveCount(1);
            result.First().Approved.Should().BeTrue();

            Mocker.GetMock<IParsingService>()
                .Verify(
                    c => c.Map(
                        It.Is<ParsedEpisodeInfo>(p => p.FullSeason && p.SeasonNumber == 1 && p.SeriesTitle == "Example Anime Title"),
                        It.IsAny<int>(),
                        It.IsAny<int>(),
                        It.IsAny<string>(),
                        It.IsAny<SearchCriteriaBase>()),
                    Times.Once());
        }

        [Test]
        public void should_treat_bare_title_with_parenthesized_year_as_season_pack_during_anime_season_search()
        {
            var series = Builder<Series>.CreateNew()
                .With(s => s.Title = "Noir")
                .With(s => s.TvdbId = 79099)
                .Build();

            var episodes = new List<Episode> { Builder<Episode>.CreateNew().With(e => e.SeasonNumber = 1).Build() };

            var criteria = new AnimeSeasonSearchCriteria
            {
                Series = series,
                SeasonNumber = 1,
                SceneTitles = new List<string> { "Noir" },
                Episodes = episodes
            };

            _reports = new List<ReleaseInfo>
            {
                new ReleaseInfo { Title = "[SavI0r] Noir (2001) [BD][1080p][AV1][OPUS][Multi Dual Audio]" }
            };

            var remoteEpisode = new RemoteEpisode
            {
                Series = series,
                Episodes = episodes
            };

            Mocker.GetMock<IParsingService>()
                .Setup(c => c.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<SearchCriteriaBase>()))
                .Returns(remoteEpisode);

            _pass1.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<SearchCriteriaBase>())).Returns(DownloadSpecDecision.Accept);
            GivenSpecifications(_pass1);

            var result = Subject.GetSearchDecision(_reports, criteria).ToList();

            result.Should().HaveCount(1);
            result.First().Approved.Should().BeTrue();

            Mocker.GetMock<IParsingService>()
                .Verify(
                    c => c.Map(
                        It.Is<ParsedEpisodeInfo>(p =>
                            p.FullSeason &&
                            p.SeasonNumber == 1 &&
                            p.SeriesTitle == "Noir" &&
                            p.Quality.Quality == Quality.Bluray1080p),
                        It.IsAny<int>(),
                        It.IsAny<int>(),
                        It.IsAny<string>(),
                        It.IsAny<SearchCriteriaBase>()),
                    Times.Once());
        }

        [Test]
        public void should_not_treat_polluted_short_title_as_season_pack_during_anime_season_search()
        {
            var series = Builder<Series>.CreateNew()
                .With(s => s.Title = "Noir")
                .With(s => s.TvdbId = 79099)
                .Build();

            var criteria = new AnimeSeasonSearchCriteria
            {
                Series = series,
                SeasonNumber = 1,
                SceneTitles = new List<string> { "Noir" },
                Episodes = new List<Episode>()
            };

            _reports = new List<ReleaseInfo>
            {
                new ReleaseInfo { Title = "[SavI0r] Synduality: Noir [BD][1080p][AV1][OPUS][Multi Dual Audio]" }
            };

            _pass1.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<SearchCriteriaBase>())).Returns(DownloadSpecDecision.Accept);
            GivenSpecifications(_pass1);

            var result = Subject.GetSearchDecision(_reports, criteria).ToList();

            result.Should().HaveCount(1);
            result.First().Rejections.Should().Contain(r => r.Message == "Unable to parse release");

            Mocker.GetMock<IParsingService>()
                .Verify(
                    c => c.Map(
                        It.Is<ParsedEpisodeInfo>(p => p.FullSeason && p.SeriesTitle == "Noir"),
                        It.IsAny<int>(),
                        It.IsAny<int>(),
                        It.IsAny<string>(),
                        It.IsAny<SearchCriteriaBase>()),
                    Times.Never());
        }

        [TestCase("[SavI0r] El Cazador de la Bruja [DVD][480p][AV1][OPUS][Dual Audio]")]
        public void should_not_treat_bare_title_as_season_pack_outside_anime_search(string releaseTitle)
        {
            GivenSpecifications(_pass1, _pass2, _pass3);

            var series = Builder<Series>.CreateNew()
                .With(s => s.Title = "El Cazador de la Bruja")
                .Build();

            _reports = new List<ReleaseInfo> { new ReleaseInfo { Title = releaseTitle } };

            // Standard season search (not anime) should reject as unparseable
            var criteria = new SeasonSearchCriteria
            {
                Series = series,
                SeasonNumber = 1,
                SceneTitles = new List<string> { "El Cazador de la Bruja" },
                Episodes = new List<Episode>()
            };

            var result = Subject.GetSearchDecision(_reports, criteria).ToList();

            result.Should().HaveCount(1);
            result.First().Rejections.Should().Contain(r => r.Message == "Unable to parse release");
        }

        [TestCase("[DVD_ISO] El Cazador de la Bruja (US, FUNi)")]
        [TestCase("[Starlight] El Cazador de la Bruja (h264)")]
        [TestCase("El Cazador De La Bruja(RAW)")]
        public void should_not_treat_bare_title_with_unsafe_metadata_as_season_pack(string releaseTitle)
        {
            var series = Builder<Series>.CreateNew()
                .With(s => s.Title = "El Cazador de la Bruja")
                .With(s => s.TvdbId = 79099)
                .Build();

            var criteria = new AnimeSeasonSearchCriteria
            {
                Series = series,
                SeasonNumber = 1,
                SceneTitles = new List<string> { "El Cazador de la Bruja" },
                Episodes = new List<Episode>()
            };

            _reports = new List<ReleaseInfo> { new ReleaseInfo { Title = releaseTitle } };

            _pass1.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<SearchCriteriaBase>())).Returns(DownloadSpecDecision.Accept);
            GivenSpecifications(_pass1);

            var result = Subject.GetSearchDecision(_reports, criteria).ToList();

            result.Should().HaveCount(1);
            result.First().Rejections.Should().Contain(r => r.Message == "Unable to parse release");
        }

        [TestCase("[SavI0r] El Cazador de la Bruja OVA [DVD][480p]")]
        [TestCase("[SavI0r] El Cazador de la Bruja Movie [DVD][480p]")]
        [TestCase("[SavI0r] El Cazador de la Bruja Special [DVD][480p]")]
        public void should_not_treat_bare_title_with_special_keywords_as_season_pack(string releaseTitle)
        {
            var series = Builder<Series>.CreateNew()
                .With(s => s.Title = "El Cazador de la Bruja")
                .With(s => s.TvdbId = 79099)
                .Build();

            var criteria = new AnimeSeasonSearchCriteria
            {
                Series = series,
                SeasonNumber = 1,
                SceneTitles = new List<string> { "El Cazador de la Bruja" },
                Episodes = new List<Episode>()
            };

            _reports = new List<ReleaseInfo> { new ReleaseInfo { Title = releaseTitle } };

            _pass1.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<SearchCriteriaBase>())).Returns(DownloadSpecDecision.Accept);
            GivenSpecifications(_pass1);

            var result = Subject.GetSearchDecision(_reports, criteria).ToList();

            result.Should().HaveCount(1);
            result.First().Rejections.Should().Contain(r => r.Message == "Unable to parse release");
        }

        [TestCase("[SavI0r] El Cazador de la Bruja [DVD][480p][AV1][OPUS][Dual Audio]")]
        public void should_not_treat_bare_title_as_season_pack_for_season_0(string releaseTitle)
        {
            var series = Builder<Series>.CreateNew()
                .With(s => s.Title = "El Cazador de la Bruja")
                .With(s => s.TvdbId = 79099)
                .Build();

            var criteria = new AnimeSeasonSearchCriteria
            {
                Series = series,
                SeasonNumber = 0,
                SceneTitles = new List<string> { "El Cazador de la Bruja" },
                Episodes = new List<Episode>()
            };

            _reports = new List<ReleaseInfo> { new ReleaseInfo { Title = releaseTitle } };

            _pass1.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<SearchCriteriaBase>())).Returns(DownloadSpecDecision.Accept);
            GivenSpecifications(_pass1);

            var result = Subject.GetSearchDecision(_reports, criteria).ToList();

            result.Should().HaveCount(1);
            result.First().Rejections.Should().Contain(r => r.Message == "Unable to parse release");
        }

        [TestCase("[HcLs] Hai to Gensou no Grimgar + OVA [1080p]")]
        [TestCase("[Group] Hai to Gensou no Grimgar OVA [720p]")]
        [TestCase("[Group] Hai to Gensou no Grimgar Special [BD]")]
        public void should_treat_ova_release_as_special_during_special_search(string releaseTitle)
        {
            var series = Builder<Series>.CreateNew()
                .With(s => s.Title = "Hai to Gensou no Grimgar")
                .With(s => s.TvdbId = 306141)
                .With(s => s.SeriesType = SeriesTypes.Anime)
                .Build();

            var episode = Builder<Episode>.CreateNew()
                .With(e => e.SeasonNumber = 0)
                .With(e => e.EpisodeNumber = 1)
                .Build();

            var criteria = new SpecialEpisodeSearchCriteria
            {
                Series = series,
                SceneTitles = new List<string> { "Hai to Gensou no Grimgar" },
                Episodes = new List<Episode> { episode },
                EpisodeQueryTitles = new[] { "Hai to Gensou no Grimgar OVA" }
            };

            _reports = new List<ReleaseInfo> { new ReleaseInfo { Title = releaseTitle } };

            var remoteEpisode = new RemoteEpisode
            {
                Series = series,
                Episodes = new List<Episode> { episode }
            };

            Mocker.GetMock<IParsingService>()
                .Setup(c => c.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<SearchCriteriaBase>()))
                .Returns(remoteEpisode);

            _pass1.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<SearchCriteriaBase>())).Returns(DownloadSpecDecision.Accept);
            GivenSpecifications(_pass1);

            var result = Subject.GetSearchDecision(_reports, criteria).ToList();

            result.Should().HaveCount(1);
            result.First().Approved.Should().BeTrue();

            Mocker.GetMock<IParsingService>()
                .Verify(
                    c => c.Map(
                        It.Is<ParsedEpisodeInfo>(p => p.SeasonNumber == 0 && p.EpisodeNumbers.Contains(1)),
                        It.IsAny<int>(),
                        It.IsAny<int>(),
                        It.IsAny<string>(),
                        It.IsAny<SearchCriteriaBase>()),
                    Times.Once());
        }

        [TestCase("[Group] Hai to Gensou no Grimgar Extras [BD]")]
        [TestCase("[Group] Hai to Gensou no Grimgar NCOP [BD]")]
        [TestCase("[Group] Hai to Gensou no Grimgar NCED [BD]")]
        [TestCase("[Group] Hai to Gensou no Grimgar Menu [BD]")]
        [TestCase("[Group] Hai to Gensou no Grimgar Bonus [BD]")]
        public void should_not_treat_non_episode_markers_as_special(string releaseTitle)
        {
            var series = Builder<Series>.CreateNew()
                .With(s => s.Title = "Hai to Gensou no Grimgar")
                .With(s => s.TvdbId = 306141)
                .With(s => s.SeriesType = SeriesTypes.Anime)
                .Build();

            var episode = Builder<Episode>.CreateNew()
                .With(e => e.SeasonNumber = 0)
                .With(e => e.EpisodeNumber = 1)
                .Build();

            var criteria = new SpecialEpisodeSearchCriteria
            {
                Series = series,
                SceneTitles = new List<string> { "Hai to Gensou no Grimgar" },
                Episodes = new List<Episode> { episode },
                EpisodeQueryTitles = new[] { "Hai to Gensou no Grimgar OVA" }
            };

            _reports = new List<ReleaseInfo> { new ReleaseInfo { Title = releaseTitle } };

            _pass1.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<SearchCriteriaBase>())).Returns(DownloadSpecDecision.Accept);
            GivenSpecifications(_pass1);

            var result = Subject.GetSearchDecision(_reports, criteria).ToList();

            result.Should().HaveCount(1);
            result.First().Rejections.Should().Contain(r => r.Message == "Unable to parse release");
        }

        [Test]
        public void should_not_use_special_fallback_for_multi_special_search()
        {
            var series = Builder<Series>.CreateNew()
                .With(s => s.Title = "Hai to Gensou no Grimgar")
                .With(s => s.TvdbId = 306141)
                .With(s => s.SeriesType = SeriesTypes.Anime)
                .Build();

            var episodes = new List<Episode>
            {
                Builder<Episode>.CreateNew().With(e => e.SeasonNumber = 0).With(e => e.EpisodeNumber = 1).Build(),
                Builder<Episode>.CreateNew().With(e => e.SeasonNumber = 0).With(e => e.EpisodeNumber = 2).Build()
            };

            var criteria = new SpecialEpisodeSearchCriteria
            {
                Series = series,
                SceneTitles = new List<string> { "Hai to Gensou no Grimgar" },
                Episodes = episodes,
                EpisodeQueryTitles = new[] { "Hai to Gensou no Grimgar OVA" }
            };

            _reports = new List<ReleaseInfo> { new ReleaseInfo { Title = "[Group] Hai to Gensou no Grimgar OVA [BD]" } };

            _pass1.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<SearchCriteriaBase>())).Returns(DownloadSpecDecision.Accept);
            GivenSpecifications(_pass1);

            var result = Subject.GetSearchDecision(_reports, criteria).ToList();

            result.Should().HaveCount(1);
            result.First().Rejections.Should().Contain(r => r.Message == "Unable to parse release");
        }

        [Test]
        public void should_not_use_special_fallback_for_non_anime_series()
        {
            var series = Builder<Series>.CreateNew()
                .With(s => s.Title = "Some Show")
                .With(s => s.TvdbId = 12345)
                .With(s => s.SeriesType = SeriesTypes.Standard)
                .Build();

            var episode = Builder<Episode>.CreateNew()
                .With(e => e.SeasonNumber = 0)
                .With(e => e.EpisodeNumber = 1)
                .Build();

            var criteria = new SpecialEpisodeSearchCriteria
            {
                Series = series,
                SceneTitles = new List<string> { "Some Show" },
                Episodes = new List<Episode> { episode },
                EpisodeQueryTitles = new[] { "Some Show Special" }
            };

            _reports = new List<ReleaseInfo> { new ReleaseInfo { Title = "[Group] Some Show Special [BD]" } };

            _pass1.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<SearchCriteriaBase>())).Returns(DownloadSpecDecision.Accept);
            GivenSpecifications(_pass1);

            var result = Subject.GetSearchDecision(_reports, criteria).ToList();

            result.Should().HaveCount(1);
            result.First().Rejections.Should().Contain(r => r.Message == "Unable to parse release");
        }

        [TestCase("[HcLs] Hai to Gensou no Grimgar(Grimgar of Fantasy and Ash) + OVA (Dual Audio) [BD 1080p x265 10bit]_Rokey")]
        [TestCase("[HcLs] Hai to Gensou no Grimgar (Grimgar of Fantasy and Ash) + OVA (Dual Audio) [BD 1080p x265 10bit]_Rokey")]
        [TestCase("[SubGroup] Hai to Gensou no Grimgar(Alt Title) + OVA [720p]")]
        [TestCase("[Group] Hai to Gensou no Grimgar(Alt Title) OVA [BD]-Tag")]
        public void should_match_bare_title_with_inline_alt_name_and_ova(string releaseTitle)
        {
            var series = Builder<Series>.CreateNew()
                .With(s => s.Title = "Hai to Gensou no Grimgar")
                .With(s => s.TvdbId = 306141)
                .With(s => s.SeriesType = SeriesTypes.Anime)
                .Build();

            var episode = Builder<Episode>.CreateNew()
                .With(e => e.SeasonNumber = 0)
                .With(e => e.EpisodeNumber = 1)
                .Build();

            var criteria = new SpecialEpisodeSearchCriteria
            {
                Series = series,
                SceneTitles = new List<string> { "Hai to Gensou no Grimgar" },
                Episodes = new List<Episode> { episode },
                EpisodeQueryTitles = new[] { "Hai to Gensou no Grimgar OVA" }
            };

            _reports = new List<ReleaseInfo> { new ReleaseInfo { Title = releaseTitle } };

            var remoteEpisode = new RemoteEpisode
            {
                Series = series,
                Episodes = new List<Episode> { episode }
            };

            Mocker.GetMock<IParsingService>()
                .Setup(c => c.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<SearchCriteriaBase>()))
                .Returns(remoteEpisode);

            _pass1.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<SearchCriteriaBase>())).Returns(DownloadSpecDecision.Accept);
            GivenSpecifications(_pass1);

            var result = Subject.GetSearchDecision(_reports, criteria).ToList();

            result.Should().HaveCount(1);
            result.First().Approved.Should().BeTrue();

            Mocker.GetMock<IParsingService>()
                .Verify(
                    c => c.Map(
                        It.Is<ParsedEpisodeInfo>(p => p.SeasonNumber == 0 && p.EpisodeNumbers.Contains(1)),
                        It.IsAny<int>(),
                        It.IsAny<int>(),
                        It.IsAny<string>(),
                        It.IsAny<SearchCriteriaBase>()),
                    Times.Once());
        }

        [Test]
        public void should_not_accept_bare_title_via_season_pack_fallback_for_season_0()
        {
            // A title that would succeed via TryBuildAnimeSeasonPackInfo for Season 1
            // must be rejected for Season 0 — the guard returns null immediately.
            var series = Builder<Series>.CreateNew()
                .With(s => s.Title = "Hai to Gensou no Grimgar")
                .With(s => s.TvdbId = 306141)
                .Build();

            var episodes = new List<Episode> { Builder<Episode>.CreateNew().With(e => e.SeasonNumber = 0).Build() };

            var criteria = new AnimeSeasonSearchCriteria
            {
                Series = series,
                SeasonNumber = 0,
                SceneTitles = new List<string> { "Hai to Gensou no Grimgar" },
                Episodes = episodes
            };

            _reports = new List<ReleaseInfo>
            {
                new ReleaseInfo { Title = "[F-D] Hai to Gensou no Grimgar [480P][Dual-Audio]" }
            };

            _pass1.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<SearchCriteriaBase>())).Returns(DownloadSpecDecision.Accept);
            GivenSpecifications(_pass1);

            var result = Subject.GetSearchDecision(_reports, criteria).ToList();

            result.Should().HaveCount(1);
            result.First().Rejections.Should().Contain(r => r.Message == "Unable to parse release");

            // Verify Map was never called — the fallback should not have produced a synthetic ParsedEpisodeInfo
            Mocker.GetMock<IParsingService>()
                .Verify(
                    c => c.Map(
                        It.Is<ParsedEpisodeInfo>(p => p.FullSeason && p.SeasonNumber == 0),
                        It.IsAny<int>(),
                        It.IsAny<int>(),
                        It.IsAny<string>(),
                        It.IsAny<SearchCriteriaBase>()),
                    Times.Never());
        }

        [TestCase("[Group] Sword Art Online S01E01 [720p]")]
        public void should_not_use_bare_title_fallback_when_parser_resolves_normally(string releaseTitle)
        {
            var series = Builder<Series>.CreateNew()
                .With(s => s.Title = "Sword Art Online")
                .With(s => s.TvdbId = 259640)
                .Build();

            var episodes = new List<Episode> { Builder<Episode>.CreateNew().With(e => e.SeasonNumber = 1).Build() };

            var criteria = new AnimeSeasonSearchCriteria
            {
                Series = series,
                SeasonNumber = 1,
                SceneTitles = new List<string> { "Sword Art Online" },
                Episodes = episodes
            };

            _reports = new List<ReleaseInfo> { new ReleaseInfo { Title = releaseTitle } };

            var remoteEpisode = new RemoteEpisode
            {
                Series = series,
                Episodes = episodes
            };

            Mocker.GetMock<IParsingService>()
                .Setup(c => c.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<SearchCriteriaBase>()))
                .Returns(remoteEpisode);

            _pass1.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<SearchCriteriaBase>())).Returns(DownloadSpecDecision.Accept);
            GivenSpecifications(_pass1);

            var result = Subject.GetSearchDecision(_reports, criteria).ToList();

            result.Should().HaveCount(1);
            result.First().Approved.Should().BeTrue();

            // Normal parser should handle this — Map should be called with the parser's output, not a synthetic pack
            Mocker.GetMock<IParsingService>()
                .Verify(
                    c => c.Map(
                        It.Is<ParsedEpisodeInfo>(p => !p.FullSeason),
                        It.IsAny<int>(),
                        It.IsAny<int>(),
                        It.IsAny<string>(),
                        It.IsAny<SearchCriteriaBase>()),
                    Times.Once());
        }

        [TestCase("[Group] Sword Art Online 01-25 [1080p]")]
        public void should_not_use_bare_title_fallback_for_absolute_range_release(string releaseTitle)
        {
            // Absolute range releases should be parsed normally, not by the bare-title fallback
            var series = Builder<Series>.CreateNew()
                .With(s => s.Title = "Sword Art Online")
                .With(s => s.TvdbId = 259640)
                .Build();

            var episodes = new List<Episode> { Builder<Episode>.CreateNew().With(e => e.SeasonNumber = 1).Build() };

            var criteria = new AnimeSeasonSearchCriteria
            {
                Series = series,
                SeasonNumber = 1,
                SceneTitles = new List<string> { "Sword Art Online" },
                Episodes = episodes
            };

            _reports = new List<ReleaseInfo> { new ReleaseInfo { Title = releaseTitle } };

            var remoteEpisode = new RemoteEpisode
            {
                Series = series,
                Episodes = episodes
            };

            Mocker.GetMock<IParsingService>()
                .Setup(c => c.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<SearchCriteriaBase>()))
                .Returns(remoteEpisode);

            _pass1.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<SearchCriteriaBase>())).Returns(DownloadSpecDecision.Accept);
            GivenSpecifications(_pass1);

            var result = Subject.GetSearchDecision(_reports, criteria).ToList();

            result.Should().HaveCount(1);
            result.First().Approved.Should().BeTrue();

            // Should use parser's output, not a synthetic full season pack
            Mocker.GetMock<IParsingService>()
                .Verify(
                    c => c.Map(
                        It.Is<ParsedEpisodeInfo>(p => p.FullSeason),
                        It.IsAny<int>(),
                        It.IsAny<int>(),
                        It.IsAny<string>(),
                        It.IsAny<SearchCriteriaBase>()),
                    Times.Never());
        }

        [Test]
        public void should_not_treat_bare_title_with_mismatched_tvdbid_as_season_pack()
        {
            var series = Builder<Series>.CreateNew()
                .With(s => s.Title = "El Cazador de la Bruja")
                .With(s => s.TvdbId = 79099)
                .Build();

            var criteria = new AnimeSeasonSearchCriteria
            {
                Series = series,
                SeasonNumber = 1,
                SceneTitles = new List<string> { "El Cazador de la Bruja" },
                Episodes = new List<Episode>()
            };

            // Report has a different TvdbId — should be rejected
            _reports = new List<ReleaseInfo>
            {
                new ReleaseInfo { Title = "[Group] El Cazador de la Bruja [DVD]", TvdbId = 99999 }
            };

            _pass1.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<SearchCriteriaBase>())).Returns(DownloadSpecDecision.Accept);
            GivenSpecifications(_pass1);

            var result = Subject.GetSearchDecision(_reports, criteria).ToList();

            result.Should().HaveCount(1);
            result.First().Rejections.Should().Contain(r => r.Message == "Unable to parse release");
        }

        [Test]
        public void should_not_use_special_fallback_for_non_season0_episode()
        {
            // Ensure the OVA/Special fallback only fires for Season 0 episodes
            var series = Builder<Series>.CreateNew()
                .With(s => s.Title = "Test Anime")
                .With(s => s.SeriesType = SeriesTypes.Anime)
                .Build();

            var episode = Builder<Episode>.CreateNew()
                .With(e => e.SeasonNumber = 1)
                .With(e => e.EpisodeNumber = 1)
                .Build();

            var criteria = new SpecialEpisodeSearchCriteria
            {
                Series = series,
                SceneTitles = new List<string> { "Test Anime" },
                Episodes = new List<Episode> { episode },
                EpisodeQueryTitles = new[] { "Test Anime OVA" }
            };

            _reports = new List<ReleaseInfo> { new ReleaseInfo { Title = "[Group] Test Anime OVA [BD]" } };

            _pass1.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<SearchCriteriaBase>())).Returns(DownloadSpecDecision.Accept);
            GivenSpecifications(_pass1);

            var result = Subject.GetSearchDecision(_reports, criteria).ToList();

            result.Should().HaveCount(1);
            result.First().Rejections.Should().Contain(r => r.Message == "Unable to parse release");
        }
    }
}
