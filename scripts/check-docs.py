#!/usr/bin/env python3
"""The drift gate: the checks that would have caught the documentation going stale.

Seven checks, each of which failed silently once:
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
     SAYS_IT_IS_GONE where a live file spells a name in order to announce it is gone;
  6. a number the docs print is a number the tree computes — the protocol version and
     the plan's acceptance-criteria total. Both went stale inside one release, and a
     headline figure no gate computes is a claim that rots on the next edit.
  7. the two tables that enumerate the type model list the families the type model has.
     A scalar family reached the book by somebody remembering which pages tabulate it,
     and both tables were missed for a whole release.

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
    if not (ROOT / name).exists():
        continue
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

# **`\b` is the wrong boundary for a name containing `-` or `.`.** `\b` sits between a
# word character and a non-word one, so `--skip-files\b` matches inside
# `--skip-files-larger-than` and `\bfjord-viewer\b` inside
# `fjord-viewer-x86_64-linux-musl`, a real release-asset name — a required gate blocking a
# merge over prose that is correct. The hyphenated and dotted names below therefore reject a
# neighbouring `-`, `.` or word character on the side a longer token could extend them from.
# The bare identifiers keep `\b`, so `CodeIndex.cs` still reads as the deleted file it is.
RETIRED_NAMES = [
    ('code.sigla', r"(?<![\w.-])code\.sigla\b", "the worked example is `demo.sigla`, the indexer writes `dotnet.sigla`"),
    ('fjord-viewer', r"(?<![\w.-])fjord-viewer(?![-\w])", "retired; a browser application reads the database over the wire"),
    ('--syntax-only', r"--syntax-only(?![\w.-])", "deleted: a run that cannot resolve refuses"),
    ('--skip-files', r"--skip-files(?![\w.-])", "deleted with `--syntax-only`"),
    ('flag-day.sh', r"(?<![\w.-])flag-day\.sh\b", "retired: its nine steps are the guards named in clients/dotnet/README.md"),
    ('index-repo-glean.sh', r"(?<![\w.-])index-repo-glean\.sh\b", "retired with the Glean comparison"),
    ('src.TypeOf', r"\bsrc\.TypeOf\b", "gone with `code.sigla`; the display strings are on `codemarkup.Definition` and two `csharp` predicates"),
    ('Declared.First', r"\bDeclared\.First\b", "deleted: conflicts are reported, not resolved"),
    ('CodeIndex', r"\bCodeIndex\b", "`DotnetIndex`"),
    ('AssemblyReference', r"\bAssemblyReference\b", "deleted: `msbuild.Assembly` names only an assembly a project in the graph produces, and `Compilation` is the edge to it"),
    ('AssemblyDependent', r"\bAssemblyDependent\b", "deleted with `AssemblyReference`, whose reverse it was"),
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
    ('src.Line', r"\bsrc\.Line\b", "`src.FileLine` is the line table"),
    ('src.Decl', r"\bsrc\.Decl\b", "the declaration layer is per-kind in `csharp.*`; a UI reads `codemarkup.Definition`"),
    ('src.Ref', r"\bsrc\.Ref\b", "`codemarkup.FileXRef` / `SymbolXRef`"),
    ('src.Module', r"\bsrc\.Module\b", "no module layer; a declaration names its file"),
    ('src.SearchByName', r"\bsrc\.SearchByName\b", "`codemarkup.SearchEntry` / `SymbolByName`"),
    ('src.ExternalRef', r"\bsrc\.ExternalRef\b", "`codemarkup.SymbolXRef`"),
    ('src.Extends', r"\bsrc\.Extends\b", "`code.Extends { type, base }` in the sample schema"),
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
# Keyed on the retired thing's **name**, not on the regex that finds it: an allowlist keyed
# on a pattern's source silently stops applying the moment the pattern is edited, and an
# exemption that quietly lapses is a gate that fires on prose it was told to allow.
# `check_the_allowlist_is_live` below refuses a key no retired entry declares.
SAYS_IT_IS_GONE = {
    "website/content/clients.md": {
        "fjord-viewer": "the paragraph announcing its retirement and what replaces it",
    },
    "docs/gitnexus.md": {
        "fjord-viewer": "the sentence separating what the retirement removed (a rendering) from what it did not (the queries), which is the whole reason the `context` verdict stays a tick",
    },
    "website/content/status.md": {
        "fjord-viewer": "the 'not built' row that says so — the page index.md sends a reader to for the honest list, where a silent absence would read as an oversight",
    },
    "clients/dotnet/README.md": {
        "flag-day.sh": "'there was a script for this, and it is gone', then the guards that replaced it",
    },
    "AGENTS.md": {
        "flag-day.sh": "names what the checklist used to be, before naming what it is now",
    },
    "clients/dotnet/Boxops.Fjord.Tests/OptionsTests.cs": {
        "--syntax-only": "asserts the deleted flag is refused rather than ignored",
        "--skip-files": "the same, for the flag deleted with it",
    },
    "clients/dotnet/Boxops.Fjord.Tests/LedgerTests.cs": {
        "Declared.First": "states which earlier gate moved `deduped`, to explain the number this one asserts",
    },
}


# **Areas rather than named files, because the expensive failure is the *next* page.** A file
# named individually gets a gate; the one added beside it next month does not, and nothing says
# so. `web` and `wasm` are consumers of the tree that live outside the workspace — `web` is the
# bundle CI publishes — and the root `Cargo.toml` is precisely where a retired *crate* name
# survives, as `members`. `CHANGELOG.md` and `scratchpad` are swept and then excused by HISTORY
# below: an excuse for a file no sweep reaches reads as coverage and is not.
LIVE_AREAS = ("crates", "clients", "website", "schemas", "scripts", ".github", "docs", "bench", "web", "wasm", "scratchpad")
LIVE_FILES = ("README.md", "AGENTS.md", "PLAN.md", "CLAUDE.md", "Cargo.toml", "CHANGELOG.md")

live: list[Path] = []
for area in LIVE_AREAS:
    base = ROOT / area
    if base.exists():
        live += [
            p
            for p in base.rglob("*")
            # Generated output: MSBuild's `obj`/`bin`, the site generator's `site`, and
            # npm's `node_modules`/`dist`. Sweeping generated files is false-positive
            # surface and nothing in them is documentation.
            if not {"site", "obj", "bin", "node_modules", "dist"} & set(p.parts)
            if p.suffix
            in {".rs", ".md", ".cs", ".llw", ".toml", ".yml", ".py", ".sh", ".sigla", ".csproj", ".props", ".slnx", ".json", ".mjs", ".ts"}
        ]
live += [ROOT / name for name in LIVE_FILES if (ROOT / name).exists()]

def sweep(entries: list[tuple[str, str, str]], paths: list[Path]) -> None:
    for key, pattern, instead in entries:
        retired = re.compile(pattern)
        for path in paths:
            rel = str(path.relative_to(ROOT))
            # This gate names every retired thing in order to hunt it, and its controls
            # name them in order to plant them. Neither is describing the tree.
            if is_history(rel) or rel in {"scripts/check-docs.py", "scripts/test_check_docs.py"}:
                continue
            if key in SAYS_IT_IS_GONE.get(rel, {}):
                continue
            text = path.read_text(encoding="utf-8", errors="ignore")
            for line_no, line in enumerate(text.splitlines(), 1):
                if retired.search(line):
                    fail(f"{rel}:{line_no} names retired `{key}` — {instead}: {line.strip()[:70]}")


# An exemption for a file that no longer exists, or for a name no entry declares, is an
# exemption that does nothing — and it reads like coverage. Refuse both.
KNOWN = {key for key, _, _ in RETIRED_NAMES} | {key for key, _, _ in RETIRED_IN_THE_BOOK}
for rel, exemptions in SAYS_IT_IS_GONE.items():
    if not (ROOT / rel).exists():
        fail(f"the allowlist exempts {rel}, which does not exist")
    for key in exemptions:
        if key not in KNOWN:
            fail(f"the allowlist exempts `{key}` in {rel}, which no retired entry declares")

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

# ---- 6. a number the docs print is a number the tree computes --------------------
#
# **A headline figure that no gate computes goes stale on the next edit.** The plan index's
# acceptance-criteria count did it twice in two commits — the second correction was to a
# figure the sibling commit landing beside it had already invalidated. The protocol number
# did the same: a version bump left three statements of the old one in the book, one of them
# on the page whose own description is "everything a second implementation needs". Neither is
# a list to maintain here; both are read out of the tree, so the next bump moves them.

WORDS = {
    12: "twelve", 13: "thirteen", 14: "fourteen", 15: "fifteen", 16: "sixteen",
    17: "seventeen", 18: "eighteen", 19: "nineteen", 20: "twenty",
}

spoken = re.compile(r"\bprotocol\s+(\d+)\b")
said_where: list[tuple[str, int, str, str]] = []
for path in sorted(CONTENT.glob("*.md")):
    for line_no, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
        for said in spoken.findall(line):
            said_where.append((str(path.relative_to(ROOT)), line_no, said, line.strip()[:60]))

# Only worth reading the source if a page states a number: no claim, nothing to check.
if said_where:
    PROTOCOL = ROOT / "crates/fjord-wire/src/protocol.rs"
    version = (
        re.search(r"pub const VERSION: u32 = (\d+);", PROTOCOL.read_text(encoding="utf-8"))
        if PROTOCOL.exists()
        else None
    )
    if not version:
        fail(
            "the book states a protocol number and `protocol::VERSION` cannot be read out "
            "of crates/fjord-wire/src/protocol.rs, so nothing checks it"
        )
    else:
        for rel, line_no, said, text in said_where:
            if said != version.group(1):
                fail(
                    f"{rel}:{line_no} says protocol {said}, "
                    f"and `protocol::VERSION` is {version.group(1)}: {text}"
                )

# One list per item, numbered from 1, under `## Acceptance criteria`. A fenced block inside
# the section is prose, not a criterion.
FENCE = re.compile(r"^```")
criteria: dict[str, int] = {}
for path in sorted((ROOT / "docs/unified-plan").glob("[0-9]*.md")):
    lines = path.read_text(encoding="utf-8").splitlines()
    counted, fenced, inside = [], False, False
    for line in lines:
        if line.startswith("## "):
            inside = line.strip() == "## Acceptance criteria"
            continue
        if not inside:
            continue
        if FENCE.match(line):
            fenced = not fenced
            continue
        if fenced:
            continue
        numbered = re.match(r"(\d+)\. ", line)
        if numbered:
            counted.append(int(numbered.group(1)))
    if counted:
        if counted != list(range(1, len(counted) + 1)):
            fail(f"{path.relative_to(ROOT)}'s acceptance criteria are numbered {counted}, not 1..n")
        criteria[path.name] = len(counted)

total, items = sum(criteria.values()), len(criteria)
headline = f"**{total} acceptance criteria across {WORDS.get(items, items)} items**"
INDEX = ROOT / "docs/unified-plan/README.md"
index = INDEX.read_text(encoding="utf-8") if INDEX.exists() else ""
if criteria and headline not in index:
    fail(
        f"docs/unified-plan/README.md does not carry the count the items add up to — "
        f"expected {headline!r} ({', '.join(f'{k}:{v}' for k, v in sorted(criteria.items()))})"
    )

# ---- 7. the book's type tables carry every family the type model declares ---------
#
# **A new family reaches the book by somebody remembering which pages tabulate the type
# model.** `bytes` landed with a plan item naming the two pages its author had in mind, and
# the two *tables* that enumerate the model were not among them: both listed five types for
# a model that had six, for a whole release. The concepts page's lead-in said "four" of the
# five it did list, and had since the page was written. Neither table is a list to maintain
# here — `PredicateTyNamed` is the population and `print::ty` is how each builtin is
# written, so the next family fails this until both tables carry it.

CONCEPTS_TABLE = "| Type | Written | What it is |"
WRITTEN_TABLE = "| Written | Means |"
NUMBER_WORDS = ("no", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine")


def tabulated(text: str, header: str) -> list[str] | None:
    """The first cell of every row of the table `header` opens, or `None` if there is no
    such table — which is the gate being read out from under itself, not a clean page."""
    lines = text.splitlines()
    for start, line in enumerate(lines):
        if line.strip() != header:
            continue
        cells = []
        for row in lines[start + 2 :]:  # +2 steps over the `|---|` separator
            if not row.startswith("|"):
                break
            cells.append(row.split("|")[1].strip().strip("`").strip())
        return cells
    return None


TYPE_MODEL = ROOT / "crates/fjord-schema/src/schema.rs"
PRINTER = ROOT / "crates/fjord-schema/src/syntax/print.rs"

model = TYPE_MODEL.read_text(encoding="utf-8") if TYPE_MODEL.exists() else ""
declared = re.search(r"pub enum PredicateTyNamed<N> \{\n(.*?)\n\}", model, re.S)
families = re.findall(r"^    ([A-Z][A-Za-z0-9_]*)", declared.group(1), re.M) if declared else []

printer = PRINTER.read_text(encoding="utf-8") if PRINTER.exists() else ""
# The printer is the direction the "Written" column means: a family the source spells.
spelled = dict(re.findall(r'PredicateTy::([A-Za-z]+) => out\.push_str\("([a-z]+)"\)', printer))

# A builtin is a family with no payload, and the printer owes every one of them a keyword.
# Read that way round because an arm this pattern stops matching leaves the written table
# checked against a short list, which is a gate going quiet rather than a page going stale.
builtins = re.findall(r"^    ([A-Z][A-Za-z0-9_]*),$", declared.group(1), re.M) if declared else []
if declared and printer and set(builtins) != set(spelled):
    fail(
        f"`print::ty` spells {sorted(spelled)} and `PredicateTyNamed`'s payload-free families "
        f"are {sorted(builtins)}: the two must agree, or the types table is checked against "
        f"a list shorter than the type model"
    )

if "concepts" in pages:
    listed = tabulated(pages["concepts"], CONCEPTS_TABLE)
    if not families:
        fail(
            "the concepts page tabulates the type model and `PredicateTyNamed` cannot be read "
            "out of crates/fjord-schema/src/schema.rs, so nothing checks it"
        )
    elif listed is None:
        fail(
            f"website/content/concepts.md no longer opens its type table with {CONCEPTS_TABLE!r}, "
            f"so the check that keeps it in step with `PredicateTyNamed` reads an empty table"
        )
    else:
        # `Fact(p)` names the `Fact` family; the parameter is the page's, not the model's.
        named = {cell.split("(")[0] for cell in listed}
        for family in families:
            if family not in named:
                fail(
                    f"website/content/concepts.md's type table does not list `{family}`, "
                    f"which `PredicateTyNamed` declares"
                )
        word = NUMBER_WORDS[len(families)] if len(families) < len(NUMBER_WORDS) else ""
        headline = f"{word.capitalize()} building blocks, and that is all of them:"
        if word and headline not in pages["concepts"]:
            fail(
                f"website/content/concepts.md does not carry the count its type table has rows "
                f"for — expected {headline!r} for the {len(families)} `PredicateTyNamed` declares"
            )

if "schema-language" in pages:
    listed = tabulated(pages["schema-language"], WRITTEN_TABLE)
    if not spelled:
        fail(
            "the schema-language page tabulates how a type is written and `print::ty`'s builtin "
            "arms cannot be read out of crates/fjord-schema/src/syntax/print.rs, so nothing "
            "checks it"
        )
    elif listed is None:
        fail(
            f"website/content/schema-language.md no longer opens its types table with "
            f"{WRITTEN_TABLE!r}, so the check that keeps it in step with `print::ty` reads an "
            f"empty table"
        )
    else:
        for family, written in sorted(spelled.items()):
            if written not in listed:
                fail(
                    f"website/content/schema-language.md's types table does not list "
                    f"`{written}`, which `print::ty` writes `PredicateTy::{family}` as"
                )

if findings:
    print(f"{len(findings)} finding(s):", file=sys.stderr)
    for finding in findings:
        print(f"  {finding}", file=sys.stderr)
    sys.exit(1)

print("docs are consistent: links resolve, citations resolve, nothing retired is referenced")
