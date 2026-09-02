#!/usr/bin/env bash
#
# **The flag day, walked** — every artifact that moves when `schemas/code.sigla` does.
#
# A client sends one whole-schema fingerprint and the server checks it for equality, so
# a schema edit refuses every client until each of them is rebuilt. The protocol has an
# alternative — per-predicate containment, which would make an *additive* change free —
# and it is declined: the default schema is not expected to move often, and a version
# bump is the accepted cost. That decision is what makes this script necessary.
#
#   scripts/flag-day.sh check     # is anything stale? (read-only, safe, run it in CI)
#   scripts/flag-day.sh regen     # regenerate what a machine can, then re-check
#
# **The expensive failure here is discovering step 4 after step 8**, which is why this
# walks them in order and stops at the first one that is stale rather than reporting a
# list. Steps a *person* has to do are named with what to do; steps a machine can do are
# done by `regen`.
#
# What it does not cover: `R4`'s own migration (the 61 files matching `src.Decl`) and the
# viewer's five queries. Those are a schema *re-keying* rather than a fingerprint move,
# and they have no mechanical check — see `docs/unified-plan/14-flag-day-inventory.md`.
set -uo pipefail

cd "$(dirname "$0")/.."

mode="${1:-check}"
case "$mode" in
    check | regen) ;;
    *)
        echo "usage: $0 {check|regen}" >&2
        exit 2
        ;;
esac

fail() {
    echo
    echo "STALE: $1" >&2
    [ $# -gt 1 ] && echo "  fix: $2" >&2
    exit 1
}

step() { echo "==> $1"; }

# ---- 1 · the schema, and the number it comes to ----------------------------
step "1  schemas/code.sigla"
fingerprint=$(cargo run -q --bin fjord -- schema check schemas/code.sigla 2>/dev/null |
    sed -n 's/^fingerprint //p')
[ -n "$fingerprint" ] || fail "schemas/code.sigla does not check" "fjord schema check schemas/code.sigla"
echo "    fingerprint $fingerprint"

# ---- 2, 3 · the two constants the .NET clients carry, independently --------
step "2  the constants the .NET clients carry (DotnetIndex.cs, Demo/Program.cs)"
if ! cargo test -q -p fjord-cli --test schemas the_dotnet_clients_carry_the_fingerprint \
    >/dev/null 2>&1; then
    cargo test -p fjord-cli --test schemas the_dotnet_clients_carry_the_fingerprint 2>&1 |
        sed -n 's/^.*carries a stale/  carries a stale/p'
    fail "a .NET client carries a stale fingerprint" \
        "each client carries its own schema's number — the indexer writes dotnet.sigla \
and the demo writes code.sigla; paste the one the failure names, then re-run with regen"
fi

# ---- 4 · the checked-in golden bytes ---------------------------------------
step "4  clients/dotnet/golden/*.txt"
if [ "$mode" = regen ]; then
    command -v dotnet >/dev/null ||
        fail "no dotnet on PATH, so the goldens cannot be regenerated" \
            "install the SDK named in clients/dotnet/global.json"
    ./clients/dotnet/emit-golden.sh >/dev/null || fail "emit-golden.sh failed"
fi
if ! git diff --quiet -- clients/dotnet/golden/; then
    fail "the goldens changed and are uncommitted" "review the diff, then commit it"
fi

# ---- 5 · the Rust side agrees, byte for byte ------------------------------
step "5  cargo test -p fjord-client byte_identical"
cargo test -q -p fjord-client byte_identical >/dev/null 2>&1 ||
    fail "the two clients disagree on the bytes" \
        "run ./clients/dotnet/emit-golden.sh (step 4) and look at what moved"

# ---- 6, 7 · the fixture reader, and the Glean translation -----------------
step "6  sample_schema's counts, names and key order"
cargo test -q -p fjord-cli --lib sample_schema >/dev/null 2>&1 ||
    fail "sample_schema disagrees with the schema" \
        "the predicate count, KEY_ORDER and the name lookups are all in that file"

step "7  clients/dotnet/glean/fjbench.angle"
if [ -n "$(git diff --name-only -- schemas/code.sigla)" ] &&
    [ -z "$(git diff --name-only -- clients/dotnet/glean/fjbench.angle)" ]; then
    echo "    WARNING: code.sigla changed and fjbench.angle did not."
    echo "             It is the Glean translation of the same shapes, and nothing checks it."
fi

# ---- 9 · the ordinary gate ------------------------------------------------
step "9  the suite, and the pinned lint gate"
cargo test -q >/dev/null 2>&1 || fail "the suite is red" "cargo test"
cargo +1.97.1 clippy --all-targets --workspace -- -D warnings >/dev/null 2>&1 ||
    fail "clippy is red" "cargo +1.97.1 clippy --all-targets --workspace -- -D warnings"

echo
echo "the flag day is walked: nothing is stale at $fingerprint"
echo
echo "still a person's, because nothing can check them:"
echo "  - bump the .NET package version in the same commit that re-pastes the constant"
echo "  - re-baseline any bench/FINDINGS.md figure the schema change invalidates"
