#!/usr/bin/env python3
"""Check the generated documentation site.

build_docs.py converts a subset of Markdown by hand. The failure mode of a
hand-written converter is not a crash, it is a page that renders but has raw
syntax sitting in the text, or a link that quietly points nowhere. Both look
fine to the build and wrong to a reader, so they are checked here instead.

Figures fail the same quiet way: an image whose file was never copied into the
site is a broken icon on the page and a clean exit here, so every <img> is
resolved as well, and every one of them has to carry alt text.

Run after build_docs.py, against the same output directory.
"""

from __future__ import annotations

import argparse
import html
import os
import re
import sys
from html.parser import HTMLParser

# Constructs that must have been consumed by the renderer. If one of these
# survives into visible text, either the document used something outside the
# supported subset or the renderer has a hole.
LEFTOVERS = [
    (re.compile(r"!\[[^\]]*\]\([^)]+\)"), "unrendered image"),
    (re.compile(r"\*\*"), "unrendered bold (**)"),
    (re.compile(r"^\s*\|.*\|\s*$", re.MULTILINE), "unrendered table row"),
    (re.compile(r"^\s*#{1,6}\s"), "unrendered heading"),
    (re.compile(r"^\s*[-*+]\s+\S", re.MULTILINE), "unrendered list item"),
    (re.compile(r"\[[^\]]+\]\([^)]+\)"), "unrendered link"),
    (re.compile(r"```"), "unrendered code fence"),
]


class Extractor(HTMLParser):
    """Visible text and outgoing links, ignoring code blocks."""

    def __init__(self) -> None:
        super().__init__(convert_charrefs=True)
        self.text: list[str] = []
        self.links: list[str] = []
        self.stylesheets: list[str] = []
        self.images: list[tuple[str, str]] = []
        self._depth_code = 0

    def handle_starttag(self, tag, attrs):
        attributes = dict(attrs)

        if tag in ("code", "pre"):
            self._depth_code += 1
        elif tag == "a" and attributes.get("href"):
            self.links.append(attributes["href"])
        elif tag == "img":
            self.images.append((attributes.get("src", ""), attributes.get("alt", "")))
        elif tag == "link" and attributes.get("rel") == "stylesheet":
            self.stylesheets.append(attributes.get("href", ""))

    def handle_endtag(self, tag):
        if tag in ("code", "pre") and self._depth_code > 0:
            self._depth_code -= 1

    def handle_data(self, data):
        if self._depth_code == 0:
            self.text.append(data)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", default=os.getcwd())
    parser.add_argument("--out", default=None)
    args = parser.parse_args()

    repo = os.path.abspath(args.repo)
    out = os.path.abspath(args.out or os.path.join(repo, "Website"))
    docs = os.path.join(out, "docs")

    if not os.path.isdir(docs):
        print(f"error: no docs at {docs}; run build_docs.py first", file=sys.stderr)
        return 1

    problems: list[str] = []
    pages = 0

    for root, _, files in os.walk(docs):
        for name in sorted(files):
            if not name.endswith(".html"):
                continue

            pages += 1
            path = os.path.join(root, name)
            relative = os.path.relpath(path, out).replace(os.sep, "/")

            with open(path, encoding="utf-8") as handle:
                page = handle.read()

            extractor = Extractor()
            extractor.feed(page)
            text = "".join(extractor.text)

            for pattern, description in LEFTOVERS:
                match = pattern.search(text)
                if match:
                    excerpt = match.group(0).strip().replace("\n", " ")[:60]
                    problems.append(f"{relative}: {description}: {excerpt!r}")

            for href in extractor.links + extractor.stylesheets:
                problem = check_link(href, path, out)
                if problem:
                    problems.append(f"{relative}: {problem}")

            for src, alt in extractor.images:
                if not src:
                    problems.append(f"{relative}: image without a source")
                    continue

                problem = check_link(src, path, out)
                if problem:
                    problems.append(f"{relative}: {problem.replace('link', 'image')}")

                if not alt.strip():
                    problems.append(f"{relative}: image without alt text: {src!r}")

    if not pages:
        problems.append("no pages were generated")

    # Every package must be reachable from the docs index.
    index = os.path.join(docs, "index.html")
    if not os.path.isfile(index):
        problems.append("docs/index.html is missing")
    else:
        with open(index, encoding="utf-8") as handle:
            listed = handle.read()

        for package_id in sorted(os.listdir(os.path.join(repo, "Packages"))):
            if not os.path.isfile(os.path.join(repo, "Packages", package_id, "package.json")):
                continue
            if html.escape(package_id) not in listed:
                problems.append(f"docs/index.html does not link {package_id}")

            # The listing page links every package card straight at this path,
            # and it is only written when the package has a README.md. A
            # package that has a CHANGELOG or Documentation~ but no README
            # still produces a site the index can link, so nothing else here
            # would notice the card going nowhere.
            if not os.path.isfile(os.path.join(docs, package_id, "index.html")):
                problems.append(
                    f"docs/{package_id}/index.html is missing: the package needs a README.md, "
                    "which is what the listing page's card links to"
                )

    if problems:
        for problem in problems:
            print(f"error: {problem}", file=sys.stderr)
        return 1

    print(f"ok: {pages} documentation page(s) render cleanly")
    return 0


def check_link(href: str, page_path: str, out: str) -> str | None:
    if href.startswith(("http://", "https://", "mailto:")):
        return None

    target, _, anchor = href.partition("#")
    if not target:
        return None

    resolved = os.path.normpath(os.path.join(os.path.dirname(page_path), target))

    # Anything under the published site must actually exist in it.
    if os.path.commonpath([resolved, out]) == out:
        if not os.path.exists(resolved):
            return f"broken internal link {href!r}"
        return None

    return f"link escapes the site root: {href!r}"


if __name__ == "__main__":
    raise SystemExit(main())
