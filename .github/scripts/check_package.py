#!/usr/bin/env python3
"""Check a VPM package's manifest and the files around it.

Split out of verify.sh so that, like the rest of this repository's Python, it
runs inside the pinned container rather than against whatever interpreter the
host happens to have.

Usage: check_package.py <repo-root> <package-directory>
"""

import json
import os
import sys

repo, package = sys.argv[1], sys.argv[2]
problems = []

manifest_path = os.path.join(package, "package.json")
with open(manifest_path, encoding="utf-8") as handle:
    manifest = json.load(handle)

for field in ("name", "displayName", "version", "unity", "description"):
    if not manifest.get(field):
        problems.append(f"package.json is missing '{field}'")

package_id = os.path.basename(package.rstrip("/"))
if manifest.get("name") != package_id:
    problems.append(f"package.json name '{manifest.get('name')}' != folder '{package_id}'")

expected_license = "Apache-2.0"
if manifest.get("license") != expected_license:
    problems.append(
        f"package.json license '{manifest.get('license')}' != '{expected_license}'"
    )

for license_path in (
    os.path.join(repo, "LICENSE"),
    os.path.join(package, "LICENSE.md"),
):
    if not os.path.exists(license_path):
        problems.append(f"missing Apache-2.0 license file: {license_path}")
        continue
    with open(license_path, encoding="utf-8") as handle:
        license_text = handle.read()
    if "Apache License" not in license_text or "Version 2.0" not in license_text:
        problems.append(f"{license_path} is not an Apache License 2.0 text")

version = manifest.get("version", "")
if version and not all(part.isdigit() for part in version.split("-")[0].split(".")):
    problems.append(f"version '{version}' is not numeric dotted form")

changelog = os.path.join(package, "CHANGELOG.md")
if os.path.exists(changelog):
    with open(changelog, encoding="utf-8") as handle:
        if f"[{version}]" not in handle.read():
            problems.append(f"CHANGELOG.md has no entry for version {version}")

with open(os.path.join(repo, "source.json"), encoding="utf-8") as handle:
    source = json.load(handle)

listed = source.get("packages") or []
if manifest.get("name") not in listed:
    problems.append(f"{manifest.get('name')} is not listed in source.json packages")

# A dependency on a sibling package in this repository has to name one that
# exists, or VCC resolves it against the wider VPM world and installs
# something else -- or nothing at all, with the package silently inert.
for dependency in (manifest.get("vpmDependencies") or {}):
    if not dependency.startswith(source.get("id", "\0")):
        continue
    if dependency not in listed:
        problems.append(
            f"vpmDependencies names '{dependency}', which is not a package in this repository"
        )

# Every non-tilde file Unity imports needs a .meta, or GUIDs churn per install.
missing = []
for root, dirs, files in os.walk(package):
    dirs[:] = [d for d in dirs if not d.endswith("~") and not d.startswith(".")]
    for name in dirs:
        target = os.path.join(root, name)
        if not os.path.exists(target + ".meta"):
            missing.append(os.path.relpath(target, repo))
    for name in files:
        if name.endswith(".meta") or name.startswith("."):
            continue
        if os.path.splitext(name)[1].lower() not in (".cs", ".asmdef", ".shader", ".cginc", ".hlsl", ".json", ".md"):
            continue
        target = os.path.join(root, name)
        if not os.path.exists(target + ".meta"):
            missing.append(os.path.relpath(target, repo))

problems.extend(f"missing .meta for {path}" for path in sorted(missing))

if problems:
    for problem in problems:
        print(f"error: {problem}", file=sys.stderr)
    sys.exit(1)

print(f"ok: {manifest['name']} {version}")
