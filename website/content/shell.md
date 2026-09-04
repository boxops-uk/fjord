---
title: Shell reference
description: The wire REPL — every command, what each one is for, and why a query compiles on your machine rather than the server's.
---

```bash
fjord shell <db>
```

**Always over the wire**, even against a server on the same machine — so the protocol has a
permanent exerciser and `:more` holds a real cursor across a real round trip.

The input layer is syntax highlighting from the compiler's own lexer, history, completion, and
the rule that a line with an unclosed `{` or `(` continues on the next one.

Commands are `:`-prefixed. The psql spellings (`\d`, `\l`, `\c`, `\timing`, `\more`) are accepted
as aliases, because neither prefix can begin a sigla query — so a hand trained on psql costs
nothing.

## A session

```bash
fjord --data-dir ./db shell code
```

```text
fjord shell — `code` on ./db/fjord.sock
  28 predicate(s) · rows print as jsonl · :help for commands
```

| Command | Aliases | Does |
|---|---|---|
| `<query>` | | Compile it **locally**, send it, print the rows |
| `:type <query>` | | The type of its head, without planning or running it |
| `:plan <query>` | | The plan it compiles to, without running it |
| `:facts <predicate>` | | Every row of one predicate — sugar for `X where <predicate> X` |
| `:schema [name]` | `\d`, `:d` | The schema this database is served with, or one predicate, or a prefix |
| `:more` | `\more`, `:m` | The next page of the last result |
| `:limit [n]` | | Rows per page; bare, it says what the page is |
| `:format <f>` | | How a row prints: `jsonl`, `json`, `table`, `raw` |
| `:expand [hops]` | | Show the fact a reference names, not its id. Bare toggles it |
| `:cancel` | `\cancel` | Stop the last result early |
| `:timing` | `\timing` | Toggle how long a page took |
| `:profile` | `\profile` | Toggle what a query examined, per step of its plan |
| `:list` | `\l`, `:l` | The databases on this server — a query over `fjord.db.List` |
| `:interning` | `\i`, `:i` | The write path's own counters, per database — a query over `fjord.db.Interning` |
| `:connect <db>` | `\c`, `:c` | The same session against another database |
| `:clear` | | Clear the screen |
| `:help` | `\?`, `:?`, `:h` | The table above, generated from the table itself |
| `:quit` | Ctrl-D | Leave |

`:help` is generated from the command table, so a command that exists is a command that is
listed.

### The shell compiles what you type

It holds the schema the server said it serves — the `H`/`h` exchange — so a query is compiled
**here** before it is sent. Three things follow:

- A mistake is the compiler's own diagnostic, with the code, the caret and colour, and **no round
  trip**.
- `:plan` and `:type` can be answered at all. A client never holds a plan otherwise; what was
  missing was the *schema*.
- Where the two compilers could disagree, the **server** decides what runs.

### Paging holds a real cursor

```text
sigla> :limit 3
  3 row(s) per page
sigla> F where src.File F
  : str
"Boxops.Fjord.Client/Blocks.cs"
"Boxops.Fjord.Client/Buffers.cs"
"Boxops.Fjord.Client/Crc32.cs"
  :more for the next 3 — 3 so far
sigla> :more
"Boxops.Fjord.Client/Errors.cs"
"Boxops.Fjord.Client/FjordAddress.cs"
"Boxops.Fjord.Client/FjordConnection.cs"
  :more for the next 3 — 6 so far
```

`:more` is not a re-run with an offset. The server suspended the query, encoded one detached row
per open loop level into a bytes-only token, and handed it over. Nothing is held server-side
between pages.

The line before the first row (`: str`) is the **row descriptor** — the shape of the head, sent
once per query.

### Rows print as JSON Lines

One value per line, shaped by the descriptor at every level, so a nested record is a nested
object. A page is not a document and three pages of one query are not three documents, which is
why it is line-per-row rather than an array. `:format table` is there for reading rather than
piping.

### `:expand` — show the fact a reference names

```text
sigla> R where R = codemarkup.SymbolXRef _
  : codemarkup.SymbolXRef
"#9:236"

sigla> :expand
  references expand into the facts they name, all the way down
  each one is a point read — :timing counts them per page
sigla> R where R = codemarkup.SymbolXRef _
  : codemarkup.SymbolXRef
{"target": "scip-csharp nuget Boxops.Fjord.Client 0.3.0.0 Boxops/Fjord/Client/Crc32#", "file": "Boxops.Fjord.Client/Blocks.cs", "span": {"start": 4808, "length": 5}}
```

A row carries a reference as a fact id, because that is what one is once stored — and sigla
cannot ask what it names. So the question goes on the protocol, and the client walks the answer:
breadth-first, one round trip per level of depth, one point read per distinct id, cached across
pages because a page of references into one file names that file forty times.

A reference that resolves to nothing is **reported**, not hidden: for an id out of a row it
cannot happen, so it means a damaged database — and a row printing the id instead would look like
a field somebody chose not to expand.

### `:profile` — what it examined

```text
sigla> :profile
  profile is on
  what the next query examines is reported when it ends
sigla> {f = F, at = S} where
         codemarkup.SearchEntry
           {nameLowercase = _, name = "Crc32", kind = _, symbol = T, file = _, line = _};
         codemarkup.SymbolXRef {target = T, file = F, span = S}
STEP                    EXAMINED
codemarkup.SearchEntry  904       full scan
codemarkup.SymbolXRef   5
909 examined, 5 produced
```

Per **step of the plan's body**, which is what the machine counts — so a fetch, a disjunction and
a negation each get a line. Read it against `:plan`: the plan is the intent, this is the outcome.

A profile arrives once, just before the result ends, because the tally is not final until the
last chunk has run. A `:limit` that cancels early therefore reports none rather than reporting a
different query's numbers.

### `:schema` — and prefixes

```text
sigla> :schema codemarkup.SymbolXRef
  predicate codemarkup.SymbolXRef: { target: src.Symbol, file: src.File, span: { start: int, length: int } }
sigla> :schema codemarkup.
```

An exact name describes one predicate; anything that does not resolve exactly falls back to
**prefix matching**, so `:schema codemarkup.` dumps a namespace rather than failing.

Virtual predicates are printed like any other, because the served schema is what may be *asked*
about. `fjord.db.List` is there, and `:list` is a query over it; `fjord.db.Interning` is its
sibling — the ingest cache's hits, misses and the two trees' point reads, per database, which is
how *is the interning cache working* is a query rather than a debugger session.

## Things worth trying

```sigla
:plan E where E = codemarkup.SearchEntry
                   {nameLowercase = _, name = "Crc32", kind = _, symbol = _, file = _, line = _}
:plan S where codemarkup.SymbolByName {name = "Crc32", symbol = S}
```

The same question twice, and the plans are the argument for what a second copy of the data is:
`SearchEntry` leads with `nameLowercase`, so a constraint on `name` can only filter rows the
scan already produced, while `SymbolByName` leads with `name` and seeks.

```text
  r0 <- codemarkup.SearchEntry scan
       where name == "Crc32"
  head r0#

  r0 <- codemarkup.SymbolByName seek[name = "Crc32", symbol = _]
  head r0.symbol
```

Run both with `:profile` on and read the `EXAMINED` column.

```sigla
D.name where D = codemarkup.SearchEntry _; !codemarkup.SymbolXRef {target = D.symbol}
```

Names nothing refers to — a negation, which is a test rather than a level: it binds nothing and
each source is drained to its first row.

```sigla
{name = N, file = P} where
  codemarkup.SearchEntry {nameLowercase = "crc".., name = N, kind = _, symbol = _, file = F, line = _};
  F = src.File P
```

Reading **through** a reference: `F` is a fact id, so the path it names is in another fact's key
and the plan grows a fetch level.

```text
  r0 <- codemarkup.SearchEntry seek[nameLowercase = "crc".., name = _, kind = _, symbol = _, file = _, line = _]
  r1 <- src.File fetch[r0.file]
  head {file = r1.0, name = r0.name}
```
