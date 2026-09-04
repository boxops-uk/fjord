#!/usr/bin/env python3
"""The drift gate: the checks that would have caught the documentation going stale.

Five checks, each of which failed silently once:
  1. every relative link and anchor in website/content/ resolves;
  2. every `invariants.md#iN`-style citation in crates/, docs/ and clients/ resolves
     to an anchor the registry actually declares;
  3. nothing references a documentation file that no longer exists;
  4. no `Phase [0-9]` reference survives in crates/ or website/ — the compiler-pass
     sense of "phase" is fine, a build-plan number is not (bench/ keeps its history);
  5. nothing describes a retired **name** as live — a deleted schema, a retired crate,
     a deleted script, a removed flag, a retired predicate. This is the largest of the
     five, because a file can only be deleted once and a name outlives its thing in
     prose: four hand sweeps in one release each missed what the last one missed.
     HISTORY says where naming the dead is a fact rather than a mistake, and
     SAYS_IT_IS_GONE where a live file spells a name in order to announce it is gone.

Standard library only, like the site generator. Exit 1 on any finding.
"""

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
CONTENT = ROOT / "website" / "content"

findings: list[str] = []


def fail(message: str) -> None:
    findings.append(message)


# ---- 1. site-internal links and anchors -----------------------------------------

def site_anchors(text: str) -> set[str]:
    anchors = set(re.findall(r'<a id="([A-Za-z0-9_-]+)"></a>', text))
    anchors |= set(re.findall(r"\{#([A-Za-z0-9_-]+)\}", text))
    for heading in re.findall(r"^#{1,6} +(.+?)(?:\{#[A-Za-z0-9_-]+\})?$", text, re.M):
        # **The marks come off first**, as `build.py` and `web/`'s parser both do
        # (`plain`, then `slugify`). A heading that cites an invariant carries a
        # link, and slugifying the raw text mangles the target into the anchor —
        # which reads as a missing anchor for a link that resolves perfectly.
        stripped = re.sub(r"\[([^\]]+)\]\([^)]*\)", r"\1", heading.strip())
        stripped = re.sub(r"[`*~]", "", stripped)
        slug = re.sub(r"[^a-z0-9 -]", "", stripped.lower())
        anchors.add(re.sub(r"[ -]+", "-", slug).strip("-"))
    return anchors


pages = {p.stem: p.read_text(encoding="utf-8") for p in CONTENT.glob("*.md")}
anchors = {slug: site_anchors(text) for slug, text in pages.items()}

for slug, text in pages.items():
    for target in re.findall(r"\]\(([^)]+)\)", text):
        if target.startswith(("http://", "https://", "mailto:")):
            continue
        page, _, anchor = target.partition("#")
        if page and not page.endswith(".html"):
            continue  # not a site link
        name = page[:-5] if page else slug
        if name not in pages:
            fail(f"website/content/{slug}.md links to missing page {target}")
        elif anchor and anchor not in anchors[name]:
            fail(f"website/content/{slug}.md links to missing anchor {target}")

# ---- 2. invariant citations resolve ----------------------------------------------

registry = anchors.get("invariants", set())
citation = re.compile(r"invariants\.md#([A-Za-z0-9_-]+)")

for area in ("crates", "docs", "clients", "bench"):
    for path in (ROOT / area).rglob("*"):
        if path.suffix not in {".rs", ".md", ".cs", ".llw", ".toml"}:
            continue
        text = path.read_text(encoding="utf-8", errors="ignore")
        for anchor in citation.findall(text):
            if anchor not in registry:
                fail(f"{path.relative_to(ROOT)} cites invariants.md#{anchor}, not in the registry")

# ---- 3. no references to retired documentation files -----------------------------

RETIRED = re.compile(
    r"docs/(0[1-7]-[a-z-]+|invariants|conventions|testing|fjord-cli-design|glossary"
    r"|open-decisions|performance|auth|query-surface|repository-rules"
    r"|glean|glean-comparison|glean-capabilities|phase-[0-9.]+[a-z-]*)\.md"
)

for area in ("crates", "docs", "clients", "bench", "website", "scripts", ".github"):
    base = ROOT / area
    if not base.exists():
        continue
    for path in base.rglob("*"):
        if path.suffix not in {".rs", ".md", ".cs", ".llw", ".toml", ".yml", ".py", ".sh"}:
            continue
        text = path.read_text(encoding="utf-8", errors="ignore")
        for line_no, line in enumerate(text.splitlines(), 1):
            if RETIRED.search(line):
                fail(f"{path.relative_to(ROOT)}:{line_no} references a retired doc: {line.strip()[:90]}")

for name in ("README.md", "AGENTS.md", "PLAN.md", "CLAUDE.md", "CHANGELOG.md", "Cargo.toml"):
    text = (ROOT / name).read_text(encoding="utf-8")
    for line_no, line in enumerate(text.splitlines(), 1):
        if RETIRED.search(line):
            fail(f"{name}:{line_no} references a retired doc: {line.strip()[:90]}")

# ---- 3b. no references to things the tree has retired ----------------------------
#
# **Check 3 knows about retired *files*, and a release retires more than files.** The
# schema switch deleted a schema, a crate, two scripts, a flag and six predicates, and
# every one of them was swept out of the tree by hand — four separate commits, each
# catching what the one before it missed, and three references surviving all four. A
# name is as checkable as a filename; it just needs saying once.
#
# Each entry is (pattern, what to say instead). Add a row when you delete a name.

RETIRED_NAMES = [
    (r"\bcode\.sigla\b", "the worked example is `demo.sigla`, the indexer writes `dotnet.sigla`"),
    (r"\bfjord-viewer\b", "retired; a browser application reads the database over the wire"),
    (r"--syntax-only\b", "deleted: a run that cannot resolve refuses"),
    (r"--skip-files\b", "deleted with `--syntax-only`"),
    (r"\bflag-day\.sh\b", "retired: its nine steps are the guards named in clients/dotnet/README.md"),
    (r"\bindex-repo-glean\.sh\b", "retired with the Glean comparison"),
    (r"\bsrc\.TypeOf\b", "gone with `code.sigla`; the display strings are on `codemarkup.Definition` and two `csharp` predicates"),
    (r"\bDeclared\.First\b", "deleted: conflicts are reported, not resolved"),
    (r"\bCodeIndex\b", "`DotnetIndex`"),
]

# **These are book-only, and the reason is worth stating so nobody "fixes" it by widening
# the list above.** `src.Decl`, `src.Ref` and `src.Module` are not only retired predicates,
# they are also the illustrative names half the crates reach for when a doc comment or a
# test needs a plausible schema — and a good few of those tests declare their own
# `schema src { predicate Decl … }` a line earlier, so the name is *correct* where it
# stands. Gating them tree-wide produced 150 findings and one real one. The book is
# different: it describes the tree as it is, so a predicate no schema declares is drift
# there and nowhere else.
RETIRED_IN_THE_BOOK = [
    (r"\bsrc\.Line\b", "`src.FileLine` is the line table"),
    (r"\bsrc\.Decl\b", "the declaration layer is per-kind in `csharp.*`; a UI reads `codemarkup.Definition`"),
    (r"\bsrc\.Ref\b", "`codemarkup.FileXRef` / `SymbolXRef`"),
    (r"\bsrc\.Module\b", "no module layer; a declaration names its file"),
    (r"\bsrc\.SearchByName\b", "`codemarkup.SearchEntry` / `SymbolByName`"),
    (r"\bsrc\.ExternalRef\b", "`codemarkup.SymbolXRef`"),
]

# **Where a retired name is a fact rather than a mistake.** A changelog records what a
# release did to a thing that no longer exists, a plan records the work that deleted it,
# and a closed measurement register records what was measured over it. Those are history
# and history is allowed to name the dead; everything else is describing the tree as it
# is, and must not.
HISTORY = {
    "CHANGELOG.md",
    "bench/FINDINGS.md",
    "docs/indexer-overhaul-plan.md",
    "docs/recursion-plan-adversarial-review.md",
}

def is_history(rel: str) -> bool:
    return rel in HISTORY or rel.startswith(("docs/unified-plan/", "scratchpad/"))


# **Naming a dead thing to say it is dead.** A page that announces a retirement, a test
# that asserts a deleted flag is *refused*, and a comment that says "there was a script
# for this and it is gone" all have to spell the name. Each entry is the file and the
# patterns it may name, with why — so adding one is a sentence a reader can disagree
# with, rather than a silent hole. Keyed on the file, not the line, because line numbers
# move and a wrong allowlist entry should fail loudly rather than drift onto a neighbour.
SAYS_IT_IS_GONE = {
    "website/content/clients.md": {
        r"\bfjord-viewer\b": "the paragraph announcing its retirement and what replaces it",
    },
    "docs/gitnexus.md": {
        r"\bfjord-viewer\b": "the sentence separating what the retirement removed (a rendering) from what it did not (the queries), which is the whole reason the `context` verdict stays a tick",
    },
    "website/content/status.md": {
        r"\bfjord-viewer\b": "the 'not built' row that says so — the page index.md sends a reader to for the honest list, where a silent absence would read as an oversight",
    },
    "clients/dotnet/README.md": {
        r"\bflag-day\.sh\b": "'there was a script for this, and it is gone', then the guards that replaced it",
    },
    "AGENTS.md": {
        r"\bflag-day\.sh\b": "names what the checklist used to be, before naming what it is now",
    },
    "clients/dotnet/Boxops.Fjord.Tests/OptionsTests.cs": {
        r"--syntax-only\b": "asserts the deleted flag is refused rather than ignored",
        r"--skip-files\b": "the same, for the flag deleted with it",
    },
    "clients/dotnet/Boxops.Fjord.Tests/LedgerTests.cs": {
        r"\bDeclared\.First\b": "states which earlier gate moved `deduped`, to explain the number this one asserts",
    },
}


# `docs` and `bench` are swept as areas rather than by naming their live files, because the
# expensive failure is the *next* page added under one of them: named individually, it gets no
# gate and nothing says so. HISTORY below is what excuses the ones that are history.
LIVE_AREAS = ("crates", "clients", "website", "schemas", "scripts", ".github", "docs", "bench")
LIVE_FILES = ("README.md", "AGENTS.md", "PLAN.md", "CLAUDE.md")

live: list[Path] = []
for area in LIVE_AREAS:
    base = ROOT / area
    if base.exists():
        live += [
            p
            for p in base.rglob("*")
            # `obj` and `bin` are MSBuild's output. Sweeping generated files is
            # false-positive surface and nothing in them is documentation.
            if not {"site", "obj", "bin"} & set(p.parts)
            if p.suffix
            in {".rs", ".md", ".cs", ".llw", ".toml", ".yml", ".py", ".sh", ".sigla", ".csproj", ".props", ".slnx", ".json", ".mjs", ".ts"}
        ]
live += [ROOT / name for name in LIVE_FILES if (ROOT / name).exists()]

def sweep(patterns: list[tuple[str, str]], paths: list[Path]) -> None:
    for pattern, instead in patterns:
        retired = re.compile(pattern)
        for path in paths:
            rel = str(path.relative_to(ROOT))
            if is_history(rel) or rel == "scripts/check-docs.py":
                continue
            if pattern in SAYS_IT_IS_GONE.get(rel, {}):
                continue
            text = path.read_text(encoding="utf-8", errors="ignore")
            for line_no, line in enumerate(text.splitlines(), 1):
                if retired.search(line):
                    fail(f"{rel}:{line_no} names retired `{pattern}` — {instead}: {line.strip()[:70]}")


sweep(RETIRED_NAMES, live)
sweep(RETIRED_IN_THE_BOOK, [p for p in CONTENT.glob("*.md")])

# ---- 4. no build-plan phase numbers in code or the book ---------------------------

PHASE = re.compile(r"Phase [0-9]")
for area in ("crates", "website"):
    for path in (ROOT / area).rglob("*"):
        if path.suffix not in {".rs", ".md", ".llw", ".toml"}:
            continue
        text = path.read_text(encoding="utf-8", errors="ignore")
        for line_no, line in enumerate(text.splitlines(), 1):
            if PHASE.search(line):
                fail(f"{path.relative_to(ROOT)}:{line_no} carries a phase number: {line.strip()[:90]}")

if findings:
    print(f"{len(findings)} finding(s):", file=sys.stderr)
    for finding in findings:
        print(f"  {finding}", file=sys.stderr)
    sys.exit(1)

print("docs are consistent: links resolve, citations resolve, nothing retired is referenced")
