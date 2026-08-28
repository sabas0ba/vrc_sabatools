#!/usr/bin/env bash
#
# Checks repository content that uses the pinned Python container. Keeping
# this separate lets the .NET/Nix toolchain itself run in a container without
# requiring a nested container engine there.
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"

mapfile -t PACKAGES < <(find "$REPO/Packages" -mindepth 1 -maxdepth 1 -type d | sort)

WORK="${VERIFY_WORK_DIR:-$REPO/.verify}"
OUT="$WORK/out"
PYTHON="$REPO/.github/scripts/run.sh"

log() { printf '\n\033[1m== %s\033[0m\n' "$1"; }

mkdir -p "$OUT"

log "Rendering documentation"
# Build into a copy of the deployed site. The link check resolves the package
# listing and shared stylesheet as well as the generated documentation.
rm -rf "$OUT/site"
mkdir -p "$OUT/site"
cp -r "$REPO/Website/." "$OUT/site/"
rm -rf "$OUT/site/docs"

"$PYTHON" .github/scripts/build_docs.py --repo "$REPO" --out "$OUT/site"
"$PYTHON" .github/scripts/check_docs.py --repo "$REPO" --out "$OUT/site"

log "Validating manifests"
for package in "${PACKAGES[@]}"; do
    "$PYTHON" .github/scripts/check_package.py "$REPO" "$package"
done

log "Content checks passed"
