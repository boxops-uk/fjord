"""Mutation controls for the drift gate.

`check-docs.py` is a required check with six checks in it, and every one of them exists
because the documentation went stale in that exact way once. A gate like that has two
failure modes and only one of them is loud: it can stop firing on drift it used to catch,
and nothing says so. These controls plant one violation per check and assert it is caught,
then assert a clean tree is clean — so a narrowed pattern, a lapsed allowlist entry or a
swept area quietly dropped from `LIVE_AREAS` fails here rather than in six months.

**The gate runs against a throwaway tree, not this repository.** `ROOT` is derived from the
script's own path, so each control copies the real script into a temporary tree and builds
only the slice of the repository that check needs. That also pins a second thing worth
pinning: the gate must survive a tree that is missing whole areas, because a control that
can only run against the real repository is a control nobody writes.
"""

import re
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

SCRIPT = Path(__file__).with_name("check-docs.py")
REAL_ROOT = SCRIPT.resolve().parent.parent

# The files the gate's allowlist exempts, read out of the gate rather than restated here:
# the gate refuses an exemption for a file that does not exist, so a fixture tree owes a
# stub for each one, and adding an allowlist entry must not break these controls.
EXEMPTED = re.findall(
    r'^    "([^"]+)": \{$', SCRIPT.read_text(encoding="utf-8"), flags=re.MULTILINE
)


class Tree:
    """A throwaway repository root with the real gate installed in it."""

    def __init__(self) -> None:
        self.root = Path(tempfile.mkdtemp(prefix="check-docs-"))
        (self.root / "scripts").mkdir()
        shutil.copy(SCRIPT, self.root / "scripts" / "check-docs.py")

    def write(self, rel: str, text: str) -> Path:
        path = self.root / rel
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding="utf-8")
        return path

    def copy(self, rel: str) -> Path:
        return self.write(rel, (REAL_ROOT / rel).read_text(encoding="utf-8"))

    def run(self) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            [sys.executable, str(self.root / "scripts" / "check-docs.py")],
            capture_output=True,
            text=True,
            check=False,
        )

    def close(self) -> None:
        shutil.rmtree(self.root, ignore_errors=True)


# The minimum a page needs to be a page: front matter and a heading.
PAGE = """---
title: A page
description: A page.
---

# A page

Body.
"""

INVARIANTS = """---
title: Invariants
description: The registry.
---

## I1 — the first one

It holds.
"""


class DriftGate(unittest.TestCase):
    def setUp(self) -> None:
        self.tree = Tree()
        self.addCleanup(self.tree.close)
        # Every control starts from a tree the gate passes, so a failure is the plant
        # and not the fixture.
        self.tree.write("website/content/index.md", PAGE)
        self.tree.write("website/content/invariants.md", INVARIANTS)
        for rel in EXEMPTED:
            self.tree.write(rel, "")

    def assert_clean(self) -> None:
        done = self.tree.run()
        self.assertEqual(done.returncode, 0, f"expected a clean tree:\n{done.stderr}")

    def assert_caught(self, needle: str) -> str:
        done = self.tree.run()
        self.assertEqual(done.returncode, 1, f"expected a finding, got a clean run:\n{done.stdout}")
        self.assertIn(needle, done.stderr)
        return done.stderr

    def test_a_clean_tree_is_clean(self) -> None:
        self.assert_clean()

    # ---- 1. links and anchors ----------------------------------------------------

    def test_a_link_to_a_page_that_does_not_exist_is_caught(self) -> None:
        self.tree.write("website/content/one.md", PAGE + "\nSee [the other](two.html).\n")
        self.assert_caught("two.html")

    def test_a_link_to_an_anchor_the_page_does_not_declare_is_caught(self) -> None:
        self.tree.write("website/content/one.md", PAGE + "\nSee [there](index.html#nowhere).\n")
        self.assert_caught("#nowhere")

    # ---- 2. invariant citations --------------------------------------------------

    def test_a_citation_of_an_invariant_the_registry_does_not_declare_is_caught(self) -> None:
        self.tree.write(
            "crates/fjord-thing/src/lib.rs",
            "//! Holds [I9](../../../website/content/invariants.md#i9).\n",
        )
        self.assert_caught("#i9")

    # ---- 3 and 3b. retired files, and retired names ------------------------------

    def test_a_retired_name_in_the_book_is_caught(self) -> None:
        self.tree.write("website/content/one.md", PAGE + "\nThe worked example is `code.sigla`.\n")
        self.assert_caught("code.sigla")

    def test_a_retired_name_in_a_crate_is_caught(self) -> None:
        self.tree.write("crates/fjord-thing/src/lib.rs", "//! Run `scripts/flag-day.sh` first.\n")
        self.assert_caught("flag-day.sh")

    def test_a_retired_name_in_a_hand_written_build_file_is_caught(self) -> None:
        """`.props` was outside the swept suffixes, and a retired script name lived in one
        through four hand sweeps and the first cut of this gate."""
        self.tree.write(
            "clients/dotnet/Directory.Build.props",
            "<Project>\n  <!-- see scripts/flag-day.sh -->\n</Project>\n",
        )
        self.assert_caught("flag-day.sh")

    def test_a_retired_crate_named_in_the_workspace_manifest_is_caught(self) -> None:
        """The root manifest is where a retired *crate* name survives, as a member."""
        self.tree.write("Cargo.toml", '[workspace]\nmembers = ["crates/fjord-viewer"]\n')
        self.assert_caught("fjord-viewer")

    def test_a_retired_name_in_the_published_bundle_is_caught(self) -> None:
        self.tree.write("web/README.md", "# web\n\nThe shell reads `code.sigla`.\n")
        self.assert_caught("code.sigla")

    def test_a_book_only_name_is_caught_in_the_book_and_left_alone_elsewhere(self) -> None:
        """`src.Decl` is a retired predicate *and* the illustrative name half the crates
        reach for, often beside a test's own `schema src`. Gating it tree-wide produced 150
        findings and one real one, so it is gated in the book and nowhere else."""
        self.tree.write("crates/fjord-thing/src/lib.rs", "//! e.g. `src.Decl {name = N}`.\n")
        self.assert_clean()
        self.tree.write("website/content/one.md", PAGE + "\nQuery `src.Decl {name = N}`.\n")
        self.assert_caught("src.Decl")

    def test_a_longer_name_that_merely_starts_with_a_retired_one_is_not_caught(self) -> None:
        """`\\b` sits between a word character and a non-word one, so it matches inside a
        longer hyphenated token: `--skip-files-larger-than` and the real release-asset name
        `fjord-viewer-x86_64-linux-musl` are not references to the deleted flag or crate,
        and a required gate that blocks a merge over correct prose gets switched off."""
        self.tree.write(
            "website/content/one.md",
            PAGE + "\nA tool taking `--skip-files-larger-than`, and `fjord-viewer-x86_64-linux-musl`,\n"
            "and a digest of `sample-code.sigla`.\n",
        )
        self.assert_clean()

    def test_generated_output_is_not_swept(self) -> None:
        """MSBuild's `obj`/`bin` and npm's `node_modules`/`dist` are build products. A
        finding in one is noise a reader cannot act on, and noise is what silences a gate."""
        self.tree.write("clients/dotnet/obj/Generated.cs", "// code.sigla\n")
        self.tree.write("web/node_modules/thing/readme.md", "# thing\n\n`code.sigla`\n")
        self.assert_clean()

    def test_history_may_name_the_dead(self) -> None:
        """A changelog records what a release did to a thing that no longer exists, and a
        plan records the work that deleted it. Both are swept and then excused, because an
        excuse for a file no sweep reaches reads as coverage and is not."""
        self.tree.write("CHANGELOG.md", "# Changelog\n\n`schemas/code.sigla` is deleted.\n")
        self.tree.write("docs/unified-plan/15-thing.md", "# W15\n\nRetire `code.sigla`.\n")
        self.assert_clean()

    def test_an_allowlist_entry_for_a_file_that_does_not_exist_is_caught(self) -> None:
        """The allowlist is keyed on a file and a name. A tree missing the file it exempts
        has an exemption doing nothing, which reads exactly like coverage."""
        self.assertTrue(EXEMPTED, "the gate's allowlist parsed as empty")
        gone = self.tree.root / EXEMPTED[0]
        gone.unlink()
        stderr = self.assert_caught("does not exist")
        self.assertIn(EXEMPTED[0], stderr)

    def test_the_allowlist_survives_a_pattern_being_rewritten(self) -> None:
        """It is keyed on the retired thing's *name*, not on the regex that finds it: an
        allowlist keyed on a pattern's source silently lapses the moment the pattern is
        edited, and the gate then fires on the prose it was told to allow. Proved by
        exempting a real file and watching the announcement pass."""
        self.tree.copy("clients/dotnet/README.md")
        for rel in (
            "website/content/clients.md",
            "website/content/status.md",
            "docs/gitnexus.md",
            "AGENTS.md",
            "clients/dotnet/Boxops.Fjord.Tests/OptionsTests.cs",
            "clients/dotnet/Boxops.Fjord.Tests/LedgerTests.cs",
        ):
            self.tree.copy(rel)
        done = self.tree.run()
        self.assertNotIn("flag-day.sh", done.stderr)
        self.assertNotIn("does not exist", done.stderr)

    # ---- 4. build-plan phase numbers ---------------------------------------------

    def test_a_build_plan_phase_number_is_caught(self) -> None:
        self.tree.write("crates/fjord-thing/src/lib.rs", "//! Landed in Phase 3.\n")
        self.assert_caught("phase number")

    # ---- 6. numbers the tree computes --------------------------------------------

    def test_a_protocol_number_the_tree_does_not_carry_is_caught(self) -> None:
        self.tree.write(
            "crates/fjord-wire/src/protocol.rs", "pub const VERSION: u32 = 4;\n"
        )
        self.tree.write("website/content/one.md", PAGE + "\n    connected: protocol 3\n")
        self.assert_caught("says protocol 3")

    def test_the_protocol_number_the_tree_carries_is_left_alone(self) -> None:
        self.tree.write(
            "crates/fjord-wire/src/protocol.rs", "pub const VERSION: u32 = 4;\n"
        )
        self.tree.write("website/content/one.md", PAGE + "\n    connected: protocol 4\n")
        self.assert_clean()

    def test_a_missing_protocol_source_is_a_finding_rather_than_a_traceback(self) -> None:
        self.tree.write("website/content/one.md", PAGE + "\n    protocol 4\n")
        stderr = self.assert_caught("nothing checks it")
        self.assertNotIn("Traceback", stderr)

    def test_a_headline_criteria_count_that_the_items_do_not_add_up_to_is_caught(self) -> None:
        self.tree.write(
            "docs/unified-plan/01-one.md",
            "# One\n\n## Acceptance criteria\n\n1. First.\n2. Second.\n",
        )
        self.tree.write(
            "docs/unified-plan/README.md",
            "# The plan\n\n**3 acceptance criteria across one items**\n",
        )
        self.assert_caught("does not carry the count the items add up to")

    def test_the_count_the_items_add_up_to_is_accepted(self) -> None:
        self.tree.write(
            "docs/unified-plan/01-one.md",
            "# One\n\n## Acceptance criteria\n\n1. First.\n2. Second.\n",
        )
        self.tree.write(
            "docs/unified-plan/README.md",
            "# The plan\n\n**2 acceptance criteria across 1 items**\n",
        )
        self.assert_clean()

    def test_a_fenced_block_inside_the_criteria_is_not_counted(self) -> None:
        """A numbered line inside a fence is a transcript, not a criterion."""
        self.tree.write(
            "docs/unified-plan/01-one.md",
            "# One\n\n## Acceptance criteria\n\n1. First.\n\n```\n2. not a criterion\n```\n",
        )
        self.tree.write(
            "docs/unified-plan/README.md",
            "# The plan\n\n**1 acceptance criteria across 1 items**\n",
        )
        self.assert_clean()

    def test_criteria_numbered_out_of_order_are_caught(self) -> None:
        """A list that skips or repeats a number means a criterion was added or deleted
        without renumbering, and the total is then a coincidence."""
        self.tree.write(
            "docs/unified-plan/01-one.md",
            "# One\n\n## Acceptance criteria\n\n1. First.\n3. Third.\n",
        )
        self.tree.write(
            "docs/unified-plan/README.md",
            "# The plan\n\n**2 acceptance criteria across 1 items**\n",
        )
        self.assert_caught("not 1..n")


if __name__ == "__main__":
    unittest.main()
