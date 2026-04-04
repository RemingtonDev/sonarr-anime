using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine.Specifications.Search;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.DecisionEngineTests.Search
{
    [TestFixture]
    public class SeasonMatchSpecificationFixture : TestBase<SeasonMatchSpecification>
    {
        private RemoteEpisode _remoteEpisode;

        [SetUp]
        public void Setup()
        {
            var series = Builder<Series>.CreateNew()
                .Build();

            _remoteEpisode = new RemoteEpisode
            {
                Series = series,
                ParsedEpisodeInfo = new ParsedEpisodeInfo
                {
                    SeasonNumber = 1,
                    FullSeason = true
                }
            };
        }

        [Test]
        public void should_accept_when_no_search_criteria()
        {
            Subject.IsSatisfiedBy(_remoteEpisode, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_reject_standard_season_mismatch()
        {
            var criteria = new SeasonSearchCriteria { SeasonNumber = 3 };

            Subject.IsSatisfiedBy(_remoteEpisode, criteria).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_accept_standard_season_match()
        {
            var criteria = new SeasonSearchCriteria { SeasonNumber = 1 };

            Subject.IsSatisfiedBy(_remoteEpisode, criteria).Accepted.Should().BeTrue();
        }
    }
}
