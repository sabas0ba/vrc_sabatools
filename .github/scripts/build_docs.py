#!/usr/bin/env python3
"""Render the repository's Markdown documentation into a static site.

The listing page tells VCC where the packages are. This builds the other half:
the prose that explains what they do, published next to it on the same Pages
site so a link from the listing does not leave for GitHub.

Sources are the Markdown already in the repository -- each package's README,
CHANGELOG and Documentation~ folder. Nothing is written twice: the docs on
GitHub and the docs on the site are the same files.

Images are part of that subset: the figures under a package's
Documentation~/images are copied into the site next to the page that uses them,
so a document reads the same on GitHub and here.

Markdown is converted here rather than with a library, for the same reason
build_listing.py talks to the GitHub API directly: this runs in CI and the
fewer third-party moving parts it has, the fewer ways it breaks. The subset is
what the repository's own documents use, and build_docs_check.py fails the
build if a document reaches for something outside it.
"""

from __future__ import annotations

import argparse
import html
import json
import os
import re
import shutil
import sys
from dataclasses import dataclass, field
from typing import Callable, Iterable

# ---------------------------------------------------------------------------
# Inline markdown
# ---------------------------------------------------------------------------

_CODE = re.compile(r"`([^`]+)`")
_IMAGE = re.compile(r"!\[([^\]]*)\]\(([^)\s]+)\)")
_BOLD = re.compile(r"\*\*([^*]+)\*\*")
_LINK = re.compile(r"\[([^\]]+)\]\(([^)\s]+)\)")
_AUTOLINK = re.compile(r"<(https?://[^>\s]+)>")


@dataclass
class Rewriter:
    """Where a link points, and where an image the page uses ends up."""

    link: Callable[[str], str]
    image: Callable[[str], str]


def render_inline(text: str, links: Rewriter) -> str:
    """Escape a line of Markdown and apply the inline constructs."""
    placeholders: list[str] = []

    def stash(markup: str) -> str:
        placeholders.append(markup)
        return f"\x00{len(placeholders) - 1}\x00"

    # Code spans first: their contents must not be treated as markup.
    def code(match: re.Match) -> str:
        return stash(f"<code>{html.escape(match.group(1))}</code>")

    text = _CODE.sub(code, text)

    # Images before links: the two spellings differ only by the leading "!".
    def image(match: re.Match) -> str:
        alt, target = match.group(1), match.group(2)
        return stash(
            f'<img src="{html.escape(links.image(target), quote=True)}" '
            f'alt="{html.escape(alt, quote=True)}" loading="lazy">'
        )

    text = _IMAGE.sub(image, text)

    def link(match: re.Match) -> str:
        label, target = match.group(1), match.group(2)
        return stash(
            f'<a href="{html.escape(links.link(target), quote=True)}">'
            f"{render_inline(label, links)}</a>"
        )

    text = _LINK.sub(link, text)

    def autolink(match: re.Match) -> str:
        url = match.group(1)
        return stash(f'<a href="{html.escape(url, quote=True)}">{html.escape(url)}</a>')

    text = _AUTOLINK.sub(autolink, text)

    text = html.escape(text)
    text = _BOLD.sub(lambda m: f"<strong>{m.group(1)}</strong>", text)

    for index, markup in enumerate(placeholders):
        text = text.replace(f"\x00{index}\x00", markup)

    return text


# ---------------------------------------------------------------------------
# Block markdown
# ---------------------------------------------------------------------------


@dataclass
class Heading:
    level: int
    text: str
    anchor: str


@dataclass
class Document:
    title: str
    body: str
    headings: list[Heading] = field(default_factory=list)


def slugify(text: str, taken: set[str]) -> str:
    slug = re.sub(r"[^\w\- ]+", "", text, flags=re.UNICODE).strip().lower()
    slug = re.sub(r"[\s_]+", "-", slug) or "section"

    candidate = slug
    suffix = 2
    while candidate in taken:
        candidate = f"{slug}-{suffix}"
        suffix += 1

    taken.add(candidate)
    return candidate


def render_markdown(source: str, links: Rewriter) -> Document:
    lines = source.replace("\r\n", "\n").split("\n")
    out: list[str] = []
    headings: list[Heading] = []
    anchors: set[str] = set()

    index = 0
    title = ""

    def close_paragraph(buffer: list[str]) -> None:
        if buffer:
            out.append("<p>" + "<br>".join(buffer) + "</p>")
            buffer.clear()

    paragraph: list[str] = []

    while index < len(lines):
        line = lines[index]
        stripped = line.strip()

        # --- fenced code ------------------------------------------------
        if stripped.startswith("```"):
            close_paragraph(paragraph)
            language = stripped[3:].strip()
            index += 1
            code: list[str] = []
            while index < len(lines) and not lines[index].strip().startswith("```"):
                code.append(lines[index])
                index += 1
            index += 1

            klass = f' class="language-{html.escape(language, quote=True)}"' if language else ""
            out.append(f"<pre><code{klass}>" + html.escape("\n".join(code)) + "</code></pre>")
            continue

        # --- blank ------------------------------------------------------
        if not stripped:
            close_paragraph(paragraph)
            index += 1
            continue

        # --- horizontal rule --------------------------------------------
        if re.fullmatch(r"-{3,}|\*{3,}", stripped):
            close_paragraph(paragraph)
            out.append("<hr>")
            index += 1
            continue

        # --- heading ------------------------------------------------------
        heading = re.match(r"(#{1,6})\s+(.*)$", stripped)
        if heading:
            close_paragraph(paragraph)
            level = len(heading.group(1))
            text = heading.group(2).strip()
            anchor = slugify(text, anchors)

            if level == 1 and not title:
                title = re.sub(r"`", "", text)

            headings.append(Heading(level, text, anchor))
            out.append(
                f'<h{level} id="{html.escape(anchor, quote=True)}">'
                f"{render_inline(text, links)}</h{level}>"
            )
            index += 1
            continue

        # --- table --------------------------------------------------------
        if stripped.startswith("|") and index + 1 < len(lines) and _is_table_rule(lines[index + 1]):
            close_paragraph(paragraph)
            header = _split_row(stripped)
            index += 2
            rows: list[list[str]] = []
            while index < len(lines) and lines[index].strip().startswith("|"):
                rows.append(_split_row(lines[index].strip()))
                index += 1

            out.append(_render_table(header, rows, links))
            continue

        # --- blockquote ---------------------------------------------------
        if stripped.startswith(">"):
            close_paragraph(paragraph)
            quote: list[str] = []
            while index < len(lines) and lines[index].strip().startswith(">"):
                quote.append(lines[index].strip()[1:].strip())
                index += 1

            inner = "<br>".join(render_inline(q, links) for q in quote if q)
            out.append(f"<blockquote><p>{inner}</p></blockquote>")
            continue

        # --- lists --------------------------------------------------------
        if re.match(r"[-*+]\s+", stripped) or re.match(r"\d+\.\s+", stripped):
            close_paragraph(paragraph)
            block, index = _consume_list(lines, index)
            out.append(_render_list(block, links))
            continue

        # --- figure -------------------------------------------------------
        # A line that is nothing but an image is a figure, not a paragraph that
        # happens to contain one. The figures carry their own titles and
        # captions, so nothing is added around them here.
        if _IMAGE.fullmatch(stripped):
            close_paragraph(paragraph)
            out.append(f"<figure>{render_inline(stripped, links)}</figure>")
            index += 1
            continue

        paragraph.append(render_inline(stripped, links))
        index += 1

    close_paragraph(paragraph)
    return Document(title=title or "Documentation", body="\n".join(out), headings=headings)


def _is_table_rule(line: str) -> bool:
    return bool(re.fullmatch(r"\|(?:\s*:?-{2,}:?\s*\|)+", line.strip()))


def _split_row(line: str) -> list[str]:
    return [cell.strip() for cell in line.strip().strip("|").split("|")]


def _render_table(header: list[str], rows: list[list[str]], links: Rewriter) -> str:
    head = "".join(f"<th>{render_inline(cell, links)}</th>" for cell in header)
    body = "".join(
        "<tr>" + "".join(f"<td>{render_inline(cell, links)}</td>" for cell in row) + "</tr>"
        for row in rows
    )
    return f"<div class=\"table-scroll\"><table><thead><tr>{head}</tr></thead><tbody>{body}</tbody></table></div>"


@dataclass
class ListItem:
    text: str
    indent: int
    ordered: bool
    children: list["ListItem"] = field(default_factory=list)


def _consume_list(lines: list[str], index: int) -> tuple[list[ListItem], int]:
    items: list[ListItem] = []

    while index < len(lines):
        raw = lines[index]
        stripped = raw.strip()

        if not stripped:
            # A blank line ends the list unless another item follows.
            if index + 1 < len(lines) and re.match(r"\s*([-*+]|\d+\.)\s+", lines[index + 1]):
                index += 1
                continue
            break

        match = re.match(r"(\s*)([-*+]|\d+\.)\s+(.*)$", raw)
        if not match:
            # A wrapped continuation line belongs to the item above it.
            if items:
                items[-1].text += " " + stripped
                index += 1
                continue
            break

        indent = len(match.group(1))
        ordered = match.group(2)[-1] == "."
        items.append(ListItem(text=match.group(3).strip(), indent=indent, ordered=ordered))
        index += 1

    return _nest(items), index


def _nest(flat: list[ListItem]) -> list[ListItem]:
    roots: list[ListItem] = []
    stack: list[ListItem] = []

    for item in flat:
        while stack and stack[-1].indent >= item.indent:
            stack.pop()

        if stack:
            stack[-1].children.append(item)
        else:
            roots.append(item)

        stack.append(item)

    return roots


def _render_list(items: list[ListItem], links: Rewriter) -> str:
    if not items:
        return ""

    tag = "ol" if items[0].ordered else "ul"
    parts = []

    for item in items:
        inner = render_inline(item.text, links)
        if item.children:
            inner += _render_list(item.children, links)
        parts.append(f"<li>{inner}</li>")

    return f"<{tag}>" + "".join(parts) + f"</{tag}>"


# ---------------------------------------------------------------------------
# Site assembly
# ---------------------------------------------------------------------------


@dataclass
class Page:
    source: str
    output: str
    title: str
    section: str
    package_dir: str


def discover(repo: str) -> tuple[list[Page], list[dict]]:
    """Pages to render, and the package metadata behind them."""
    pages: list[Page] = []
    packages: list[dict] = []

    packages_dir = os.path.join(repo, "Packages")
    for package_id in sorted(os.listdir(packages_dir)):
        manifest_path = os.path.join(packages_dir, package_id, "package.json")
        if not os.path.isfile(manifest_path):
            continue

        with open(manifest_path, encoding="utf-8") as handle:
            manifest = json.load(handle)

        display = manifest.get("displayName", package_id)
        packages.append({"id": package_id, "manifest": manifest})

        candidates = [
            ("README.md", "index.html", display),
            ("CHANGELOG.md", "changelog.html", "変更履歴"),
        ]

        docs_dir = os.path.join(packages_dir, package_id, "Documentation~")
        if os.path.isdir(docs_dir):
            for name in sorted(os.listdir(docs_dir)):
                if name.endswith(".md"):
                    candidates.append(
                        (os.path.join("Documentation~", name), name[:-3] + ".html", name[:-3])
                    )

        for relative, output, title in candidates:
            source = os.path.join(packages_dir, package_id, relative)
            if os.path.isfile(source):
                pages.append(
                    Page(
                        source=source,
                        output=os.path.join(package_id, output),
                        title=title,
                        section=display,
                        package_dir=os.path.join(packages_dir, package_id),
                    )
                )

    return pages, packages


def make_rewriter(
    page: Page, pages: list[Page], repo_url: str, docs_root: str, assets: dict[str, str]
) -> Rewriter:
    """Resolve a page's links and images against the site being built."""
    by_source = {os.path.normpath(p.source): p for p in pages}
    page_dir = os.path.dirname(os.path.join("docs", page.output))

    def resolve(target: str) -> str:
        return os.path.normpath(os.path.join(os.path.dirname(page.source), target))

    def link(target: str) -> str:
        if target.startswith(("http://", "https://", "#", "mailto:")):
            return target

        anchor = ""
        if "#" in target:
            target, anchor = target.split("#", 1)
            anchor = "#" + anchor

        if not target:
            return anchor

        resolved = resolve(target)
        rendered = by_source.get(resolved)

        if rendered is not None:
            relative = os.path.relpath(
                os.path.join("docs", rendered.output), page_dir
            ).replace(os.sep, "/")
            return relative + anchor

        # Not part of the site: send the reader to the repository, which is
        # where the file actually lives.
        repo_relative = os.path.relpath(resolved, repo_url_root).replace(os.sep, "/")
        return f"{repo_url}/blob/main/{repo_relative}{anchor}"

    def image(target: str) -> str:
        if target.startswith(("http://", "https://", "data:")):
            return target

        resolved = resolve(target)
        if not os.path.isfile(resolved):
            raise SystemExit(
                f"error: {page.source} references a missing image: {target}"
            )

        # Images live beside the Markdown, under Documentation~ so that Unity
        # ignores them. The tilde is an editor convention and has no business
        # in a URL, so it is dropped on the way into the site.
        relative = os.path.relpath(resolved, page.package_dir).replace(os.sep, "/")
        if relative.startswith("../"):
            raise SystemExit(
                f"error: {page.source} uses an image from outside its package: {target}"
            )

        if relative.startswith("Documentation~/"):
            relative = relative[len("Documentation~/"):]

        package_id = os.path.basename(page.package_dir.rstrip(os.sep))
        destination = os.path.join(docs_root, package_id, relative.replace("/", os.sep))
        assets[resolved] = destination

        return os.path.relpath(
            os.path.join("docs", package_id, relative), page_dir
        ).replace(os.sep, "/")

    return Rewriter(link=link, image=image)


repo_url_root = ""


TEMPLATE = """<!doctype html>
<html lang="ja">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>{title}</title>
<link rel="stylesheet" href="{root}assets/tokens.css">
<link rel="stylesheet" href="{root}docs/docs.css">
</head>
<body>
<a class="skip" href="#content">本文へ</a>
<div class="shell">
<nav aria-label="ドキュメント">
  <a class="brand" href="{root}index.html">{site_name}</a>
  {nav}
</nav>
<main id="content">
{toc}
<article>
{body}
</article>
<footer>
  <a href="{repo_url}/blob/main/{source}">このページのソース</a>
</footer>
</main>
</div>
</body>
</html>
"""


def build_nav(pages: list[Page], current: Page, root: str) -> str:
    sections: dict[str, list[Page]] = {}
    for page in pages:
        sections.setdefault(page.section, []).append(page)

    parts = []
    for section, entries in sections.items():
        parts.append(f"<p class=\"nav-section\">{html.escape(section)}</p><ul>")
        for entry in entries:
            href = root + "docs/" + entry.output.replace(os.sep, "/")
            active = ' class="active"' if entry is current else ""
            parts.append(f'<li><a href="{href}"{active}>{html.escape(entry.title)}</a></li>')
        parts.append("</ul>")

    return "".join(parts)


def build_toc(document: Document) -> str:
    entries = [h for h in document.headings if 2 <= h.level <= 3]
    if len(entries) < 3:
        return ""

    parts = ['<aside class="toc"><p class="nav-section">目次</p><ul>']
    for heading in entries:
        parts.append(
            f'<li class="level-{heading.level}">'
            f'<a href="#{html.escape(heading.anchor, quote=True)}">{html.escape(heading.text)}</a></li>'
        )
    parts.append("</ul></aside>")
    return "".join(parts)


def main() -> int:
    global repo_url_root

    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", default=os.getcwd())
    parser.add_argument("--out", default=None, help="site root (default: <repo>/Website)")
    args = parser.parse_args()

    repo = os.path.abspath(args.repo)
    repo_url_root = repo
    out = os.path.abspath(args.out or os.path.join(repo, "Website"))

    with open(os.path.join(repo, "source.json"), encoding="utf-8") as handle:
        source_meta = json.load(handle)

    repo_url = source_meta.get("infoLink", {}).get(
        "url", "https://github.com/" + source_meta.get("githubRepo", "")
    ).rstrip("/")
    site_name = source_meta.get("name", "Docs")

    pages, packages = discover(repo)
    if not pages:
        print("error: no documentation sources found", file=sys.stderr)
        return 1

    docs_root = os.path.join(out, "docs")
    if os.path.isdir(docs_root):
        shutil.rmtree(docs_root)

    written = []
    assets: dict[str, str] = {}

    for page in pages:
        with open(page.source, encoding="utf-8") as handle:
            document = render_markdown(
                handle.read(), make_rewriter(page, pages, repo_url, docs_root, assets)
            )

        depth = len(page.output.replace(os.sep, "/").split("/"))
        root = "../" * depth

        html_out = TEMPLATE.format(
            title=html.escape(f"{document.title} | {site_name}"),
            root=root,
            site_name=html.escape(site_name),
            nav=build_nav(pages, page, root),
            toc=build_toc(document),
            body=document.body,
            repo_url=html.escape(repo_url, quote=True),
            source=html.escape(
                os.path.relpath(page.source, repo).replace(os.sep, "/"), quote=True
            ),
        )

        destination = os.path.join(docs_root, page.output)
        os.makedirs(os.path.dirname(destination), exist_ok=True)
        with open(destination, "w", encoding="utf-8", newline="\n") as handle:
            handle.write(html_out)

        written.append(os.path.relpath(destination, out).replace(os.sep, "/"))

    for source, destination in sorted(assets.items()):
        os.makedirs(os.path.dirname(destination), exist_ok=True)
        shutil.copyfile(source, destination)

    _write_stylesheet(docs_root)
    _write_index(docs_root, pages, packages, site_name, repo_url)

    for path in written:
        print(f"wrote {path}")

    print(
        f"ok: {len(written)} page(s) from {len(packages)} package(s), "
        f"{len(assets)} image(s)"
    )
    return 0


def _write_index(
    docs_root: str, pages: list[Page], packages: list[dict], site_name: str, repo_url: str
) -> None:
    cards = []
    for package in packages:
        manifest = package["manifest"]
        entry = next((p for p in pages if p.output.startswith(package["id"])), None)
        href = entry.output.replace(os.sep, "/") if entry else "#"

        cards.append(
            "<li>"
            f'<a href="{html.escape(href, quote=True)}">'
            f'<span class="card-title">{html.escape(manifest.get("displayName", package["id"]))}</span>'
            f'<span class="card-id">{html.escape(package["id"])}</span>'
            f'<span class="card-desc">{html.escape(manifest.get("description", ""))}</span>'
            "</a></li>"
        )

    body = (
        "<h1>ドキュメント</h1>"
        "<p>収録パッケージの説明です。リポジトリ内の Markdown をそのまま公開しています。</p>"
        '<ul class="cards">' + "".join(cards) + "</ul>"
    )

    html_out = TEMPLATE.format(
        title=html.escape(f"ドキュメント | {site_name}"),
        root="../",
        site_name=html.escape(site_name),
        nav=build_nav(pages, pages[0], "../"),
        toc="",
        body=body,
        repo_url=html.escape(repo_url, quote=True),
        source="README.md",
    )

    with open(os.path.join(docs_root, "index.html"), "w", encoding="utf-8", newline="\n") as handle:
        handle.write(html_out)


def _write_stylesheet(docs_root: str) -> None:
    os.makedirs(docs_root, exist_ok=True)
    with open(os.path.join(docs_root, "docs.css"), "w", encoding="utf-8", newline="\n") as handle:
        handle.write(DOCS_CSS)


DOCS_CSS = """/* Generated by .github/scripts/build_docs.py. The palette lives in
   ../assets/tokens.css, shared with the listing page. */
* { box-sizing: border-box; }

body {
  margin: 0;
  background: var(--bg);
  color: var(--text);
  font-family: system-ui, -apple-system, "Segoe UI", "Hiragino Sans", "Noto Sans JP", sans-serif;
  line-height: 1.75;
}

.skip {
  position: absolute;
  left: -9999px;
}

.skip:focus {
  left: 1rem;
  top: 1rem;
  padding: 0.5rem 1rem;
  background: var(--accent);
  color: var(--accent-text);
  border-radius: 6px;
  z-index: 10;
}

.shell {
  display: grid;
  grid-template-columns: minmax(0, 15rem) minmax(0, 1fr);
  gap: 2.5rem;
  max-width: 72rem;
  margin: 0 auto;
  padding: 2rem 1.25rem 4rem;
}

nav {
  position: sticky;
  top: 2rem;
  align-self: start;
  max-height: calc(100vh - 4rem);
  overflow-y: auto;
  font-size: 0.92rem;
}

.brand {
  display: block;
  font-weight: 700;
  font-size: 1.1rem;
  color: var(--text);
  text-decoration: none;
  margin-bottom: 1.5rem;
}

.nav-section {
  margin: 1.25rem 0 0.4rem;
  font-size: 0.78rem;
  letter-spacing: 0.04em;
  text-transform: uppercase;
  color: var(--muted);
}

nav ul, .toc ul {
  list-style: none;
  margin: 0;
  padding: 0;
}

nav li a {
  display: block;
  padding: 0.25rem 0.6rem;
  margin-left: -0.6rem;
  border-radius: 6px;
  color: var(--muted);
  text-decoration: none;
}

nav li a:hover { background: var(--code-bg); color: var(--text); }
nav li a.active { background: var(--accent); color: var(--accent-text); }

main { min-width: 0; }

article > *:first-child { margin-top: 0; }

h1, h2, h3, h4 { line-height: 1.35; margin: 2rem 0 0.75rem; }
h1 { font-size: 1.9rem; }
h2 { font-size: 1.4rem; border-bottom: 1px solid var(--border); padding-bottom: 0.35rem; }
h3 { font-size: 1.15rem; }

a { color: var(--accent); }

code {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 0.9em;
  background: var(--code-bg);
  padding: 0.15em 0.4em;
  border-radius: 4px;
}

pre {
  background: var(--code-bg);
  border: 1px solid var(--border);
  border-radius: 8px;
  padding: 0.9rem 1rem;
  overflow-x: auto;
}

pre code { background: none; padding: 0; }

blockquote {
  margin: 1.25rem 0;
  padding: 0.1rem 1rem;
  border-left: 3px solid var(--accent);
  color: var(--muted);
}

hr { border: 0; border-top: 1px solid var(--border); margin: 2.5rem 0; }

/* Figures are SVG and scale to whatever width is left, but never past their
   natural size: past it the labels grow larger than the body text. */
figure {
  margin: 1.5rem 0;
  overflow-x: auto;
}

figure img {
  display: block;
  max-width: 100%;
  height: auto;
  border: 1px solid var(--border);
  border-radius: 8px;
}

.table-scroll { overflow-x: auto; margin: 1.25rem 0; }

table { border-collapse: collapse; width: 100%; font-size: 0.94rem; }
th, td { border: 1px solid var(--border); padding: 0.5rem 0.7rem; text-align: left; vertical-align: top; }
th { background: var(--code-bg); }

.toc {
  float: right;
  margin: 0 0 1.5rem 1.5rem;
  padding: 0.75rem 1rem;
  border: 1px solid var(--border);
  border-radius: 8px;
  background: var(--surface);
  font-size: 0.88rem;
  max-width: 18rem;
}

.toc a { color: var(--muted); text-decoration: none; }
.toc a:hover { color: var(--text); }
.toc .level-3 { padding-left: 1rem; }

.cards { list-style: none; padding: 0; display: grid; gap: 1rem; }

.cards a {
  display: block;
  padding: 1rem 1.2rem;
  border: 1px solid var(--border);
  border-radius: 10px;
  background: var(--surface);
  text-decoration: none;
  color: var(--text);
}

.cards a:hover { border-color: var(--accent); }
.card-title { display: block; font-weight: 700; }
.card-id { display: block; font-size: 0.85rem; color: var(--muted); font-family: ui-monospace, monospace; }
.card-desc { display: block; margin-top: 0.5rem; color: var(--muted); }

footer {
  margin-top: 3rem;
  padding-top: 1rem;
  border-top: 1px solid var(--border);
  font-size: 0.88rem;
  color: var(--muted);
}

@media (max-width: 52rem) {
  .shell { grid-template-columns: minmax(0, 1fr); gap: 1rem; }
  nav { position: static; max-height: none; }
  .toc { float: none; max-width: none; margin-left: 0; }
}
"""


if __name__ == "__main__":
    raise SystemExit(main())
