#!/usr/bin/env bash
# Index a .NET checkout into a fresh Fjord database, and leave it there to query.
#
#   ./clients/dotnet/index-repo.sh /path/to/Some.slnx [database]
#
# The readiness file is the synchronisation, as in run-demo.sh: it appears only once the
# listener is accepting, so waiting on it is a signal rather than a race (operations §5).
#
# Everything the indexer takes is passed through, so the knobs are its own:
#
#   ./clients/dotnet/index-repo.sh ~/src/OrchardCore/OrchardCore.sln code --max-files 5000
#
# The server is left running until this script exits and the database survives it — the
# last lines say how to open a shell on it.
set -euo pipefail

if [ $# -lt 1 ]; then
    echo "usage: $0 <path-to-solution-or-project> [database] [indexer flags...]" >&2
    exit 2
fi

source_path="$(cd "$(dirname "$1")" && pwd)/$(basename "$1")"

# **Named, not discovered.** The indexer takes a solution or a project and no longer
# guesses one out of a directory: a repository with two solutions in it gave whichever
# sorted first, silently, and reported the index complete under `--strict`.
case "$source_path" in
    *.sln|*.slnx) input=(--sln "$source_path") ;;
    *.csproj)     input=(--project "$source_path") ;;
    *) echo "name a .sln, .slnx or .csproj — not $source_path" >&2; exit 2 ;;
esac
shift

database="code"
if [ $# -gt 0 ] && [[ "$1" != --* ]]; then
    database="$1"
    shift
fi

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
scratch="${FJORD_INDEX_DIR:-/tmp/fj-index}"

# A named store root keeps its socket beside it (operations §2), so `fjord query`
# finds the same server this indexer wrote to without being told where.
socket="$scratch/db/fjord.sock"

cargo build --manifest-path "$root/Cargo.toml" --bin fjord --release
fjord="$root/target/release/fjord"

# A fresh database each run: the point of indexing something large is measuring what it
# costs, and a second run over a database that already holds the answers measures dedup.
rm -rf "$scratch"
mkdir -p "$scratch"

# **The schema, composed once by the tool that owns resolution.** `dotnet.sigla` reaches
# five files by import, and following an import is sigla's job — so it is baked here and
# the indexer sends the result. Never checked in: a composed schema is derived from the
# files beside it, and a copy in the repository is one that goes stale silently.
baked="$scratch/dotnet.composed.sigla"
"$fjord" --schema-path "$root/schemas" schema compose "$root/schemas/dotnet.sigla" > "$baked"

# **No database is created here, and the framework list is not asked for.** A checkout
# compiling for two frameworks is two databases named `<database>#<tfm>`, and their names
# are not known until the design-time build has run — so discovering them ahead of time
# cost a second full load of the whole solution. The indexer creates what it needs from
# `--schema` as it reaches each target.

"$fjord" --data-dir "$scratch/db" serve --ready-file "$scratch/ready" &
server=$!
trap 'kill "$server" 2>/dev/null || true' EXIT

for _ in $(seq 1 200); do
    [ -e "$scratch/ready" ] && break
    sleep 0.1
done

[ -e "$scratch/ready" ] || { echo "the server never became ready" >&2; exit 1; }

dotnet run --project "$root/clients/dotnet/Boxops.Fjord.Indexer" --configuration Release -- \
    "${input[@]}" \
    --schema "$baked" \
    --at "$socket//$database" \
    "$@"

echo
echo "the database(s) are at $scratch/db, and the server is about to stop. To ask them things:"
echo "  $fjord --data-dir $scratch/db serve &"
echo "  $fjord --data-dir $scratch/db list"

# **Listed rather than predicted.** Which databases exist is now the run's answer — it
# creates one per target framework as it reaches them — so naming them here would be this
# script guessing at what it deliberately stopped computing.
for name in $("$fjord" --data-dir "$scratch/db" list 2>/dev/null | awk 'NR>1 {print $1}'); do
    echo "  $fjord --data-dir $scratch/db query '$name' 'F where src.File F' --limit 20 --timing"
done
