#!/usr/bin/env bash
#
# Assembles a Unity project holding the core package, the matching SDK-specific
# packages and the VRChat SDK, so their code is compiled and run against the
# real SDK types instead of being skipped.
#
# The SDK comes from fetch.sh, which pins it by hash and runs in a container.
# Only the Unity Editor itself is taken from the host.
#
# The two lanes are separate projects on purpose: com.vrchat.avatars and
# com.vrchat.worlds are not meant to share one, which is the reason
# sabatools.avatar and sabatools.world are separate packages in the first
# place. A single project holding both would verify an arrangement no user has.
#
# Re-running refreshes just the directories this script owns. Everything else
# is left alone, because the SDK generates assets of its own on first import
# that Unity's import cache then expects to still be there. Pass --clean to
# start over.
#
# Usage: assemble.sh <avatars|worlds> [project-directory] [--clean]
#        (default project: <repo>/build/<lane>Project)
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../../.." && pwd)"
CIPROJECT="$REPO/.github/verify/CIProject"

LANE="${1:-}"
case "$LANE" in
    avatars)
        SDK_PACKAGE="com.vrchat.avatars"
        TOOL_PACKAGE="io.github.sabas0ba.sabatools.avatar"
        EXTRA_TOOL_PACKAGE="io.github.sabas0ba.sabatools.avatar-materials"
        EXTRA_VPM_PACKAGE="jp.lilxyzw.liltoon"
        TESTS="$HERE/Tests/Avatar"
        ;;
    worlds)
        SDK_PACKAGE="com.vrchat.worlds"
        TOOL_PACKAGE="io.github.sabas0ba.sabatools.world"
        EXTRA_TOOL_PACKAGE=""
        EXTRA_VPM_PACKAGE=""
        TESTS="$HERE/Tests/World"
        ;;
    *)
        echo "usage: assemble.sh <avatars|worlds> [project-directory] [--clean]" >&2
        exit 2
        ;;
esac

PROJECT="${2:-$REPO/build/${LANE}Project}"
VPM="${VPM_DIR:-$REPO/build/vpm-$LANE}"

if [ "${3:-}" = "--clean" ]; then
    rm -rf "$PROJECT"
fi

if [ ! -d "$VPM/$SDK_PACKAGE" ] || [ ! -d "$VPM/com.vrchat.base" ] || \
   { [ -n "$EXTRA_VPM_PACKAGE" ] && [ ! -d "$VPM/$EXTRA_VPM_PACKAGE" ]; }; then
    echo "fetching the pinned VRChat SDK ($LANE)"
    "$HERE/fetch.sh" "$LANE" "$VPM"
fi

mkdir -p "$PROJECT/Packages" "$PROJECT/ProjectSettings" "$PROJECT/Assets"

# Replace a directory we own, leaving the rest of the project untouched.
replace() {
    local source="$1" target="$2"
    rm -rf "$target"
    cp -r "$source" "$target"
}

cp "$CIPROJECT/ProjectSettings/ProjectVersion.txt" "$PROJECT/ProjectSettings/"

# Not the CI project's manifest: the SDK needs the built-in module set a real
# VRChat project gets from Unity's 3D template, and reports the shortfall as
# CS1069 rather than as a missing dependency.
cp "$HERE/manifest.json" "$PROJECT/Packages/manifest.json"

# The SDK-facing tests. They cannot live in the CI project, which has no SDK.
replace "$TESTS" "$PROJECT/Assets/SdkTests"

# Embedded packages resolve against the working tree and pull their own
# registry dependencies, so nothing has to be listed in manifest.json.
replace "$REPO/Packages/io.github.sabas0ba.sabatools.core" \
    "$PROJECT/Packages/io.github.sabas0ba.sabatools.core"
replace "$REPO/Packages/$TOOL_PACKAGE" "$PROJECT/Packages/$TOOL_PACKAGE"
if [ -n "$EXTRA_TOOL_PACKAGE" ]; then
    replace "$REPO/Packages/$EXTRA_TOOL_PACKAGE" "$PROJECT/Packages/$EXTRA_TOOL_PACKAGE"
fi
replace "$VPM/com.vrchat.base" "$PROJECT/Packages/com.vrchat.base"
replace "$VPM/$SDK_PACKAGE" "$PROJECT/Packages/$SDK_PACKAGE"
if [ -n "$EXTRA_VPM_PACKAGE" ]; then
    replace "$VPM/$EXTRA_VPM_PACKAGE" "$PROJECT/Packages/$EXTRA_VPM_PACKAGE"
fi

# The Worlds SDK ships a scene template Unity cannot walk: the file name and
# the type name inside disagree, so MonoScript.GetClass() returns null and
# SceneTemplateAsset.CreatePipeline() hands that null to Activator. Unity walks
# every scene template on every scene save, so the exception lands on whatever
# saved the scene and the test framework counts it as a failure.
#
# Removing the asset removes the walk. It is an entry in the New Scene dialog
# and nothing else; no test instantiates it.
rm -f \
    "$PROJECT/Packages/com.vrchat.worlds/Editor/VRCSDK/SDK3/VRCDefaultWorldScene.scenetemplate" \
    "$PROJECT/Packages/com.vrchat.worlds/Editor/VRCSDK/SDK3/VRCDefaultWorldScene.scenetemplate.meta"

echo "assembled $PROJECT"
find "$PROJECT" -maxdepth 2 -not -path '*/.*' -not -name Library -not -name Temp | sort
