# sonarr-anime

[![Build](https://github.com/RemingtonDev/sonarr-anime/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/RemingtonDev/sonarr-anime/actions/workflows/build.yml)
[![GitHub Release](https://img.shields.io/github/v/release/RemingtonDev/sonarr-anime)](https://github.com/RemingtonDev/sonarr-anime/releases/latest)

sonarr-anime is a minimal [Sonarr](https://github.com/Sonarr/Sonarr) v4 fork for anime fans who already like Sonarr but want a better anime search experience. It keeps Sonarr behaving like Sonarr, while improving anime search so it can find packs first and avoid hammering indexers with episode-by-episode queries when a batch release already covers what you need.

## Why this fork exists

Upstream Sonarr is the right choice for almost everything, but anime releases often show up in season packs, batch packs, specials, and naming styles that make a pack-first approach especially useful. This fork focuses on that narrow problem and nothing more.

- **Pack-first anime search** helps reduce redundant queries by trying broad anime pack matches before falling back to individual episode searches.
- **Better anime pack handling** improves matching for common batch release formats, including multi-season packs and anime specials/OVAs.
- **Minimal fork scope** keeps the rest of the experience familiar for people who already use Sonarr and just want better anime search behavior.

## What stays the same

This is not a reinvention of Sonarr. The goal is to keep the fork close to upstream Sonarr and limit changes to the anime-specific search path.

The UI, library management, download clients, quality profiles, indexer configuration, and non-anime behavior are intended to stay effectively upstream. In practice, this fork should feel like stock Sonarr for everything except the anime search improvements it was created to add. The promise here is simple: better anime search, with minimal impact on the codebase and on the day-to-day Sonarr experience you already know.

## Tested and compatibility

This fork has been tested on anime indexers through Prowlarr, with the focus on the kinds of public anime-oriented sources where pack releases are common and query efficiency matters.

That means the current validation is strongest for anime indexers and Prowlarr-backed setups. Private trackers have not been broadly validated yet, so they should be treated as possible but not guaranteed. If you run into compatibility issues, reports are welcome. Debug logs are especially helpful, and if a private tracker needs fork-specific fixes, that work can be planned if logs are provided or testing access is shared.

## Keeping up with Sonarr

Sonarr is the upstream project, and this fork is downstream from it. sonarr-anime tracks upstream Sonarr v4 stable releases rather than trying to become a separate product line.

When upstream Sonarr ships a new stable release, this repository syncs that release and reapplies the small set of anime-specific changes on top. That keeps the fork easier to review, easier to maintain, and much closer to upstream Sonarr than a broad long-term divergence would be.

## Get it

Download release builds from the [GitHub Releases page](https://github.com/RemingtonDev/sonarr-anime/releases/latest).

Container images are published at [ghcr.io/remingtondev/sonarr-anime](https://github.com/RemingtonDev/sonarr-anime/pkgs/container/sonarr-anime).

## Support

Issues and PRs are welcome for anime-search regressions, anime indexer compatibility, and other fork-specific behavior. If something breaks, a bug report with debug logs is the most useful starting point.

If a private tracker needs testing, fixes can still be planned. Logs are helpful, and if hands-on testing is needed, sharing an invite or test access can make that possible. For general Sonarr questions or non-anime behavior, the best place is still the [upstream Sonarr community](https://wiki.servarr.com/sonarr).

## License

Same as upstream Sonarr: [GNU GPL v3](http://www.gnu.org/licenses/gpl.html).
