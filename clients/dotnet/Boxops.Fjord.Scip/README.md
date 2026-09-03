# `scip2fjord` — a SCIP index into a Fjord database

Every language with a SCIP indexer — TypeScript, Java, Scala, Rust, Python, Go, Ruby —
reaches a Fjord database through this one program, and none of them needs anything written
here. That is R9's claim, and the thing it does *not* mean is "no work": positions have to
be converted, kinds projected, and a name recovered where the index gives none.

```
scip2fjord --input index.scip --at /tmp/fjord.sock//code
scip2fjord --input index.scip --emit blocks.bin
```

## What it depends on

`Boxops.Fjord.Client`, and nothing else. **No indexer**, which is the point of it: Run 8a
published the write seam, and a converter that had to reach into the .NET indexer to use it
would mean the seam had not been published at all. **No protobuf package** either — what
this reads is four messages and eleven fields, and the wire format is varints and
length-delimited bytes, so a code generator and a package feed would be a poor trade for
two hundred lines that can be read.

## What it writes, and what it does not

`src.File`, the line table, `src.Symbol`, `config.Setting`, and the `codemarkup` surface:
`Definition`, `FileDefinition`, `FileXRef`, `SymbolXRef`, `SearchEntry`, `SymbolByName`.
Twelve predicates against a database of a hundred and thirty-eight — a client declares the
shapes it uses, not the database's whole schema.

There is **no declaration layer, no type graph and no build layer**, because a SCIP index
contains none of those. Revision 2 required this converter to synthesise `src.Decl`,
`src.Module` and a search index for a language it has no compiler for; `codemarkup` removed
the need, and inventing a declaration model per occurrence would have put facts in a
database that nothing could stand behind.

## Two things to know before pointing it at a real index

**The database's fingerprint is compiled in.** `ScipFacts.Schema` carries
`schemas/index.sigla`'s, because that is the composite a cross-language index belongs in. A
schema change there is a rebuild of this program — the handshake is an equality check, and
that is the designed failure.

**Two producers writing symbols into one database must agree about descriptors.** A SCIP
symbol is a string, and `src.Symbol` is keyed on it: if a C# indexer and a TypeScript
indexer spell the same symbol differently, the database holds two identities for one thing
and no query can tell. Nothing here can check that, so a database fed from two producers
should say which schemes it holds through
`config.Setting {dimension = "symbol-scheme"}` — this one writes `scip`.

## The style layer, which comes free

A SCIP `Occurrence` carries a symbol *and* a syntax kind over one span, so the pass that
fills the cross-reference layer has already read everything highlighting needs.
`src.FileLineStyles` is opaque — fjord stores the bytes and defines nothing about them — so
this writes three unsigned varints per token (start column in bytes, length in bytes, and
SCIP's own `SyntaxKind` number) and names the format `scip-syntax-1` through
`config.Setting {dimension = "style-encoding"}`. A consumer that does not recognise the
name renders those lines plain.
