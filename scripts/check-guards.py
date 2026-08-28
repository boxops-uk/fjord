#!/usr/bin/env python3
"""Check that the ignored-test ledger is exact, owned, and built.

Every ignored Rust test is either a pending guard or test machinery:

    #[ignore = "guard: <claim>, owned by Movement <N>"]
    #[ignore = "not a guard: <why>"]

The manifest below is the machine-readable ledger. Keeping it separate from the
attributes is deliberate: deleting or inventing an ignored guard must disagree
with something independent and fail. The harness list supplies the other half:
an attribute behind a configuration the workspace does not build is not a guard.
"""

import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent

MOVEMENTS = range(0, 9)
CLOSED_MOVEMENTS = {0}

# name: (owner, claim). This is intentionally duplicated from the source
# attributes: agreement by construction would not prove that no obligation was
# deleted, added, re-owned, or weakened.
EXPECTED_GUARDS: dict[str, tuple[int, str]] = {
    "a_with_block_parses_and_the_corpus_executes_it": (
        7,
        "a `with` block's source spelling parses, and the corpus executes it end to end",
    ),
    "a_programs_local_names_are_the_catalogues_locals": (
        1,
        "a program's declared local names are the compiler catalogue's locals, and each resolves to the id the catalogue mints for it",
    ),
    "generated_namespace_exhaustion_is_refused_by_name": (
        6,
        "a generated magic or delta namespace that exhausts the tag space is refused by name rather than wrapping",
    ),
    "canonical_ids_survive_rule_scheduling_and_expansion_strategy": (
        3,
        "a relation's canonical ids are the same under any rule scheduling and either expansion strategy",
    ),
    "the_materialisation_projection_holds_over_the_source_corpus": (
        7,
        "the materialisation projection over the full source corpus, scalar and union heads refused by name",
    ),
    "a_segmented_relation_reads_within_a_bounded_factor_of_a_batch_built_one": (
        1,
        "an empty-range seek, a narrow seek, a point lookup and a full scan cost a bounded factor more on an N-round relation than on a batch-built one, and do not grow with N",
    ),
    "simultaneous_scc_rules_see_the_accumulated_relation": (
        3,
        "rules of one SCC evaluated in the same round observe one another's accumulated relation and not one another's delta",
    ),
    "the_budget_is_charged_through_a_chokepoint_rather_than_by_convention": (
        2,
        "charging the budget is structural — the driver and the generator hold types through which no work can be done without charging it",
    ),
    "a_failure_in_any_transformed_phase_falls_back_silently": (
        6,
        "a fault injected at any phase of the transformed candidate takes the unmagicked fallback and emits no diagnostic the user did not provoke",
    ),
    "the_plain_path_dispatch_contract_holds_end_to_end": (
        8,
        "the plain-path dispatch contract holds through the executable seam, the server and inspection",
    ),
    "dnf_expansion_amplifies_prefix_level_scans": (
        3,
        "DNF expansion re-enters every prefix level once per clause, measured with a store spy rather than argued",
    ),
    "magic_answers_what_the_unmagicked_program_answers": (
        6,
        "a magicked program answers what the unmagicked one answers, negative-only and partially-bound predicates included",
    ),
    "a_program_cursor_names_the_program_and_the_world_it_was_made_in": (
        4,
        "a cursor whose program fingerprint has moved is refused, and the envelope carries the world stamp through a program resume",
    ),
    "reads_virtual_covers_generated_rules": (
        8,
        "`reads_virtual` is computed over generated rules, not only over the rules the user wrote",
    ),
    "one_base_snapshot_serves_every_rule_and_every_round": (
        2,
        "one base snapshot is observed by every rule and every round — not one per rule, which would multiply fjall's open-snapshot count",
    ),
    "relation_snapshots_are_released_on_every_exit_path": (
        4,
        "base and relation snapshots are both live during execution and both at zero after an answer-page suspend, a cancellation mid-fixpoint, a materialisation or limit error, and normal completion",
    ),
    "materialisation_allocates_only_with_retained_bytes": (
        3,
        "materialisation allocates with bytes actually retained and with nothing else — not with rejected attempts, not with duplicates, not with rounds",
    ),
}

EXPECTED_NON_GUARDS = {
    "crashing_writer_child_process",
    "crashing_creator_child_process",
    "crashing_finisher_child_process",
    "print_the_union_schema_fingerprint",
}

GUARD = re.compile(r"^guard: (?P<claim>.+?), owned by Movement (?P<owner>\d+)$")
NOT_A_GUARD = re.compile(r"^not a guard: .+$")
IGNORE_ATTR = re.compile(
    r'#\[ignore\s*=\s*"(?P<reason>(?:[^"\\]|\\.)*)"\][\s\S]*?\bfn\s+(?P<name>\w+)'
)


def source_ignores() -> tuple[dict[str, tuple[str, str]], list[str]]:
    """Return every source attribute by leaf test name, plus parse findings."""
    found: dict[str, tuple[str, str]] = {}
    findings: list[str] = []
    for path in sorted((ROOT / "crates").rglob("*.rs")):
        source = path.read_text(encoding="utf-8")
        for match in IGNORE_ATTR.finditer(source):
            name = match.group("name")
            reason = match.group("reason")
            where = f"{path.relative_to(ROOT)}"
            if name in found:
                findings.append(
                    f"two ignored tests are both named `{name}`: {found[name][1]}, {where}"
                )
            found[name] = (reason, where)
    return found, findings


def listed_ignores() -> set[str]:
    """Return the ignored tests the workspace harness actually builds."""
    proc = subprocess.run(
        ["cargo", "test", "--locked", "--workspace", "-q", "--", "--ignored", "--list"],
        cwd=ROOT,
        capture_output=True,
        text=True,
    )
    if proc.returncode != 0:
        print(proc.stdout, file=sys.stderr)
        print(proc.stderr, file=sys.stderr)
        raise RuntimeError("could not list ignored tests")

    names = set()
    for line in proc.stdout.splitlines():
        if line.endswith(": test"):
            names.add(line[: -len(": test")].rsplit("::", 1)[-1])
    return names


def check_ledger(
    ignores: dict[str, tuple[str, str]],
    listed: set[str],
    *,
    expected_guards: dict[str, tuple[int, str]] = EXPECTED_GUARDS,
    expected_non_guards: set[str] = EXPECTED_NON_GUARDS,
    source_findings: list[str] | None = None,
    movements: range = MOVEMENTS,
    closed_movements: set[int] = CLOSED_MOVEMENTS,
) -> tuple[list[str], list[str]]:
    """Check a ledger and return (findings, printable pending-guard rows)."""
    findings = list(source_findings or [])
    actual_guards: dict[str, tuple[int, str]] = {}
    actual_non_guards: set[str] = set()

    for name, (reason, where) in sorted(ignores.items()):
        if NOT_A_GUARD.fullmatch(reason):
            actual_non_guards.add(name)
            continue

        match = GUARD.fullmatch(reason)
        if not match:
            if reason.startswith("guard: "):
                findings.append(
                    f"{where}: `{name}` is a guard that does not name its owner as "
                    "`owned by Movement <N>`"
                )
            else:
                findings.append(
                    f"{where}: `{name}` is ignored with a reason in neither form:\n"
                    f'    "{reason}"\n'
                    "    expected `guard: <claim>, owned by Movement <N>` "
                    "or `not a guard: <why>`"
                )
            continue

        owner = int(match.group("owner"))
        claim = match.group("claim")
        if owner not in movements:
            findings.append(
                f"{where}: `{name}` is owned by Movement {owner}, which is not a movement"
            )
        elif owner in closed_movements:
            findings.append(
                f"{where}: `{name}` is owned by Movement {owner}, which has closed — "
                "un-ignore it or re-own it"
            )
        actual_guards[name] = (owner, claim)

    for name in sorted(set(expected_guards) - set(actual_guards)):
        findings.append(f"the documented guard `{name}` is missing from the source ledger")
    for name in sorted(set(actual_guards) - set(expected_guards)):
        findings.append(f"`{name}` is a pending guard but is not in the documented ledger")
    for name in sorted(set(expected_guards) & set(actual_guards)):
        if actual_guards[name] != expected_guards[name]:
            findings.append(
                f"`{name}` moved or changed its claim:\n"
                f"    expected {expected_guards[name]!r}\n"
                f"    found    {actual_guards[name]!r}"
            )

    for name in sorted(expected_non_guards - actual_non_guards):
        findings.append(f"the documented non-guard `{name}` is missing from the ignored tests")
    for name in sorted(actual_non_guards - expected_non_guards):
        findings.append(f"`{name}` is ignored as a non-guard but is not in the documented ledger")

    for name in sorted(set(ignores) - listed):
        findings.append(
            f"{ignores[name][1]}: `{name}` is `#[ignore]`d in the source but the harness "
            "does not list it — a guard the suite never builds is not a guard"
        )
    for name in sorted(listed - set(ignores)):
        findings.append(
            f"the harness lists `{name}` as ignored, but no `#[ignore]` attribute was found for it"
        )

    rows = [
        f"  Movement {owner}: {name} — {claim}"
        for name, (owner, claim) in sorted(actual_guards.items())
    ]
    return findings, rows


def main() -> int:
    ignores, parse_findings = source_ignores()
    try:
        listed = listed_ignores()
    except RuntimeError as error:
        print(error, file=sys.stderr)
        return 1

    findings, rows = check_ledger(
        ignores,
        listed,
        source_findings=parse_findings,
    )
    if findings:
        print("The coverage ledger does not read as one:\n", file=sys.stderr)
        for finding in findings:
            print(f"  - {finding}", file=sys.stderr)
        return 1

    print(
        f"Coverage ledger: {len(rows)} pending guard(s), "
        f"{len(ignores) - len(rows)} non-guard(s)."
    )
    for row in rows:
        print(row)
    return 0


if __name__ == "__main__":
    sys.exit(main())
