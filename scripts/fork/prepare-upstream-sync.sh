#!/usr/bin/env bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
METADATA_FILE="${ROOT_DIR}/.fork/upstream.json"
FAILED_PATCH_FILE="${ROOT_DIR}/.fork/last_failed_patch"

if ! command -v jq >/dev/null 2>&1; then
  echo "jq is required" >&2
  exit 1
fi

if [[ ! -f "${METADATA_FILE}" ]]; then
  echo "Missing ${METADATA_FILE}" >&2
  exit 1
fi

UPSTREAM_REF="${1:?Usage: prepare-upstream-sync.sh <upstream-ref> [upstream-version] [upstream-commit]}"
UPSTREAM_VERSION="${2:-${UPSTREAM_REF#v}}"
UPSTREAM_COMMIT="${3:-$(git rev-parse "${UPSTREAM_REF}^{commit}")}"
ARCHIVE_REF="${UPSTREAM_COMMIT}"
PATCH_DIR="$(jq -r '.patch_directory' "${METADATA_FILE}")"

cd "${ROOT_DIR}"

git rev-parse --verify "${ARCHIVE_REF}^{commit}" >/dev/null

tmpdir="$(mktemp -d)"
cleanup() {
  rm -rf "${tmpdir}"
}
trap cleanup EXIT

rm -f "${FAILED_PATCH_FILE}"

git archive --format=tar "${ARCHIVE_REF}" | tar -xf - -C "${tmpdir}"

find "${ROOT_DIR}" -mindepth 1 -maxdepth 1 \
  ! -name .git \
  ! -name .fork \
  ! -name scripts \
  -exec rm -rf {} +

rsync -a --delete \
  --exclude '.git' \
  --exclude '.fork' \
  --exclude 'scripts/fork' \
  "${tmpdir}/" "${ROOT_DIR}/"

git add -A

shopt -s nullglob
patches=("${PATCH_DIR}"/*.patch)
shopt -u nullglob

if [[ "${#patches[@]}" -eq 0 ]]; then
  echo "No patches found in ${PATCH_DIR}" >&2
  exit 1
fi

for patch in "${patches[@]}"; do
  echo "Applying ${patch}"
  printf '%s\n' "$(basename "${patch}")" > "${FAILED_PATCH_FILE}"
  git apply --3way --whitespace=nowarn "${patch}"
done

rm -f "${FAILED_PATCH_FILE}"

jq \
  --arg upstream_ref "${UPSTREAM_REF}" \
  --arg upstream_commit "${UPSTREAM_COMMIT}" \
  --arg upstream_version "${UPSTREAM_VERSION}" \
  '.upstream_ref = $upstream_ref
   | .upstream_commit = $upstream_commit
   | .upstream_version = $upstream_version' \
  "${METADATA_FILE}" > "${METADATA_FILE}.tmp"

mv "${METADATA_FILE}.tmp" "${METADATA_FILE}"

echo "Prepared fork tree from ${UPSTREAM_REF}"
