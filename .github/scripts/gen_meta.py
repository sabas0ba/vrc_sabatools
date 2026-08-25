#!/usr/bin/env python3
"""Generate missing Unity .meta files for the packages.

Unity creates these on first import, but with a GUID that differs per project
-- and committed GUIDs are what keep references stable across installs, which
is why check_package.py fails when one is absent. This writes a .meta with a
fresh random GUID for every folder and importable file that lacks one, and
never touches an existing .meta.

Like the rest of this repository's Python, run it through the pinned
container:

    .github/scripts/run.sh .github/scripts/gen_meta.py
"""

import os
import sys
import uuid

FOLDER_TEMPLATE = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

MONO_TEMPLATE = """fileFormatVersion: 2
guid: {guid}
MonoImporter:
  externalObjects: {{}}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {{instanceID: 0}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

TEXT_TEMPLATE = """fileFormatVersion: 2
guid: {guid}
TextScriptImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

ASMDEF_TEMPLATE = """fileFormatVersion: 2
guid: {guid}
AssemblyDefinitionImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

# Extensions Unity imports, and the importer each one needs. Kept in step with
# the extension list in check_package.py: a file type that script demands a
# .meta for is a file type this one has to be able to write.
TEMPLATES = {
    ".cs": MONO_TEMPLATE,
    ".asmdef": ASMDEF_TEMPLATE,
    ".json": TEXT_TEMPLATE,
    ".md": TEXT_TEMPLATE,
    ".shader": TEXT_TEMPLATE,
    ".cginc": TEXT_TEMPLATE,
    ".hlsl": TEXT_TEMPLATE,
}


def write_meta(target: str, template: str) -> int:
    meta = target + ".meta"
    if os.path.exists(meta):
        return 0
    with open(meta, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(template.format(guid=uuid.uuid4().hex))
    print(f"created {meta}")
    return 1


def main() -> int:
    packages_dir = os.path.join(os.getcwd(), "Packages")
    if not os.path.isdir(packages_dir):
        print("error: no Packages/ directory here; run from the repository root", file=sys.stderr)
        return 1

    created = 0
    for package_id in sorted(os.listdir(packages_dir)):
        package = os.path.join(packages_dir, package_id)
        if not os.path.isdir(package):
            continue
        for root, dirs, files in os.walk(package):
            # Unity ignores hidden folders and ~ suffixed ones (Samples~,
            # Documentation~), so they get no .meta.
            dirs[:] = [d for d in dirs if not d.endswith("~") and not d.startswith(".")]
            for name in sorted(dirs):
                created += write_meta(os.path.join(root, name), FOLDER_TEMPLATE)
            for name in sorted(files):
                if name.startswith(".") or name.endswith(".meta"):
                    continue
                template = TEMPLATES.get(os.path.splitext(name)[1].lower())
                if template:
                    created += write_meta(os.path.join(root, name), template)

    print(f"ok: {created} .meta file(s) created")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
