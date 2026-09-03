"""Mutation controls for the exhaustiveness probe's guards.

`check-exhaustive.sh` edits a tracked file and puts it back with an `EXIT` trap, so
every one of its exits runs `git checkout -- "$file"`. That makes the *order* of the
trap and the dirty-tree guard load-bearing: armed too early, the guard's own refusal
deletes the edit it just declined to touch, with no stash and no reflog to recover
from. These provoke each exit and assert what the tree looks like afterwards.

The script runs against a throwaway repository rather than this one, because a control
for "does it destroy uncommitted work" must not be able to destroy any.
"""

import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path

SCRIPT = Path(__file__).with_name("check-exhaustive.sh")

# The two files the script knows how to edit, and the anchor it inserts after.
TARGETS = {
    "engine": ("crates/fjord-engine/src/syntax.rs", "pub enum Ty {"),
    "schema": ("crates/fjord-schema/src/schema.rs", "pub enum PredicateTyNamed<N> {"),
}

EDIT = "// a developer, mid-edit\n"


def git(repo: Path, *args: str) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        [
            "git",
            "-c",
            "user.email=test@example.invalid",
            "-c",
            "user.name=test",
            "-c",
            "commit.gpgsign=false",
            *args,
        ],
        cwd=repo,
        capture_output=True,
        text=True,
        check=True,
    )


class Probe(unittest.TestCase):
    def setUp(self) -> None:
        self.repo = Path(tempfile.mkdtemp())
        self.addCleanup(shutil.rmtree, self.repo, ignore_errors=True)

        (self.repo / "scripts").mkdir()
        shutil.copy2(SCRIPT, self.repo / "scripts" / SCRIPT.name)

        for path, anchor in TARGETS.values():
            target = self.repo / path
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_text(f"{anchor}\n    Int,\n}}\n", encoding="utf-8")

        git(self.repo, "init", "-q")
        git(self.repo, "add", "-A")
        git(self.repo, "commit", "-qm", "the tree the probe edits")

    def run_probe(self, argument: str) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            ["bash", "scripts/check-exhaustive.sh", argument],
            cwd=self.repo,
            capture_output=True,
            text=True,
        )

    def contents(self, which: str) -> str:
        return (self.repo / TARGETS[which][0]).read_text(encoding="utf-8")

    def test_a_dirty_target_is_refused_and_left_exactly_as_it_was(self) -> None:
        """**The guard must not be the thing that loses the work.**

        The refusal tells the reader to commit or stash; firing the restore trap on the
        way out makes that impossible, and the loss is silent.
        """
        for which, (path, _) in TARGETS.items():
            with self.subTest(which):
                before = self.contents(which) + EDIT
                (self.repo / path).write_text(before, encoding="utf-8")

                result = self.run_probe(which)

                self.assertEqual(result.returncode, 2, result.stderr)
                self.assertIn("uncommitted changes", result.stderr)
                self.assertEqual(
                    self.contents(which),
                    before,
                    f"{path} was modified by the run that refused to touch it",
                )

    def test_an_unknown_argument_touches_nothing(self) -> None:
        for which, (path, _) in TARGETS.items():
            (self.repo / path).write_text(
                self.contents(which) + EDIT, encoding="utf-8"
            )

        expected = {which: self.contents(which) for which in TARGETS}

        result = self.run_probe("neither")

        self.assertEqual(result.returncode, 2, result.stderr)
        self.assertIn("usage:", result.stderr)
        for which in TARGETS:
            self.assertEqual(self.contents(which), expected[which])

    def test_the_throwaway_variant_is_put_back_when_the_run_ends(self) -> None:
        """The trap still earns its place: the `Probe` variant never survives a run.

        There is no cargo here, so the build step names no site and the script exits
        through its "the workspace built" failure — which is an exit like any other,
        and the one that proves the restore is not conditional on success.
        """
        for which in TARGETS:
            with self.subTest(which):
                clean = self.contents(which)

                result = self.run_probe(which)

                self.assertNotEqual(result.returncode, 0)
                self.assertEqual(
                    self.contents(which),
                    clean,
                    "the throwaway `Probe` variant outlived the run",
                )
                self.assertNotIn("Probe", self.contents(which))


if __name__ == "__main__":
    unittest.main()
