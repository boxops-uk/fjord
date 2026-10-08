# Connection handover — an authorising proxy that leaves the data path

| | |
|---|---|
| **Issue** | [#89](https://github.com/boxops-uk/fjord/issues/89) — **its body still describes rev1's grant design and links the deleted file; repointing it is part of the first PR that lands** |
| **Area** | `fjord-server` (`server`, `admission`, `session`, `stats`), `operations.mdx` |
| **Invariants** | **`ops-I10`** (the transport is the trust boundary) and **proposed `ops-I11`** (a principal is never content) — this revision exists because rev1 collided with the second and did not know it. `ops-I1` untouched: the proxy never opens the store. `ops-I2`/`ops-I6` are where policy is evaluated |
| **Format** | unchanged |
| **Protocol** | **unchanged** — no Fjord frame moves, no `ErrorCode` is added, and no Fjord client library changes. A *preamble between an application and its own proxy* is a contract outside Fjord; see Shape A |
| **New dependencies** | none. `libc` is already a dependency, used for `getrlimit` (`admission.rs:209`) and the accept-time errnos (`admission.rs:98`) — but nothing in the workspace uses `AsyncFd`, `recvmsg`, `sendmsg` or descriptor passing today, so "no new dependency" means the crate need not be added, **not** that the mechanism is proven here |
| **Supersedes** | `connection-handover-rev1.md`, deleted rather than kept beside this file |

## What changed from rev1, and why

rev1 was written in September and filed as #89 three releases later. Its mechanism survived
review intact; its **trust story did not**. The substantive change is one idea:

> **The proxy attests. It does not authorise.**

rev1 had the proxy send a *grant* — "may open database X in mode Y" — which Fjord enforced.
That makes the proxy a second source of authorisation truth, and `PLAN.md`'s authentication
design (proposed `ops-I11`) rules it out in as many words: *"a forwarded identity is one the
server takes on trust from a hop it cannot verify"*. rev1 claimed `ops-I10` was "strengthened
rather than bent" without citing or engaging that section at all.

Also changed: the refusal shape is settled rather than asserted, **and tied to a placement
without which it does not hold**; the startup frame has five fields and not four; the latency
criterion named the wrong instrument; `§F8` is not a `§`; and the "one intrusive edit" is both
smaller than rev1 said (in the trait) and larger (in `session::serve`, which is public).

Two of rev1's claims this document previously "corrected" were **right, and the correction was
wrong** — rev1's reading of the refusal path, and the extent to which the TCP door is already
exercised by tests. Both are restored below, in their place.

## The problem

A deployment that wants per-database authorisation puts a proxy in front of Fjord — which is
what `operations.mdx:83-85` prescribes:

> **Unix socket only, by default** (`ops-I10`). `--listen-tcp host:port` opens TCP. That is
> reachability, not access control: the handshake accepts anonymous, so put a gateway in front.

The proxy terminates TCP, authorises, and relays to Fjord's Unix socket — and then sits in the
data path **for the life of every connection**. It costs a hop and a copy on every frame, in
both directions, for a decision it made once at connect. A query answering ten thousand rows
pays the proxy ten thousand times for one authorisation.

## The mechanism

`SCM_RIGHTS`. The proxy already holds a Unix socket to Fjord; instead of relaying bytes over
it forever, it **passes the client's socket file descriptor** over it once:

```
client ──TCP──▶ proxy          proxy ──sendmsg(SCM_RIGHTS: fd, attestation)──▶ fjord
                                         (proxy closes its fd and forgets)
client ◀────────────── TCP ──────────────────────────────────────────────────▶ fjord
```

The kernel shares the socket object rather than copying it, so nothing about the TCP connection
changes from the client's side — same sequence numbers, same window, no reconnect. The proxy is
not a faster relay; it is **not a relay**. No extra hop, no second copy, no per-connection
memory, and the proxy can be restarted without dropping live sessions.

**And `ops-I10` comes out ahead, not merely intact.** The proxy owns the TCP listener and
passes descriptors in, so Fjord itself never needs `--listen-tcp`: the door that the invariant's
whole safety argument asks you to leave shut *stays shut*. A relaying proxy today connects to a
Unix socket, so this is not a regression either way — but it means handover is reachable by a
deployment that has never opted into the TCP door, which rev1 did not notice it was offering.

## Why this server takes it cheaply

`Accepting` (`server.rs:264-275`) is already the seam, and `accept_loop` (`server.rs:305-358`)
is generic over it — admission, refusal, accept-error backoff and task spawning are all
transport-agnostic already. **A handover listener is a third implementation** whose `accept` is
a `recvmsg` returning a received descriptor instead of a real accept. A received TCP fd becomes
`tokio::net::TcpStream::from_std`, which splits into the same
`tokio::net::tcp::Owned{Read,Write}Half` pair the `TcpListener` impl already yields
(`server.rs:288-297`), so everything downstream is untouched.

tokio does not expose ancillary data, so the control socket is a
`std::os::unix::net::UnixStream` driven through `tokio::io::unix::AsyncFd` with
`libc::recvmsg`.

The client needs **nothing**: `Transport` is already `Unix | Tcp`, the client is holding an
ordinary TCP socket, and it never learns the other end changed process.

**Correction to rev1.** `Startup` has **five** fields, not four
(`crates/fjord-wire/src/protocol.rs:351-379`):

```rust
pub struct Startup {
    pub version: u32,
    pub database: String,
    pub mode: Mode,
    pub schema_fingerprint: u64,
    pub predicates: Vec<(String, u64)>,
}
```

`predicates` arrived later and carries `I13`'s actual rule, **subset containment**, so
`handshake` performs *two* schema refusals and not one: fingerprint equality
(`session.rs:413`), then per-predicate containment (`session.rs:421-446`), each with its own
error. Nothing here changes that — but an implementer reading rev1's four-field shorthand would
have gone looking for one check, and as the refusal section below shows, *both* of them leak.

## The trap that decides the shape: who has read what

A socket cannot be handed over mid-protocol without also handing over **the bytes the proxy has
already consumed**. That is the trap in every fd-handover design, and it decides the shape.

### Shape A — hand over before any Fjord byte is read (recommended, unchanged from rev1)

The proxy authorises on transport facts alone and hands the fd over **before** the client's
`Startup` frame arrives. Fjord then reads `Startup` itself, exactly as today. Nothing has been
consumed; the handshake code does not move.

**What the proxy authorises on is the deployment's choice, and it is not free.** Peer address
costs the client nothing. A pre-protocol preamble, or a token exchange the proxy defines, costs
*the application* a change — it is a contract between an application and its own proxy, outside
Fjord and outside Fjord's clients. That distinction is why the header table's "no client change"
is a claim about Fjord's protocol and client libraries and not about every deployment, and it is
why **acceptance criterion 1 pins its fixture to peer-address authorisation**: an unmodified
.NET client is only a witness if nothing asked it to speak a preamble.

### Shape B — hand over after the startup frame

The proxy reads `Startup` to learn the database and mode, authorises *that*, and hands over the
fd **plus the frame bytes it consumed**. Fjord must then resume mid-stream.

Shape B needs no preamble at all, which is its whole appeal — but it puts a "here are the bytes
I already read" path into the session, and a bug there is a protocol desynchronisation rather
than a clean failure. **Build A; keep B in mind only if a deployment genuinely cannot change the
application and peer address is not enough.**

### What kills handover entirely

**A proxy that transforms the byte stream cannot hand over.** If it terminates TLS, it is in the
crypto path for the life of the connection and no descriptor passing can remove it. Handover
suits a proxy that authorises and forwards *verbatim*: plaintext on a trusted network, or TLS
terminated at an ingress ahead of it. This belongs in the book before someone tries it behind an
mTLS terminator and finds the latency unchanged.

## The reconciliation: bounded delegation, not a second authoriser

This section is why rev2 exists, and its first draft got the argument right by the wrong route.

`PLAN.md`'s authentication design proposes **`ops-I11`: a principal is never content.**
Authorisation is *configuration held by the server process*; identity is *attested*, held by the
peer. It defines one `Principal` with three attestors — `Peer { uid, gid, pid }` from
`SO_PEERCRED`, `Spiffe { id, expires_at }` from a verified X.509-SVID, `Token { subject }`
against a JWKS — plus `Anonymous`, deliberately in the enum so that "the port is reachable by
whoever can route to it" is a value a policy can refuse rather than an absence nothing can
express. It evaluates **authorisation at `(database, mode)` and no finer, once at handshake**.

And it rules out exactly what rev1 proposed:

> The server must be the terminator — a gateway that terminates TLS has consumed the
> certificate, and a forwarded identity is one the server takes on trust from a hop it cannot
> verify — so `ops-I10` is not reversed but made real: the trust boundary moves into the process
> that enforces it.

**rev1's grant is a forwarded authorisation decision.** Calling it a grant rather than an
identity ("may open X in mode Y", never "is authenticated") does not escape the objection; it
sharpens it, because a grant bypasses the server's policy altogether where a forwarded identity
would at least still be evaluated against one.

### What the answer is *not*

It is tempting to answer that this hop is *verifiable* where a TLS gateway's is not — the kernel
vouches for the peer of the control socket, `SO_PEERCRED` is `ops-I11`'s own first attestor. **That
argument does not work, and it is worth recording why so it is not reached for again.**

`SO_PEERCRED` vouches for the **asserter** — the uid, gid and pid of the process at the other end.
A JWKS signature vouches for the **assertion** — an external issuer signed "subject = S". The
server can check the second and cannot check the first; nothing outside the proxy vouches for
*"this connection belongs to subject S"*. And hop-verifiability cannot be the distinguishing
property in any case: a TLS-terminating gateway forwarding an identity over the same Unix socket
would have an equally kernel-attested uid, so the argument would admit precisely the hop
`PLAN.md` rules out. `PLAN.md`'s `Peer` attestor works because there the peer **is** the subject.
Under handover the peer is the proxy and the subject is someone else.

### What the answer is

**The trust is delegation, bounded by the server's own configuration, and never verification.**

- The kernel identifies the **attestor**, which is what makes a configured allowlist of
  attestors possible at all — that is all `SO_PEERCRED` is for here.
- The operator configures **which uid may attest, and for which subjects**. That is the
  load-bearing premise, not a consequence of one.
- Within that configured range the proxy is **believed**, and that range is the blast radius.
  Said plainly: a compromised proxy can act as any subject the operator let it name.

What this buys over rev1 is a bound that can be stated and audited. A grant could authorise a
`(database, mode)` pair **no server policy mentions at all**. A delegated subject can only
select among outcomes the operator has already written down, so the reachable set is the union
of the policies of the subjects that uid may assert. `ops-I11`'s *rule* — a principal is never
content, authorisation is the server's configuration — therefore holds unamended, and there is
one source of authorisation truth.

### The wire contract, stated so it cannot be read two ways

The handover message carries **an opaque subject name, and nothing else**. The server reads the
attesting uid from `SO_PEERCRED` itself and mints a **distinct** principal:

```rust
Attested { by: uid, subject: String }
```

It must **never** carry, or be able to construct, `Spiffe`, `Token` or `Peer`. Those are
variants the server would otherwise have *verified*, and a proxy that can serialise one has
forged a crypto-backed identity nobody checked — the forwarded identity this document exists to
prevent. Provenance lives in the type, so a policy can match on *how* a principal was obtained
rather than trusting that the right code path produced it.

This is a **fourth attestor**, and `PLAN.md` says "One `Principal`, three attestors" — so it is
an amendment, recorded where `PLAN.md:243` already keeps them ("**What this contradicts today**
(amendments to make when built)"), taken when handover's policy half lands. The rule holds
unamended; the enumeration does not.

### The degenerate case, named

An operator who genuinely wants the proxy to decide can express it: a policy that accepts
whatever subject the proxy names. That is a *configuration choice with a name*, and its bound is
still statable — the union above, with the union taken over everything. Naming it here is
cheaper than having it re-invented as a default.

### What this costs in sequencing, honestly

Handover's *policy* half depends on `ops-I11`, which is proposed and unbuilt. Its *mechanism*
half does not depend on it at all, and **the wins land with the mechanism, not with the policy**:
at PR 3 the per-frame hop and copy are gone, a proxy restart no longer drops live sessions, and
the proxy holds no per-connection memory. In that intermediate state the proxy still
authenticates at connect and enforces its decision by *declining to hand over* — which is
exactly the gateway `operations.mdx:83-85` already prescribes, minus the relay. Handover does not
block on an auth system, and it does not smuggle one in either.

## The refusal shape, and the placement without which it is a fiction

rev1 said a request outside the grant is "a refusal **by name**". `PLAN.md:231-232` says the
opposite, and says why: *"a database a principal may not see answers `UnknownDatabase`, not a
distinguishable refusal (anything else enumerates the catalogue)"*. A named refusal is an
enumeration oracle. **The split, which satisfies both:**

| The request | The answer | Why |
|---|---|---|
| A database the principal may not see | `UnknownDatabase` | Existence is the disclosure. This is already the answer for a database that is simply absent (`ServerError::Unservable { .. } \| NoDatabase → ErrorCode::UnknownDatabase`, `error.rs:189`), so the two are indistinguishable by construction rather than by care |
| `ReadWrite` against a read-only policy on a database the principal *may* see | `ModeRefused`, by name | The principal was already told this database exists, so naming the refusal leaks nothing — and an operator chasing a mode escalation needs to tell it from a typo. `ServerError::ModeRefused \| Sealed(_)` already map to one code (`error.rs:198`), so "sealed" and "read-only by policy" are indistinguishable too, which is the subtle case |

Both reuse existing variants — `UnknownDatabase = 2` and `ModeRefused = 4`
(`protocol.rs:393-426`) — so there is no new wire variant, and no change to the .NET client's
independently maintained enum. (A new variant would also have needed its own provoking test per
AGENTS.md. It would *not* have been a flag day: that is this repository's term for a
schema-fingerprint move, and `ErrorCode` discriminants are append-only.)

**The rule is only as good as where it is evaluated, and the obvious place breaks it.**
`handshake` binds the database at `session.rs:387` — before either schema refusal at
`session.rs:413` and `:421-446`, and well before the `ops-I2` sealed check at `session.rs:452-457`.
So a check placed beside the sealed check leaves a database the principal may not see
distinguishable from an absent one **four** ways:

1. `InUse`, if another process holds it or a copy into the root is still finishing — a
   *guarded* arm that fires before the `UnknownDatabase` fallback
   (`error.rs:183-187`, guarded on `source.is_locked() || CatalogError::NoStore`, against the
   fallback at `:189`), so the indistinguishability the table below relies on holds for an
   absent database and **not** for a locked one;
2. `SchemaMismatch { expected, actual }`, which hands back **its real fingerprint** to any probe
   that sends a non-matching non-zero one;
3. `SchemaNotContained`, which names its predicates;
4. a timing oracle — and worse than a side channel, free work: the comment at
   `session.rs:381-383` says a bind "walks the root's sidecars, and on a miss opens a store —
   replaying its journals, which is seconds on a large database", so an unauthorised peer can
   make the server do seconds of blocking work per probe.

**So there are two evaluation points, and that is the constraint on the signature:**

- **Visibility** is decided on `startup.database` — a string in the frame — **before the bind**.
  Nothing has been opened, so there is no fingerprint to leak, no predicate names, no lock
  status and no replay. `UnknownDatabase` is then indistinguishable from absent *by
  construction*, which is the only way acceptance criterion 2 can hold.
- **Mode** is decided after, beside the existing sealed check, where the database is known
  visible and `ops-I6` resolves the session's mode.

## Admission, which is where this quietly goes wrong

Connections handed over arrive **by a different door**. `Admission::try_admit` is called in
`accept_loop` (`server.rs:326`), so a handover listener going through the same loop inherits the
cap for free.

**rev1 was right about the refusal path, and this document was wrong to correct it.** Once
`Accepting::accept` has returned an owned `TcpStream` split into owned halves, every permitless
path already closes the descriptor by ordinary Rust ownership: `refuse` (`server.rs:390-415`)
takes both halves **by value**, and the no-refusal-budget path drops them at its `continue`
(`server.rs:331-334`). Both of those are already descriptors with no *connection* permit —
`try_refuse` takes a separate refusal permit — so there is nothing to generalise and nothing to
rewrite. rev1's "composes with `refuse()` as it stands" holds.

**The genuinely new hazard is one layer earlier, inside `accept`.** `recvmsg` installs the
descriptor in this process *before* `Accepting::accept` returns, so the window belongs to the
listener and not to the loop:

- a `recvmsg` that installs an fd and then returns `Err`, or whose attestation payload fails to
  decode, lands on `on_accept_error(..); continue` with **nothing holding the fd**;
- an `SCM_RIGHTS` array carrying **more descriptors than expected** leaves the extras orphaned
  even on the success path;
- a message discarded for any reason must close every descriptor it carried.

The rule is therefore: **take an `OwnedFd` at the syscall boundary**, before anything can fail,
and close every fd in any message that is not turned into a connection. After that the existing
paths compose unchanged. A peer that passes the attestor check can exhaust the server's
descriptors through the error path alone, which is why this is the hazard and the refusal path
is not.

**The proxy's descriptors and Fjord's are now two pools.** `Admission::from_fd_limit`
(`admission.rs:145`) derives the cap from *this* process's `RLIMIT_NOFILE` via `getrlimit`, so
it cannot know anything about the proxy's descriptor pressure. Handover moves where the ceiling
binds. On the measurement that motivates this: `F8` is **not** a numbered section — it is a row
in `bench/FINDINGS.md`'s appendix table of the eight original hypotheses (`FINDINGS.md:1641`),
resolved under "What is still open" (`FINDINGS.md:1510-1522`). rev1 cited it as `§F8`, which is
not addressable. The register is closed, so it is cited **for the lesson** — descriptor
exhaustion under flood is a real failure mode on this shape — and never for a figure.

## Stats, and what "operator-visible" actually costs

`ServerStats` (`stats.rs:84-103`) is a flat struct of relaxed `AtomicU64`s. A
`connections_handed_over` counter is the established pattern and costs nothing: a field, an
incrementing method, a `#[must_use]` accessor, and a call from the handover listener's admission
path.

**But that does not make it operator-visible, and rev1 implied it would.** `stats.rs:1-9` is
explicit that there is deliberately no exporter and no endpoint, because *"a `/metrics` listener
would be a second port on a server whose `ops-I10` safety argument rests on binding being
default-closed"*. The only `fjord.db.*` predicates are `List` and `Interning`, and connection
counters are not among them. Surfacing one means adding to
`crates/fjord-server/schemas/catalogue.sigla` — **a schema move, which AGENTS.md calls a flag
day**. Separate decision, not in this work item: the counter lands, the predicate does not.

## The signatures that have to change

**`Accepting::Context`, and it is smaller than rev1 said.** `Accepting` and `accept_loop` are
both **private** to `crates/fjord-server/src/server.rs`. The whole edit is one file: the trait
(`server.rs:264`), two impls (`server.rs:277`, `server.rs:288`), the generic fn and its bound
(`server.rs:305`, `server.rs:312`), and exactly **two** call sites (`server.rs:183` in
`Listener::run`, `server.rs:552` in `serve_on`'s `opted_in` future). No public API, nothing
outside this file. An associated type with `type Context = ();` on the two existing listeners is
right: a listener that carries no context should not have to say so at every call site.

**And it is larger than rev1 said, one layer down.** Getting a principal to the evaluation points
means `session::serve` (`session.rs:286`), which is `pub` and re-exported as `fjord_server::serve`
(`lib.rs:63`). rev1 presented the trait edit as the only intrusive change; threading context to
its destination is a public-API change too. It has no external caller today, so the compiler will
not catch a mistake there — review has to.

**`Session` itself does not move.** Its mode is resolved once and the struct is immutable after
(`session.rs:261-277`, `mode: startup.mode` at `session.rs:476`), which is what forces both
evaluation points inside `handshake` rather than anywhere later.

## Build order

Each lands on its own, green, reviewable in one sitting. PRs 1–3 are neutral to `ops-I11` and
deliver every win; policy arrives at 7.

| # | What | Depends on |
|---|---|---|
| 1 | `Accepting::Context`, `()` for both listeners. Pure refactor, no behaviour change. **Repoint issue #89's body at this document** while here | — |
| 2 | The handover listener: control socket, `AsyncFd` + `recvmsg`, `OwnedFd` at the syscall boundary, `TcpStream::from_std`, the control socket's peer uid checked against a configured allowlist, `--handover-socket` default-closed in the house shape of `--listen-tcp` | 1 |
| 3 | Every discard path closes its descriptors, with the descriptor-count flood guard, and the `connections_handed_over` counter | 2 |
| 4 | The .NET end-to-end: a proxy fixture, peer-address authorisation, a handed-over handshake and query | 2, 3 |
| 5 | The measurement: relaying proxy versus handing-over proxy, as a distribution | 4 |
| 6 | `operations.mdx`: the deployment, including the TLS exclusion | 2–5 |
| 7 | Policy at `(database, mode)` against an `Attested` principal, both evaluation points, the split refusal rule; the shared peer-credential helper extracted as `ops-I11`'s `Peer` attestor; `PLAN.md`'s amendment slot updated for the fourth attestor | `ops-I11` |

PR 2 is the one to keep honest: a control socket that accepts descriptors is an authority, so the
uid check ships **with** the listener, never after it. Note what that check is and is not — on
the *control* socket the uid authorises an **attestor**, where `PLAN.md`'s `Peer` attestor reads
the *data* socket's peer as the **subject**. The syscall is shared; the semantics are not. One
credential-reading helper answers the "two divergent checks" worry without making PR 2 wait on
`ops-I11`, which is why the extraction is listed at 7 and the check itself at 2.

## The ledger, and why there is no PR 0

An earlier draft of this document opened the build order with a gate change, on the reading that
AGENTS.md requires a pending `#[ignore]`d guard for every not-yet-built subsystem. **That
reading is wrong**: the obligation is scoped to invariants — *"Every invariant owns a guard test,
written up front"* (`AGENTS.md:95`) — and this document claims no new invariant. Every guard here
lands **green, with the code that satisfies it**, which is the normal path. Nothing blocks on a
gate change.

The finding that produced that draft is still real and worth recording, because it will bite
whoever first wants a pending guard in this area. `scripts/check-guards.py:111` parses the owner
as a decimal:

```python
GUARD = re.compile(r"^guard: (?P<claim>.+?), owned by Movement (?P<owner>\d+)$")
```

validated against `MOVEMENTS = range(0, 9)` with `CLOSED_MOVEMENTS = {0}` — `PLAN.md`'s Recursion
movements specifically — with every pending guard also matching the hardcoded `EXPECTED_GUARDS`
manifest (`check-guards.py:28-97`). So `owned by #89` does not merely fail validation, it fails
to parse, and the gate reports a guard that "does not name its owner". There is no non-Recursion
owner anywhere in the tree.

Admitting one is **eight sites, not three**, which is why it should not be done speculatively:
the regex (`:111`), the owner type in `check-guards.py:28` *and* `test_check_guards.py:20` (both
`tuple[int, str]`), the unconditional `int()` coercion (`:192`), the movement-range and
closed-movement branch (`:194-202`), the row formatter (`:233`, which would print
"Movement #89"), the two "neither form" diagnostics (`:180-188`), and the module docstring
(`:5-7`) — plus a rule for whether an issue owner is still *open*, which nothing checks today,
and an injectable parameter mirroring `movements`/`closed_movements` so
`scripts/test_check_guards.py`'s mutation controls can exercise it. Both scripts are required CI
gates.

The one case that would want it is PR 7's refusal guards, written before `ops-I11` exists. That
is `ops-I11`'s decision to make, not handover's.

**Whether handover earns a named invariant** is still open, and the candidate is sharp enough to
state: *a received descriptor is owned — every discard path closes it*. It has a mechanical guard
(descriptor count across a flood) and it is load-bearing under exactly the load the cap exists
for. Against: the operational registry's Guard column is sparse on purpose
(`invariants.mdx:428-431`), these are "about a *deployment* rather than a data structure", and
the next free number is `ops-I12` only if `ops-I11` lands first. Decide it at PR 3, with the
guard in hand — and note that deciding *yes* is what would make a pending guard, and therefore
the eight sites above, actually necessary.

## Acceptance criteria

1. **A handed-over connection is indistinguishable to a client.** The .NET client, which shares
   no constants with the Rust one, completes a handshake and a query over a connection it opened
   to a proxy that handed it over — asserted end to end, because "the protocol does not change"
   is the claim and a client that was never told is the only thing that can prove it. The proxy
   authorises on **peer address**, so nothing asked the client to speak a preamble.
   **The lift is one new thing, not two:** no proxy or relay fixture exists anywhere in this
   repository and that must be built — but the TCP door is *not* unexercised.
   `fjord_cli::testing::serving_on_tcp` (`crates/fjord-cli/src/testing.rs:157`) already serves
   both doors through the very `accept_loop` call site this work touches, and
   `the_same_question_answers_the_same_over_either_door`
   (`crates/fjord-cli/src/commands/query.rs:658`) already drives a real query over it. Extend
   that fixture rather than inventing one. What is genuinely missing is a TCP path in the *.NET*
   harness (`FjordServer.cs` launches `serve --ready-file` only) and any test of the
   `--listen-tcp` flag through the CLI.
2. **Policy is evaluated at two points, and the oracle is closed by the first.** Visibility on
   `startup.database` **before the bind**; mode beside the `ops-I2` sealed check after. A test
   per outcome: a database in policy opens; one the principal may not see answers
   `UnknownDatabase` **byte-identically to an absent one, with no bind having happened**; a
   `ReadWrite` request against a read-only policy is refused by name. The negative test matters
   most — probe an invisible database with a wrong non-zero fingerprint and assert the answer
   does not differ from absent. (With `ops-I11`.)
3. **The control socket refuses a peer that is not an allowed attestor** — its peer uid read and
   checked against configured configuration, provoked by a test connecting as another uid. This
   is self-contained and `ops-I11`-free. The *shared* credential-reading helper, and its reuse as
   `ops-I11`'s `Peer` attestor on the data socket, is PR 7 — because there the peer is the
   subject and here it is the attestor, and conflating them is the error this split prevents.
4. **The cap holds on the handover path, and every discard path closes its descriptors** —
   asserted by descriptor count across a flood, not by reading the code, and the flood must
   include `recvmsg` error and decode-failure paths and a message carrying more descriptors than
   expected. This is the one that would otherwise be found in production.
5. **A proxy restart does not drop live sessions** — the property that is not just latency: kill
   the proxy mid-query and the rows still arrive.
6. **The latency claim is measured, not asserted** — reported as a distribution rather than a
   mean. The win should be a constant per frame, so it should show hardest on small-row mixes.
   **rev1 named the wrong instrument:** `crates/fjord-cli/examples/loadgen.rs` queries
   `code.File`/`Decl`/`KindOf`/`Ref`/`Extends`/`Span` from `workload::catalogue()` and never
   touches `SearchByName`; §11's code-search workload lives in
   `crates/fjord-cli/examples/codesearch.rs`. Measure with **both**, and say which is which.
7. **The book gains the deployment** — a `### A proxy that hands over` subsection under
   `operations.mdx`'s existing `## Deployment` (`operations.mdx:107`), carrying the
   TLS-termination exclusion and the blast-radius statement, which are the two things that will
   otherwise be discovered the expensive way.

## Traps

- **Do not let the attestation become a grant again.** The moment the proxy's message says what
  the connection *may do* rather than *who it is*, `ops-I11` is gone and there are two
  authorisation paths. The pressure to do it will come from it being easier.
- **Do not let the proxy assert a variant the server would otherwise have verified.** A
  serialised `Spiffe` or `Token` on the control socket is a crypto-backed identity nobody
  checked. The message carries an opaque subject; the server mints `Attested`.
- **Do not place the visibility check after the bind.** It reads as the natural spot, beside the
  sealed check, and it silently reopens the enumeration oracle through the schema refusals — plus
  seconds of attacker-controlled work per probe.
- **Do not harden the refusal path and call the descriptor question answered.** It is already
  safe by ownership; the window is inside `accept`, between `recvmsg` and an `OwnedFd`.
- **Do not hand over a descriptor the proxy has written to.** Shape A's whole safety is that the
  proxy has neither read nor written a Fjord byte.
- **Do not assume `sendmsg` sends the fd promptly under load.** In-flight descriptors are a
  bounded kernel resource; a proxy that queues thousands of handovers against a stalled server
  holds both ends' descriptors. The control socket wants backpressure and a bound of its own.
- **Do not ship the control socket without its attestor check.** Anything that can connect to it
  can name any subject, which makes an unchecked control socket a larger hole than the TCP port
  `ops-I10` is careful to leave closed.
- **`ops-I1` stays the store's.** The proxy authorises; it never opens a database, and nothing
  here gives it a reason to.

## Not in scope

- **Authentication itself.** What the proxy does before it decides is the proxy's, and
  deliberately outside Fjord. `ops-I11` is cited here, not designed here.
- **Handing a connection *back*.** A session ends at the server; there is no path from Fjord to
  the proxy and no reason to build one.
- **Multi-server routing.** One control socket, one server. A proxy in front of several is the
  same mechanism repeated and needs nothing new here.
- **Operator-visible connection stats.** The counter lands; the `fjord.db.*` predicate that would
  surface it is a schema move and a separate decision.
- **Revoking a delegation mid-session.** Authorisation is decided once at handshake, so a live
  handed-over session outlives a change to the attestor's configured subject range — the same
  residual `PLAN.md` states for its own credentials, and bounded the same way, by a maximum
  connection lifetime if one is ever wanted.
