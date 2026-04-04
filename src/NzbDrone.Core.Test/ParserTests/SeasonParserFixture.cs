using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.ParserTests
{
    [TestFixture]
    public class SeasonParserFixture : CoreTest
    {
        [TestCase("30.Series.Season.04.HDTV.XviD-DIMENSION", "30 Series", 4)]
        [TestCase("Sonarr.and.Series.S02.720p.x264-DIMENSION", "Sonarr and Series", 2)]
        [TestCase("The.Series.US.S03.720p.x264-DIMENSION", "The Series US", 3)]
        [TestCase(@"Series.of.Sonarr.S03.720p.BluRay-CLUE\REWARD", "Series of Sonarr", 3)]
        [TestCase("Series Time S02 720p HDTV x264 CRON", "Series Time", 2)]
        [TestCase("Series.2021.S04.iNTERNAL.DVDRip.XviD-VCDVaULT", "Series 2021", 4)]
        [TestCase("Series Five 0 S01 720p WEB DL DD5 1 H 264 NT", "Series Five 0", 1)]
        [TestCase("30 Series S03 WS PDTV XviD FUtV", "30 Series", 3)]
        [TestCase("The Series Season 4 WS PDTV XviD FUtV", "The Series", 4)]
        [TestCase("Series Season 1 720p WEB DL DD 5 1 h264 TjHD", "Series", 1)]
        [TestCase("The Series Season4 WS PDTV XviD FUtV", "The Series", 4)]
        [TestCase("Series S 01 720p WEB DL DD 5 1 h264 TjHD", "Series", 1)]
        [TestCase("Series Confidential   Season 3", "Series Confidential", 3)]
        [TestCase("Series.S01.720p.WEBDL.DD5.1.H.264-NTb", "Series", 1)]
        [TestCase("Series.Makes.It.Right.S02.720p.HDTV.AAC5.1.x265-NOGRP", "Series Makes It Right", 2)]
        [TestCase("My.Series.S2014.720p.HDTV.x264-ME", "My Series", 2014)]
        [TestCase("Series.Saison3.VOSTFR.HDTV.XviD-NOTAG", "Series", 3)]
        [TestCase("Series.SAISON.1.VFQ.PDTV.H264-ACC-ROLLED", "Series", 1)]
        [TestCase("Series Title - Series 1 (1970) DivX", "Series Title", 1)]
        [TestCase("SeriesTitle.S03.540p.AMZN.WEB-DL.DD+2.0.x264-RTN", "SeriesTitle", 3)]
        [TestCase("Series.Title.S01.576p.BluRay.DD5.1.x264-HiSD", "Series Title", 1)]
        [TestCase("Series.Stagione.3.HDTV.XviD-NOTAG", "Series", 3)]
        [TestCase("Series.Stagione.3.HDTV.XviD-NOTAG", "Series", 3)]
        [TestCase("Series No More S01 2023 1080p WEB-DL AVC AC3 2.0 Dual Audio -ZR-", "Series No More", 1)]
        [TestCase("Series Title / S1E1-8 of 8 [2024, WEB-DL 1080p] + Original + RUS", "Series Title", 1)]
        [TestCase("Series Title / S2E1-16 of 16 [2022, WEB-DL] RUS", "Series Title", 2)]
        [TestCase("[hchcsen] Mobile Series 00 S01 [BD Remux Dual Audio 1080p AVC 2xFLAC] (Kidou Senshi Gundam 00 Season 1)", "Mobile Series 00", 1)]
        [TestCase("[HorribleRips] Mobile Series 00 S1 [1080p]", "Mobile Series 00", 1)]
        [TestCase("[Zoombie] Series 100: Bucket List S01 [Web][MKV][h265 10-bit][1080p][AC3 2.0][Softsubs (Zoombie)]", "Series 100: Bucket List", 1)]
        [TestCase("Seriesless (2016/S01/WEB-DL/1080p/AC3 5.1/DUAL/SUB)", "Seriesless (2016)", 1)]
        [TestCase("Series Title S01 (1080p BluRay x265 HEVC 10 bits AAC 5.1 Tigole)", "Series Title", 1)]
        public void should_parse_full_season_release(string postTitle, string title, int season)
        {
            var result = Parser.Parser.ParseTitle(postTitle);
            result.SeasonNumber.Should().Be(season);
            result.SeriesTitle.Should().Be(title);
            result.EpisodeNumbers.Should().BeEmpty();
            result.AbsoluteEpisodeNumbers.Should().BeEmpty();
            result.FullSeason.Should().BeTrue();
        }

        [TestCase("Acropolis Series S05 EXTRAS DVDRip XviD RUNNER", "Acropolis Series", 5)]
        [TestCase("Punky Series S01 EXTRAS DVDRip XviD RUNNER", "Punky Series", 1)]
        [TestCase("Instant Series S03 EXTRAS DVDRip XviD OSiTV", "Instant Series", 3)]
        [TestCase("The.Series.S03.Extras.01.Deleted.Scenes.720p", "The Series", 3)]
        [TestCase("The.Series.S03.Extras.02.720p", "The Series", 3)]
        public void should_parse_season_extras(string postTitle, string title, int season)
        {
            var result = Parser.Parser.ParseTitle(postTitle);
            result.SeasonNumber.Should().Be(season);
            result.SeriesTitle.Should().Be(title);
            result.EpisodeNumbers.Should().BeEmpty();
            result.AbsoluteEpisodeNumbers.Should().BeEmpty();
            result.FullSeason.Should().BeTrue();
            result.IsSeasonExtra.Should().BeTrue();
        }

        [TestCase("Series.to.Me.S03.SUBPACK.DVDRip.XviD-REWARD", "Series to Me", 3)]
        [TestCase("The.Series.S02.SUBPACK.DVDRip.XviD-REWARD", "The Series", 2)]
        [TestCase("Series.S11.SUBPACK.DVDRip.XviD-REWARD", "Series", 11)]
        public void should_parse_season_subpack(string postTitle, string title, int season)
        {
            var result = Parser.Parser.ParseTitle(postTitle);
            result.SeasonNumber.Should().Be(season);
            result.SeriesTitle.Should().Be(title);
            result.EpisodeNumbers.Should().BeEmpty();
            result.AbsoluteEpisodeNumbers.Should().BeEmpty();
            result.FullSeason.Should().BeTrue();
            result.IsSeasonExtra.Should().BeTrue();
        }

        [TestCase("The.Series.2016.S02.Part.1.1080p.NF.WEBRip.DD5.1.x264-NTb", "The Series 2016", 2, 1)]
        [TestCase("The.Series.S07.Vol.1.1080p.NF.WEBRip.DD5.1.x264-NTb", "The Series", 7, 1)]
        [TestCase("The.Series.S06.P1.1080p.Blu-Ray.10-Bit.Dual-Audio.TrueHD.x265-iAHD", "The Series", 6, 1)]
        public void should_parse_partial_season_release(string postTitle, string title, int season, int seasonPart)
        {
            var result = Parser.Parser.ParseTitle(postTitle);
            result.SeasonNumber.Should().Be(season);
            result.SeriesTitle.Should().Be(title);
            result.EpisodeNumbers.Should().BeEmpty();
            result.AbsoluteEpisodeNumbers.Should().BeEmpty();
            result.FullSeason.Should().BeFalse();
            result.IsPartialSeason.Should().BeTrue();
            result.SeasonPart.Should().Be(seasonPart);
        }

        [TestCase("The Series S01-05 WS BDRip X264-REWARD-No Rars", "The Series", 1)]
        [TestCase("Series.Title.S01-S09.1080p.AMZN.WEB-DL.DDP2.0.H.264-NTb", "Series Title", 1)]
        [TestCase("Series Title S01 - S07 BluRay 1080p x264 REPACK -SacReD", "Series Title", 1)]
        [TestCase("Series Title Season 01-07 BluRay 1080p x264 REPACK -SacReD", "Series Title", 1)]
        [TestCase("Series Title Season 01 - Season 07 BluRay 1080p x264 REPACK -SacReD", "Series Title", 1)]
        [TestCase("Series Title Complete Series S01 S04 (1080p BluRay x265 HEVC 10bit AAC 5.1 Vyndros)", "Series Title", 1)]
        [TestCase("Series Title S01 S04 (1080p BluRay x265 HEVC 10bit AAC 5.1 Vyndros)", "Series Title", 1)]
        [TestCase("Series Title S01 04 (1080p BluRay x265 HEVC 10bit AAC 5.1 Vyndros)", "Series Title", 1)]
        public void should_parse_multi_season_release(string postTitle, string title, int firstSeason)
        {
            var result = Parser.Parser.ParseTitle(postTitle);
            result.SeasonNumber.Should().Be(firstSeason);
            result.SeriesTitle.Should().Be(title);
            result.EpisodeNumbers.Should().BeEmpty();
            result.AbsoluteEpisodeNumbers.Should().BeEmpty();
            result.FullSeason.Should().BeTrue();
            result.IsPartialSeason.Should().BeFalse();
            result.IsMultiSeason.Should().BeTrue();
        }

        [TestCase("[SubGroup] Sword Art Online S01-S04 1080p BluRay x264", "Sword Art Online", 1, new[] { 1, 2, 3, 4 })]
        [TestCase("[SubGroup] Sword Art Online S01-S02 1080p BluRay", "Sword Art Online", 1, new[] { 1, 2 })]
        [TestCase("[Reaktor] Attack on Titan S01-S03 BD 1080p", "Attack on Titan", 1, new[] { 1, 2, 3 })]
        public void should_parse_anime_multi_season_range(string postTitle, string title, int firstSeason, int[] expectedSeasons)
        {
            var result = Parser.Parser.ParseTitle(postTitle);
            result.SeriesTitle.Should().Be(title);
            result.SeasonNumber.Should().Be(firstSeason);
            result.FullSeason.Should().BeTrue();
            result.IsMultiSeason.Should().BeTrue();
            result.SeasonNumbers.Should().BeEquivalentTo(expectedSeasons);
        }

        [TestCase("[SubGroup] Sword Art Online S01+S02+S03+S04 1080p BluRay", "Sword Art Online", 1, new[] { 1, 2, 3, 4 })]
        [TestCase("[SubGroup] Title S01+S03 1080p", "Title", 1, new[] { 1, 3 })]
        [TestCase("[Tenrai-Sensei] Sword Art Online S1+S2+S3+S4+The Movie+OVAs", "Sword Art Online", 1, new[] { 1, 2, 3, 4 })]
        [TestCase("[Anime Time] Sword Art Online (S01+S02+S03+S04+...)", "Sword Art Online", 1, new[] { 1, 2, 3, 4 })]
        [TestCase("[Kosaka] Sword Art Online (S01+S02+S03+S04+...)", "Sword Art Online", 1, new[] { 1, 2, 3, 4 })]
        public void should_parse_anime_multi_season_list(string postTitle, string title, int firstSeason, int[] expectedSeasons)
        {
            var result = Parser.Parser.ParseTitle(postTitle);
            result.SeriesTitle.Should().Be(title);
            result.SeasonNumber.Should().Be(firstSeason);
            result.FullSeason.Should().BeTrue();
            result.IsMultiSeason.Should().BeTrue();
            result.SeasonNumbers.Should().BeEquivalentTo(expectedSeasons);
        }

        [TestCase("[Anime Time] Sword Art Online (S01+S02+S03+S04+Movies+Specials+OVAs) [BD][1080p][HEVC 10bit x265][AAC][Eng Sub]", "Sword Art Online", 1, new[] { 1, 2, 3, 4 })]
        [TestCase("[Kosaka] Sword Art Online (S01+S02+S03+S04+Movies+Specials+OVAs) [BD 1080p HEVC x265 10bit][Dual Audio][Multi Subs]", "Sword Art Online", 1, new[] { 1, 2, 3, 4 })]
        [TestCase("[Tenrai-Sensei] Sword Art Online S1+S2+S3+S4+The Movie+OVAs [BD 1080p]", "Sword Art Online", 1, new[] { 1, 2, 3, 4 })]
        [TestCase("[AceAres] Sword Art Online S01+S02+S03+S04 + Movie + Special [BD 1080p HEVC x265]", "Sword Art Online", 1, new[] { 1, 2, 3, 4 })]
        public void should_parse_anime_multi_season_with_specials_metadata(string postTitle, string title, int firstSeason, int[] expectedSeasons)
        {
            var result = Parser.Parser.ParseTitle(postTitle);
            result.Should().NotBeNull();
            result.SeriesTitle.Should().Be(title);
            result.SeasonNumber.Should().Be(firstSeason);
            result.IsMultiSeason.Should().BeTrue();
            result.FullSeason.Should().BeTrue();
            result.Special.Should().BeFalse();
            result.SeasonNumbers.Should().BeEquivalentTo(expectedSeasons);
        }

        [TestCase("[SubGroup] Sword Art Online S01 1080p BluRay x264")]
        [TestCase("[HorribleRips] Mobile Suit Gundam 00 S1 [1080p]")]
        public void should_not_parse_anime_single_season_as_multi(string postTitle)
        {
            var result = Parser.Parser.ParseTitle(postTitle);
            result.Should().NotBeNull();
            result.FullSeason.Should().BeTrue();
            result.IsMultiSeason.Should().BeFalse();
        }

        [TestCase("Series Title S03 Specials DVDRip XviD RUNNER", "Series Title", 3)]
        [TestCase("Series Title S05 Special 720p HDTV", "Series Title", 5)]
        public void should_downgrade_single_season_special_to_special(string postTitle, string title, int season)
        {
            var result = Parser.Parser.ParseTitle(postTitle);
            result.Should().NotBeNull();
            result.SeriesTitle.Should().Be(title);
            result.SeasonNumber.Should().Be(season);
            result.FullSeason.Should().BeFalse();
            result.Special.Should().BeTrue();
            result.IsMultiSeason.Should().BeFalse();
        }

        [TestCase("[SubGroup] Title S01+S02+")]
        [TestCase("[SubGroup] Title S01+S02+Sx")]
        [TestCase("[SubGroup] Title S01+S02+1")]
        public void should_not_parse_malformed_anime_multi_season_tail(string postTitle)
        {
            var result = Parser.Parser.ParseTitle(postTitle);

            // May parse as single-season via another regex, or fail entirely;
            // either way it must not be detected as a multi-season pack
            if (result != null)
            {
                result.IsMultiSeason.Should().BeFalse();
            }
        }

        [Test]
        public void should_populate_season_numbers_for_standard_multi_season()
        {
            var result = Parser.Parser.ParseTitle("Series.Title.S01-S09.1080p.AMZN.WEB-DL.DDP2.0.H.264-NTb");
            result.IsMultiSeason.Should().BeTrue();
            result.SeasonNumbers.Should().BeEquivalentTo(Enumerable.Range(1, 9));
        }

        [TestCase("[SubGroup] Grimgar S01+Specials [1080p]", "Grimgar", 1)]
        [TestCase("[SubGroup] Grimgar S01+Special [1080p]", "Grimgar", 1)]
        [TestCase("[SubGroup] Grimgar S01+OVA [1080p]", "Grimgar", 1)]
        [TestCase("[SubGroup] Grimgar S01+OAV [1080p]", "Grimgar", 1)]
        [TestCase("[SubGroup] Grimgar S01 + OVA [1080p]", "Grimgar", 1)]
        [TestCase("[SubGroup] Grimgar S01 + Specials [1080p]", "Grimgar", 1)]
        [TestCase("[Trix] Grimgar: Ashes and Illusions S01+OVA (Batch) [BDRip 1080p AV1 Opus] (Dual Audio, Multi Subs, VOSTFR) | Hai to Gensou no Grimgar 灰と幻想のグリムガル Season S1 OAV Specials", "Grimgar: Ashes and Illusions", 1)]
        public void should_parse_anime_season_plus_specials_as_full_season(string postTitle, string title, int season)
        {
            var result = Parser.Parser.ParseTitle(postTitle);
            result.Should().NotBeNull();
            result.SeriesTitle.Should().Be(title);
            result.SeasonNumber.Should().Be(season);
            result.FullSeason.Should().BeTrue();
            result.Special.Should().BeFalse();
            result.IsMultiSeason.Should().BeFalse();
        }

        [Test]
        public void should_not_parse_season_folders()
        {
            var result = Parser.Parser.ParseTitle("Season 3");
            result.Should().BeNull();
        }
    }
}
