#!/usr/bin/env python3
"""Mutation controls for the link gate.

A gate nobody has seen fail is a gate nobody knows works. Each of these plants one
dead link in a tree the gate passes, and requires it to be found — and the last one
plants the defect the gate this replaced actually had, so it cannot come back.

The tree is a real one: the gate's own files, the site's remark plugins, a symlink
to `web/node_modules`, and two pages. It needs a built `fjord` and runs a server per
control, which is slow and is the point — what is being tested is that the database
answers, not that a regular expression does.
"""

import os
import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent

INDEX = """export const meta = { "title": "Overview", "description": "The book." }

Body.

## A section

Prose under it.
"""

INVARIANTS = """export const meta = { "title": "Invariants", "description": "The registry." }

## I1 — the first one

<a id="i1"></a>

It holds.
"""


def fjord() -> str | None:
    found = os.environ.get("FJORD")
    if found and Path(found).exists():
        return found
    for candidate in ("target/release/fjord", "target/debug/fjord"):
        if (ROOT / candidate).exists():
            return str(ROOT / candidate)
    return None


@unittest.skipUnless(fjord(), "needs a built fjord — `cargo build --release --bin fjord`")
@unittest.skipUnless((ROOT / "web/node_modules/@mdx-js/mdx").exists(), "needs web/ installed")
class LinkGate(unittest.TestCase):
    def setUp(self) -> None:
        self.root = Path(tempfile.mkdtemp(dir="/tmp", prefix="fjlt"))
        self.addCleanup(shutil.rmtree, self.root, ignore_errors=True)

        (self.root / "scripts").mkdir()
        for name in ("check-links.py", "docs.sigla"):
            shutil.copy(ROOT / "scripts" / name, self.root / "scripts" / name)

        (self.root / "web" / "mdx").mkdir(parents=True)
        shutil.copy(ROOT / "web/mdx/headings.mjs", self.root / "web/mdx/headings.mjs")
        shutil.copy(ROOT / "web/docs-facts.mjs", self.root / "web/docs-facts.mjs")
        (self.root / "web/node_modules").symlink_to(ROOT / "web/node_modules")

        self.content = self.root / "web/src/content"
        self.content.mkdir(parents=True)
        self.write("index.mdx", INDEX)
        self.write("invariants.mdx", INVARIANTS)

    def write(self, name: str, text: str) -> None:
        (self.content / name).write_text(text, encoding="utf-8")

    def cite(self, rel: str, text: str) -> None:
        path = self.root / rel
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding="utf-8")

    def run_gate(self) -> subprocess.CompletedProcess:
        return subprocess.run(
            ["python3", str(self.root / "scripts/check-links.py")],
            capture_output=True,
            text=True,
            env={**os.environ, "FJORD": fjord()},
        )

    def assert_clean(self) -> None:
        done = self.run_gate()
        self.assertEqual(done.returncode, 0, f"expected a clean tree:\n{done.stderr}")

    def assert_caught(self, needle: str) -> None:
        done = self.run_gate()
        self.assertEqual(done.returncode, 1, f"expected a finding:\n{done.stdout}")
        self.assertIn(needle, done.stderr)

    def test_a_clean_tree_is_clean(self) -> None:
        self.assert_clean()

    def test_a_link_to_a_page_that_does_not_exist_is_caught(self) -> None:
        self.write("index.mdx", INDEX + "\nSee [the other](two.html).\n")
        self.assert_caught("two.html")

    def test_a_link_to_an_anchor_the_page_does_not_declare_is_caught(self) -> None:
        self.write("index.mdx", INDEX + "\nSee [there](invariants.html#nowhere).\n")
        self.assert_caught("#nowhere")

    def test_an_anchor_a_page_declares_by_hand_resolves(self) -> None:
        self.write("index.mdx", INDEX + "\nSee [I1](invariants.html#i1).\n")
        self.assert_clean()

    def test_a_citation_from_a_crate_of_an_anchor_that_is_not_there_is_caught(self) -> None:
        self.cite(
            "crates/fjord-thing/src/lib.rs",
            "//! Holds [I9](../../../web/src/content/invariants.mdx#i9).\n",
        )
        self.assert_caught("#i9")

    def test_a_citation_from_a_crate_of_a_page_that_is_not_there_is_caught(self) -> None:
        self.cite(
            "crates/fjord-thing/src/lib.rs",
            "//! See [storage](../../../web/src/content/storage.mdx).\n",
        )
        self.assert_caught("storage.html")

    def test_a_citation_of_a_page_that_is_not_the_registry_is_checked_too(self) -> None:
        """The gate this replaced only ever checked `invariants.md#`, so a dead anchor on
        any other page was invisible — and twelve of them were live in the crates when
        this one first ran."""
        self.write("storage.mdx", INDEX.replace("Overview", "Storage"))
        self.cite(
            "crates/fjord-thing/src/lib.rs",
            "//! See [chapter 3](../../../web/src/content/storage.mdx#no-such-section).\n",
        )
        self.assert_caught("#no-such-section")

    def test_a_heading_inside_a_code_fence_is_not_an_anchor(self) -> None:
        """**The defect the old gate had.** Its anchor regex matched `^#{1,6} ` without
        tracking fences, so a `#` comment in a code block became an anchor — it invented
        eight across four pages, and passed every link that named one."""
        self.write(
            "index.mdx",
            INDEX + "\n```text\n# Comments start with a hash.\n```\n"
            "\nSee [there](index.html#comments-start-with-a-hash).\n",
        )
        self.assert_caught("#comments-start-with-a-hash")


if __name__ == "__main__":
    unittest.main()
