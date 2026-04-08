#!/usr/bin/env bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
METADATA_FILE="${ROOT_DIR}/.fork/upstream.json"
PATCH_DIR="${ROOT_DIR}/.fork/patches"

if ! command -v jq >/dev/null 2>&1; then
  echo "jq is required" >&2
  exit 1
fi

if [[ ! -f "${METADATA_FILE}" ]]; then
  echo "Missing ${METADATA_FILE}" >&2
  exit 1
fi

UPSTREAM_REF="${1:-$(jq -r '.upstream_ref' "${METADATA_FILE}")}"
FORK_REF="${2:-HEAD}"

cd "${ROOT_DIR}"

git rev-parse --verify "${UPSTREAM_REF}^{commit}" >/dev/null
git rev-parse --verify "${FORK_REF}^{commit}" >/dev/null

rm -f "${PATCH_DIR}/"*.patch

git diff --binary --full-index "${UPSTREAM_REF}" "${FORK_REF}" -- \
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
  src/Sonarr.Api.V3/openapi.json \
  > "${PATCH_DIR}/0001-release-and-branding.patch"

git diff --binary --full-index "${UPSTREAM_REF}" "${FORK_REF}" -- \
  src/NzbDrone.Core \
  src/NzbDrone.Host/Startup.cs \
  > "${PATCH_DIR}/0002-anime-core.patch"

git diff --binary --full-index "${UPSTREAM_REF}" "${FORK_REF}" -- \
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
  src/NzbDrone.Windows.Test \
  > "${PATCH_DIR}/0003-fork-tests.patch"

jq \
  --arg upstream_ref "${UPSTREAM_REF}" \
  --arg upstream_commit "$(git rev-parse "${UPSTREAM_REF}^{commit}")" \
  --arg fork_ref "$(git rev-parse "${FORK_REF}^{commit}")" \
  --arg upstream_version "${UPSTREAM_REF#v}" \
  '.upstream_ref = $upstream_ref
   | .upstream_commit = $upstream_commit
   | .upstream_version = $upstream_version
   | .fork_ref = $fork_ref' \
  "${METADATA_FILE}" > "${METADATA_FILE}.tmp"

mv "${METADATA_FILE}.tmp" "${METADATA_FILE}"

echo "Generated patch stack in ${PATCH_DIR}"
