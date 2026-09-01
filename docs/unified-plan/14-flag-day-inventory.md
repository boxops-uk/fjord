# W14 · The flag-day inventory — everything that moves when `code.sigla` does

| | |
|---|---|
| **Issues** | revision 2's Run 4e; [#39](https://github.com/boxops-uk/fjord/issues/39) inherits it |
| **Area** | `schemas/`, `clients/dotnet`, `crates/fjord-cli`, `crates/fjord-client`, `crates/fjord-viewer`, `bench/` |
| **Used by** | **W6** and **R4** — the two changes in this plan that move `code.sigla` |

## Two flag days, in this order, and why not one

- **W6** is `Breaking`, naming exactly one predicate: eight arrive at fingerprints that cost nothing,
  and `src.Line` is deleted in favour of `src.FileLine` (D5). The schema-level number moves.
- **R4** is `Breaking` too: `src.Decl`'s key changes, and 61 files read it.

Combining them halves the ceremony and produces a diff nobody can review — AGENTS.md's named
dominant failure mode. **Keep them separate, W6 first.** There will be more of them: the handshake
alternative that would have made additive changes free is declined below, so every future schema
edit is a client version bump, executed by this checklist.

## The handshake change that would make additive changes free — **declined**

The protocol carries an alternative and nothing uses it. The server checks two claims in order
(`fjord-server/src/session.rs:350-380`): whole-schema equality first, then **per-predicate
containment** —

> a producer that writes six of twenty-seven predicates has a different whole-schema fingerprint and
> is not wrong about anything. What it claims is the shapes it uses; what is checked is that this
> database holds each of them, *identically* — a predicate whose key gained a field is a predicate
> whose stored rows this client would encode wrongly, and it is refused **by name** rather than
> as two numbers that differ.

**Decision: keep the single whole-schema constant.** The default schema is not expected to move
often, and when it does, a version bump is an acceptable cost. So the .NET client goes on sending
`0` for the predicate list, exactly as its own comment describes (`FjordConnection.cs:155-162`:
*"**No per-predicate claims**, which is what a client carrying a constant has to send"*), and the
consequence is accepted rather than engineered around:

| | one whole-schema constant (**chosen**) | per-predicate claims (declined) |
|---|---|---|
| an additive schema change | **flag day** — every client refused until it re-pastes | free |
| a Breaking change | refused as two numbers that differ | refused **by name**: `src.Decl` |
| what the client carries | 1 constant | ~20 constants, one per predicate it writes |

**So both changes in this plan are flag days, deliberately**, and the inventory below is how each is
executed. Recorded here rather than deleted, because the first person to find a flag day expensive
will propose exactly this and should find the reason it was declined.

## The inventory, verified in the tree

`fjord schema fingerprint schemas/code.sigla` is **`0xb08eea634e866a75`** today, and that is
byte-for-byte the constant the clients carry.

| # | Artifact | What it holds | Who regenerates it |
|---|---|---|---|
| 1 | `schemas/code.sigla` | the schema | a person |
| 2 | `clients/dotnet/Boxops.Fjord.Indexer/CodeIndex.cs` | `SchemaFingerprint`, 27 predicate constants, the `*Fact` helpers, and the field order | a person |
| 3 | `clients/dotnet/Boxops.Fjord.Demo/Program.cs:71` | the **same** fingerprint, restated independently on purpose | a person |
| 4 | `clients/dotnet/golden/blocks.txt`, `golden/unions.txt` | the checked-in golden bytes | `./clients/dotnet/emit-golden.sh` |
| 5 | `crates/fjord-client/tests/byte_identical_with_dotnet.rs` | the Rust-side schema and corpus, **stated independently** (`fn schema()` at `:44`) | a person |
| 6 | `crates/fjord-cli/src/sample_schema.rs` | the predicate count (`:169`), `KEY_ORDER` (`:245`, `src.Decl` at `:247`), the name lookups | a person |
| 7 | `clients/dotnet/glean/fjbench.angle` | the Glean translation of the same shapes | a person |
| 8 | `crates/fjord-viewer/src/query.rs` | five sigla queries over `src.*` | a person (R4 only) |
| 9 | `crates/fjord-cli/src/workload.rs`, `examples/loadgen.rs` | the workload's predicates | a person (R4 only) |
| 10 | `crates/fjord-cli/tests/{cli,over_a_server}.rs`, `crates/fjord-viewer/tests/over_a_real_index.rs` | path constants and expectations | a person |
| 11 | `website/content/*.md`, `scripts/bench.sh:35`, `clients/dotnet/*.sh` | example commands naming the schema | a person |
| 12 | the 61 files matching `git grep -l 'src\.Decl'` | R4's migration | a person |

## The order — and the cycle inside it

```
1. edit schemas/code.sigla
2. fjord schema fingerprint schemas/code.sigla          → the new number
3. paste it into CodeIndex.cs and Demo/Program.cs       (two independent restatements)
4. ./clients/dotnet/emit-golden.sh                      → golden/blocks.txt, golden/unions.txt
5. cargo test -p fjord-client byte_identical unions_are_byte_identical
6. sample_schema.rs — count, names, KEY_ORDER
7. clients/dotnet/glean/fjbench.angle
8. (R4 only) the 61 files, the viewer's five queries, the workload
9. cargo test && cargo +1.97.1 clippy --all-targets --workspace -- -D warnings
```

**Step 5 depends on step 4, and step 4 needs a .NET SDK that the gating job does not have.** The
`test` job — the required check — installs no .NET; `package` installs one and runs no tests. So a
Rust test asserts a fingerprint only a .NET run can produce, and the .NET run is a person's laptop.
**R0.5's recommendation (fold `setup-dotnet` + `dotnet test` into the `test` job) closes this too**,
and is the reason to prefer it over a separate job.

## Acceptance criteria

1. **The inventory is executable.** `scripts/flag-day.sh` (or a numbered checklist in
   `clients/dotnet/README.md`) walks steps 1–9 and **fails loudly** at any step whose artifact is
   stale — in particular, it must fail if `golden/blocks.txt` was not regenerated.
2. **A stale constant is a red suite, not a runtime refusal.** A test asserts that
   `fjord schema fingerprint schemas/code.sigla` equals the constant in `CodeIndex.cs` (parsed out
   of the C# source, which is a grep, not a build). Today nothing does, and the first sign of a
   stale constant is a refused handshake at somebody's site.
3. **A version bump rides with the constant.** Because the whole-schema claim is what a client
   sends, a moved fingerprint is a client release: the .NET package version is bumped in the same
   commit that re-pastes the constant, and the refusal an un-upgraded client gets is asserted by a
   test (`a_client_carrying_the_previous_fingerprint_is_refused`) so the failure is the designed one
   and not a surprise.
4. **Both flag days are rehearsed before either is landed** — on a branch, end to end, with the
   suite green — because the expensive failure here is discovering step 4 after step 8.
