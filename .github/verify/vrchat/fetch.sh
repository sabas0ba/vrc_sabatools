#!/usr/bin/env bash
#
# Materialises the VRChat SDK packages listed in a lock file into a work
# directory.
#
# The download runs in a container so the result depends only on the lock file
# and the pinned image, not on what VCC or ALCOM has cached locally, and so no
# VPM tooling has to be installed on the host.
#
# Usage: fetch.sh <avatars|worlds> [output-directory]
#        (default output: <repo>/build/vpm-<lane>)
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../../.." && pwd)"

LANE="${1:-}"
case "$LANE" in
    avatars | worlds) ;;
    *)
        echo "usage: fetch.sh <avatars|worlds> [output-directory]" >&2
        exit 2
        ;;
esac

LOCK="$HERE/packages.$LANE.lock"
OUT="${2:-$REPO/build/vpm-$LANE}"

[ -f "$LOCK" ] || { echo "error: $LOCK not found" >&2; exit 1; }

# Pinned by digest, not by tag: a moved tag must not change what this fetches.
# alpine:3.20 as of 2026-08-23.
IMAGE="docker.io/library/alpine@sha256:c64c687cbea9300178b30c95835354e34c4e4febc4badfe27102879de0483b5e"

ENGINE="${CONTAINER_ENGINE:-}"
if [ -z "$ENGINE" ]; then
    for candidate in podman docker; do
        if command -v "$candidate" >/dev/null 2>&1; then
            ENGINE="$candidate"
            break
        fi
    done
fi

if [ -z "$ENGINE" ]; then
    echo "error: neither podman nor docker was found; set CONTAINER_ENGINE" >&2
    exit 1
fi

mkdir -p "$OUT"

# Git Bash rewrites arguments that look like absolute paths into Windows paths,
# which would mangle the container-side ones.
export MSYS2_ARG_CONV_EXCL='*'
export MSYS_NO_PATHCONV=1

host_path() {
    if command -v cygpath >/dev/null 2>&1; then
        cygpath -w "$1"
    else
        printf '%s' "$1"
    fi
}

"$ENGINE" run --rm \
    -v "$(host_path "$HERE"):/verify:ro" \
    -v "$(host_path "$OUT"):/out" \
    "$IMAGE" \
    /bin/sh /verify/fetch-inner.sh "/verify/packages.$LANE.lock" /out
