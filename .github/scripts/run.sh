#!/usr/bin/env bash
#
# Runs one of this repository's Python scripts inside a container.
#
# The scripts use nothing but the standard library, so what this buys is not
# dependency isolation but a fixed interpreter: the same Python builds the
# listing and the docs on a maintainer's laptop, on a CI runner, and in a year.
# There is deliberately no fallback to whatever `python3` happens to be on the
# host — a silent fallback is how the reproducibility gets lost.
#
# Usage: run.sh <script> [args...]
#
#   .github/scripts/run.sh .github/scripts/build_docs.py --out Website
#
# The repository is mounted at /repo and that is the working directory, so
# repo-relative arguments work unchanged. Absolute paths that point inside the
# repository are rewritten to their container equivalent, which keeps callers
# that build paths from $PWD working; anything outside the repository is not
# visible and the script will say so rather than fail obscurely.
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"

# Pinned by digest, not by tag: python:3.12-slim as of 2026-08-23.
IMAGE="docker.io/library/python@sha256:2c941e860699f878900b0edc2403613c234d4b32eda3cc9fa7036991a2a63c4a"

if [ "$#" -lt 1 ]; then
    echo "usage: run.sh <script> [args...]" >&2
    exit 2
fi

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

# Git Bash rewrites arguments that look like absolute paths into Windows ones,
# which would mangle the container-side mount point.
export MSYS2_ARG_CONV_EXCL='*'
export MSYS_NO_PATHCONV=1

host_path() {
    if command -v cygpath >/dev/null 2>&1; then
        cygpath -w "$1"
    else
        printf '%s' "$1"
    fi
}

RUN_ARGS=(--rm -v "$(host_path "$REPO"):/repo" -w /repo)

# No network unless the caller asks for it. Only build_listing.py talks to the
# GitHub API; the rest read and write local files. Denying the socket by
# default means the step that carries a token is the only step that could send
# one anywhere, which is a smaller thing to reason about than trusting every
# step not to.
if [ "${CONTAINER_NETWORK:-none}" = "none" ]; then
    RUN_ARGS+=(--network=none)
fi

# Who the container writes as, which decides whether the files it leaves in the
# work tree belong to the caller.
#
# Rootless podman already maps container root onto the invoking user, so it
# needs nothing — and passing --user actively breaks it, because that uid is
# then mapped again into the user namespace and lands on a subuid that owns
# none of the mounted files.
#
# Docker runs the container as real root, so it needs to be told.
if [ "$ENGINE" = "docker" ] && command -v id >/dev/null 2>&1; then
    case "$(uname -s)" in
        MINGW* | MSYS* | CYGWIN*) ;;
        *) RUN_ARGS+=(--user "$(id -u):$(id -g)") ;;
    esac
fi

# What build_listing.py needs from the workflow: a token to authenticate with
# and the repository to query. A container starts with an empty environment, so
# anything a script reads from os.environ has to be named here — the runner's
# variables are not inherited the way they were before.
#
# Passed by name, so values never appear in the command line or the logs.
for name in GITHUB_TOKEN GH_TOKEN GITHUB_REPOSITORY; do
    if [ -n "${!name:-}" ]; then
        RUN_ARGS+=(-e "$name")
    fi
done

# Absolute paths into the repository become their container equivalent. A
# caller that assembled a path from $PWD would otherwise hand the container a
# location it cannot see.
ARGS=()
for arg in "$@"; do
    case "$arg" in
        "$REPO")
            ARGS+=("/repo")
            ;;
        "$REPO"/*)
            ARGS+=("/repo/${arg#"$REPO"/}")
            ;;
        /* | [A-Za-z]:[\\/]*)
            echo "error: '$arg' is outside the repository, so the container cannot see it" >&2
            exit 1
            ;;
        *)
            ARGS+=("$arg")
            ;;
    esac
done

exec "$ENGINE" run "${RUN_ARGS[@]}" "$IMAGE" python3 "${ARGS[@]}"
