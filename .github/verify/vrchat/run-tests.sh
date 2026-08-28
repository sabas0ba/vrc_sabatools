#!/usr/bin/env bash
#
# Runs the SDK-facing EditMode tests for one lane inside a project that has the
# real VRChat SDK. This is the tier .github/verify/verify.sh cannot reach: the
# SDK assemblies are not redistributable and are not on NuGet, so the avatar
# and world modules are neither compiled nor run there.
#
# Assembles the project first if it is not there. The Unity Editor comes from
# the host: running it in a container needs a licence, which is the same
# constraint .github/workflows/unity.yml documents.
#
# Only EditMode tests are run. No PlayMode tier, and so no project-settings
# session: the layer list and collision matrix a VRChat project needs matter
# for physics, and nothing here enters play mode.
#
# Usage:
#   UNITY=/path/to/Unity ./run-tests.sh <avatars|worlds> [project-directory]
#
# UNITY may also be a Unity Hub install root, in which case the editor matching
# ProjectVersion.txt is used.
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../../.." && pwd)"

LANE="${1:-}"
case "$LANE" in
    avatars)
        SDK_PACKAGE="com.vrchat.avatars"
        FILTER="SabaTools.Inspect.Avatar.SdkTests"
        ;;
    worlds)
        SDK_PACKAGE="com.vrchat.worlds"
        FILTER="SabaTools.Inspect.World.SdkTests"
        ;;
    *)
        echo "usage: run-tests.sh <avatars|worlds> [project-directory]" >&2
        exit 2
        ;;
esac

PROJECT="${2:-$REPO/build/${LANE}Project}"

VERSION="$(sed -n 's/^m_EditorVersion: *//p' "$REPO/.github/verify/CIProject/ProjectSettings/ProjectVersion.txt")"
[ -n "$VERSION" ] || { echo "error: could not read the editor version from ProjectVersion.txt" >&2; exit 1; }

resolve_unity() {
    if [ -n "${UNITY:-}" ]; then
        if [ -d "$UNITY" ]; then
            for candidate in "$UNITY/$VERSION/Editor/Unity.exe" "$UNITY/$VERSION/Editor/Unity"; do
                [ -x "$candidate" ] && { printf '%s' "$candidate"; return; }
            done
        fi
        printf '%s' "$UNITY"
        return
    fi

    for root in "/c/Program Files/Unity/Hub/Editor" "$HOME/Unity/Hub/Editor" "/Applications/Unity/Hub/Editor"; do
        for candidate in "$root/$VERSION/Editor/Unity.exe" "$root/$VERSION/Editor/Unity"; do
            [ -x "$candidate" ] && { printf '%s' "$candidate"; return; }
        done
    done
}

UNITY_BIN="$(resolve_unity)"
if [ -z "$UNITY_BIN" ] || [ ! -x "$UNITY_BIN" ]; then
    echo "error: Unity $VERSION was not found; set UNITY to the editor or to the Hub install root" >&2
    exit 1
fi

[ -d "$PROJECT/Packages/$SDK_PACKAGE" ] || "$HERE/assemble.sh" "$LANE" "$PROJECT"

RESULTS="$PROJECT/TestResults/results.xml"
LOG="$PROJECT/unity.log"
mkdir -p "$PROJECT/TestResults"
rm -f "$RESULTS" "$LOG"

to_native() {
    if command -v cygpath >/dev/null 2>&1; then
        cygpath -w "$1"
    else
        printf '%s' "$1"
    fi
}

echo "running $LANE tests in $PROJECT"

# The SDK ships its own test assemblies, and some of them fail for reasons that
# have nothing to do with these packages. Filter to ours so the exit status
# means something.
set +e
"$UNITY_BIN" \
    -batchmode \
    -projectPath "$(to_native "$PROJECT")" \
    -runTests -testPlatform EditMode \
    -testFilter "$FILTER" \
    -testResults "$(to_native "$RESULTS")" \
    -logFile "$(to_native "$LOG")"
set -e

if [ ! -f "$RESULTS" ]; then
    echo "error: no test results were written; see $LOG" >&2
    grep -E "error CS" "$LOG" | head -20 >&2 || true
    exit 1
fi

"$REPO/.github/scripts/run.sh" \
    .github/scripts/check_unity_results.py "$RESULTS"
