#!/usr/bin/env bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
METADATA_FILE="${ROOT_DIR}/.fork/upstream.json"

if ! command -v jq >/dev/null 2>&1; then
  echo "jq is required" >&2
  exit 1
fi

upstream_version="$(jq -r '.upstream_version' "${METADATA_FILE}")"
upstream_ref="$(jq -r '.upstream_ref' "${METADATA_FILE}")"
upstream_commit="$(jq -r '.upstream_commit' "${METADATA_FILE}")"

echo "UPSTREAM_VERSION=${upstream_version}"
echo "UPSTREAM_REF=${upstream_ref}"
echo "UPSTREAM_COMMIT=${upstream_commit}"
