# Authorisation — Fjord terminates mTLS, and a principal is never content

| | |
|---|---|
| **Issues** | [#89](https://github.com/boxops-uk/fjord/issues/89) (socket handover, now a subsection rather than the subject). The authorisation story itself wants its own issue; see *Filing* |
| **Area** | `fjord-server` (`session`, `server`, `admission`, `catalogue`, `stats`), `fjord-cli` (`cli`, `config`, `commands/serve`), `operations.mdx`, `invariants.mdx`. **No format change. No protocol change.** |
| **Invariants** | **`ops-I10`** made real rather than reversed; **`ops-I11`** proposed and defined here; `ops-I1` untouched; `ops-I2`/`ops-I6` are where the mode half is decided |
| **New dependencies** | `rustls` 0.23, `tokio-rustls`, a crypto provider, `x509-parser` for SAN extraction, optionally the `spiffe` crate — **all in `fjord-server` only**, which cannot reach the WebAssembly build (see *What this cannot break*) |
| **Supersedes** | `connection-handover-rev2.md`, deleted rather than kept beside this file. Its mechanism findings are folded in; its trust apparatus is **deleted rather than defended**, because Fjord terminating mTLS removes the problem that apparatus existed to solve |
| **Relation to `PLAN.md`** | `PLAN.md`'s Authentication section is the design of record today. This document is the detailed plan that section should shrink to a pointer at. Where the two differ, the differences are listed in *What this changes in the roadmap* |
| **Review guide** | `.review/auth.html` — the concepts, the survey of other databases, and the choices with their costs |

## Why there is a new document rather than a revision of the handover one

The handover design (rev1, then rev2) kept running into the same wall from different angles. rev1
had an authorising proxy send a *grant* that Fjord enforced. rev2 replaced that with an
*attestation* and a configured delegation bound, which was sounder but still answered the
question "how much do we trust a hop we cannot verify?"

The question was wrong. **If Fjord terminates mTLS itself, there is no hop to trust.** The
client's certificate is verified by the process that enforces the policy, which is exactly what
`ops-I10` has always asked for and what `PLAN.md` already proposed. Everything rev2 built — the
`Attested { by, subject }` variant, the configured subject range, the blast-radius statement —
exists only to make a forwarded identity tolerable, and a forwarded identity is no longer needed.
So that machinery is deleted, not refined. What survives from rev2 is its mechanism work: the
`Accepting::Context` edit, the descriptor-ownership window, the pre-bind placement, and the
refusal shape.

## The problem

Fjord has no authentication. The handshake accepts anonymous, and `ops-I10` — *"no in-database
auth; the transport is the trust boundary"* — holds only because binding is default-closed: a
Unix socket unless somebody types `--listen-tcp`, and whoever opens TCP takes on the gateway in
front of it. What it does not answer is *who is at the other end and what may they do*.

There is no credential concept anywhere in the tree to build on. `Startup`
(`crates/fjord-wire/src/protocol.rs:351-379`) has five fields and spends **zero bytes** on
anything credential-shaped — no reserved range, no placeholder varint. The "reserved credential
slot" is a documentation-level phrase repeated in five places
(`fjord-server/src/lib.rs:40-42`, `server.rs:494-495`, `web/src/content/wire-protocol.mdx:85-90`,
`web/src/content/cli.mdx:64-65`, `fjord-client/src/address.rs:41-42`), asserting that there is
*room* to add this without moving `protocol::VERSION`. It is a promise, not a field, and this
design keeps the promise by needing no bytes at all.

## The rule: a principal is never content

**No principal, credential, role or grant is ever stored as a fact, in a sidecar, or anywhere
inside a database directory.** Authorisation is *configuration* held by the server process;
identity is *attested*, held by the peer.

Two properties of Fjord force this rather than merely favouring it:

- **A Complete database is unwritable** (`ops-I2`). A mutable principal namespace —
  `CREATE ROLE` — has nowhere to live. Postgres's answer is a cluster-wide shared catalogue;
  Fjord has no cluster.
- **Content identity is load-bearing.** A database has a fingerprint, and `ops-I4` builds on it.
  Principals as facts would make the identity of the data depend on who may read it. The test is
  stated as a guard: *ingest under a policy and the content identity is byte-identical to the
  same ingest with no policy.*

**This is a genuinely novel position, and the document must not pretend otherwise.** A survey of
eleven systems is in the review guide; the finding that matters here is that **every mainstream
client-server database stores principals inside the thing it protects** — Postgres in
`pg_authid` under `PGDATA/global` (cluster-wide, in `pg_basebackup`, *not* in `pg_dump`),
CockroachDB in `system.users` as an ordinary replicated SQL table, MySQL in the `mysql.*` grant
tables as InnoDB rows, MongoDB in `admin.system.users`, Cassandra in `system_auth.*`. So does
**etcd**, which is usually cited as the config-held counterexample and is not: its users and
roles live in its own backend store, and `--client-cert-auth` only supplies the identity of a
user that must already exist as stored data.

Even `pg_hba.conf`, the closest thing to this idea in wide use, only selects an *authentication
method* — the authorisation decision still falls through to the in-database grant system. **No
surveyed system makes configuration the entire authorisation decision with no catalogue
fallback.** Fjord would be first, and the honest framing is "unlike all of them", not "like
Postgres but stricter".

Two pieces of prior art argue *for* it rather than against:

- **CockroachDB is the cautionary tale.** Making principals ordinary rows bought uniform
  backup and restore semantics, and also produced a shipped bug (issue #20718, fixed by PR
  #104215) where a dropped user's live session kept inheriting the `public` role's privileges,
  because the session's notion of "does this principal exist" and the row's actual existence
  were governed by the same generically-consistent machinery as ordinary data.
- **ClickHouse is the natural experiment**, and its result is negative. It supports both
  config-file users and SQL-created users, the two stores cannot manage the same account, and
  its own documentation recommends migrating fully to one. Redis's hybrid has the same shape:
  `ACL SETUSER` without `ACL SAVE` silently vanishes on restart. **Config-as-source-of-truth
  only holds if nothing else is also allowed to be a source of truth** — which is the argument
  for keeping this rule absolute rather than adding an opt-in in-database mechanism later.

One objection does not apply to Fjord and should be named so nobody imports it: ClickHouse's
problem with config-held users is that **credential material** — password hashes — ends up in
the config tree and its backups. Fjord's configuration holds *policy and no secrets*, because
identity arrives in a certificate the client holds. There is nothing in the file worth stealing.

## Identity: Fjord terminates mTLS

`rustls` verifies that the client's certificate chains to a configured trust anchor and is
within its validity period. **It deliberately does not decide identity** — there is no SAN
matching and no authorisation in `rustls`, which is the application's job. So:

1. `WebPkiClientVerifier::builder(roots).build()` requires a verified client certificate;
   `.allow_unauthenticated()` is what permits anonymous, and is how an operator keeps today's
   behaviour.
2. After the handshake, `ServerConnection::peer_certificates()` yields the chain, leaf first.
   `CertificateDer` is raw DER, so the leaf is re-parsed with `x509-parser` and its
   **URI SAN** read — which is where a SPIFFE X.509-SVID carries `spiffe://<trust-domain>/<path>`.
   An SVID carries **exactly one** URI SAN and a validator must reject more than one.
3. That string becomes the principal. No rewrite language in the first version — but
   **budget for one**: Kafka's `ssl.principal.mapping.rules` is the only mature
   certificate-to-principal rewrite mechanism in the survey, and etcd's lack of one (exact CN
   match only) is a documented real-world sharp edge.

```rust
enum Principal {
    Anonymous,                       // deliberately a value, not an absence
    Peer { uid: u32, gid: u32, pid: Option<u32> },
    Spiffe { id: String },
}
```

`Anonymous` is in the enum on purpose: *"the port is reachable by whoever can route to it"*
becomes something a policy can refuse rather than an absence nothing can express. `Peer` is free
— `tokio::net::UnixStream::peer_cred()` is a **safe wrapper that already exists**, no `libc` and
no `unsafe` in our code; `std`'s equivalent is still nightly-only. A `Token` attestor against a
JWKS stays in `PLAN.md`'s order as a later step and would be *a new frame kind on stream 0
before `STARTUP`*, never a field appended to the startup payload.

**Revocation is a bounded staleness window, not revocation**, and that is the industry position
rather than a shortcut: `rustls` supports **CRL only** for client certificates, with no OCSP path
at all, so the accepted practice is short certificate lifetimes. SPIRE's convention is to rotate
at roughly half the TTL — note that half-TTL is **SPIRE's operational convention, not the SPIFFE
specification**, and the commonly cited few-hour ceiling is community practice rather than a
standard. Every surveyed database has a revocation residual and every one documents it:
Postgres's own manual says of a revoked privilege that *"this is not a completely secure way to
prevent object access"* for sessions already open. Fjord's residual is the same kind of thing,
stated the same way.

## Authorisation: `(database, mode)`, at two points, and no finer

The policy answers one question: **may this principal open this database in this mode?** It is
operator configuration, reloadable, and never inside a database.

**Granularity is a decision, not a limitation.** Every attempt in the survey to go finer cost
something real: Postgres's row-level security rewrites every query with an extra predicate, and
a policy function that is `VOLATILE` rather than `STABLE` silently degrades to a sequential scan;
MySQL's column or table grants force per-statement privilege checks for *every* connection, not
just the narrowly granted one; MongoDB declined entirely, stopping at the collection and pushing
field-level control into a hand-built `$redact` view. Per-predicate authorisation here would put
a principal into query compilation, and per-fact reopens `ops-I9` at ownership's price. Neither
is a gap.

**And it is evaluated once, at handshake — which needs defending, because nobody else does it.**
Not even etcd: its per-connection token model re-checks the token's `authRevision` on *every
single apply*, because its grants are mutable mid-connection and can go stale. Fjord can claim
once-at-handshake honestly for a reason that does not apply to etcd: **its databases are
immutable and its policy is reloadable configuration rather than writable state**, so there is no
mid-connection mutation for a re-check to catch. A policy reload applies to the next handshake.
Say that explicitly rather than implying it is how such systems normally work.

### The two evaluation points, which the refusal shape depends on

`handshake` binds the database at `session.rs:387` — **before** both schema refusals
(`session.rs:413` equality, `:421-446` containment) and before the `ops-I2` sealed check
(`session.rs:452-457`). A single check placed beside the sealed check would leave a database the
principal may not see distinguishable from an absent one four ways: `InUse` if it is locked (a
guarded arm at `error.rs:183-187` that fires ahead of the `UnknownDatabase` fallback at `:189`),
`SchemaMismatch` handing back **its real fingerprint**, `SchemaNotContained` naming its
predicates, and a timing oracle that is worse than a side channel — the comment at
`session.rs:381-383` says a bind *"walks the root's sidecars, and on a miss opens a store —
replaying its journals, which is seconds on a large database"*, so an unauthorised peer could
make the server do seconds of work per probe.

So:

- **Visibility** is decided on `startup.database`, a string in the frame, **before the bind**.
  Nothing has been opened, so there is no fingerprint to leak, no predicate names, no lock status
  and no replay.
- **Mode** is decided after, beside the sealed check, where the database is known visible and
  `ops-I6` resolves the session's mode.

| The request | The answer | Why |
|---|---|---|
| A database the principal may not see | `UnknownDatabase` | Existence is the disclosure, and anything distinguishable enumerates the catalogue. Already the answer for an absent database (`error.rs:189`), so the two are identical **by construction** — which only holds because no bind happened |
| `ReadWrite` against a read-only policy, on a visible database | `ModeRefused`, by name | The principal already knows this database exists, so naming it leaks nothing, and an operator needs to tell a mode escalation from a typo. `ModeRefused | Sealed(_)` already map to one code (`error.rs:198`), so "sealed" and "read-only by policy" are indistinguishable too |

Both reuse existing variants (`protocol.rs:393-426`), so **no new `ErrorCode`** and no change to
the .NET client's independently maintained enum.

### The catalogue must be filtered, and it is cheap

`fjord.db.List` would otherwise enumerate every database to anyone. The filter goes between
`registry.catalog().list()` (`session.rs:1668`, which fetches the whole unfiltered `Listing`) and
`Catalogue::materialise` (`session.rs:1679`) — or inside `listing_rows()` (`catalogue.rs:272`)
before any `Row` is encoded. Either way it is **at the `FactStore` seam, before anything the
executor can see**, so the hot loop pays nothing and a filtered catalogue is the only one a plan
ever runs against. This confirms `PLAN.md`'s claim rather than taking it on trust.

### Where the policy lives, and the flag discipline

There is already configuration-file machinery: `config.rs:98-163` defines a `File` struct with
`serde(deny_unknown_fields)`, read as JSON from `--config <path>` or `./fjord.json` in
`main.rs:238` before any subcommand dispatch. A policy has somewhere to live.

**But opting in must stay a flag, and that distinction is load-bearing.** `--listen-tcp`'s safety
is stated as the absence of any other route: *"there is no configuration file entry, no
environment variable and no 'listen on localhost by default' that could turn it on while nobody
was looking"* (`server.rs:488-492`). So: `--listen-tls` and `--policy <path>` are flags, default
closed, logged loudly in the banner the way `serve.rs:118-123` already logs TCP. The policy's
*content* is a file; the decision to have one is a flag.

**Name the bootstrap moment.** Kafka's lesson is that externalising ACLs does not remove the
chicken-and-egg problem, it relocates it to a static escape hatch (`super.users`,
`allow.everyone.if.no.acl.found`) needed at first boot. Fjord's equivalent is: with no
`--policy`, behaviour is exactly today's — `Anonymous`, everything visible. That is the bypass,
it is the default, and it must be *said* rather than left as an emergent property.

## Socket handover, rescoped to what has prior art

Handover survives, with a sharper boundary. The dividing line is **not** whether a middlebox does
authorisation — it is **whether it must consume the bytes**:

| What the middlebox needs | Hand over? | What Fjord gets |
|---|---|---|
| Connection facts only — source address, rate, concurrency | **Yes.** It reads nothing | Verified mTLS, straight from the client |
| The first bytes only — SNI, ALPN — inspected with `MSG_PEEK` | **Yes.** Peeked data stays in the receive queue | Verified mTLS |
| Decrypted application bytes — per-query limits, identity-keyed limits | **No** | — |

The first two rows are the well-precedented kind: systemd socket activation, and HAProxy, nginx
and Envoy's reload handoffs, all pass **listening** sockets or pre-authorisation connections.
Envoy's documentation is explicit that *"existing connections are not transferred to the new
Envoy process."* And handover makes Fjord strictly better off on `ops-I10`: the middlebox owns
the port, so **Fjord never needs `--listen-tcp` at all** — the door the invariant asks you to
leave shut stays shut, while Fjord still does verified mTLS. It also makes PROXY protocol
unnecessary, because the process holding the real socket can call `getpeername()`.

**The third row is declined, and the evidence is strong enough to state as a decision.**
PgBouncer shipped exactly this — the `-R`/takeover feature passed already-accepted connections
between instances over `SCM_RIGHTS` — deprecated it in PR #894 (2023) and **deleted it** in
1.26.0 (2026-09-23, PR #1581); `src/takeover.c` is gone. The maintainer's first listed reason:
*"It does not work with TLS connections. Most production PgBouncer setups use TLS connections in
some way."* Its third reason independently confirms this document's own trap from production:
*"Any data that's in the SBuf's iobuf is lost during takeover (this seems quite bad)."*

**kTLS would make it technically possible, and we tested that rather than arguing it.** A probe
on kernel 6.8.0-1061-aws established `TCP_ULP="tls"` with `TLS_TX`/`TLS_RX`, passed the
descriptor to another process over `SCM_RIGHTS`, closed the sender's copy, and the receiver read
and wrote plaintext while `tcpdump -A` confirmed the plaintext never reached the wire. It works
in both orderings and full-duplex, and the kernel source says why: the TLS context hangs off
`icsk->icsk_ulp_data` on the `struct sock`, not a per-fd or per-process table, so any new
reference carries the crypto with it. **It is still declined**, for three reasons: the identity
would be forwarded again, undoing the whole point of this document; TLS 1.3 `KeyUpdate` pauses
kTLS RX until userspace resupplies the key, so the backend is not a dumb plaintext consumer;
and nobody ships it for this purpose, so it is a research spike rather than a pattern. Recorded
because the result is worth having, not because it changes the decision.

No surveyed database hands over a pre-handshake descriptor to a server that then does its own
TLS. That is a **genuine gap in prior art for this specific mechanism** and the document says so
rather than letting the mTLS comparisons imply it has been validated.

## What this cannot break

**The WebAssembly build is structurally safe.** `scripts/build-wasm.sh` checks
`-p fjord-engine` and `-p fjord-schema` only; `fjord-engine` depends on `fjord-encoding`,
`fjord-schema` and `fjord-store` and has no edge, direct or transitive, to `fjord-server`.
AGENTS.md's module map forbids the reverse direction, and the workspace already sets
`resolver = "3"` specifically to stop feature unification leaking between unrelated parts of the
tree. So `rustls` in `fjord-server` cannot reach the browser build. The only way to break this is
a dependency edge running the wrong way, which the module map prohibits.

**The protocol does not move.** mTLS settles identity before frame zero, so `protocol::VERSION`
is unchanged and `decode_startup` keeps refusing trailing bytes. The acceptance test for that is
the one `PLAN.md` already names: the .NET client, which shares no constants with the Rust one,
still connects.

## What this changes in the roadmap

- `ops-I10`'s "reserved credential slot in the handshake" is **retired** — mTLS needs no bytes.
  Five documentation sites say it and all five change (listed in *The problem*).
- The "accepts anonymous" notes in `fjord-server` and the CLI become `Principal::Anonymous`, a
  value rather than an absence.
- `ops-I11` is added to the operational registry. Note the registry's Guard column is sparse on
  purpose (`invariants.mdx:428-431`) — but this one gets a guard, because
  `a_principal_is_never_written_to_a_database` is mechanical.
- `PLAN.md`'s Authentication section shrinks to a pointer at this document.
- **Handover is resequenced after mTLS**, not before it. Before mTLS it serves only the
  weak-identity deployments and needs a delegation story; after mTLS it is a clean transport
  optimisation with none.

## Build order

`PLAN.md`'s order was *principal → policy → mTLS → Workload API → connection lifetime*. This
keeps it and inserts handover late, where it is cheap and honest.

| # | What | Depends on |
|---|---|---|
| 1 | `Principal` exists, `Anonymous` and `Peer` only, nothing refuses anything. The five "reserved credential slot" sites retired. `ops-I11` and its guard in the registry | — |
| 2 | A policy that can refuse: `--policy`, loaded at `serve`, visibility **pre-bind** and mode at the sealed check, `UnknownDatabase` for the invisible, the catalogue filtered | 1 |
| 3 | mTLS: `--listen-tls`, default-closed on `--listen-tcp`'s terms, `Spiffe` from the URI SAN. Acceptance: `VERSION` does not move and the .NET client still connects | 1, 2 |
| 4 | `Accepting::Context` — one private trait, two impls, two call sites, no behaviour change (salvaged from the handover work, and useful on its own) | — |
| 5 | The handover listener: control socket, `recvmsg`, `OwnedFd` at the syscall boundary, `O_NONBLOCK` set explicitly, `TcpStream::from_std`, then the same mTLS path | 3, 4 |
| 6 | Every discard path closes its descriptors, with the descriptor-count flood guard, and a `connections_handed_over` counter | 5 |
| 7 | The Workload API and rotation; then connection lifetime, which is the revocation residual's bound | 3 |
| 8 | The book: `operations.mdx` gains the deployment and the exclusions | 2-6 |

## Traps

- **Do not let the policy become content.** The moment a grant is a fact, `ops-I11` is gone and
  the content identity of a database depends on who may read it.
- **Do not add an in-database principal mechanism later, even behind a flag.** ClickHouse ran
  that experiment: two stores that cannot manage the same account, and operators who lose track
  of which is authoritative.
- **Do not place the visibility check after the bind.** It reads as the natural spot, beside the
  sealed check, and silently reopens the enumeration oracle through the schema refusals — plus
  seconds of attacker-controlled work per probe.
- **`O_NONBLOCK` is a property of the open file description, not of the descriptor.** A received
  fd shares the sender's status flags, so whether it is non-blocking depends on the load
  balancer. Set it explicitly on the received fd regardless: tokio documents passing a blocking
  stream to `TcpStream::from_std` as *"always erroneous"*, with behaviour that may become a panic.
- **Do not harden the refusal path and call the descriptor question answered.** It is already
  safe by ownership — `refuse()` takes both halves by value and the no-budget path drops them
  (`server.rs:331-334`). The window is inside `accept`, between `recvmsg` and an `OwnedFd`.
- **`rustls` has no OCSP for client certificates.** Plan on short lifetimes, not on revocation
  infrastructure, and do not promise revocation anywhere in the book.
- **Half-TTL is SPIRE's convention, not SPIFFE's specification.** Cite it as practice.
- **An X.509-SVID has exactly one URI SAN.** More than one must be rejected, not have the first
  one taken.
- **Do not let a refusal distinguish itself.** Any new error on the visibility path is an
  enumeration oracle; the guard is that an invisible database and an absent one are
  byte-identical.

## Acceptance criteria

1. **A principal is never written to a database.** Ingest under a policy and the content
   identity is byte-identical to the same ingest with no policy — `ops-I11`'s guard, and the one
   that makes the rule mechanical rather than aspirational.
2. **An unauthorised database is indistinguishable from a missing one**, asserted against the
   leaky paths specifically: probe an invisible database with a wrong non-zero fingerprint and
   with a predicate set, and assert the answer does not differ from absent, and that **no bind
   occurred**.
3. **A mode escalation is refused by name**, and a sealed database and a read-only policy are
   indistinguishable from each other.
4. **The catalogue lists only what the principal may see.**
5. **The .NET client still connects at protocol version 2**, with and without TLS — the
   independent implementation is what makes "the protocol does not move" a fact rather than a
   claim.
6. **The control socket refuses a peer that is not an allowed attestor**, provoked by a test
   connecting as another uid, using `UnixStream::peer_cred`.
7. **A refused handover closes the received descriptor**, by descriptor count across a flood that
   includes `recvmsg` error paths, decode failures, and a message carrying more descriptors than
   expected.
8. **A handed-over connection is indistinguishable to a client**, with the proxy authorising on
   connection facts only. Extend `fjord_cli::testing::serving_on_tcp`
   (`crates/fjord-cli/src/testing.rs:157`), which already serves both doors — and which
   `the_same_question_answers_the_same_over_either_door` (`query.rs:658`) already drives a query
   through. What is genuinely missing is a TCP path in the .NET harness and a proxy fixture.
9. **The book gains the deployment and the exclusions** — no OCSP, no revocation, the
   decrypted-bytes row of the handover table, and the bootstrap default.

## Not in scope

- **Authentication mechanisms beyond mTLS and peer credentials.** A JWKS-verified token is a
  later attestor and a new frame kind, never a startup field.
- **Finer granularity than `(database, mode)`**, priced above and declined.
- **Revoking access mid-session.** Decided once at handshake; the bound is a maximum connection
  lifetime, and the residual is stated rather than closed.
- **In-database principals**, permanently, per `ops-I11` and ClickHouse's experiment.
- **Handing a connection back**, or multi-server routing.
- **Operator-visible connection stats.** A counter lands; a `fjord.db.*` predicate for it is a
  schema move and a separate decision.

## Filing

This document covers more than #89, which is specifically the socket-handover mechanism and now
corresponds to build-order steps 4-6. Two options, and the second is recommended: rescope #89 to
the whole authorisation story, or **open a new issue for authorisation and leave #89 as the
handover mechanism, blocked on it**. The second keeps #89's discussion intact and makes the
dependency visible. Either way #89's body needs rewriting: it currently describes rev1's grant,
its "refusal by name", and its "one intrusive edit", all three of which are now wrong, and links
a deleted file.
