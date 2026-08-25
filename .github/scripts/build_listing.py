#!/usr/bin/env python3
"""Build a VPM repository listing (index.json) from this repo's GitHub Releases.

The release workflow attaches two assets to every release:

  * ``<package>-<version>.zip``   the package payload VCC downloads
  * ``<package>-<version>.json``  that package's manifest, already augmented
                                  with ``url`` and ``zipSHA256``

This script simply collects those manifests and nests them under
``packages.<id>.versions.<version>``, which is the shape VCC expects. Doing the
hashing at release time means we never have to re-download the zips here.
"""

from __future__ import annotations

import argparse
import json
import os
import sys
import urllib.error
import urllib.request
from typing import Any

GITHUB_API = "https://api.github.com"


def die(message: str):
    print(f"error: {message}", file=sys.stderr)
    raise SystemExit(1)


def request_json(url: str, token: str | None) -> Any:
    headers = {
        "Accept": "application/vnd.github+json",
        "User-Agent": "sabatools-listing-builder",
        "X-GitHub-Api-Version": "2022-11-28",
    }
    if token:
        headers["Authorization"] = f"Bearer {token}"

    request = urllib.request.Request(url, headers=headers)
    with urllib.request.urlopen(request, timeout=60) as response:
        return json.loads(response.read().decode("utf-8"))


def request_bytes(url: str, token: str | None) -> bytes:
    headers = {
        "Accept": "application/octet-stream",
        "User-Agent": "sabatools-listing-builder",
    }
    if token:
        headers["Authorization"] = f"Bearer {token}"

    request = urllib.request.Request(url, headers=headers)
    with urllib.request.urlopen(request, timeout=120) as response:
        return response.read()


def iter_releases(repo: str, token: str | None):
    page = 1
    while True:
        url = f"{GITHUB_API}/repos/{repo}/releases?per_page=100&page={page}"
        try:
            batch = request_json(url, token)
        except urllib.error.HTTPError as exc:
            if exc.code == 404:
                # A repository with no releases yet is a valid starting state.
                return
            raise

        if not batch:
            return

        yield from batch
        page += 1


def collect_versions(repo: str, token: str | None, allowed: set[str]) -> dict[str, dict]:
    """Map package id -> {version -> manifest}."""
    packages: dict[str, dict] = {}

    for release in iter_releases(repo, token):
        if release.get("draft"):
            continue

        tag = release.get("tag_name", "<untagged>")

        for asset in release.get("assets", []):
            name = asset.get("name", "")
            if not name.endswith(".json"):
                continue

            try:
                raw = request_bytes(asset["url"], token)
                manifest = json.loads(raw.decode("utf-8"))
            except (urllib.error.URLError, json.JSONDecodeError, KeyError) as exc:
                print(f"warning: skipping asset {name} of {tag}: {exc}", file=sys.stderr)
                continue

            package_id = manifest.get("name")
            version = manifest.get("version")

            if not package_id or not version:
                print(f"warning: {name} of {tag} has no name/version", file=sys.stderr)
                continue

            if allowed and package_id not in allowed:
                print(f"note: {package_id} is not listed in source.json, skipping", file=sys.stderr)
                continue

            if not manifest.get("url"):
                print(f"warning: {package_id} {version} has no download url, skipping", file=sys.stderr)
                continue

            entry = packages.setdefault(package_id, {})
            if version in entry:
                print(f"warning: duplicate {package_id} {version}, keeping the first", file=sys.stderr)
                continue

            entry[version] = manifest
            print(f"found {package_id} {version}")

    return packages


def version_sort_key(version: str) -> tuple:
    """Best-effort semver ordering; unparsable parts fall back to string compare."""
    core = version.split("+", 1)[0]
    core, _, prerelease = core.partition("-")

    numbers = []
    for part in core.split("."):
        numbers.append(int(part) if part.isdigit() else 0)

    while len(numbers) < 3:
        numbers.append(0)

    # A release always sorts above its own prereleases.
    return (*numbers[:3], 1 if not prerelease else 0, prerelease)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", default="source.json", help="listing metadata")
    parser.add_argument("--output", default="Website/index.json", help="generated listing")
    parser.add_argument("--repo", default=os.environ.get("GITHUB_REPOSITORY"), help="owner/name")
    args = parser.parse_args()

    if not args.repo:
        die("repository is unknown; pass --repo owner/name or set GITHUB_REPOSITORY")

    try:
        with open(args.source, "r", encoding="utf-8") as handle:
            source = json.load(handle)
    except OSError as exc:
        die(f"cannot read {args.source}: {exc}")
    except json.JSONDecodeError as exc:
        die(f"{args.source} is not valid JSON: {exc}")

    token = os.environ.get("GITHUB_TOKEN") or os.environ.get("GH_TOKEN")
    if not token:
        print("warning: no GITHUB_TOKEN set, using unauthenticated API limits", file=sys.stderr)

    allowed = set(source.get("packages") or [])
    collected = collect_versions(args.repo, token, allowed)

    listing = {
        "name": source.get("name", "Unnamed Listing"),
        "id": source.get("id", "com.example.vpm"),
        "url": source.get("url", ""),
        "author": source.get("author", ""),
        "description": source.get("description", ""),
        "packages": {},
    }

    for optional in ("bannerUrl", "infoLink"):
        if source.get(optional):
            listing[optional] = source[optional]

    for package_id in sorted(collected):
        versions = collected[package_id]
        ordered = sorted(versions, key=version_sort_key, reverse=True)
        listing["packages"][package_id] = {
            "versions": {version: versions[version] for version in ordered}
        }

    output_dir = os.path.dirname(args.output)
    if output_dir:
        os.makedirs(output_dir, exist_ok=True)

    with open(args.output, "w", encoding="utf-8") as handle:
        json.dump(listing, handle, indent=2, ensure_ascii=False)
        handle.write("\n")

    total = sum(len(v) for v in collected.values())
    print(f"wrote {args.output}: {len(collected)} package(s), {total} version(s)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
