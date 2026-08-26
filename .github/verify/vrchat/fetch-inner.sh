#!/bin/sh
#
# Runs inside the container started by fetch.sh. Downloads each pinned package,
# checks its SHA256 and extracts it.
#
# Busybox already provides wget, unzip and sha256sum, so this needs no package
# installation: the toolchain is fully determined by the image digest.
set -eu

LOCK="$1"
OUT="$2"

while read -r name version sha url; do
    case "$name" in
        '' | \#*) continue ;;
    esac

    # A lock line whose hash has never been filled in must stop the run. The
    # alternative -- downloading and trusting whatever arrives -- would make
    # the lock file decorative.
    case "$sha" in
        FILL_ME_IN* | TODO*)
            echo "error: $name $version has no sha256 in the lock file" >&2
            echo "       take it from https://packages.vrchat.com/official and fill it in" >&2
            exit 1
            ;;
    esac

    archive="$OUT/$name-$version.zip"
    target="$OUT/$name"

    if [ ! -f "$archive" ]; then
        echo "downloading $name $version"
        # Download to a temporary name so an interrupted run cannot leave a
        # truncated archive that looks cached on the next one.
        wget -q -O "$archive.part" "$url"
        mv "$archive.part" "$archive"
    else
        echo "cached: $name $version"
    fi

    if ! echo "$sha  $archive" | sha256sum -c - >/dev/null 2>&1; then
        echo "error: sha256 mismatch for $name $version" >&2
        echo "       expected $sha" >&2
        echo "       got      $(sha256sum "$archive" | cut -d' ' -f1)" >&2
        exit 1
    fi

    rm -rf "$target"
    mkdir -p "$target"
    unzip -q "$archive" -d "$target"

    echo "ok: $name $version -> $target"
done < "$LOCK"

# The container writes as root. Leave everything group and world writable so the
# host user can replace or delete the tree without needing the same uid.
chmod -R a+rwX "$OUT"
