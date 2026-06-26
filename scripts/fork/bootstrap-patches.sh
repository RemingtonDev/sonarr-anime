#!/usr/bin/env bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
METADATA_FILE="${ROOT_DIR}/.fork/upstream.json"
GENERATOR="${ROOT_DIR}/scripts/fork/generate-patches.sh"

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

UPSTREAM_BASE="${UPSTREAM_REF}"
if ! git rev-parse --verify "${UPSTREAM_BASE}^{commit}" >/dev/null 2>&1; then
  upstream_repository="$(jq -r '.upstream_repository' "${METADATA_FILE}")"
  git fetch --force "${upstream_repository}" "${UPSTREAM_REF}"
  UPSTREAM_BASE="$(git rev-parse FETCH_HEAD)"
fi

git rev-parse --verify "${FORK_REF}^{commit}" >/dev/null

"${GENERATOR}" "${UPSTREAM_BASE}" "${FORK_REF}"

jq \
  --arg upstream_ref "${UPSTREAM_REF}" \
  --arg upstream_commit "$(git rev-parse "${UPSTREAM_BASE}^{commit}")" \
  --arg fork_ref "$(git rev-parse "${FORK_REF}^{commit}")" \
  --arg upstream_version "${UPSTREAM_REF#v}" \
  '.upstream_ref = $upstream_ref
   | .upstream_commit = $upstream_commit
   | .upstream_version = $upstream_version
   | .fork_ref = $fork_ref' \
  "${METADATA_FILE}" > "${METADATA_FILE}.tmp"

mv "${METADATA_FILE}.tmp" "${METADATA_FILE}"
