#!/usr/bin/env python3
"""**The link gate, asked of the product.**

Every link in the book, and every citation of the book from anywhere else in the
tree, resolves to a page that exists and an anchor that page declares.

This used to be two regular expressions inside `check-docs.py`. It is two sigla
queries now, because that is what the questions are:

    # a link that names a page the book does not have
    {from = F, to = P} where doc.Link {from = F, toPage = P, toAnchor = _};
                             !doc.Page P

    # a link that names an anchor its page does not declare
    {from = F, page = P, anchor = A} where
      doc.Link {from = F, toPage = P, toAnchor = A};
      A != ""; doc.Page P; !doc.Anchor {page = P, id = A}

Two things came out of moving them. The facts are produced by **the site's own MDX
parse** (`web/docs-facts.mjs`), so an anchor the gate accepts is an anchor a reader
can land on — the old gate computed anchors itself, matched `^#{1,6} ` inside code
fences, and invented eight that no page declares. And the citation check is no
longer specific to the invariant registry: the query does not care which page a
link names, so every `web/src/content/<page>.mdx#<anchor>` in the tree is checked,
not just the ones that say `invariants`.

Needs a built `fjord` (`$FJORD`, or `target/release/fjord`) and `web/node_modules`.
Exit 1 on any finding.
"""

import json
import os
import re
import shutil
import signal
import subprocess
import sys
import tempfile
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent

# Where a citation of the book can be written. `web` is here for the pages citing
# each other from inside authored HTML — `docs-facts.mjs` reads those — and for the
# site's own source, which cites the registry like any crate does.
AREAS = ("crates", "docs", "clients", "bench", "scripts", ".github", "schemas", "wasm")
ROOT_FILES = ("README.md", "AGENTS.md", "PLAN.md", "CLAUDE.md", "CHANGELOG.md", "Cargo.toml")
SUFFIXES = {".rs", ".md", ".mdx", ".cs", ".llw", ".toml", ".yml", ".py", ".sh", ".sigla", ".ts", ".tsx", ".mjs"}

CITATION = re.compile(r"web/src/content/([a-z0-9-]+)\.mdx(?:#([A-Za-z0-9_-]+))?")

# **A gate's own controls cite pages on purpose.** `test_check_docs.py` plants a link
# to `one.html` in a temporary tree to prove a finding is raised; scanned from here
# that reads as six dead links to a page that only ever exists inside the fixture.
# Named rather than pattern-matched, so a real file cannot fall through by being
# called `test_`-something.
FIXTURES = {"scripts/test_check_docs.py", "scripts/test_check_links.py"}

DEAD_PAGE = (
    "{from = F, to = P} where doc.Link {from = F, toPage = P, toAnchor = _}; !doc.Page P"
)
DEAD_ANCHOR = (
    "{from = F, page = P, anchor = A} where "
    "doc.Link {from = F, toPage = P, toAnchor = A}; "
    'A != ""; doc.Page P; !doc.Anchor {page = P, id = A}'
)


def binary() -> Path:
    found = os.environ.get("FJORD")
    if found:
        return Path(found)
    for candidate in ("target/release/fjord", "target/debug/fjord"):
        if (ROOT / candidate).exists():
            return ROOT / candidate
    sys.exit(
        "no fjord to ask: build one with `cargo build --release --bin fjord`, "
        "or point $FJORD at it"
    )


def cited(root: Path) -> list[str]:
    """Every citation of a page of the book, from everywhere that is not the book."""
    out = []

    def scan(path: Path) -> None:
        if path.suffix not in SUFFIXES:
            return
        text = path.read_text(encoding="utf-8", errors="ignore")
        rel = path.relative_to(root).as_posix()
        if rel in FIXTURES:
            return
        for line_no, line in enumerate(text.splitlines(), 1):
            for page, anchor in CITATION.findall(line):
                out.append(
                    json.dumps(
                        {
                            "predicate": "doc.Link",
                            "fact": {
                                "from": f"{rel}:{line_no}",
                                "toPage": page,
                                "toAnchor": anchor or "",
                            },
                        }
                    )
                )

    for area in AREAS:
        base = root / area
        if not base.exists():
            continue
        for path in sorted(base.rglob("*")):
            if path.is_file():
                scan(path)
    for name in ROOT_FILES:
        if (root / name).exists():
            scan(root / name)
    return out


def main() -> int:
    fjord = binary()
    if not (ROOT / "web/node_modules/@mdx-js/mdx").exists():
        sys.exit(
            "the book's dependencies are not installed: the facts come from the site's "
            "own MDX parse, so this needs `npm ci` in web/"
        )

    facts = subprocess.run(
        ["node", "docs-facts.mjs"],
        cwd=ROOT / "web",
        capture_output=True,
        text=True,
    )
    if facts.returncode != 0:
        sys.stderr.write(facts.stderr)
        return 1

    # A short root on purpose: a Unix socket path is `SUN_LEN`-bounded, and the
    # default temporary directory under a long working directory has overrun it.
    work = Path(tempfile.mkdtemp(dir="/tmp", prefix="fjl"))
    try:
        book = work / "book.jsonl"
        book.write_text(facts.stdout, encoding="utf-8")
        citations = cited(ROOT)
        (work / "cited.jsonl").write_text("\n".join(citations) + "\n", encoding="utf-8")

        data = work / "db"
        server = subprocess.Popen(
            [str(fjord), "--data-dir", str(data), "serve"],
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        try:
            socket = data / "fjord.sock"
            for _ in range(100):
                if socket.exists():
                    break
                if server.poll() is not None:
                    return 1
                time.sleep(0.1)
            else:
                sys.stderr.write("the server never opened its socket\n")
                return 1

            def run(*args: str) -> subprocess.CompletedProcess:
                return subprocess.run(
                    [str(fjord), "--data-dir", str(data), *args],
                    capture_output=True,
                    text=True,
                )

            made = run("create", "docs", "--schema", str(ROOT / "scripts/docs.sigla"))
            if made.returncode != 0:
                sys.stderr.write(made.stderr or made.stdout)
                return 1
            written = run("write", "docs", str(book), str(work / "cited.jsonl"))
            if written.returncode != 0:
                sys.stderr.write(written.stderr or written.stdout)
                return 1

            findings = []
            for what, query in (("page", DEAD_PAGE), ("anchor", DEAD_ANCHOR)):
                asked = run("query", "docs", query, "--format", "jsonl")
                if asked.returncode != 0:
                    sys.stderr.write(asked.stderr or asked.stdout)
                    return 1
                for line in asked.stdout.splitlines():
                    if not line.strip():
                        continue
                    row = json.loads(line)
                    if what == "page":
                        findings.append(
                            f"{row['from']} links to {row['to']}.html, which is not a page"
                        )
                    else:
                        findings.append(
                            f"{row['from']} links to {row['page']}.html#{row['anchor']}, "
                            f"an anchor that page does not declare"
                        )
        finally:
            server.send_signal(signal.SIGTERM)
            server.wait(timeout=30)
    finally:
        shutil.rmtree(work, ignore_errors=True)

    if findings:
        for finding in sorted(findings):
            sys.stderr.write(f"{finding}\n")
        sys.stderr.write(f"\n{len(findings)} dead link(s)\n")
        return 1

    print(
        f"every link resolves: {facts.stdout.count(chr(10))} facts about the book "
        f"and {len(citations)} citation(s) of it, asked in sigla"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
