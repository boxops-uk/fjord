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
"$root/clients/dotnet/index-repo.sh" "$source_path" code --styles > "$scratch.log" 2>&1 || {
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

echo "==> exporting"
"$fjord" --data-dir "$scratch/db" export "code#$framework" --to "$out/code.fjmem"

echo "==> composing the schema"
"$fjord" --schema-path "$root/schemas" schema compose "$root/schemas/dotnet.sigla" \
    > "$out/code.sigla"

image=$(wc -c < "$out/code.fjmem")
packed=$(gzip -9 -c "$out/code.fjmem" | wc -c)
echo "==> $out/code.fjmem: $image bytes ($packed gzipped)"
