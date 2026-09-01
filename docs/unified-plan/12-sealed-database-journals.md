# W12 · A sealed database is its tables — `finish` flushes what ingest left in memory

| | |
|---|---|
| **Issue** | [#43](https://github.com/boxops-uk/fjord/issues/43) — `[08]`; unrelated to the schema work and reviewable on its own |
| **Area** | `fjord-store-fjall` (`catalog.rs`, `store.rs`, `identity.rs`, `meta.rs`) |
| **Depends on** | nothing |
| **Blocks** | nothing. It **invalidates a published number** if the read-path benchmarks were measured on unflushed databases — see *What else this touches* |
| **Invariants** | `ops-I2`/`ops-I3` are the constraints; `ops-I4` is the guard that proves no fact moved |
| **Fingerprint** | none. Sealed identity **must not move** — that is criterion 1 |

## Claim

After `finish`, a database's data is in its tables; the artifact's size is its tables plus a stated
residual; and `FJORD_META.bytes` means what a packaging step needs it to mean.

## The cause is one missing call, and it is worse than the issue reports

`Catalog::seal` (`catalog.rs:815-846`) is the whole of finish's work on the store:

```rust
    // Durable first. …
    db.persist()?;

    // Then merge, and merge *here* …
    db.compact()?;

    let identity = identity::compute(db, schema, entry.meta.schema_fingerprint)?;
```

- `db.persist()` (`store.rs:571-575`) is `fjall::PersistMode::SyncAll` — it **fsyncs the write-ahead
  journal** and does not touch memtables.
- `db.compact()` (`store.rs:606-623`) is `major_compact()` per tree — it merges **already-flushed
  SSTable segments** and never sees a memtable.
- **`FjallDb::flush_to_tables()` exists** (`store.rs:1309-1324`, `rotate_memtable_and_wait` per
  tree) — and is `#[cfg(any(test, feature = "proptest"))]`, with a doc comment saying it is *"a test
  fixture, and it exists to make a guard non-vacuous"*.

So whatever is still resident in a memtable when `finish` runs is never written to a table, is
invisible to the compaction that follows, and remains only in the journal.

**Measured, on this tree**: 208,000 facts ingested over a socket into a fresh database, then
`finish`:

```
$ du -ah --max-depth=1 <instance>
1.2M  keyspaces        # of which the table files are  60 KB
 29M  0.jnl
 30M  total
$ cat FJORD_META
{ "status": "complete", "facts": 208000, "bytes": 30150886, … }
```

**60 KB of tables against 29 MB of journal.** The reporter's own case — 66 MB of tables against
105 MB of journal — is the same mechanism at a size where ingest happened to cross fjall's internal
flush threshold often enough to fill tables as well.

## The gate that is green while the claim is false

`sealing_merges_every_tree_into_one_table` (`crates/fjord-store-fjall/tests/finish.rs:514`) is the
guard for the merge claim, and it calls `db.flush_to_tables()` **itself**, four times, before
sealing — with an asserted precondition that some tree has more than one table, *"so this test would
pass without compacting anything"*. The precondition is honest and the test is a good test of
`compact`. What it cannot see is that **the production path never creates the state it sets up**.
This is exactly the class AGENTS.md warns about, and it is worth recording in the book beside the
fix.

## Three corrections to the issue

1. **`FJORD_META.bytes` is already the on-disk directory size**, not logical fact bytes:
   `record()` (`catalog.rs:850-867`) calls `identity::directory_size(&entry.path)`
   (`identity.rs:271-290`), which sums every file under the instance directory — journals included —
   *"Measured after the sync, so it counts what is actually there."* So the issue's *"`fjord list`
   already knows the honest number … a tool that trusts the metadata and a tool that trusts `du`
   disagree by 2.6×"* is backwards: both are on-disk numbers and they should agree. The reporter's
   `bytes: 212405684` (202.6 MiB) against `du` 170 M is a **third** thing to explain — most likely a
   journal reclaimed by fjall on a later open, *after* the size was recorded. Criterion 6 pins it.
2. **The ask "a second field for on-disk footprint" is already met**; what is missing, if anyone
   wants it, is the *logical* number.
3. **The journal is not load-bearing after `finish` in the sense the issue means** — but it is
   load-bearing *today*, because the data is only there. That inversion is the finding.

## What else this touches

- **Opening a sealed database replays the journal into memtables.** A 105 MB journal is 105 MB of
  recovery and resident memory at every open, per reader process — and `operations` §5 says a
  Complete database is the thing **copied per reader process**.
- **Reads may be served from a recovered memtable rather than merged tables.** `compact`'s own doc
  prices this: *"a re-seek into an unmerged tree was measured at up to 180× one into a merged tree —
  790 µs against 4.7 µs"*. Any read-path number measured on a sealed-but-unflushed database is a
  number measured against the wrong shape. **Which published figures this touches must be
  established, not assumed** (criterion 7).

## The work

**1 · Flush, in `seal`.** Promote `flush_to_tables` out of `#[cfg(test)]` (its doc comment changes
with it — it stops being a fixture), and order the seal:

```
persist()  →  flush_to_tables()  →  compact()  →  persist()  →  identity::compute()  →  record()
```

The trailing `persist` is what keeps `ops-I3` exactly as it reads: durable first, status flip last,
and nothing computed from bytes a power loss could take back.

**2 · Then measure what remains, and decide.** After the flush the journal's contents are redundant,
but fjall reclaims journals only inside its own flush worker: `JournalManager::maintenance()`
(`fjall-3.1.8/src/journal/manager.rs:115-166`) removes sealed journals once every keyspace's
persisted seqno has caught up, and it is reached only from the `Flush` worker-message handler
(`worker_pool.rs:150-206`), gated on a hardcoded 64 MB journal position. There is **no
`Keyspace`/`Database` API to force it**. So one of three, chosen on the measurement:

- **(a)** the residual is a bounded, small active journal — document it in `operations.md`, assert a
  bound, done;
- **(b)** the residual is material — open an upstream fjall issue for a public
  `seal_journals()`/`gc()` and pin the version once it lands;
- **(c)** neither — `finish` writes the sealed tables into a fresh instance directory, which is a
  copy and a much larger change. **Do not reach for (c) before measuring.**

**3 · Never delete `*.jnl` from underneath fjall.** Not as a workaround, not behind a flag. The
recovery path is fjall's and a hand-deleted journal is a database whose next open is undefined.

**4 · A stated artifact contract.** `operations.md` says what a `Complete` directory contains, and
`FJORD_META.bytes` gets one sentence: *the on-disk size of the instance directory at the moment of
sealing*.

## Acceptance criteria

1. **No fact moves.** The sealed identity of a fixture database is **byte-identical before and after
   this change** — `ops-I4`'s hash is over the facts, so this is the criterion that proves a flush is
   a flush. Assert on a fixture whose identity is currently recorded (`§15a`'s
   `0x462058b7b0671d29` construction, at test scale).
2. **The tables hold the data.** A new test — `sealing_leaves_the_data_in_tables` — ingests without
   any manual flush, seals, and asserts every predicate's table count is ≥ 1 and that the summed
   table bytes are within a stated factor of `FJORD_META.bytes`. **It must fail on `main`.** This is
   the guard the existing merge test could not be.
3. **The existing merge guard keeps its precondition.** `sealing_merges_every_tree_into_one_table`
   still asserts *"nothing to merge"* is false before sealing — it is now testing `compact` on top
   of a production path that also flushes, and its manual flushes stay, because a test that stops
   asserting its own precondition is the failure mode being fixed.
4. **The artifact ratio is asserted, with a number.** `a_sealed_artifact_is_its_tables` — after
   `finish`, `directory_size` is at most *N*× the summed table size, with *N* chosen from the
   measurement and written into the test's message. **fjall already exposes the instruments**, so
   nothing has to be measured by shelling out: `Database::journal_count()` (`db.rs:277`),
   `journal_disk_space()` (`:287`) and `disk_space()` (`:311`) — assert on the first two
   directly. This is the guard `ops-I2`/`ops-I3` never had: the `ops-I*` table in `invariants.md`
   has no Guard column, and this is its first entry.
5. **A sealed database reopens and answers identically.** Open, run a query battery, compare rows
   against the pre-seal answers — the flush must not change what is readable, only where it is read
   from.
6. **The reporter's 2.6× is explained, not merely fixed.** Measure `directory_size` and
   `journal_disk_space()` at three points
   — immediately after `record()`, after a close-and-reopen, and after a second reopen — and record
   which of them moves. If fjall reclaims on open, then `FJORD_META.bytes` **over-reports a sealed
   artifact** and either the measurement moves later or the field's meaning is documented as
   "at seal".
7. **The read-path exposure is established.** State, with evidence, whether the benchmark databases
   behind `§1`, `§2`, `§6` and `§11` were sealed on this path — and therefore whether any published
   read number was measured against an unmerged tree. If they were, they join R7's re-run list
   (W13); if they were not, say why (a large ingest crosses fjall's own flush threshold repeatedly,
   so a 25M-fact database is mostly tables regardless). **Do not assume the benign answer.**
8. **`finish`'s cost is re-priced.** `compact` is already 23 s on an 18M-fact index; the flush adds
   to it. Record the new figure in `bench/FINDINGS.md` — `finish` is the operation whose whole job is
   to say "this is finished", so the cost is acceptable and the number should still be public.
9. **The book.** `operations.md` says what a sealed directory contains and what `bytes` means (W10).
10. **The full gate**: `cargo test`, clippy/fmt, `check-guards.py`.

## Open question for review

**Should a logical "fact bytes" number join `FJORD_META`?** The issue asks for it in the belief that
`bytes` is already logical. It is not, so the question is whether a packaging step wants both.
Recommendation: **no** — one honest on-disk number is what a packaging step asserts against, and a
second number invites the two to disagree. Recorded as Q7.
