---
title: Operations
description: The lifecycle, the ten operational invariants, ownership and locking, deployment shapes, and the operational gaps this design names rather than hides.
---

Operational rules live in their own namespace — always written `ops-Ix`, never confused with the
engine invariants. Individual commands reference them by number.

## The lifecycle

```text
   create ──▶ Writable ──▶ finish ──▶ Complete        (and Broken, for a failed one)
                  │                       │
            ingest, derive           read only, forever
```

| State | Means |
|---|---|
| **Writable** | Ingestion happens here. Many writers, one process |
| **Complete** | Sealed. Every open-for-write is refused at session establishment |
| **Broken** | A database whose build failed. Named so it is not mistaken for either of the above |

`create → ingest base → derive → finish` is the phase order (`ops-I8`). A deriver reads the frozen
base through a sealed snapshot and writes only derived predicates; prefix-disjointness makes
read/write disjointness **structural** rather than a convention.

## The ten operational invariants

| # | Statement |
|---|---|
| `ops-I1` | **Single-process store ownership.** A storage directory is opened by exactly one process; a running server owns every database under its root; no silent connect→open fallback. **A process, not a thread** — and that is what makes per-key exclusion inside the process sufficient |
| `ops-I2` | **Complete = immutable.** Once Complete, every open-for-write is refused at session establishment — structural, not per write |
| `ops-I3` | **Finish ordering.** Durable first, then flip the status via an atomic sidecar write as the **last** durable action. Never observable that metadata says Complete while data is not durable |
| `ops-I4` | **Reproducibility.** A database built twice from identical inputs is identical; identity is `hash(canonical schema, base facts)`. Timestamps and random ids are descriptive, never identity. Conflict handling is order-independent — strict reject, neither first- nor last-writer-wins |
| `ops-I5` | **One write funnel.** Every writer passes the same pipeline: schema-validate → dedup identical → reject same-key-different-value. **One pipeline, not one thread** |
| `ops-I6` | **Session modes.** A session declares `read-only` or `read-write` at open, resolved once against the database's status |
| `ops-I7` | **The filesystem is the catalog.** No manifest of databases; enumeration is a walk of the store root plus sidecar reads. Any index is rebuildable and never authoritative |
| `ops-I8` | **Derivation is phased.** create → ingest base → derive → finish |
| `ops-I9` | **No cross-database anything in P0.** No cross-database queries, stacking or ownership |
| `ops-I10` | **No in-database auth; the transport is the trust boundary.** No RBAC; authentication is the transport's job. Safe only because binding is default-closed |

Two of them are worth reading together, because a chain of reasoning that once connected them has
been explicitly **cut**: `ops-I4` (reproducibility) and `ops-I5` (one funnel) were both once read as
requiring a single writer thread. Neither does. `ops-I4`'s hash is a **multiset** over each fact's
logical form, so write order and writer count cannot move it, and `ops-I5` asks that there be no
path around the rules, not that one core apply them. What actually needed serialising was the
key-to-fact bijection, and it now has a mechanism of its own — see
[Storage](storage.html#the-other-half-one-key-names-exactly-one-fact).

## Ownership and locking

- A **root lock** gives one process the store root. A root held by something that is not listening
  on the socket is refused **by name** rather than opened — the message says both halves, because
  "something holds this and is not answering" is the genuinely confusing case.
- The **socket is the server-detection mechanism**, and there is no other autodetect. Its presence
  is what a client uses; its absence is what an offline lifecycle command uses.
- Inside the server, writers are excluded **per key**, not per database. What remains at the
  database level is the **seal barrier**: writers hold it shared, `finish` takes it exclusive, and
  that is `ops-I2`.
- `list` and `describe` take no lock and open no store (`ops-I7`), so they work while a server
  holds everything under the root.

## On disk

```text
<data_dir>/
├── fjord.sock                     # the socket; its presence ⇒ a server is here
└── <name>/<instance>/             # instance: a ULID
    ├── FJORD_META                 # the sidecar: temp + fsync + rename (ops-I3)
    │     name, instance, status, format version, schema fingerprint,
    │     content fingerprint (at finish), counts, size, created_at
    ├── schema/                    # the embedded canonical schema
    ├── keyspaces/<n>/tables/      # the LSM tables — where a sealed database's data is
    ├── <n>.jnl                    # the write-ahead journal, and a residual after sealing
    └── lock, version
```

The sidecar is the fast enumeration path; the embedded schema copy is the durable fallback **and**
the source a server reads a database's schema back from. The field list is fixed and deliberately
has no "externally modified" flag — there is no such concept (`ops-I6`) — and no provenance field
yet. Both are additions the versioned format can take later, which is what the format version is
for.

### What a `Complete` directory contains

`finish` runs, in this order: **fsync, flush every memtable to a table, merge, fsync again,
compute the identity, flip the status.** The flush is what makes the claim on this section true —
before it existed, whatever ingest had left resident was written nowhere but the journal, and a
sealed database served it from a memtable recovered at every open. 520,000 facts came to 1.2 MB
of tables and 73 MB of journal (`bench/FINDINGS.md` §20).

So after sealing:

- **the data is in `keyspaces/<n>/tables/`**, one table per tree where the merge could manage it;
- **a journal residual remains**, and it is not small. fjall reclaims a sealed journal only
  inside its own flush worker, above a threshold of its own, and exposes no way to ask; so the
  directory is bigger after this change than before it — 21% on the corpus above. That is the
  trade: a table-backed read path against a larger artifact, and the read path is the one paid
  for on every query forever (`compact` prices a re-seek into an unmerged tree at up to 180×).
- **`FJORD_META.bytes` is the on-disk size of the instance directory at the moment of sealing**,
  journal included. Not a logical fact count, and not a promise about later: it is measured after
  the final fsync, which is the only moment the number is both honest and stable.

:::warn Never remove a `*.jnl` from a sealed directory
Not as a tidy-up and not behind a flag. Recovery is the backend's, and a database whose journal
was deleted from underneath it has an undefined next open. If the residual matters for your
packaging, copy the tables and re-ingest, or wait on the upstream request §20 names.
:::

## Running a server

```bash
fjord --data-dir /var/lib/fjord serve --ready-file /run/fjord.ready
```

- **Binds a Unix socket only, by default** (`ops-I10`). TCP is `--listen-tcp host:port`, with no
  config-file entry and no environment variable, so a port can only appear because somebody typed
  one. It is an opt-in to *reachability*, not to access control: the handshake accepts anonymous,
  so whoever passes it is taking on the gateway in front of it.
- **`--ready-file` appears after the listener accepts.** The socket path is derived rather than
  chosen, so the file only has to appear — but it has to appear *after*, or it is a race dressed as
  a signal.
- **Connections are capped, and the cap defaults to half the soft descriptor limit**
  (`--max-connections`). Descriptors are the resource a connection and the store compete for, so
  the half that is not spent on sockets is what keeps the store readable and the connections
  already accepted answerable. Past the cap a connection is told `Busy` and closed — the one
  refusal that arrives before the handshake — and past the small budget for *saying* so, the
  excess is closed without a word.
- **An `accept` that fails never ends the server.** `EMFILE` is a statement about the process,
  and a loop that propagated it would drop every live connection in order to refuse one new one.
  Descriptor exhaustion is logged, counted and backed off by 50 ms; every other accept failure
  ends that connection alone. What this does *not* do is reserve a place for a caller who has not
  arrived: a flood that fills the cap is refused by name, and so is the next admin query. The
  descriptors it reserves are for the store and for the connections already being served, and
  offline `list`/`describe` (`ops-I7`) need no server at all.
- **A stream is a task, and one fair writer drains them round-robin**, so a long query does not
  delay a short one on the same connection.
- **Blocking work runs on a blocking pool**, never on the reactor.
- **Every query carries a rows-examined ceiling**: 64,000,000 rows by default, charged per row
  pulled off a scan, on the query and count paths alike. It is the one limit on *input* — a scan
  whose filters reject every row produces nothing while doing all the work, so a limit counting
  rows answered would read zero on exactly the query that needs stopping. A listener may set a
  tighter one. It refuses a run and never changes an answer, so it is policy, not semantics.
  See [Query efficiency](query-efficiency.html#the-ceiling-is-deployment-policy).

### Systemd, roughly

```text
[Service]
ExecStart=/usr/local/bin/fjord --data-dir /var/lib/fjord serve \
         --ready-file /run/fjord/ready
Restart=on-failure
```

There is nothing to configure beyond the root: the socket derives from it, and clients find it
without being told where the data is. Access control is the socket's permissions.

## Deployment shapes

### Build in CI, serve elsewhere

The workflow the design assumes is **a fresh sealed artifact per build**:

```bash
fjord --data-dir ./out create code --schema ./schemas/demo.sigla
fjord --data-dir ./out serve --ready-file ./ready &
# … a producer writes facts over the socket …
fjord --data-dir ./out finish code
```

Lifecycle commands work with **no server running** — that is the amendment the offline path exists
for. A reader always goes through a server.

Then the artifact is a directory: `tar` it, publish it, and unpack it into a store root on the
serving side. There is no registration step, because the filesystem is the catalog — a server
resolves every bind against the disk, so an instance that appears under a live root is bound by the
next session that asks for it, with nothing restarted and nothing told.

#### Publish by rename — required for a live root

That same property is why **the instance directory must appear all at once**, and for a root a
server is serving this is a requirement and not a preference: copying into place can *destroy the
artifact you are publishing*. Unpack or `rsync` into a staging path *inside the same store root*,
then `mv` the finished instance directory into `<root>/<name>/<instance>/`:

```bash
staging="$root/.staging-$instance"                   # dot-prefixed: the scan ignores it
mkdir -p "$staging" && tar -C "$staging" -xf code.tar
mkdir -p "$root/code"
mv "$staging/$instance" "$root/code/$instance"       # one atomic rename
rmdir "$staging"
```

A rename within a filesystem is atomic, so a session either does not see the instance or sees all
of it. **This is what the tool itself does**, not an extra demand on operators: `fjord create`
builds a database in a `.create-<ulid>/` scratch inside the root and moves the finished instance
directory in under one `fs::rename`, which is the reason a killed `create` leaves no half-built
database behind. An `rsync` straight into the root is the odd one out.

Copying straight into `<root>/<name>/<instance>/` instead publishes the directory at its first byte
and leaves it half a database for as long as the copy runs: the sidecar and the store files arrive
in whatever order the copy chose, and a bind landing in between finds a database that records facts
it cannot read. What such a bind gets depends on how far the copy has got, and the second half is
the one that costs an artifact:

- **Before the store's own marker file lands**, the bind is **refused**: it names the instance,
  says the store has not arrived, and answers the retryable `InUse` rather than "no such database".
  Nothing is written into the directory. This is an outage for every session that asks during the
  copy, and nothing worse.
- **After it lands and before the last of the store's internal manifests does**, the bind *opens*
  the directory, and the open is a recovery: the storage engine treats a part-delivered internal
  keyspace as one it never finished creating and **deletes it**, files the copy had already sent
  included. The bind is then refused — the sealed sidecar records a fact count, the recovered store
  holds fewer, and they are compared — but the refusal comes after the delete. The copy then
  finishes, having sent every path it owed, and the published artifact is **permanently
  unopenable**: a fresh reader of it fails on a file that is no longer there. Republishing is the
  only repair.

The fact-count check is why a *wrong answer* is no longer among the outcomes — before it, that bind
was served `READY` on a `Complete` database answering zero rows, for the life of the process, while
`fjord.db.List` went on reporting the count the sidecar records. It is a refusal and not a fix: the
comparison can only be made once the store is open, and opening it is what deleted the files.

Two details make the staging path work. It must be **under the store root** so the `mv` is a rename
and not a copy; and it must start with a dot, which is what keeps the scan from reading a
half-written sidecar out of it (`.` names are the catalog's own — the root lock and `create`'s
scratch are two more).

### Scaling readers

A server owns its snapshot. Horizontal scaling is **more processes, each with its own copy** of an
immutable Complete database. That is what `ops-I2` plus tar-able directories buy, and it is the
place the design is most exposed: a copy per reader multiplies the artifact rather than amortising
it, and shipping bytes is a real cost. It is recorded as the honest counterweight to the "no
cross-database anything" rule.

### Freshness

A Complete database is immutable, so "freshness" is **how often a new one is built**. That makes it
an indexing-throughput question rather than a serving one, and it is why write throughput is
measured but not yet targeted.

## Backup and restore

Not built as commands, and mostly not needed as ones:

- **Backup is a tar of the directory, Complete databases only.** A file-level copy of a Writable
  database is unsafe under single-process ownership and explicitly out of scope. Include the
  sidecar.
- **Restore unpacks to a staging path and renames the instance directory in** — the shape
  ["Publish by rename"](#publish-by-rename-required-for-a-live-root) describes, and for the same
  reason: no registration step exists, so an instance directory becomes visible the moment it
  appears, and one that appears a file at a time is a database nothing can serve until the last
  file lands — and, past a certain point in the copy, one a bind will damage beyond repair.
  Validate that the sidecar parses, the status is Complete, and — if you recorded it — the content
  fingerprint matches, before the rename rather than after it.
- The same mechanism serves the future copy-on-start reader-scaling mode.

## Gaps this design names

Each of these is a *specified* absence rather than an oversight. They are listed here so an
operator is not surprised by one.

| Gap | Where it stands |
|---|---|
| `fjord write` from files | Unbuilt. The file format, the block encoding and the sync-marker splitting rule are all defined; the pipeline is not wired to a command |
| `db verify` | Unbuilt. Recomputing the content fingerprint is cheap and specified. Structural at-rest validation (the two maps agreeing, scan order holding) is guarded at *write* time and nowhere after a crash-and-recover |
| Per-predicate statistics | Not recorded. `finish` is the natural place, and nothing feeds the reorderer's selectivity heuristic today — which is why it does not have one |
| Retention | No policy engine, and the workflow generates the problem: "a fresh artifact per run" means rebuilds accumulate and `db rm` is a manual verb. "Keep the newest *n* Complete instances" is the shape to build |
| Provenance and properties | No field records what a database was built from. Both are safe as descriptive metadata; neither must ever become a *functional* input, because the schema is embedded and identity is content |
| Per-stream flow control | Deferred. Bounded per-stream queues plus per-connection backpressure in the meantime |
| A resumable deadline | A timeout unwinds terminally rather than handing back a cursor — a mid-descent position is not representable in the token as it stands |
| A per-connection or per-user budget | The rows-examined ceiling is per *run*. Nothing sums a session's work, so a client sending many cheap-but-not-free queries is bounded only by the ceiling on each one |
| Admission control past the connection cap | Concurrent *connections* are capped and excess is refused by name. Nothing caps in-flight **queries**: they queue on the blocking pool as latency rather than rejection, which is what `bench/FINDINGS.md`'s F8 measured. A queue-depth limit and a wall-clock deadline are the two halves still missing |
| Authentication | None, by design (`ops-I10`). The handshake has no credential field at all |

## Two operational rules worth internalising

**Never reach past a server.** A bare name always means "ask the local server", and a failure says
what to do about it rather than quietly opening the directory — because a server may be holding it.
This is why there is no `--embedded` flag: under the address grammar, a path in the `where` slot is
a socket.

**Sealing is where the artifact's shape is decided.** `finish` merges every tree before it walks
them, which on a large database is tens of seconds of rewriting with nothing to show for it. That
is stated *before* the wait rather than explained after it — and it is the difference between a
reader seeking once per plan level per page and seeking up to 180× that.
