"""Mutation controls for the version gate.

`check-versions.py` exists because two documents disagreed and nothing noticed: the
flag-day checklist said to bump the package version in the commit that re-pastes the
fingerprint constant, the release process bumps versions in a release commit, and following
the second meant three fingerprints moved while the package went on claiming the version
already live on nuget.org. A gate for that has one quiet failure mode — it can stop firing
and say nothing — so each control plants one violation against a throwaway tree and asserts
it is caught.

**The controls build their own client tree**, because the gate must survive a checkout that
is missing areas: `ROOT` comes from the script's own path, and a control that could only run
against this repository is a control nobody writes.
"""

import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

SCRIPT = Path(__file__).with_name("check-versions.py")

PROPS = """<Project>
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <Version>0.6.2</Version>
  </PropertyGroup>
</Project>
"""

# A packable project that states a fingerprint — the shape the gate is about.
INDEXER = """<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>true</IsPackable>
    <PackageId>Boxops.Fjord.Indexer</PackageId>
    <Version>{version}</Version>
  </PropertyGroup>
</Project>
"""

SOURCE = """internal static class DotnetIndex
{{
    public const ulong SchemaFingerprint = {fingerprint};
}}
"""

# A packable project that states none, which must stay out of scope entirely.
CLIENT = """<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>true</IsPackable>
    <PackageId>Boxops.Fjord.Client</PackageId>
  </PropertyGroup>
</Project>
"""

RECORD = """# package\tversion\tfingerprint
Boxops.Fjord.Indexer\t0.6.2\t0x4471c3f35a45b7da
"""


class Tree:
    """A throwaway client tree with the gate copied into it."""

    def __init__(self) -> None:
        self.root = Path(tempfile.mkdtemp())
        self.dotnet = self.root / "clients" / "dotnet"
        (self.root / "scripts").mkdir(parents=True)
        self.dotnet.mkdir(parents=True)
        shutil.copy(SCRIPT, self.root / "scripts" / SCRIPT.name)

        self.write("Directory.Build.props", PROPS)
        self.write("PUBLISHED.tsv", RECORD)
        self.indexer(version="0.7.0", fingerprint="0x29c029f2f24572e1")
        self.write("Boxops.Fjord.Client/Boxops.Fjord.Client.csproj", CLIENT)

    def write(self, path: str, text: str) -> None:
        target = self.dotnet / path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(text)

    def getting_started(self, tool: str, version: str) -> None:
        book = self.root / "web" / "src" / "content"
        book.mkdir(parents=True, exist_ok=True)
        (book / "getting-started.mdx").write_text(
            f"Tool '{tool}' (version '{version}') was successfully installed.\n"
        )

    def indexer(self, version: str, fingerprint: str) -> None:
        self.write(
            "Boxops.Fjord.Indexer/Boxops.Fjord.Indexer.csproj",
            INDEXER.format(version=version),
        )
        self.write(
            "Boxops.Fjord.Indexer/DotnetIndex.cs",
            SOURCE.format(fingerprint=fingerprint),
        )

    def run(self) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            [sys.executable, str(self.root / "scripts" / SCRIPT.name)],
            capture_output=True,
            text=True,
            check=False,
        )

    def destroy(self) -> None:
        shutil.rmtree(self.root, ignore_errors=True)


class VersionGate(unittest.TestCase):
    def setUp(self) -> None:
        self.tree = Tree()
        self.addCleanup(self.tree.destroy)

    def assert_caught(self, fragment: str) -> None:
        result = self.tree.run()
        self.assertEqual(result.returncode, 1, f"the gate passed:\n{result.stdout}")
        self.assertIn(fragment, result.stderr)

    def test_a_clean_tree_is_clean(self) -> None:
        result = self.tree.run()
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("versions are publishable", result.stdout)

    def test_a_moved_fingerprint_at_the_published_version_is_caught(self) -> None:
        """The defect itself: the state `main` was actually in."""
        self.tree.indexer(version="0.6.2", fingerprint="0x29c029f2f24572e1")
        self.assert_caught("but the version is still 0.6.2")

    def test_a_patch_bump_is_not_enough_for_a_moved_fingerprint(self) -> None:
        """A refused handshake is breaking, so the minor has to move, not the patch."""
        self.tree.indexer(version="0.6.3", fingerprint="0x29c029f2f24572e1")
        self.assert_caught("bump the minor")

    def test_a_minor_bump_is_enough(self) -> None:
        self.tree.indexer(version="0.7.0", fingerprint="0x29c029f2f24572e1")
        self.assertEqual(self.tree.run().returncode, 0)

    def test_a_major_bump_is_enough(self) -> None:
        """The minor is `0.x`'s breaking slot; a major is a superset, not a violation."""
        self.tree.indexer(version="1.0.0", fingerprint="0x29c029f2f24572e1")
        self.assertEqual(self.tree.run().returncode, 0)

    def test_an_unmoved_fingerprint_needs_no_bump(self) -> None:
        """The gate is about fingerprints, not about versions: it must not demand a bump."""
        self.tree.indexer(version="0.6.2", fingerprint="0x4471c3f35a45b7da")
        self.assertEqual(self.tree.run().returncode, 0, self.tree.run().stderr)

    def test_a_version_behind_the_published_one_is_caught(self) -> None:
        self.tree.indexer(version="0.5.0", fingerprint="0x29c029f2f24572e1")
        self.assert_caught("a version cannot go backwards")

    def test_a_fingerprint_differing_only_in_hex_case_is_not_a_move(self) -> None:
        """Compared as numbers, so re-pasting a constant in another case is not a release."""
        self.tree.indexer(version="0.6.2", fingerprint="0X4471C3F35A45B7DA")
        self.assertEqual(self.tree.run().returncode, 0, self.tree.run().stderr)

    # ---- the census -----------------------------------------------------------------

    def test_a_coupled_package_with_no_row_is_caught(self) -> None:
        """The way this gate would rot: a new package joins the class uncovered."""
        self.tree.write(
            "Boxops.Fjord.Other/Boxops.Fjord.Other.csproj",
            INDEXER.replace("Boxops.Fjord.Indexer", "Boxops.Fjord.Other").format(
                version="0.1.0"
            ),
        )
        self.tree.write(
            "Boxops.Fjord.Other/Other.cs", SOURCE.format(fingerprint="0xdeadbeefdeadbeef")
        )
        self.assert_caught("PUBLISHED.tsv has no row for it")

    def test_a_row_for_a_package_that_lost_its_constant_is_caught(self) -> None:
        self.tree.write("Boxops.Fjord.Indexer/DotnetIndex.cs", "internal static class X { }\n")
        self.assert_caught("no longer states a fingerprint constant")

    def test_a_row_for_a_package_that_is_gone_is_caught(self) -> None:
        shutil.rmtree(self.tree.dotnet / "Boxops.Fjord.Indexer")
        self.assert_caught("no longer packable or no longer exists")

    def test_a_package_stating_no_fingerprint_is_left_alone(self) -> None:
        """`Boxops.Fjord.Client` is packable, carries no constant, and needs no row."""
        result = self.tree.run()
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertNotIn("Boxops.Fjord.Client", result.stdout + result.stderr)

    def test_a_project_that_is_not_packable_is_out_of_scope(self) -> None:
        """`Demo` and `Scip` state constants and ship nothing, so they are not in the class."""
        self.tree.write(
            "Boxops.Fjord.Demo/Boxops.Fjord.Demo.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup></PropertyGroup></Project>\n",
        )
        self.tree.write(
            "Boxops.Fjord.Demo/Program.cs", SOURCE.format(fingerprint="0x03678fcd1e7924e3")
        )
        self.assertEqual(self.tree.run().returncode, 0)

    # ---- the number the book shows a reader -----------------------------------------

    def test_a_stale_version_in_the_books_install_output_is_caught(self) -> None:
        """Sample output a reader checks their terminal against, so it must be publishable."""
        self.tree.getting_started("boxops.fjord.indexer", "0.5.0")
        self.assert_caught("the book's sample output")

    def test_the_books_install_output_matching_the_published_version_is_clean(self) -> None:
        self.tree.getting_started("boxops.fjord.indexer", "0.6.2")
        self.assertEqual(self.tree.run().returncode, 0, self.tree.run().stderr)

    def test_the_book_installing_an_unrecorded_tool_is_caught(self) -> None:
        self.tree.getting_started("boxops.fjord.unknown", "0.6.2")
        self.assert_caught("has no row for it, so nothing checks the version it prints")

    def test_a_tree_with_no_book_is_not_a_finding(self) -> None:
        """The gate must survive a checkout missing whole areas."""
        self.assertEqual(self.tree.run().returncode, 0)

    # ---- the record's own shape -----------------------------------------------------

    def test_a_malformed_row_is_caught(self) -> None:
        self.tree.write("PUBLISHED.tsv", "Boxops.Fjord.Indexer 0.6.2 0x4471c3f35a45b7da\n")
        self.assert_caught("tab-separated field")

    def test_a_missing_record_is_caught(self) -> None:
        (self.tree.dotnet / "PUBLISHED.tsv").unlink()
        self.assert_caught("is missing, so nothing records what is live")

    def test_two_different_fingerprints_in_one_package_are_caught(self) -> None:
        """A package claims one schema; two constants mean the gate cannot tell which."""
        self.tree.write(
            "Boxops.Fjord.Indexer/Other.cs", SOURCE.format(fingerprint="0xdeadbeefdeadbeef")
        )
        self.assert_caught("different fingerprints")

    def test_a_version_the_project_inherits_is_the_one_checked(self) -> None:
        """Dropping the override must fall back to the default, not to nothing."""
        self.tree.write(
            "Boxops.Fjord.Indexer/Boxops.Fjord.Indexer.csproj",
            INDEXER.format(version="0.7.0").replace("<Version>0.7.0</Version>", ""),
        )
        self.assert_caught("but the version is still 0.6.2")

    def test_generated_output_is_not_read_for_constants(self) -> None:
        """`obj/` holds generated copies of source; reading them would double-count."""
        self.tree.write(
            "Boxops.Fjord.Indexer/obj/Debug/Generated.cs",
            SOURCE.format(fingerprint="0xdeadbeefdeadbeef"),
        )
        self.assertEqual(self.tree.run().returncode, 0, self.tree.run().stderr)


if __name__ == "__main__":
    unittest.main()
