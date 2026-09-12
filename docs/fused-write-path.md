# The fused write path

**Measured:** 2026-09-12, on `0.4.0` (`43c6fb0`), release builds, 8-core linux ·
**Status:** the server's path. `intern_block` remains as the reference implementation
two guards compare it against.

Reproduce every number below with:

```sh
cargo run --release -p fjord-ingest --example write_allocations
```

## The question

The write path was suspected of unnecessary allocation and copying, and the read path of
being much better. Both are true, and the gap is larger than it looks from the code.

## What the write path did

Wire bytes became storage bytes through **three** representations, not two:

1. `block::decode_block` builds a `Vec<WireFact>` — a tree owning a `String` per string
   field, a `Vec<u8>` per bytes field, a `Box` per record and per nested reference.
2. `intern.rs`'s `resolve` walks that tree to build a second one, the codec's `Value`,
   owning the same strings again.
3. `encode_key`/`encode_typed` walk *that* and produce the storage bytes. `index_key_for`
   then copies those a third time, to put the four-byte predicate tag in front.

Only the third is kept. The first two exist so the next stage has something to walk, and
are dropped the moment the fact is written.

## Baseline

Separating the hops, 2,000 facts:

| | wire bytes | allocations/fact | bytes/fact |
|---|---|---|---|
| `decode_block`, nested references | 96,544 | 4.0 | 283 |
| `decode_block`, references by id | 54,864 | 2.0 | 190 |
| `encode_key`, top level only | — | 3.0 | 63.5 |

**Decode alone allocates about seven times the bytes it is reading** — 380,890 from 54,864
on the wire — and every one of them is discarded.

Timing, from `fjord-cli`'s `examples/ingest` (24,100 facts, 108,100 interns):

| layer | ms | facts/s |
|---|---|---|
| `create` (intern + commit, no decode) | 146.8 | 164,126 |
| `block:create` (adds wire decode) | 188.0 | 128,179 |

**Wire decode is 41.2 ms of 188 ms — 22% of the cold path, 39% of the warm one.**

## The read path, for comparison

Measured the same way, over `FrozenStore` — the allocation-free store the engine's own I9
guard reads from, because `MemStore` clones a `Vec` per row and would report its own cost
as the engine's. 2,000 rows:

| | allocations/row | bytes/row |
|---|---|---|
| scan + count, nothing retained | **0.0** (13 in total) | 0.5 |
| scan + project a string | **1.0** | 104.8 |

So the read path is already at the floor: nothing per row while scanning, and exactly one
copy at the escape boundary, which is what I9 promises. The write path was doing
**23–29 allocations per fact** for the same shape of work — one fact in, one fact out.

## The walk

`crates/fjord-ingest/src/fused.rs`, ~300 lines. It walks the wire bytes and the declared
type together, writing storage bytes straight into a reused buffer, and hands the store a
`&[u8]`. It never builds a `WireFact` or a `Value`.

Two things shape it:

- **`FactSink::resolve_or_create` already takes slices.** The store never wanted an owned
  `Vec`, only somewhere to read from — so nothing had to change below the seam.
- **A nested reference is why this is not simply streaming.** A parent's key holds its
  target's `FactId`, and that id does not exist until the target has been interned. So the
  walk is depth first, and a child's bytes are built in a buffer taken from a pool while
  the parent's is half-written. The pool fills to the nesting depth and stops: the deepest
  chain a peer may send is bounded at 64.

## Results

4,000 facts, release, against real fjall stores. Both numbers include the store, which is
most of the work and identical either way — read the difference.

| shape | | allocations | bytes | per fact |
|---|---|---|---|---|
| by id | reference | 92,404 | 5,824,043 | 23.10 / 1,456 |
| | fused | 52,403 | 3,899,263 | 13.10 / 975 |
| | **saved** | **43%** | **33%** | 10.0 allocations a fact |
| nested | reference | 117,180 | 6,569,639 | 29.30 / 1,642 |
| | fused | 57,179 | 4,066,749 | 14.29 / 1,017 |
| | **saved** | **51%** | **38%** | 15.0 allocations a fact |

Time, best of five: **−14.9%** by id, **−15.9%** nested.

**And the walk itself allocates nothing per fact.** Against a sink that stores nothing,
1,000 facts cost 9 allocations and 2,000 cost 10 — the difference is the `Ingested::ids`
vector doubling, which is the caller's answer rather than the walk's cost. That is
`crates/fjord-ingest/tests/fused_is_allocation_free.rs`, a guard rather than an
instrument, using the N-and-2N technique I9's own guard uses.

So the 13–14 allocations a fact that remain are **the store's** — see below.

## Correctness

Two guards, and they are the reason `intern_block` still exists.

`crates/fjord-ingest/tests/fused_agrees.rs` runs both paths over the same blocks into two
stores and compares **every stored row, byte for byte** — key and value, not the ids,
which are allocation order and would agree trivially. It covers a scalar key, a record key
holding a reference, a nested record, a union with an empty arm and a scalar arm, a value
side, references sent as ids and as nested facts, and dedup counts.

It earned its place on the first run, catching a real bug: **a key's top-level record is
flat and a value's is not.** `encode_key` writes a key's fields back to back so a prefix
seek can reach them; `encode_typed` writes the value whole, wrapper and all. The walk
applied the key's rule to both and produced a value missing its record marker — which
would have been a silently different database, not a crash.

`crates/fjord-ingest/tests/fused_agrees_always.rs` draws the **schema** as well as the
facts. `arb_schema_and_fact` generates up to four predicates of records, unions,
references, ints, strings and bytes nested three deep, with union discriminants
deliberately out of declaration order and text drawn from the edges — the empty string,
embedded NULs, an emoji. Both paths ingest the same block into stores of their own and
every stored row is compared byte for byte.

The block holds the drawn fact **three times**, which is not padding: the first creates
and the rest deduplicate, so the paths have to agree about what was already there as well
as about what to write. A key one byte different would create three rows where the
reference path created one, and the counts would say so before the bytes did. A refusal
has to be a refusal on both sides too — a fused path that accepted what the reference one
rejects would write a fact the database has decided it does not hold, which is worse than
being slow.

Each case builds two real stores in two temp directories, so the case count is a budget
rather than a preference: **32 cases, 19 s**. `PROPTEST_CASES=512` raises it without an
edit. Mutation-checked twice: dropping a value's record wrapper, and resolving a union by
position rather than by discriminant. Both fail it, and it shrinks to a minimal input —
the second reports `UnknownDiscriminant(5)`, which is the generator's deliberately
non-positional tag doing its job.

### What it cost to trust it

**The first teeth-check passed, and that was my error rather than the test's.** I mutated
the value-framing line with a single-line string replacement, but `cargo fmt` had reflowed
that call across seven lines earlier in the session, so the replacement matched nothing
and I had "verified" an unmodified build. The lesson is to assert the mutation landed —
the corrected check prints the line it changed.

Chasing the apparent gap produced something worth keeping anyway.
`crates/fjord-ingest/tests/generator_census.rs` counts what the generator actually
reaches, because a property built on a generator that never produces the shape in
question cannot fail for the right reason. Over 500 draws:

| shape | draws |
|---|---|
| schemas holding bytes | 146 |
| schemas holding a union | 353 |
| schemas holding a reference | 278 |
| predicates whose key is a record | 885 |
| predicates with a value side | 625 |
| …whose value is a record — the shape the bug lived in | 442 |

Abundant, which is what disproved my hypothesis and sent me back to the mutation. The
census stays because the next person to add a property here will want the same reassurance,
and because a generator's reach changes when its weights do. It is also what earns the
low case count: 32 cases combining shapes the census proves are reached beats 512 cases
of an unexamined generator.

## Where the rest of it is, and what "the standard of the read path" means

`resolve_or_create` asked directly, with the key bytes already in hand. 2,000 facts:

| `resolve_or_create` | allocations/fact | bytes/fact |
|---|---|---|
| create | **13.16** | 866 |
| deduplicate, from cache | **1.00** | 28.4 |
| create, staged (one batch a block) | **10.17** | 906 |

**The store is already at the read path's standard for the common case.** One allocation
to resolve a reference that is already there is the same figure the read path pays to
project a string, and on a real index most references are exactly that — the write rung
measures 4.49 interns a fact at a 77.7% cache hit rate. Every one of the 13 is on the
**create** path.

So the target is narrower than "the write path": it is *creating* a fact. And the floor
is not zero — a cache that remembers a key has to own it — but one or two is reachable
where thirteen is not.

There is no cold-dedup row in that table. Nothing in the instrument empties the cache, and
a second handle on the same directory would fight `ops-I1`'s lock; the real cold figure is
`examples/ingest`'s `dedup:cold` rung, which reopens the database to get it.

### What has been done, and what is left

1. ~~**Land the fused walk.**~~ Done: `fjord-server`'s `write_block` calls `fuse_block` on
   both the unstaged and the staged branch, with one `Scratch` per block. That removes the
   10–15 allocations a fact *above* the store.
2. ~~**The index key was built twice per staged create.**~~ Half done, and the measurement
   is why only half. `stage_fact` now takes the index key by move from the caller that
   already composed it: **11.17 → 10.17 allocations a fact.** The same change to the
   unstaged `put_fact` measured 13.16 → 13.16 and was reverted — `intern` needs that key
   again afterwards, for the cache, so passing it down only trades a construction for a
   clone. The unstaged twin carries a comment saying so.
3. **Commit once per block, not once per fact.** The largest remaining term and the only
   one with a design question attached. `examples/ingest` prices it: committing is
   **54.1 ms of create's 146.8 ms**. `PLAN.md`'s 12f proposes it, `Staged` exists, the
   server already has `--commit-per-block`, and the instrument has the flag. What is not
   settled is whether it becomes the default, which is a durability question rather than
   an allocation one.
4. **The interning cache owns a copy of every key.** Unavoidable in shape, and it is per
   *distinct* key rather than per reference, so it is already amortised. Last.

## What a fused walk still owes

1. **Error fidelity.** `TupleEncoder::record` and `union` take a closure returning the
   codec's error, so an ingest error raised inside one is flattened to
   `IngestError::Codec`, losing its own words. The fix is to give the encoder a form
   generic over the caller's error.
2. **The `take_blob` duplication.** `fjord_wire::value::take_blob` is private and
   `fused.rs` reimplements it as `blob`. It should be exposed rather than copied — two
   readings of a length-prefixed blob is exactly the kind of pair that drifts.
3. **Keeping the reference implementation honest.** `intern_block` is now dead code in
   production and live only in the two differential guards, which is a stable arrangement
   only while those guards run. They do, in the ordinary suite, at 19 s. If that ever
   becomes too slow to keep, the answer is to delete `intern_block` rather than to stop
   comparing against it: two paths that must agree byte for byte and nothing checking is
   the situation that produced the hex/base64 bug.

## Caveats

- The fixtures are synthetic: a two-predicate schema with one reference, an int and a
  string, at 2,000–4,000 facts. The real corpus is 65 predicates with deeper nesting, and
  the ratio there is unmeasured.
- The timing is best-of-five in one process on an otherwise idle box, not a benchmark
  harness. The allocation counts are exact; treat the percentages as directional.
- `fjord-ingest` gained dev-dependencies for the instrument: the counting allocator, and
  `fjord-engine` plus `fjord-store`'s `proptest` feature for the read-path comparison. The
  engine edge is dev-only and creates no cycle — the engine does not depend on this crate
  — but it exists so that the read and write figures come out of one command, and it would
  be the first thing to drop if that stopped being worth it.
