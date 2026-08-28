"""Mutation controls for the ignored-guard ledger gate."""

import importlib.util
import unittest
from pathlib import Path


SCRIPT = Path(__file__).with_name("check-guards.py")
SPEC = importlib.util.spec_from_file_location("check_guards", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
check_guards = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(check_guards)


class LedgerChecks(unittest.TestCase):
    def check(
        self,
        ignores: dict[str, tuple[str, str]],
        listed: set[str],
        expected: dict[str, tuple[int, str]],
        *,
        closed: set[int] | None = None,
    ) -> list[str]:
        findings, _ = check_guards.check_ledger(
            ignores,
            listed,
            expected_guards=expected,
            expected_non_guards=set(),
            movements=range(0, 9),
            closed_movements=closed or set(),
        )
        return findings

    def test_the_exact_documented_ledger_passes(self) -> None:
        ignores = {
            "pending": (
                "guard: the promised property holds, owned by Movement 2",
                "fixture.rs",
            )
        }
        self.assertEqual(
            self.check(ignores, {"pending"}, {"pending": (2, "the promised property holds")}),
            [],
        )

    def test_a_reason_in_neither_form_fails(self) -> None:
        findings = self.check(
            {"pending": ("temporarily skipped", "fixture.rs")},
            {"pending"},
            {},
        )
        self.assertTrue(any("reason in neither form" in finding for finding in findings))

    def test_a_guard_naming_no_owner_fails(self) -> None:
        findings = self.check(
            {"pending": ("guard: the promised property holds", "fixture.rs")},
            {"pending"},
            {},
        )
        self.assertTrue(any("does not name its owner" in finding for finding in findings))

    def test_a_guard_owned_by_a_closed_movement_fails(self) -> None:
        reason = "guard: the promised property holds, owned by Movement 2"
        findings = self.check(
            {"pending": (reason, "fixture.rs")},
            {"pending"},
            {"pending": (2, "the promised property holds")},
            closed={2},
        )
        self.assertTrue(any("which has closed" in finding for finding in findings))

    def test_a_guard_the_harness_does_not_build_fails(self) -> None:
        reason = "guard: the promised property holds, owned by Movement 2"
        findings = self.check(
            {"pending": (reason, "fixture.rs")},
            set(),
            {"pending": (2, "the promised property holds")},
        )
        self.assertTrue(any("harness does not list it" in finding for finding in findings))

    def test_deleting_a_documented_guard_fails(self) -> None:
        findings = self.check(
            {},
            set(),
            {"pending": (2, "the promised property holds")},
        )
        self.assertTrue(
            any("documented guard `pending` is missing" in finding for finding in findings)
        )

    def test_adding_an_undocumented_guard_fails(self) -> None:
        reason = "guard: an invented property holds, owned by Movement 2"
        findings = self.check(
            {"invented": (reason, "fixture.rs")},
            {"invented"},
            {},
        )
        self.assertTrue(any("not in the documented ledger" in finding for finding in findings))

    def test_weakening_a_guard_fails(self) -> None:
        reason = "guard: a weaker property holds, owned by Movement 2"
        findings = self.check(
            {"pending": (reason, "fixture.rs")},
            {"pending"},
            {"pending": (2, "the promised property holds")},
        )
        self.assertTrue(any("moved or changed its claim" in finding for finding in findings))

    def test_reowning_a_guard_fails(self) -> None:
        reason = "guard: the promised property holds, owned by Movement 3"
        findings = self.check(
            {"pending": (reason, "fixture.rs")},
            {"pending"},
            {"pending": (2, "the promised property holds")},
        )
        self.assertTrue(any("moved or changed its claim" in finding for finding in findings))


if __name__ == "__main__":
    unittest.main()
