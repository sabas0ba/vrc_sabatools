#!/bin/sh
#
# Dockerfile が build 時に実体化した Nix development profile で command を
# 実行する。実行時の flake 評価や Host tool への fallback は行わない。
set -eu

profile=${SABATOOLS_PROFILE:-/nix/var/nix/profiles/sabatools-dev}

if [ ! -e "$profile" ]; then
    echo "development profile was not found: $profile" >&2
    exit 1
fi

if [ "$#" -eq 0 ]; then
    exec nix develop "$profile" --command bash
fi

exec nix develop "$profile" --command "$@"
