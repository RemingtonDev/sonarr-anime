# sonarr-anime

[![Build](https://github.com/RemingtonDev/sonarr-anime/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/RemingtonDev/sonarr-anime/actions/workflows/build.yml)
[![GitHub Release](https://img.shields.io/github/v/release/RemingtonDev/sonarr-anime)](https://github.com/RemingtonDev/sonarr-anime/releases/latest)
[![GHCR Image](https://img.shields.io/badge/ghcr.io-sonarr--anime-blue)](https://github.com/RemingtonDev/sonarr-anime/pkgs/container/sonarr-anime)

A minimal [Sonarr](https://github.com/Sonarr/Sonarr) v4 fork focused on reducing redundant anime search queries. It patches the search pipeline to try pack search first, season search second, and per-episode only as a last resort.

## What this fork does

Upstream Sonarr's anime search always falls through to per-episode queries — one HTTP request per episode — creating unnecessary load on genre-oriented indexers. This fork inserts intelligent early-exit checks into the search cascade:

1. **Pack search** — if an approved release covers all wanted episodes across seasons, stop immediately
2. **Season search** — per season, if an approved release covers all wanted episodes, skip its episodes
3. **Per-episode search** — only for episodes not covered by steps 1-2

Additional capabilities:
- **Multi-season pack support** — recognizes `S01-S04` range and `S01+S02+S03+S04` list formats, uses them to skip later-season queries
- **Bare-title pack matching** — during anime season search, releases like `[Group] Example Anime Title [1080p]` can be matched as Season 1 even when they include no episode numbers, no `S01`, and no `Season 1`, as long as they are just the series title plus safe trailing metadata
- **Anime specials/OVAs** — Season 0 searches use OVA/Special-aware query generation instead of the pack fallback
- **Bare-title + OVA matching** — releases with inline alternate-title parentheses and trailing metadata are correctly recognized during Season 0 special search
- **S01+Specials co-coverage** — when a season pack explicitly advertises special content (e.g. `S01+Specials`, `S01+OVA`), the pack maps both the season episodes and exactly one wanted Season 0 special, allowing the S00 search to be skipped entirely
- **Pack-first parity across supported backends** — protocol-backed search backends emit a broad title-only query before season-specific queries, matching the native pack-first behavior. Cross-season dedup prevents the same broad query from being sent twice
- **Grab history fallback for unparseable titles** — when a download completes with a title that cannot be parsed (common for some anime packs), the fork falls back to the original grab history to identify the series and episodes. This prevents automatic imports from being blocked for releases already validated and approved during the search phase.

## What this fork does not do

- **Not a full product divergence.** The UI, library management, indexer configuration, download clients, quality profiles, and all non-anime search behavior are identical to upstream Sonarr v4.
- **Not a replacement for upstream support.** Bugs or questions about general Sonarr behavior (import, notifications, database, API) should go to [upstream Sonarr](https://github.com/Sonarr/Sonarr).
- **Some backend variants remain lightly tested.** Validation so far has focused on accessible indexers and standard protocol-backed backends. Non-standard implementations may behave differently.

## Example behavior

Illustrative example only:

| Series | Seasons | Episodes | Queries (fork example) | Queries (upstream-style example) | Per-episode fallbacks |
|--------|---------|----------|------------------------|----------------------------------|-----------------------|
| Anime Title | S01-S04 | 96 | 2 | ~104 | 0 |
| Anime Title: Season 1 | S01 | 12 | 6 | ~15 | 0 |
| Anime Title: Long Season | S01 | 26 | 3 | ~29 | 0 |

The goal is simple: if a broad pack or season pack already covers the wanted episodes, the fork avoids sending unnecessary per-episode searches afterward. Actual results depend on indexer behavior and available releases.

## Tested paths

| Test path | Command | What it validates |
|-----------|---------|-------------------|
| Direct indexer path | `/test-e2e [series] [seasons]` | Pack-first cascade, broad query suppression, episode skip logic |
| Aggregator-backed path | `/test-e2e-prowlarr [series] [seasons]` | Same cascade behavior through a protocol-backed path |
| CI unit tests | Automatic on push/PR | Core test suite across Windows, macOS, and Linux |
| CI unit tests (Postgres) | Automatic on push/PR | Core test suite on Ubuntu with Postgres environment enabled |
| CI integration tests | Automatic on push/PR | Full integration test matrix across Windows, macOS, and Linux |

**Note on query counts:** Native indexer implementations can be leaner than protocol-backed paths, which may also emit identifier-based and title-variant queries. The acceptance criteria are broad-first ordering, cross-season suppression, and cascade/skip behavior, not identical query totals across every backend.

## Known limitations

- Fork tracks Sonarr v4 (current stable) only; v5/develop is not targeted
- Pack detection depends on release title parsing — unusual naming conventions may not be recognized
- Protocol-backed indexers with non-standard search parameter support may not benefit from broad-first ordering
- Multi-season coverage still depends on what a given indexer exposes for the searched title

## Install

### GitHub release archives

Download the latest release from the [Releases page](https://github.com/RemingtonDev/sonarr-anime/releases/latest). Releases are published with version tags such as `v4.0.17.812`. Archives are available for Linux (x64, arm, arm64), macOS (x64, arm64), Windows (x64, x86), and FreeBSD.

Extract and run like stock Sonarr — the binary is a drop-in replacement.

### Docker

Public images are available on GitHub Container Registry (GHCR):

```
ghcr.io/remingtondev/sonarr-anime
```

Supported architectures: `linux/amd64` and `linux/arm64`.

### Docker quick start

**docker run:**

```bash
docker run -d \
  --name sonarr-anime \
  -p 8989:8989 \
  -v /path/to/config:/config \
  -v /path/to/tv:/tv \
  -v /path/to/downloads:/downloads \
  ghcr.io/remingtondev/sonarr-anime:latest
```

**docker compose:**

```yaml
services:
  sonarr:
    image: ghcr.io/remingtondev/sonarr-anime:latest
    container_name: sonarr-anime
    ports:
      - "8989:8989"
    volumes:
      - ./config:/config
      - /path/to/tv:/tv
      - /path/to/downloads:/downloads
    restart: unless-stopped
```

### NAS / self-hosted container platforms

The public images support `amd64` and `arm64`, covering Synology, QNAP, Unraid, and similar NAS platforms.

Required mounts:

| Mount | Purpose |
|-------|---------|
| `/config` | Sonarr configuration, database, and logs. Persists across container restarts. |
| `/tv` | TV library root folder. Must match what you configure inside Sonarr. |
| `/downloads` | Download client output. Must be accessible to both Sonarr and your download client. |

For conservative installs, pin to an exact release tag such as `4.0.17.812`. Use `latest` only if you want the current stable build.

**Upgrading:**

1. Stop the container
2. Pull the new image (`docker pull ghcr.io/remingtondev/sonarr-anime:latest`)
3. Restart with the same mounted `/config`

Your configuration and database are preserved in the `/config` volume.

## Versioning

Releases use Sonarr's normal numeric build versioning.

Example:

- GitHub release tag: `v4.0.17.812`
- Docker image tag: `4.0.17.812`

Successful `main` builds publish:

- a GitHub Release tagged `v<version>`
- packaged release archives
- a GHCR image tagged `<version>`
- `latest` after the matching GitHub Release has been created successfully

## Image tags

| Tag | Source | Description |
|-----|--------|-------------|
| `latest` | `main` | Alias for the most recent successful GitHub Release |
| `4.0.17.812` | `main` | Pinned image tag for a specific published release |

Stable releases come from `main`.

## Security

This is an independent fork, not the official Sonarr distribution. It is not affiliated with or endorsed by the Sonarr project.

See [SECURITY.md](SECURITY.md) for how to report vulnerabilities in fork-owned code and release infrastructure.

## Support expectations

Issues are welcome for:
- Reproducible anime-search regressions (pack cascade, query counts, parser failures)
- Indexer compatibility reports for genre-oriented indexers and protocol-backed backends
- Fork-owned workflow, docs, or release problems

For general Sonarr support (UI, import, notifications, download clients, database), please use the [upstream Sonarr community](https://wiki.servarr.com/sonarr). This fork does not change those features.

## Upstream sync

This fork tracks Sonarr v4 and is rebased onto upstream manually when needed. There is no automatic sync PR flow; upstream changes are reviewed and merged deliberately to keep the fork surface small.

## For developers

- [Contributing guide](CONTRIBUTING.md) — how to contribute to this fork

## License

Same as upstream Sonarr — [GNU GPL v3](http://www.gnu.org/licenses/gpl.html).
