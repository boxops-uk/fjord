#!/usr/bin/env bash
#
# **Is a new type-model variant a compile error at every site that must handle it?**
#
# Adds a throwaway variant to the enum named by the argument, builds the workspace,
# and reports every site the compiler names. Then puts the tree back.
#
#   scripts/check-exhaustive.sh schema   # fjord_schema::schema::PredicateTyNamed
#   scripts/check-exhaustive.sh engine   # fjord_engine::syntax::Ty
#
# **It fails the build by design, so it is not wired into CI.** What it guards is the
# defect class where a joint `match (ty, value)` absorbs a new family into a wildcard:
# the family then fails at run time, as a corrupt row or a refused frame, instead of
# at the compiler. The sites that must be named are enumerated in the transcript in
# `bench/FINDINGS.md`; a shorter list than that one is a regression.
#
# Silencing a named site to see the next one is the method: add `_ => todo!(),` to
# the match the compiler points at and build again, until it compiles. The union of
# every pass is the answer.
set -uo pipefail

cd "$(dirname "$0")/.."

case "${1:-}" in
    schema)
        file=crates/fjord-schema/src/schema.rs
        anchor='pub enum PredicateTyNamed<N> {'
        ;;
    engine)
        file=crates/fjord-engine/src/syntax.rs
        anchor='pub enum Ty {'
        ;;
    *)
        echo "usage: $0 {schema|engine}" >&2
        exit 2
        ;;
esac

if ! git diff --quiet -- "$file"; then
    echo "$file has uncommitted changes; commit or stash them first" >&2
    exit 2
fi

# **Armed below the guard above, and the order is the whole point.** `restore` is a
# `git checkout` of a tracked file, so a trap installed before the dirty-tree check
# fires on that check's own `exit 2` and deletes the edit the message just refused to
# touch — with no stash and no reflog to recover from. `scripts/test_check_exhaustive.py`
# holds it here.
restore() { git checkout -- "$file"; }
trap restore EXIT

# A name no arm can already be matching.
perl -0pi -e "s/\Q$anchor\E/$anchor\n    Probe,/" "$file"

echo "==> a throwaway \`Probe\` variant added to $file"
echo

sites=$(cargo build --workspace --all-targets --message-format short 2>&1 \
    | grep -E 'error\[E0004\]' \
    | sed 's/: error\[E0004\].*//' \
    | sort -u)

if [ -z "$sites" ]; then
    echo "FAIL: the workspace built. A new variant reached run time unnamed." >&2
    exit 1
fi

echo "$sites"
echo
echo "==> $(echo "$sites" | wc -l) site(s) named by the compiler in this pass"
