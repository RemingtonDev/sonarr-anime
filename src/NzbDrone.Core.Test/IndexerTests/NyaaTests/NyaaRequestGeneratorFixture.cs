using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Indexers.Nyaa;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.IndexerTests.NyaaTests
{
    public class NyaaRequestGeneratorFixture : CoreTest<NyaaRequestGenerator>
    {
        private SeasonSearchCriteria _seasonSearchCriteria;
        private AnimeEpisodeSearchCriteria _animeSearchCriteria;
        private AnimeSeasonSearchCriteria _animeSeasonSearchCriteria;

        [SetUp]
        public void SetUp()
        {
            Subject.Settings = new NyaaSettings()
            {
                BaseUrl = "http://127.0.0.1:1234/",
            };

            _seasonSearchCriteria = new SeasonSearchCriteria()
            {
                SceneTitles = new List<string>() { "Naruto Shippuuden" },
                SeasonNumber = 1,
            };

            _animeSearchCriteria = new AnimeEpisodeSearchCriteria()
            {
                SceneTitles = new List<string>() { "Naruto Shippuuden" },
                AbsoluteEpisodeNumber = 9,
                SeasonNumber = 1,
                EpisodeNumber = 9
            };

            _animeSeasonSearchCriteria = new AnimeSeasonSearchCriteria()
            {
                SceneTitles = new List<string>() { "Naruto Shippuuden" },
                SeasonNumber = 3
            };
        }

        [Test]
        public void should_not_search_season()
        {
            var results = Subject.GetSearchRequests(_seasonSearchCriteria);

            results.GetAllTiers().Should().HaveCount(0);
        }

        [Test]
        public void should_search_season()
        {
            Subject.Settings.AnimeStandardFormatSearch = true;
            var results = Subject.GetSearchRequests(_seasonSearchCriteria);

            results.GetAllTiers().Should().HaveCount(1);

            var page = results.GetAllTiers().First().First();

            page.Url.FullUri.Should().Contain("term=Naruto+Shippuuden+s01");
        }

        [Test]
        public void should_use_only_absolute_numbering_for_anime_search()
        {
            var results = Subject.GetSearchRequests(_animeSearchCriteria);

            results.GetTier(0).Should().HaveCount(2);
            var pages = results.GetTier(0).Take(2).Select(t => t.First()).ToList();

            pages[0].Url.FullUri.Should().Contain("term=Naruto+Shippuuden+9");
            pages[1].Url.FullUri.Should().Contain("term=Naruto+Shippuuden+09");
        }

        [Test]
        public void should_also_use_standard_numbering_for_anime_search()
        {
            Subject.Settings.AnimeStandardFormatSearch = true;
            var results = Subject.GetSearchRequests(_animeSearchCriteria);

            results.GetTier(0).Should().HaveCount(3);
            var pages = results.GetTier(0).Take(3).Select(t => t.First()).ToList();

            pages[0].Url.FullUri.Should().Contain("term=Naruto+Shippuuden+9");
            pages[1].Url.FullUri.Should().Contain("term=Naruto+Shippuuden+09");
            pages[2].Url.FullUri.Should().Contain("term=Naruto+Shippuuden+s01e09");
        }

        [Test]
        public void should_search_by_standard_season_number()
        {
            Subject.Settings.AnimeStandardFormatSearch = true;
            var results = Subject.GetSearchRequests(_animeSeasonSearchCriteria);

            results.GetTier(0).Should().HaveCount(2);

            var pages = results.GetTier(0).Select(t => t.First()).ToList();

            pages[0].Url.FullUri.Should().Contain("term=Naruto+Shippuuden");
            pages[0].Url.FullUri.Should().NotContain("+s03");
            pages[1].Url.FullUri.Should().Contain("term=Naruto+Shippuuden+s03");
        }

        [Test]
        public void should_search_broad_title_only_when_standard_format_disabled()
        {
            Subject.Settings.AnimeStandardFormatSearch = false;
            var results = Subject.GetSearchRequests(_animeSeasonSearchCriteria);

            results.GetTier(0).Should().HaveCount(1);

            var page = results.GetTier(0).First().First();

            page.Url.FullUri.Should().Contain("term=Naruto+Shippuuden");
            page.Url.FullUri.Should().NotContain("+s");
        }

        [Test]
        public void should_emit_broad_query_per_scene_title()
        {
            _animeSeasonSearchCriteria.SceneTitles = new List<string> { "Title One", "Title Two" };

            var results = Subject.GetSearchRequests(_animeSeasonSearchCriteria);

            results.GetTier(0).Should().HaveCount(2);

            var pages = results.GetTier(0).Select(t => t.First()).ToList();

            pages[0].Url.FullUri.Should().Contain("term=Title+One");
            pages[1].Url.FullUri.Should().Contain("term=Title+Two");
        }

        [Test]
        public void should_emit_broad_and_standard_queries_per_scene_title()
        {
            Subject.Settings.AnimeStandardFormatSearch = true;
            _animeSeasonSearchCriteria.SceneTitles = new List<string> { "Title One", "Title Two" };

            var results = Subject.GetSearchRequests(_animeSeasonSearchCriteria);

            results.GetTier(0).Should().HaveCount(4);

            var pages = results.GetTier(0).Select(t => t.First()).ToList();

            pages[0].Url.FullUri.Should().Contain("term=Title+One");
            pages[0].Url.FullUri.Should().NotContain("+s");
            pages[1].Url.FullUri.Should().Contain("term=Title+One+s03");
            pages[2].Url.FullUri.Should().Contain("term=Title+Two");
            pages[2].Url.FullUri.Should().NotContain("+s");
            pages[3].Url.FullUri.Should().Contain("term=Title+Two+s03");
        }

        [Test]
        public void should_deduplicate_identical_scene_titles()
        {
            Subject.Settings.AnimeStandardFormatSearch = true;
            _animeSeasonSearchCriteria.SceneTitles = new List<string> { "Naruto Shippuuden", "Naruto Shippuuden", "Other Title" };

            var results = Subject.GetSearchRequests(_animeSeasonSearchCriteria);

            results.GetTier(0).Should().HaveCount(4);

            var pages = results.GetTier(0).Select(t => t.First()).ToList();

            pages[0].Url.FullUri.Should().Contain("term=Naruto+Shippuuden");
            pages[0].Url.FullUri.Should().NotContain("+s");
            pages[1].Url.FullUri.Should().Contain("term=Naruto+Shippuuden+s03");
            pages[2].Url.FullUri.Should().Contain("term=Other+Title");
            pages[2].Url.FullUri.Should().NotContain("+s");
            pages[3].Url.FullUri.Should().Contain("term=Other+Title+s03");
        }

        [Test]
        public void should_suppress_broad_query_already_emitted_in_series_search()
        {
            Subject.Settings.AnimeStandardFormatSearch = true;

            var broadEmitted = new HashSet<string> { "Naruto Shippuuden" };

            _animeSeasonSearchCriteria.BroadQueriesEmitted = broadEmitted;

            var results = Subject.GetSearchRequests(_animeSeasonSearchCriteria);

            // Should only have the season-specific query, not the broad title-only one
            results.GetTier(0).Should().HaveCount(1);

            var page = results.GetTier(0).First().First();
            page.Url.FullUri.Should().Contain("term=Naruto+Shippuuden+s03");
        }

        [Test]
        public void should_emit_broad_query_when_not_yet_in_emitted_set()
        {
            Subject.Settings.AnimeStandardFormatSearch = true;

            var broadEmitted = new HashSet<string>();
            _animeSeasonSearchCriteria.BroadQueriesEmitted = broadEmitted;

            var results = Subject.GetSearchRequests(_animeSeasonSearchCriteria);

            // Should have both broad and season-specific queries
            results.GetTier(0).Should().HaveCount(2);
            broadEmitted.Should().BeEmpty();
        }

        [Test]
        public void should_still_emit_broad_query_when_no_emitted_set_provided()
        {
            Subject.Settings.AnimeStandardFormatSearch = true;
            _animeSeasonSearchCriteria.BroadQueriesEmitted = null;

            var results = Subject.GetSearchRequests(_animeSeasonSearchCriteria);

            results.GetTier(0).Should().HaveCount(2);

            var pages = results.GetTier(0).Select(t => t.First()).ToList();
            pages[0].Url.FullUri.Should().Contain("term=Naruto+Shippuuden");
            pages[0].Url.FullUri.Should().NotContain("+s");
        }

        [Test]
        public void should_not_mutate_shared_broad_query_state()
        {
            Subject.Settings.AnimeStandardFormatSearch = true;

            var broadEmitted = new HashSet<string> { "Other Title" };
            _animeSeasonSearchCriteria.BroadQueriesEmitted = broadEmitted;

            Subject.GetSearchRequests(_animeSeasonSearchCriteria);

            broadEmitted.Should().BeEquivalentTo(new[] { "Other Title" });
        }

        [Test]
        public void should_emit_s00e01_for_single_episode_search_with_season_0()
        {
            Subject.Settings.AnimeStandardFormatSearch = true;

            var criteria = new SingleEpisodeSearchCriteria
            {
                SceneTitles = new List<string> { "Grimgar" },
                SeasonNumber = 0,
                EpisodeNumber = 1
            };

            var results = Subject.GetSearchRequests(criteria);

            results.GetTier(0).Should().HaveCount(1);

            var page = results.GetTier(0).First().First();
            page.Url.FullUri.Should().Contain("term=Grimgar+s00e01");
        }

        [Test]
        public void should_not_emit_s00e01_when_standard_format_search_disabled()
        {
            Subject.Settings.AnimeStandardFormatSearch = false;

            var criteria = new SingleEpisodeSearchCriteria
            {
                SceneTitles = new List<string> { "Grimgar" },
                SeasonNumber = 0,
                EpisodeNumber = 1
            };

            var results = Subject.GetSearchRequests(criteria);

            results.GetAllTiers().Should().HaveCount(0);
        }

        [Test]
        public void should_emit_anime_special_alias_queries_for_anime_series()
        {
            var criteria = new SpecialEpisodeSearchCriteria
            {
                Series = new Series { SeriesType = SeriesTypes.Anime },
                SceneTitles = new List<string> { "Grimgar" },
                EpisodeQueryTitles = new[] { "Grimgar OVA 2.5" }
            };

            var results = Subject.GetSearchRequests(criteria);
            var pages = results.GetAllTiers().Select(t => t.First()).ToList();

            // 1 episode-title query + 4 alias queries (ova, oav, special, specials)
            pages.Should().HaveCount(5);
            pages[0].Url.FullUri.Should().Contain("term=Grimgar+OVA+2.5");
            pages[1].Url.FullUri.Should().Contain("term=Grimgar+ova");
            pages[2].Url.FullUri.Should().Contain("term=Grimgar+oav");
            pages[3].Url.FullUri.Should().Contain("term=Grimgar+special");
            pages[4].Url.FullUri.Should().Contain("term=Grimgar+specials");
        }

        [Test]
        public void should_not_emit_anime_special_alias_queries_for_non_anime_series()
        {
            var criteria = new SpecialEpisodeSearchCriteria
            {
                Series = new Series { SeriesType = SeriesTypes.Standard },
                SceneTitles = new List<string> { "Some Show" },
                EpisodeQueryTitles = new[] { "Some Show Special Episode" }
            };

            var results = Subject.GetSearchRequests(criteria);
            var pages = results.GetAllTiers().Select(t => t.First()).ToList();

            // Only the episode-title query, no alias queries
            pages.Should().HaveCount(1);
            pages[0].Url.FullUri.Should().Contain("term=Some+Show+Special+Episode");
        }
    }
}
