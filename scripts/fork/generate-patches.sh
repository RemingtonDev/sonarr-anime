#!/usr/bin/env bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
METADATA_FILE="${ROOT_DIR}/.fork/upstream.json"

usage() {
  echo "Usage: generate-patches.sh <upstream-base-ref> [fork-ref|--worktree]" >&2
}

if [[ "$#" -lt 1 || "$#" -gt 2 ]]; then
  usage
  exit 1
fi

if ! command -v jq >/dev/null 2>&1; then
  echo "jq is required" >&2
  exit 1
fi

if [[ ! -f "${METADATA_FILE}" ]]; then
  echo "Missing ${METADATA_FILE}" >&2
  exit 1
fi

UPSTREAM_BASE="$1"
FORK_REF="${2:-HEAD}"
PATCH_DIR="$(jq -r '.patch_directory' "${METADATA_FILE}")"

cd "${ROOT_DIR}"

git rev-parse --verify "${UPSTREAM_BASE}^{commit}" >/dev/null

diff_target=()
if [[ "${FORK_REF}" != "--worktree" ]]; then
  git rev-parse --verify "${FORK_REF}^{commit}" >/dev/null
  diff_target=("${FORK_REF}")
fi

rm -f "${PATCH_DIR}/"*.patch

write_patch() {
  local output="$1"
  shift

  git diff --binary --full-index "${UPSTREAM_BASE}" "${diff_target[@]}" -- "$@" > "${PATCH_DIR}/${output}"
}

write_patch 0001-release-and-branding.patch \
  .dockerignore \
  .github \
  .gitignore \
  CONTRIBUTING.md \
  Dockerfile \
  Dockerfile.release \
  README.md \
  SECURITY.md \
  build.sh \
  docker/tests/mono/sonarr/Dockerfile \
  distribution/windows/setup/build.bat \
  distribution/windows/setup/sonarr.iss \
  frontend/src/App/AppUpdatedModalContent.tsx \
  frontend/src/Components/Markdown/InlineMarkdown.tsx \
  frontend/src/System/Status/About/About.tsx \
  frontend/src/typings/SystemStatus.ts \
  global.json \
  local-test/docker-compose.yml \
  src/Directory.Build.props \
  src/Sonarr.Api.V3/System/SystemController.cs \
  src/Sonarr.Api.V3/System/SystemResource.cs \
  src/Sonarr.Api.V3/openapi.json

write_patch 0002-anime-core.patch \
  src/NzbDrone.Core \
  src/NzbDrone.Host/Startup.cs

write_patch 0003-fork-tests.patch \
  src/NzbDrone.Api.Test \
  src/NzbDrone.Automation.Test \
  src/NzbDrone.Common.Test \
  src/NzbDrone.Core.Test \
  src/NzbDrone.Host.Test \
  src/NzbDrone.Integration.Test \
  src/NzbDrone.Libraries.Test \
  src/NzbDrone.Mono.Test \
  src/NzbDrone.Test.Common \
  src/NzbDrone.Update.Test \
  src/NzbDrone.Windows.Test

echo "Generated patch stack in ${PATCH_DIR}"
