#!/usr/bin/env bash
#
# Verify the packages without a Unity installation.
#
# What this proves:
#   * the core Editor assembly compiles, with its UnityEngine usage checked
#     against REAL UnityEngine reference assemblies (Unity's own
#     UnityEngine.Modules NuGet package)
#   * every package's Editor/Core is free of Unity, and the decision-making
#     code in it RUNS and behaves: rank thresholds, the checklist, the texture
#     memory estimate, the avatar and world limits and the report writer are
#     executed on a plain .NET runtime. See offline/.
#   * the documentation site renders, with no raw Markdown left in the text,
#     no broken internal links and no missing images
#   * each package's manifest, CHANGELOG entry and .meta files line up
#
# What this does NOT prove:
#   * UnityEditor API signatures. UnityEditor.dll is not redistributable, so
#     `UnityEditorStub.cs` stands in for it and is written by hand.
#   * anything in sabatools.avatar, sabatools.avatar-materials or
#     sabatools.world that touches the VRChat SDK. The SDK's assemblies are not
#     redistributable and are not on NuGet,
#     and a hand-written SDK stub would assert signatures rather than check
#     them. Those assemblies are compiled and run against the real SDK by
#     .github/verify/vrchat/ instead; only their Editor/Core is covered here.
#   * anything that needs a live editor: CollectDependencies, the prefab and
#     SerializedObject scanning, the window's IMGUI.
#   * that the VRChat performance thresholds still match VRChat's published
#     ones. They are reference values copied by hand; nothing here fetches the
#     document to compare.
#
# Requirements: dotnet SDK 8+, curl and unzip. The content checks additionally
# require podman or docker. Python is not required on the host: every script
# that needs it runs in the pinned container that .github/scripts/run.sh starts.
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"

CORE_PACKAGE="$REPO/Packages/io.github.sabas0ba.sabatools.core"

# Every package, in listing order. Discovered rather than listed so a new one
# is verified the moment it exists.
mapfile -t PACKAGES < <(find "$REPO/Packages" -mindepth 1 -maxdepth 1 -type d | sort)

WORK="${VERIFY_WORK_DIR:-$REPO/.verify}"
REFS="$WORK/refs"
OUT="$WORK/out"

UNITY_REFS_VERSION="2021.3.33"
NETFX_REFS_VERSION="1.0.3"

mkdir -p "$REFS" "$OUT"

log() { printf '\n\033[1m== %s\033[0m\n' "$1"; }
fail() { printf '\033[31merror: %s\033[0m\n' "$1" >&2; exit 1; }

for tool in dotnet curl unzip; do
    command -v "$tool" >/dev/null 2>&1 || fail "$tool is required but not installed"
done

[ -d "$CORE_PACKAGE" ] || fail "the core package is missing from Packages/"

# ---------------------------------------------------------------------------
log "Fetching reference assemblies"
# ---------------------------------------------------------------------------

fetch_nupkg() {
    local id="$1" version="$2" dest="$3"
    if [ -d "$dest" ]; then
        echo "cached: $id $version"
        return
    fi
    local url="https://api.nuget.org/v3-flatcontainer/${id}/${version}/${id}.${version}.nupkg"
    echo "downloading $id $version"
    curl -sS --fail --max-time 300 -o "$REFS/$id.nupkg" "$url"
    unzip -q -o "$REFS/$id.nupkg" -d "$dest"
    rm -f "$REFS/$id.nupkg"
}

# Unity publishes its own UnityEngine reference assemblies to NuGet.
fetch_nupkg unityengine.modules "$UNITY_REFS_VERSION" "$REFS/unity"
fetch_nupkg microsoft.netframework.referenceassemblies.net472 "$NETFX_REFS_VERSION" "$REFS/netfx"

# UnityEngine.Modules stores its entries with mode 000, and unzip faithfully
# reproduces that. Root does not care; an ordinary CI user cannot read a single
# DLL. Applied outside fetch_nupkg so a restored cache is fixed up too.
chmod -R u+rwX "$REFS"

UNITY_DIR="$REFS/unity/lib/net35"
NETFX_DIR="$REFS/netfx/build/.NETFramework/v4.7.2"

[ -f "$UNITY_DIR/UnityEngine.CoreModule.dll" ] || fail "UnityEngine reference assemblies missing"
[ -f "$NETFX_DIR/mscorlib.dll" ] || fail ".NET Framework reference assemblies missing"

# `dotnet --list-sdks` prints "<version> [<sdk root>]"; that root is the only
# reliable way to find Roslyn across distro packages and setup-dotnet installs.
SDK_ROOT="$(dotnet --list-sdks | tail -1 | sed -E 's/^[^ ]+ \[(.*)\]$/\1/')"
[ -d "$SDK_ROOT" ] || fail "could not determine the .NET SDK root from 'dotnet --list-sdks'"

CSC_DLL="$(find "$SDK_ROOT" -name csc.dll -path '*bincore*' 2>/dev/null | head -1)"
[ -n "$CSC_DLL" ] || fail "could not locate the Roslyn compiler (csc.dll) under $SDK_ROOT"

# The Unity assemblies target net35, so the BCL references must be .NET
# Framework too - mixing in .NET 8's corelib would duplicate System.Object.
BCL=(-r:"$NETFX_DIR/mscorlib.dll" -r:"$NETFX_DIR/System.dll" -r:"$NETFX_DIR/System.Core.dll")

UNITY_ARGS=()
for dll in "$UNITY_DIR"/*.dll; do UNITY_ARGS+=(-r:"$dll"); done

# CS1701/1702: assembly version unification between net35 and net472 refs.
COMMON=(-nostdlib+ -noconfig -langversion:9.0 "-nowarn:1701,1702" -target:library -nologo)

csc() { dotnet "$CSC_DLL" "$@"; }

# ---------------------------------------------------------------------------
log "Compiling UnityEditor stub"
# ---------------------------------------------------------------------------
csc "${COMMON[@]}" "${BCL[@]}" "${UNITY_ARGS[@]}" \
    -out:"$OUT/UnityEditor.dll" "$HERE/UnityEditorStub.cs"
echo "ok"

# ---------------------------------------------------------------------------
log "Compiling the core Editor assembly (real UnityEngine references + stub)"
# ---------------------------------------------------------------------------
# The packages are Editor-only, so there is no Runtime assembly to build.
# Only core is compiled here: the other Editor assemblies reference a VRChat
# SDK or are exercised in the matching SDK lane, which cannot be obtained the
# way UnityEngine can.
mapfile -t EDITOR_SOURCES < <(find "$CORE_PACKAGE/Editor" -name '*.cs' | sort)
[ "${#EDITOR_SOURCES[@]}" -gt 0 ] || fail "no Editor sources found under $CORE_PACKAGE"

csc "${COMMON[@]}" "${BCL[@]}" "${UNITY_ARGS[@]}" \
    -r:"$OUT/UnityEditor.dll" \
    -out:"$OUT/SabaTools.Inspect.Core.Editor.dll" "${EDITOR_SOURCES[@]}"
echo "ok: ${#EDITOR_SOURCES[@]} file(s)"

# ---------------------------------------------------------------------------
log "Checking that every Editor/Core stays free of Unity"
# ---------------------------------------------------------------------------
# The offline run below compiles each package's Editor/Core WITHOUT any Unity
# reference. That only stays possible while nobody adds a using directive for
# one, and the failure mode is confusing (a compile error in a test harness
# rather than in the file that caused it), so it is stated here as its own
# check.
#
# For the SDK packages this carries more weight than it does for core: their
# Editor/Core is the only part of them this script can reach at all.
IMPURE=0
for package in "${PACKAGES[@]}"; do
    core_dir="$package/Editor/Core"
    [ -d "$core_dir" ] || continue
    if grep -rlE '^using (UnityEngine|UnityEditor|VRC)' "$core_dir"; then
        IMPURE=1
    fi
done
[ "$IMPURE" -eq 0 ] || fail "the files listed above reference Unity or the VRChat SDK; Editor/Core must stay pure"
echo "ok: ${#PACKAGES[@]} package(s)"

# ---------------------------------------------------------------------------
log "Running the rules (no Unity)"
# ---------------------------------------------------------------------------
# Everything above proves the code compiles. This runs it: the real rank
# thresholds, checklist, memory estimate, avatar and world limits and report
# writer. The project links the shipped Editor/Core sources directly and has
# no package dependencies, so the same command is also a standalone CI job.
# VERIFY_SKIP_OFFLINE_TESTS is used only by verify.yml after that job has
# already passed, avoiding a duplicate execution in the validation job.
if [ "${VERIFY_SKIP_OFFLINE_TESTS:-0}" = "1" ]; then
    echo "skipped: already run by the non-Unity regression job"
else
    dotnet run \
        --project "$HERE/offline/SabaTools.Inspect.OfflineTests.csproj" \
        --configuration Release \
        || fail "offline rule checks failed"
fi

# ---------------------------------------------------------------------------
log "Compiling CI EditMode tests"
# ---------------------------------------------------------------------------
# These run for real inside Unity via .github/workflows/unity.yml. Compiling
# them here catches typos long before a Unity runner is spun up. The CI project
# holds core only; the SDK packages' tests live under .github/verify/vrchat.
TEST_DIR="$HERE/CIProject/Assets/Tests"
if [ -d "$TEST_DIR" ]; then
    mapfile -t TEST_SOURCES < <(find "$TEST_DIR" -name '*.cs' | sort)
    if [ "${#TEST_SOURCES[@]}" -gt 0 ]; then
        csc "${COMMON[@]}" "${BCL[@]}" "${UNITY_ARGS[@]}" \
            -r:"$OUT/SabaTools.Inspect.Core.Editor.dll" \
            -r:"$OUT/UnityEditor.dll" \
            -out:"$OUT/SabaTools.Inspect.CITests.dll" "${TEST_SOURCES[@]}"
        echo "ok: ${#TEST_SOURCES[@]} file(s)"
    else
        echo "skipped: no test sources"
    fi
else
    echo "skipped: no CI project"
fi

if [ "${VERIFY_SKIP_CONTAINER_CHECKS:-0}" = "1" ]; then
    log "Skipping container-backed content checks"
    echo "skipped: run .github/verify/verify-content.sh on the container host"
else
    bash "$HERE/verify-content.sh"
fi

log "All checks passed"
