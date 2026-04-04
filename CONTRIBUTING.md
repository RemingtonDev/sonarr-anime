# Contributing to sonarr-anime

This is a **minimal Sonarr v4 fork** focused exclusively on anime search efficiency. Contributions are welcome, but must stay within the fork's narrow scope.

## Principles

- **Minimal diff** — touch as few files as possible. Prefer modifying existing methods over adding new ones
- **Surgical changes** — no cosmetic changes to upstream code (formatting, renames, reordering). Never refactor upstream code "while you're in there"
- **Simple logic** — straightforward conditionals over clever abstractions. Anyone reading the code should understand the anime-specific logic immediately
- **No scope creep** — if a change doesn't reduce redundant anime search queries or support that goal, it doesn't belong here

## Branch model

- `main` is the release branch, based on upstream Sonarr v4
- Feature branches should be created from `main`
- PRs target `main`
- Upstream rebases from `Sonarr/Sonarr` are done manually when needed

## Development setup

### Tools required

- [.NET 6 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) (see `volta` config in `package.json`)
- [Yarn](https://yarnpkg.com/)
- [Git](https://git-scm.com/)
- [Docker](https://www.docker.com/) (for e2e testing)

### Build

```bash
# Full build (backend + frontend)
./build.sh --backend --frontend

# Backend only (faster iteration)
dotnet build src/Sonarr.sln -p:TreatWarningsAsErrors=false --no-restore
```

Do not use `--packages` — it tries to create Windows packages and fails on macOS/Linux.

### Test

```bash
# Full unit suite (build first, then test against _tests/ DLLs)
dotnet test _tests/net6.0/Sonarr.Core.Test.dll \
  --filter "Category!=ManualTest&Category!=IntegrationTest&Category!=AutomationTest"

# Targeted test run
dotnet test _tests/net6.0/Sonarr.Core.Test.dll \
  --filter "FullyQualifiedName~SomeFixture"
```

Do not run `dotnet test` against `src/*.csproj` directly — SA1200 warnings treated as errors block that path.

### End-to-end testing

Any PR that changes the search pipeline **must** include e2e verification:

```bash
# Direct baseline
/test-e2e "Example Series" S01-S04

# Aggregator-backed path
/test-e2e-prowlarr "Example Series" S01-S04 --indexer <supported-backend>
```

Both produce structured pass/fail reports. A feature is not complete until the e2e report confirms expected behavior.

## Pull request guidelines

- One logical change per PR
- Keep the diff small — if your PR touches more than 3-4 files, consider whether it can be split
- Include e2e test results for search-path changes
- PRs that only touch docs, workflows, or test infrastructure do not need e2e verification but should pass CI

## Code style

Follow Sonarr's conventions exactly:
- C# with 4-space indentation
- Unix line endings (LF)
- Match surrounding code style — do not introduce new patterns

## What belongs upstream

If your change improves Sonarr for all users (not just anime), consider contributing it to [Sonarr/Sonarr](https://github.com/Sonarr/Sonarr) instead. This fork is intentionally narrow.

## Questions

Open an issue or start a discussion in the repository. For general Sonarr questions unrelated to this fork, use the [upstream Sonarr community](https://wiki.servarr.com/sonarr).
