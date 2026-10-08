#!/usr/bin/env python3
"""The version gate: a published package never claims a fingerprint it was not built with.

A client sends **one** whole-schema fingerprint and the server checks it for equality, so
a schema that moves refuses every installed client carrying the old number. That refusal is
the designed failure — `a_schema_mismatch_is_refused_at_the_handshake` asserts it names both
numbers — but it is only honest if the new fingerprint ships under a *new version*. Ship it
under the version already on the registry and there are two different artifacts with one
number, which no consumer can tell apart and `scripts/publish.sh` will skip as already live.

That is the state this repository reached once, by following its own release process: three
fingerprints moved across two pull requests while `clients/dotnet/Directory.Build.props`
went on saying `0.6.2`, which is what nuget.org already had. Nothing was wrong with either
document — the flag-day checklist said to bump in the commit that re-pastes the constant,
the release process bumps versions in a release commit — and the pair had no gate.

**Two checks, and the second is the census that keeps the first honest:**

  1. For every packable project that states a fingerprint, if the fingerprint differs from
     the one its last published version carried, then the **minor** must differ too. A
     patch bump is not enough: a refused handshake is a breaking change, and under `0.x`
     the minor is where breaking lives (`CHANGELOG.md` says so outright).
  2. The set of packable projects that state a fingerprint is exactly the set `PUBLISHED.tsv`
     records. A project that grows a constant, or a package that becomes packable, joins the
     class — and a gate that silently did not cover it would pass while the defect returned.
  3. The version the book prints in `dotnet tool install`'s output is the version that is
     actually published. It is sample output a reader compares against their own terminal,
     so it has to be what they will see — and it is the *published* number rather than the
     tree's, which is why this record is the only thing that can check it.

**Only packages that carry a constant are in scope, and that is most of them excluded.**
Of the six artifacts this repository publishes, one embeds a fingerprint. `fjord-client`
reads it off the schema its caller supplies and `Boxops.Fjord.Client` takes it from the
schema object and reads the server's back at the handshake, so neither is coupled to a
schema moving. Forcing those to re-release would be lockstep bought for nothing.

Standard library only, no network. Exit 1 on any finding.
"""

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DOTNET = ROOT / "clients" / "dotnet"
RECORD = DOTNET / "PUBLISHED.tsv"
STARTED = ROOT / "web" / "src" / "content" / "getting-started.mdx"

# `Tool 'boxops.fjord.indexer' (version '0.6.2') was successfully installed.` — the sample
# output of the install command, which a reader checks their own terminal against.
INSTALLED = re.compile(r"Tool '([a-z0-9.]+)' \(version '([^']+)'\)")

# `public const ulong SchemaFingerprint = 0x…;` — and the `Demo`'s file-scoped `const`
# without the modifier, because the shape that matters is the constant, not its access.
# `0[xX]` and `_` separators because C# accepts both, and a constant the pattern misses
# reads as "this package states no fingerprint" — which is a silent pass, not a failure.
FINGERPRINT = re.compile(
    r"\bconst\s+ulong\s+SchemaFingerprint\s*=\s*(0[xX][0-9a-fA-F_]+)\s*;"
)

findings: list[str] = []


def fail(finding: str) -> None:
    findings.append(finding)


def tag(text: str, name: str) -> str | None:
    """The last value `name` is set to in `text`, or `None` if it is never set."""
    matches = re.findall(rf"<{name}>\s*([^<]+?)\s*</{name}>", text)
    return matches[-1] if matches else None


def default_version() -> str | None:
    """The version a project inherits when its own file does not state one."""
    props = DOTNET / "Directory.Build.props"
    return tag(props.read_text(), "Version") if props.exists() else None


def packable() -> dict[str, tuple[Path, str, str | None]]:
    """Every packable project, as package id -> (csproj, version, fingerprint).

    **Keyed by `PackageId` rather than by directory**, because that is the name a consumer
    installs and the name the registry holds — a project renamed on disk is the same
    package, and the record has to follow the package.
    """
    inherited = default_version()
    found: dict[str, tuple[Path, str, str | None]] = {}

    for csproj in sorted(DOTNET.glob("*/*.csproj")):
        text = csproj.read_text()

        if tag(text, "IsPackable") != "true":
            continue

        package = tag(text, "PackageId") or csproj.stem
        version = tag(text, "Version") or inherited

        if version is None:
            fail(
                f"{csproj.relative_to(ROOT)} is packable and nothing states a version — "
                f"neither the project nor Directory.Build.props"
            )
            continue

        # The whole project directory, because which file holds the constant is the
        # producer's business: `DotnetIndex.cs` today, and the gate should not have to be
        # edited when it moves.
        fingerprints = {
            match
            for source in sorted(csproj.parent.rglob("*.cs"))
            if "/obj/" not in source.as_posix() and "/bin/" not in source.as_posix()
            for match in FINGERPRINT.findall(source.read_text())
        }

        if len(fingerprints) > 1:
            fail(
                f"{package} states {len(fingerprints)} different fingerprints "
                f"({', '.join(sorted(fingerprints))}) — a package claims one schema, so "
                f"the gate cannot tell which one its version is answering for"
            )
            continue

        found[package] = (csproj, version, next(iter(fingerprints), None))

    return found


def recorded() -> dict[str, tuple[str, str]]:
    """`PUBLISHED.tsv`, as package id -> (version, fingerprint)."""
    if not RECORD.exists():
        fail(f"{RECORD.relative_to(ROOT)} is missing, so nothing records what is live")
        return {}

    rows: dict[str, tuple[str, str]] = {}

    for number, line in enumerate(RECORD.read_text().splitlines(), start=1):
        if not line.strip() or line.lstrip().startswith("#"):
            continue

        fields = line.split("\t")

        if len(fields) != 3:
            fail(
                f"PUBLISHED.tsv:{number} has {len(fields)} tab-separated field(s), not 3: "
                f"{line!r}"
            )
            continue

        package, version, fingerprint = (field.strip() for field in fields)
        rows[package] = (version, fingerprint)

    return rows


def series(version: str) -> tuple[int, int] | None:
    """`(major, minor)` — the part a breaking change has to move."""
    match = re.fullmatch(r"(\d+)\.(\d+)\.(\d+)", version)
    return (int(match.group(1)), int(match.group(2))) if match else None


def main() -> None:
    projects = packable()
    rows = recorded()

    coupled = {package for package, (_, _, fp) in projects.items() if fp is not None}

    # ---- the census, first: a gate that does not cover the class proves nothing ------

    for package in sorted(coupled - rows.keys()):
        fail(
            f"{package} states a schema fingerprint and PUBLISHED.tsv has no row for it, "
            f"so nothing checks whether its version moved when the schema did — add a row "
            f"naming the version last published and the fingerprint it carried"
        )

    for package in sorted(rows.keys() - coupled):
        reason = (
            "it is no longer packable or no longer exists"
            if package not in projects
            else "it no longer states a fingerprint constant"
        )
        fail(
            f"PUBLISHED.tsv records {package} and {reason} — remove the row, or restore "
            f"the constant it is recording"
        )

    # ---- and the claim itself --------------------------------------------------------

    for package in sorted(coupled & rows.keys()):
        csproj, version, fingerprint = projects[package]
        was_version, was_fingerprint = rows[package]

        now, before = series(version), series(was_version)

        if now is None:
            fail(f"{csproj.relative_to(ROOT)} states a version that is not a triple: {version!r}")
            continue

        if before is None:
            fail(f"PUBLISHED.tsv records {package} at a version that is not a triple: {was_version!r}")
            continue

        if int(fingerprint.replace("_", ""), 16) == int(was_fingerprint.replace("_", ""), 16):
            # Nothing moved. The version may still have moved — a release that changes no
            # schema is ordinary — so there is nothing to check here.
            continue

        if now == before:
            fail(
                f"{package} states fingerprint {fingerprint} and the published {was_version} "
                f"carries {was_fingerprint}, but the version is still {version}.\n"
                f"      A moved fingerprint is refused at the handshake, so it is breaking: "
                f"bump the minor in {csproj.relative_to(ROOT)}.\n"
                f"      Publishing as it stands would put two different artifacts under one "
                f"version number, and publish.sh would skip it as already live."
            )
        elif now < before:
            fail(
                f"{package} states version {version}, behind the published {was_version} — "
                f"a version cannot go backwards on a registry"
            )

    # ---- and the number the book shows a reader --------------------------------------

    if STARTED.exists():
        for package, version in INSTALLED.findall(STARTED.read_text()):
            # The book names the tool in lower case, which is how NuGet renders an id.
            against = {name.lower(): live for name, (live, _) in rows.items()}

            if package not in against:
                fail(
                    f"getting-started.mdx shows installing {package!r} and PUBLISHED.tsv "
                    f"has no row for it, so nothing checks the version it prints"
                )
            elif version != against[package]:
                fail(
                    f"getting-started.mdx prints {package} version {version} and "
                    f"PUBLISHED.tsv records {against[package]} as published — the book's "
                    f"sample output is what a reader compares their own terminal against"
                )

    if findings:
        print(f"{len(findings)} finding(s):", file=sys.stderr)
        for finding in findings:
            print(f"  {finding}", file=sys.stderr)
        sys.exit(1)

    carried = ", ".join(sorted(coupled)) or "no package"
    print(
        f"versions are publishable: {carried} states a schema fingerprint, every one of "
        f"them is recorded, none claims a fingerprint its published version did not carry, "
        f"and the book prints the version a reader will actually install"
    )


main()
