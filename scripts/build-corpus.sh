#!/usr/bin/env bash
# Index a checkout, export it as a store image, and put it where the site can fetch it.
#
#   ./scripts/build-corpus.sh [path-to-project-or-solution] [out-dir]
#
# **Not checked in, for the reason the WebAssembly module is not**: a binary in git is
# a binary somebody has to trust, and the build is one command. A checkout without it
# gets a page that says so rather than one that silently shows an empty index.
#
# The pipeline, and why each step is here:
#
#   index    a real compiler walks real source — Buildalyzer and Roslyn, `--styles`
#            for the highlighting, into a real database through a real server
#   finish   sealing is what computes the content identity; an image of a database
#            still being written is an image of a moment nobody can name
#   export   every row with the id it already has, because nothing in a browser can
#            intern a fact (`fjord export`, and `fjord_store_mem::dump`)
#   compose  the schema the image is keyed against, resolved through its imports —
#            the page states it and the load refuses a mismatch
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source_path="${1:-$root/clients/dotnet/Boxops.Fjord.Client/Boxops.Fjord.Client.csproj}"
out="${2:-$root/web/public/corpus}"
scratch="${FJORD_CORPUS_DIR:-/tmp/fj-corpus}"

# **The framework is named, not discovered.** A multi-targeted project indexes once
# per framework into `code#<tfm>`, and a corpus is one of them: two would be the same
# source twice, under two sets of identities.
framework="${FJORD_CORPUS_TFM:-net10.0}"

export FJORD_INDEX_DIR="$scratch"

echo "==> indexing $(basename "$source_path")"
# **Paths relative to the repository, not to the project.** The indexer's default root
# is the single project's own directory, which makes every `src.File` a bare filename —
# and a browser over that shows thirteen files in a heap, as though the checkout were
# one folder deep. The root is what puts a file back where it lives.
"$root/clients/dotnet/index-repo.sh" "$source_path" code --styles --root "$root" > "$scratch.log" 2>&1 || {
    echo "indexing failed; the log is at $scratch.log" >&2
    tail -20 "$scratch.log" >&2
    exit 1
}

# index-repo.sh leaves its server running until it exits; wait for the socket to go.
for _ in $(seq 1 50); do
    [ -S "$scratch/db/fjord.sock" ] || break
    sleep 0.1
done

fjord="$root/target/release/fjord"

echo "==> sealing code#$framework"
"$fjord" --data-dir "$scratch/db" finish "code#$framework"

mkdir -p "$out"

# **Named for what they are and not for the world they came from.** `code` is the
# world the index is written into, and naming the pair after it put the composed
# schema under a filename this tree retired with the schema it belonged to — a
# generated file wearing a deleted schema's name is one a reader places wrongly at
# a glance. `scripts/check-docs.py` holds the retired names, so that collision is a
# failed gate rather than a slow misunderstanding.
echo "==> exporting"
"$fjord" --data-dir "$scratch/db" export "code#$framework" --to "$out/corpus.fjmem"

echo "==> composing the schema"
"$fjord" --schema-path "$root/schemas" schema compose "$root/schemas/dotnet.sigla" \
    > "$out/corpus.sigla"

image=$(wc -c < "$out/corpus.fjmem")
packed=$(gzip -9 -c "$out/corpus.fjmem" | wc -c)
echo "==> $out/corpus.fjmem: $image bytes ($packed gzipped)"
