# Fjord — the roadmap

What is not built, what building each piece requires, and the record of decisions already
taken so they are not re-litigated. The design of record is the
[design book](website/README.md) — **published** as the interactive site
([`web/`](web/README.md), the pages with the engine running in them) at
<https://boxops-uk.github.io/fjord/> on every push to main, and shipped with each release as
an attested `fjord-docs-site.tar.gz` beside the binaries; the working contract is
[`AGENTS.md`](AGENTS.md); what has been measured is
[`bench/FINDINGS.md`](bench/FINDINGS.md). The history of how the system was built lives in
git, where it can be cited by commit.

**Definition of done, everywhere:** a task ends in a green test (prefer a property), and
every invariant a piece of work touches has its guard un-ignored and passing before the work
is done. When picking up an item, decompose it into task-sized leaves *at pickup* — early
decomposition is always wrong — each ending green, ordered by dependency and de-risking.

## What remains

| Work | State | Gated on |
|---|---|---|
| [File ingestion](#file-ingestion--fjord-write) | designed; format built and shared with the wire | nothing — the interning primitive it needed exists |
| [Stored derivation](#stored-derivation) | designed; two rules banked | the [re-derivation decision](#the-open-decision-re-derivation-vs-i11) |
| [The read-path benchmark](#the-read-path-benchmark-against-glean) | planned, with predictions | a quiet machine and the indexed corpus |
| [Authentication](#authentication) | design of record below; nothing built | wanting it |
| [The engine in a browser](#the-engine-in-a-browser--webassembly) | **the store split, `fjord-inspect`, `wasm/` and the lexer segment are built**; the remaining views are not | nothing |
| [Recursion](#recursion--query-local-relations-magic-sets-stratified-negation) | designed, then **amended after adversarial review** — the shape survived, its boundaries did not | [Movement 0](#movement-0--semantics-and-seams): eight semantics-and-seams decisions, four of them gating representation |
| [Operational gaps](#operational-gaps) | each named with the seam that keeps it cheap | — |
| [Language backlog](#language-backlog) | additive; none reshapes the machine | — |

---

## File ingestion — `fjord write`

**Goal.** Facts writable from files, in parallel, against one database:
`fjord write <db> [FILE...]`. A throughput feature — nothing in the lifecycle, the CLI or the
runtime waits on it.

**The design is already built where it matters.** The block format is shared with the wire
(`fjord-wire::block` — sync marker, magic, fixed-width header fields, CRC over header and
payload, the predicate **named** rather than numbered) and *a file and a socket carry the
same bytes* is a test, not an intention (`tests/one_encoding.rs`). The ten-`0xFF` sync marker
is unreachable inside a payload **by the encoding rather than by luck** — UTF-8 never uses
`0xF8`–`0xFF`, a varint's final byte is below `0x80`, and the header's `count`/`length` are
capped to keep a zero top byte — so a scan from any offset finds boundaries and nothing else,
and validation (magic, then CRC) is for torn writes and flipped bits, not disambiguation.
This splittability is a real advantage over Glean, whose binary `Batch` is one opaque
sequential blob that cannot be split, so it parallelises across batches and pushes the
chunking decision onto the producer.

**What is left is the pipeline:** the file envelope (header: magic, format version,
producing-schema fingerprint; optional footer of block offsets), the splitter (seek anywhere
→ scan to next sync → hand blocks to workers, checked from *every* offset of a multi-block
file), and a pool of workers that decode blocks and `intern_block` them concurrently.

**Two acceptance criteria are inherited rather than owed** — shuffle-invariance
(`writer_count_and_write_order_do_not_change_the_database`) and deterministic rejection under
any interleaving (`a_conflict_between_concurrent_writers_fails_exactly_one_of_them`) are
proven on the wire path, one layer down.

**Do not build the stratum optimisation first.** Intern-as-the-decode-reaches-each-fact is
correct under any number of workers; the sort-into-strata design survives only as a plausible
optimisation of fjall's bulk `ingest()`, untested against a pipeline nobody has written.
Reach for it if `examples/ingest.rs` says the write path is the ceiling, not before.

**Acceptance:**
- [ ] Facts are writable from files in parallel, and queried back.
- [ ] Ingest is order-independent: shuffling input chunks yields the same DB *or* the same
      deterministic rejection (tier-2 metamorphic).
- [ ] Same-key-different-value is deterministically rejected regardless of chunking and
      worker interleaving.
- [ ] One fact encoding, not two: a block is byte-identical on the wire and in a file.

---

## Stored derivation

**Goal.** `predicate P : … = KEY where <query>` as **facts written at build time**. The half
of "derived facts" that never reaches the executor — at query time `P` is facts in a
keyspace, scanned like any other predicate — so this is a *writer* and a *lifecycle* piece,
not a machine change. The schema grammar reserves `stored` and `derive`; the derivation body
is deliberately not in the grammar yet (`nyi/derivation`).

**Lifecycle design of record:** `ops-I8` — create → ingest base → derive → finish; a deriver
reads the frozen base via a sealed snapshot and writes only derived predicates;
prefix-disjointness makes read/write disjointness structural; embarrassingly parallel, no
stratification at first. Derived-on-derived comes later via sealed rounds — the shape to copy
is a per-predicate completion list in the sidecar plus a topological sort of the derivation
graph, computing round boundaries from the schema instead of asking the operator.

**Identity:** `ops-I4`'s hash is over the canonical schema and the **base** facts, so derived
facts are implied by identity, never part of it — re-deriving must be reproducible.

**Two rules are banked in advance, both learned from Glean's source and expensive to
rediscover:**

- **Write the query's *results*, never the body's output.** The reorderer is free to place
  the fact-producing statement above a later filter, and the fact set accumulated while the
  query runs then contains facts that are not true — the results are still correct, which is
  why the fix is to write the results and filter the fact set by them. A deriver implemented
  as "run the plan and dump what the body produced" is wrong in exactly this way, and the
  acceptance test below is shaped so it fails.
- **Negation in a stored derivation is legal here, and that is a one-way door.** Glean
  forbids it because facts derived from the *absence* of facts can be invalidated when an
  incremental database grows. Nothing can be added to a Complete database, so nothing can
  invalidate a derived fact — the ban is a cost of incrementality, not of derivation. Pinned
  by a test so a later reader does not "restore" the ban by analogy. The door: if `ops-I9`
  (no cross-database anything) ever reopens into stacking or incrementality, every stored
  derivation containing `!` becomes unsound.

### The open decision: re-derivation vs I11

**The one genuinely open decision in the project, and it gates this work.** Dropping a
predicate's two trees is O(1) and is what the physical layout was chosen for — but the
allocator's high-water mark is recovered from the last key of the very `entities` tree being
deleted, so the next write to that predicate restarts at sequence 1 and **reuses ids that
dependent predicates still reference**. The failure is a silently wrong answer, not an error,
so the rule must be decided before this work writes a derived fact. Two coherent answers:

- **Re-derivation produces a new DB.** Matches the immutable-artifact philosophy, needs no
  new machinery, and means a one-predicate fix rebuilds everything.
- **In-place, but bounded.** Legal only on a Writable DB, and only with the dependent subtree
  dropped alongside — which additionally needs the high-water mark to survive the drop, and
  the data-recovered mark cannot.

Anything more permissive — re-deriving under live readers — needs generation metadata,
dependent invalidation and generation-aware cursors, and "an O(1) tree delete" should never
be read as promising it.

**Acceptance:**
- [ ] A schema declaring a derived predicate parses, derives, and the derived facts are
      queryable exactly as base facts are — indistinguishable to the executor.
- [ ] `ops-I8` enforced and tested: a deriver cannot read its own writes or another
      deriver's, and cannot write a predicate it does not own.
- [ ] Deriving twice from the same base gives the same facts (`ops-I4`); re-deriving one
      predicate drops and rebuilds only its trees — under whichever re-derivation rule is
      chosen above.
- [ ] **Only the query's results are written** — tested with a plan the reorderer is free to
      schedule the derive above the filter in.
- [ ] A stored derivation containing `!` derives and is queryable.
- [ ] Ingest is refused after `derive` in a way the lifecycle defines, not by accident.

---

## The read-path benchmark against Glean

The write paths are measured and within 8% on equal footing
([findings §15–§17](bench/FINDINGS.md)); the read paths are not. The suite is
[`bench/glean-read-path.md`](bench/glean-read-path.md): sixteen query families over two rungs
(in-process, and over each system's wire), the same 18.3M-fact corpus both systems already
hold from one Roslyn walk, reporting **work done beside every timing** — Glean's
`facts_searched` against our `Profile.examined` — because that separates *did more work* from
*did the same work slower*.

Three predictions it exists to check, each from a design document rather than a hope: the
scan curve against database size (2.4 GB against 886 MB for the same facts); what a value
read costs us (a second point read per row, I6, against Glean's inline value — the sharpest
prediction); and what a missing feature costs (transitive closure as one recursive Angle
query against a client-side loop of round trips — the strongest argument for building
recursion).

It also closes a long-carried item: `bench/baselines/<host>.json` and a `--json` flag on the
instruments, so a number can be re-run rather than re-argued.

---

## Authentication

**Nothing here is built.** This is the design of record, condensed so the shape is argued
once; the guards are named so they can be written up front.

**The current state, honestly:** `ops-I10` — no in-database auth; the transport is the trust
boundary — holds by being default-closed: a Unix socket unless somebody types `--listen-tcp`,
and whoever opens TCP takes on the gateway in front of it. What it does not answer is *who is
at the other end and what may they do*.

**The rule that shapes everything** (proposed `ops-I11`): **a principal is never content.**
No principal, credential, role or grant is stored as a fact, in a sidecar, or anywhere inside
a database directory. Authorization is *configuration* held by the server process; identity
is *attested*, held by the peer. The Vault-against-Postgres lease pattern cannot port —
`CREATE ROLE` needs a mutable principal namespace, and principals-as-facts would enter
`ops-I4`'s identity while `ops-I2` makes a Complete database unwritable — but what the lease
*buys* (short-lived, automatic, never at rest) is delivered by attested identity, where the
issuer is external and the server only verifies: a trust bundle, a clock, a policy. Testable
as: ingest under a policy and the content identity is byte-identical to the same ingest with
no policy.

**One `Principal`, three attestors:** `Peer { uid, gid, pid }` from `SO_PEERCRED` on the Unix
socket (kernel-attested, no crypto — the server already receives and discards this);
`Spiffe { id, expires_at }` from the URI SAN of a verified X.509-SVID; `Token { subject }`
verified against a JWKS; and `Anonymous`, in the enum deliberately, so "the port is reachable
by whoever can route to it" is a value a policy can refuse rather than an absence nothing can
express. Because the verifier is written against a *bundle*, SPIRE and an OpenBao PKI engine
fill the same socket — the issuer is replaceable by construction.

**mTLS costs zero protocol bytes, and that is the argument.** The identity is settled by the
TLS handshake before the first frame; `protocol::VERSION` does not move, and
`decode_startup` keeps refusing trailing bytes. The server must be the terminator — a gateway
that terminates TLS has consumed the certificate, and a forwarded identity is one the server
takes on trust from a hop it cannot verify — so `ops-I10` is not reversed but made real: the
trust boundary moves into the process that enforces it. Bearer tokens, if ever wanted, are a
**new frame kind on stream 0 before `STARTUP`** — the precedent every protocol extension has
followed — never a field appended to the startup payload.

**Authorization at `(database, mode)` and no finer**, evaluated **once at handshake** — the
same place `ops-I6` resolves the mode and `ops-I2` refuses a write to a sealed database — so
nothing enters the executor. Operator configuration, reloadable, never inside a database. Two
consequences taken with it: a database a principal may not see answers `UnknownDatabase`, not
a distinguishable refusal (anything else enumerates the catalogue); and `fjord.db.List` must
filter by principal — cheap, because the catalogue is answered at the `FactStore` seam before
anything the hot loop can see. Per-predicate authorization is priced (a principal enters
query compilation) and not taken; per-fact reopens `ops-I9` at ownership's price and is
**not a gap**.

**Revocation, honestly:** an SVID lives an hour and a session may live longer; authorization
is decided once, so a live session outlives the credential that opened it. The design offers
a maximum connection lifetime and a time bound on the viewer's pool, and states the residual
as **a bounded staleness window, not revocation**.

**What this contradicts today** (amendments to make when built): `ops-I10`'s "reserved
credential slot in the handshake" is retired — mTLS needs no bytes and a token needs a frame
kind — and the "accepts anonymous" notes in `fjord-server` and the CLI become
`Principal::Anonymous`, a value rather than an absence.

**Build order, if built:** a principal exists (SO_PEERCRED, nothing refuses anything) → a
policy that can refuse (loaded at `serve`, evaluated once, `UnknownDatabase` for the
invisible, the catalogue filtered) → mTLS (`--listen-tls`, default-closed on `--listen-tcp`'s
terms; acceptance: `VERSION` does not move and the .NET client still connects) → the Workload
API (rotate in place at half TTL) → connection lifetime. Guards to write up front:
`a_principal_is_never_written_to_a_database`,
`an_unauthorised_database_is_indistinguishable_from_a_missing_one`,
`the_catalogue_lists_only_what_the_principal_may_see`,
`the_dotnet_client_still_connects_at_protocol_version_2`.

---

## The engine in a browser — WebAssembly

**Built, end to end through compilation.** The store split is done,
`fjord-inspect` holds the token, parse-tree, lowered and plan views, `wasm/`
builds a 280 KB module (117 KB over the wire), and `web/` is a React site that
lexes, parses, lowers, typechecks, flattens and reorders on every keystroke
against a schema the reader can edit — ending in the plan the executor would
run. What is left is *running* it.

**The goal, unchanged.** The design book's interactive segments run the real
lexer, parser, typechecker, planner, executor and transport codec, compiled to
`wasm32-unknown-unknown` — not a JavaScript imitation of them. The boundary
carries **JSON of the constructs, not a rendered string**, because a page that
receives structure can lay it out and a page that receives text can only print
it.

### Movement 1 — the seam becomes a crate, and each implementation its own ✅

Three crates replace `fjord-store`. It keeps its name and becomes **the
abstraction**; each implementation has its own crate, which is what makes a
third backend additive rather than a refactor.

| Crate | Holds |
|---|---|
| `fjord-store` | the seam: `fact_store`, `error`, `fact`, `format`, `keys`, and the shared test support (`fixture`, `fixtures`) |
| `fjord-store-mem` | `MemStore`, no longer test-gated |
| `fjord-store-fjall` | `FjallDb`, `FjallStore`, `Staged`, `FjallScan`, `lookup_cache`, and the lifecycle: `catalog`, `meta`, `schema_doc`, `identity`, `ulid` |

**The error split went as designed, with one addition.** Eleven variants stayed
on the seam and ten moved to `CatalogError` in `fjord-store-fjall`, which
carries `Store(#[from] StoreError)` so a seam fault still bubbles through one
`?`. `StoreError::Backend` is now `Box<dyn Error + Send + Sync>`, constructed
through `StoreError::backend` — `#[from]` cannot do that job, because a blanket
`From<E: Error>` would swallow every other error in the crate. The addition:
two sites that used `StoreError::Meta` for something that was never a sidecar —
a malformed id reservation in the `meta` keyspace, and a virtual catalogue
predicate that disagrees with its declaration — now box a **local** error
through `Backend`. That is the seam-correct reading: from the trait's side,
this backend failing to be what it wrote *is* the backend failing.

**What the ripple actually cost**, against the estimate of seventy-five
references: about that, and nothing surprising in them. `ServerError` and
`CliError` each gained a `Catalog` arm, and the server's `code()` — the one
place the split is visible on the wire — routes lifecycle refusals from
`CatalogError` and delegates `CatalogError::Store` to the same function the
seam's own arm uses, so no client is told anything different.

**The trap the extraction found, exactly where AGENTS.md says it lives:**
`fjord-store`'s own unit tests could no longer use `MemStore`, because a
dev-dependency on `fjord-store-mem` links a *second copy* of `fjord-store` and
the two `FactStore`s are then different types. Those tests moved with `store.rs`
into `fjord-store-fjall`, where both implementations are ordinary dependencies.
One consequence to remember: `fjord-store-fjall` dev-depends on **itself** with
`features = ["proptest"]`, because the witnesses its guards need
(`open_snapshots`, `table_counts`, `flush_to_tables`) are feature-gated and an
integration test is a separate crate. While those guards lived in `fjord-store`
the feature arrived by unification from the engine's dev-dependency.

### Movement 2 — `fjord-inspect`: a JSON view of every construct ◐

The crate exists, with `Tokens` built and the rest to come.

| View | Built from | State |
|---|---|---|
| `Tokens` — `{kind, class, span, text}` + diagnostics | `lexer::tokenize` | ✅ |
| `Tree` — a dense `{id, kind, token, label, span, children}` | the **CST**, through `cst::CstNode` | ✅ |
| `Lowered` — `{id, kind, label, ty, span, children}` plus the statement list | `Ast`, walked from the head and the body, with `Typed::ty` beside it | ✅ |
| `SchemaView` — predicates and their declared types | `syntax::{parse, lower}`, typed by `print::signature` | ✅ enough for the page; canonical form and compatibility are not shown |
| `Tokens`, for the **schema** language | `fjord_schema::syntax::lexer` — a second lexer, not a second reading of the first | ✅ |
| `Types` — per node, and the head | `Typed::ty`, `Compilation::head_ty` | ✅ folded into `Lowered` — a type is an annotation *on* a node, and a second panel would make a reader align two lists by hand |
| `Diagnostics` — code, message, labels | the sink, through `Diagnostics::in_source_order` | ✅ for every phase that reports without a schema |
| `PlanView` — steps, levels, seek keys, residuals, projections, fingerprint | `print::steps` and `print::head`, with structure around the engine's own text | ✅ |
| `Rows` and `ProfileView` | `fixtures::collect_rows` and `iter::enumerate_profiled` over a `MemStore` from `fixture::facts()` | to build |
| `WireView` — frames, blocks, and a hex dump annotated by offset | `fjord_wire::{frame, block, value, protocol}` | to build |
| `SchemaView` — predicates, canonical form, identity, compatibility | `fjord_schema::{syntax, print, fingerprint}` | to build |

**The schema is text, and the page holds it.** `syntax::read` builds a schema
from a string with no filesystem in reach, so the second editor was all it
took. Two consequences worth keeping: the module stays **stateless** — two
strings in, JSON out, no handle to a compiled schema that a page would have to
free — and a reader can *break* the schema and watch the query stop
typechecking, which is the clearest statement that these are the same phases
the server runs.

**The samples moved into the crate.** `fjord_inspect::SAMPLES` and `SCHEMA`
(the repository's own `schemas/code.sigla`, embedded) are what the page opens
with, and `every_sample_compiles_clean` is what makes them claims rather than
decoration. The page invented its own examples once; all of them were missing
the head a query requires.

**The lowered view runs the whole front end, not just typecheck.** Several
refusals a reader meets first are flatten's (`nyi/value-field`,
`reject/not-a-generator`), and a page that showed "no errors" for a query
`flatten` would refuse would be lying. The plan it produces is now shown beside
it, and **a plan exists exactly when the sink is clean** — the same rule the
server runs under, asserted rather than assumed.

**The plan view does not render the plan.** `print::plan` was split into
`print::steps` (one string per step) and `print::head`, with `plan` becoming the
join of them — so the text a page shows is byte for byte what
`fjord query --plan` shows, and `the_view_is_the_printer_rendered_apart`
reassembles one from the other to prove it. What the view adds is *structure*
around that text: the step's kind, the register it fills, whether each source
scans or seeks, how many residuals filter it. A second renderer would decode
stored bytes a second way, and the places it would differ — a constant's type, a
union alternative's name, which field a path names — are exactly the ones worth
reading.

**Levels are not steps, and the view says both.** A resume cursor holds one row
per *level*; a derive and a test bind nothing and take no cursor entry. Carrying
one number would make the other wrong somewhere a reader could not see.

**The split between the two trees is the thing to keep straight.** The parse
view is the *concrete* tree — the "lossless, untyped, grammar-shaped tree with
spans and text" the book's phase table promises — and it needs **no schema**,
which is why it could ship now: lowering resolves names against one, parsing
does not. So a browser can show it for any text at all, including the
half-typed text an interactive view spends most of its time on. The lowered
tree, the types and the plan all wait on the same thing: a schema in the page.

**Two properties, and the second was a surprise.** A node's span contains every
child's — a view that widened one by a byte would still look plausible and
would highlight the wrong text. And the leaves *do* reassemble the source: the
grammar's `skip Whitespace` keeps trivia out of what the parser matches on, not
out of the tree, so the same view drives both panes.

**One decision made while building the first view, worth keeping.** A token
carries a `class` (keyword, predicate, variable, field, …) as well as its
`kind`, and the class is decided in Rust. It paid off when the schema pane
needed highlighting: the schema language is a *second* lexer with tokens sigla
does not have (comments, namespaces), and what the page needed was two more
arms on one shared vocabulary — one stylesheet, one set of classes, and a
reader meeting one idea rather than two. A page styles what the language says a
token *is* and never re-decides it — which is what stops the highlighter growing
back in TypeScript. Both mappings are exhaustive `match`es with no wildcard, so
a token added to sigla does not compile until somebody says what it is called
and what it is.

**`tokens_json` lives in `fjord-inspect`, not in the shell.** The JSON a browser
receives is then the string the host suite asserts on, and
`a_view_is_the_same_json_on_the_host_and_in_wasm` is a consequence of there
being one encoder rather than a claim needing a test.

### Movement 3 — `fjord-wasm`: the shell, and nothing else ✅

`wasm/` at the repository root with its own `[workspace]` table, a `cdylib`
whose every export takes a `&str` and returns a `String` of JSON, and no logic —
`tokens` is one forwarding call. `scripts/build-wasm.sh` runs cargo, then
`wasm-bindgen --target web`, then `wasm-opt -Oz` if binaryen is installed, and
prints the byte size. It refuses to run if the `wasm-bindgen` CLI and crate
versions differ, because that mismatch fails with a message about a section
rather than about versions.

**How the artifact reaches the site, decided:** built, not committed.
`web/src/wasm/` is gitignored, the page says so when the module is absent, and
it does **not** degrade to a JavaScript highlighter — the highlighter is the
thing being replaced, and a fallback would hide exactly the failure that
matters.

### Movement 4 — stepping the executor: a query debugger ✅

**Built.** The site runs queries against a database in the page and steps
through the run one transition at a time: registers as they fill, the row each
`yield` answers, and the rows a residual read and dropped. What follows is the
design as it was argued before it was built, amended where building it found
something.

**Goal.** Not "run a query in the page" but *step* one: see the registers as
they fill, where the machine is in the plan, what it has yielded so far, and
what it has read to get there. A reader who has watched a nested loop backtrack
understands the executor in a way no amount of prose achieves.

**It needed no new machine, which was the bet.** `iter.rs` is a
**defunctionalised state machine** ([I7](website/content/invariants.md#i7)):
`depth`, a stack of frames, and one loop whose every iteration is exactly one
transition. Stepping is therefore *exposing one iteration at a time* — not a
second interpreter in the view crate, which would be the very thing this whole
exercise exists to avoid. The nine transitions are already there to be named:
**open** a source, **produce** a row into a register, **drain** an alternative,
**close** a level, **yield** a row, **compute** a derived bind, **pass** a test,
**fail** a test, **done**.

**Rows dropped by a residual are the point, not a detail.** They never reach the
loop — `frame.next` filters them inside the scan — and they are exactly what
makes a scan cost more than a seek, so the debugger shows each one and *which
residual rejected it* (`check_residuals` knows). The scan loop is where
[I6](website/content/invariants.md#i6) and
[I9](website/content/invariants.md#i9) live, so this is paid for the way this
repository already pays for instrumentation: `FieldOffsets::witness_row` has a
real implementation under `cfg(debug_assertions)` and an empty `#[inline]` one
otherwise.

**A Cargo feature, `fjord-engine/trace`, off by default** — not
`debug_assertions`, which is on for every dev build and off in release, and the
browser wants a *release* build with tracing in it. The hook goes exactly where
`Profile.examined` is already incremented, because that increment is why skipped
rows are counted at all: the trace point and the counter are the same site. It
rides on the `Deadline`, which is already the per-run instrumentation carrier
threaded into the row loop — and which will want a better name once it carries
two things.

**Plus a runtime `Option`, even in the traced build.** That is what keeps
[I9](website/content/invariants.md#i9) honest: the allocation guard runs with
the sink switched off and must still count zero per row. Compile-time gating
alone would leave the guard measuring code that no longer resembles what ships.

**Both configurations are tested, which is the part that matters.**
`fjord-inspect` enables the feature, so `cargo test --workspace` builds the
engine *traced* and every existing guard — alloc-free per row, no value fetch in
the scan, resume equals uninterrupted — runs against the traced build.
`cargo test -p fjord-engine` has no `fjord-inspect` in its graph and builds it
*untraced*. CI runs both. A feature nobody tests is an aspiration.

#### The database in the page

`MemStore` is wasm-clean already; what is missing is facts — and, it turns out,
a schema. `schemas/code.sigla` has **no union and no nested record**, so a
select (`.what.func?`), a union pattern, a discriminant residual and a nested
record key have nothing to bind against. A union in a *leading* key field is a
seek and behind another field is a residual — the same query shape, two costs —
which is one of the sharpest things the plan view can show, so a database that
cannot demonstrate it is not a demonstration of the language.

**So the site gets a schema of its own: `schemas/demo.sigla`.** Code-search
shaped, because that is what Fjord is for, but small enough to read in one
screen and chosen so every shape the language has appears exactly once:

| Predicate | Shape it is there for |
|---|---|
| `code.File : string` | a **scalar** key — and a prefix constraint (`"src/"..`) that excludes something |
| `code.Decl { file : File, name : string, line : int } -> string` | a **record** key with a **leading reference**, a three-field prefix a seek can pin part of, an `int` for comparisons and arithmetic, and a **value side** |
| `code.Ref { from : Decl, to : Decl }` | a reference that is **not** leading (a fact-id compare as a residual rather than a seek), and a **two-hop chain** through `from.file` |
| `code.Span { decl : Decl, at : { line : int, col : int } }` | a **nested record** inside a key |
| `code.Kind { decl : Decl, what : kind }` | a **union behind** another field — matched by a residual on the discriminant |
| `code.KindOf { what : kind, decl : Decl }` | the same fact in the other key order, so the union **leads** and the tag is a seek. The pattern `code.sigla` already uses for `Attribute`/`AttributeOf`, and for the same reason: the leading run is what a query narrows on |

with `kind` declared as `{ type : string = 5 | func : int = 2 }` — two
alternatives, tags **neither contiguous, nor starting at zero, nor in
declaration order**, so nothing that read a discriminant as a position can pass
([I10](website/content/invariants.md#i10)).

A real file rather than a string in a crate, so the same database can be built
outside the browser — `fjord create demo --schema schemas/demo.sigla` — and the
queries a reader tried in the page can be run against a real one.

**The facts: around fifteen, and each one earns its place.** Three files (one
outside `src/`, so a prefix excludes it); three declarations, two of them in one
file, so a join returns two rows for one outer row and **none** for another —
backtracking a reader can watch; two reference edges forming a chain, with one
declaration referenced by nothing, so a negation has something to be true about;
one span; and a kind per declaration in both key orders.

They are authored through `fjord_store::fact::encode` — the path the .NET golden
and the server's catalogue take — so there is one encoder, with name resolution
and field reordering included, and references are written as the fixture writes
them: sequences chosen up front so `Decl`'s `file` names a `File` that exists.
**Hand-encoding a key is the anti-pattern `AGENTS.md` names**, and its three
silent preconditions apply here exactly.

Guard: `every_sample_answers_what_it_says` — each sample query's rows asserted
in the host suite, which is the corpus's discipline applied to the demo. A
sample that answers nothing is a sample that demonstrates nothing, so the guard
also refuses an empty answer unless the sample says it expects one.

#### `Executor::advance`

The loop body of `enumerate_profiled` becomes

```rust
fn advance(&mut self, deadline: &mut Deadline<'_>) -> Result<Transition, FjordError>
```

with `enumerate_profiled` looping over it. **The yield policy stays where it
is**: what to do with a row — `Stream::Continue` or `Stream::Suspend`, and the
`depth -= 1` after — is the streaming caller's business, so `advance` reports
`Yielded` and leaves the machine standing on the head. Accessors follow for what
a debugger reads between transitions: `depth`, `state` (already public types),
and a `row` that is `Some` exactly when the machine is standing on the head.

**The trap to name up front:** `advance` must not become the place where
*policy* lives. Descending or backtracking is read off the frame rather than
carried as a variable, which is what keeps the machine defunctionalised, and a
`Transition` return value must not become a second way of saying the same thing.

The safety net for the extraction is the strongest battery in the repository:
resume-equals-uninterrupted on both stores, the corpus suspending at every cut
point from 1 to 64, alloc-free per row under a counting allocator, no value
fetch in the scan, and the I8 drop probe. New guard:
`stepping_yields_what_running_yields` — drive `advance` to completion over every
corpus entry and compare the rows with `enumerate`'s.

#### `fjord_inspect::trace`

**The whole trace in one call.** `trace(schema, query)` runs the query to
completion and answers the entire run as a list of steps, each carrying only
what *changed*: the transition, the depth, the register written, the row
examined or rejected and by which residual, the row yielded. The page folds that
into cumulative state and scrubs a local array — instant in both directions, one
round trip, no state on the boundary, nothing for JavaScript to free, and no
O(n²) from replaying a prefix per step. "Step over" is then a client-side search
for the next `Yielded` entry, and costs nothing.

A run over a fifteen-fact database is tens of transitions; a deliberately silly
one is thousands. **The cap is stated rather than silent**: past a bound the
trace stops and says it stopped, because a truncated run rendered as a whole one
is the exact failure this repository keeps guarding against.

The escape hatch, if the browser database ever stops being a toy, is a
`#[wasm_bindgen]` struct owning a live `Executor` — O(1) per step, at the cost
of state on the boundary and a `free()` for JavaScript to forget.

The view, per step: the transition and the plan step it happened at, the depth,
every register (empty, a decoded row, or a computed value), the rows yielded so
far, and `Profile.examined` as it stands. **A register is decoded against
`fact_id.predicate()`** — the predicate of the row actually bound, not the
level's — because a level with alternatives can bind rows of different
predicates, and decoding against the wrong one reads plausible bytes.

#### The page

A **run** tab: transport controls (start, back a row, back one transition, one
transition, on to the next row, play, end), a scrub bar over the whole run, the
register panel, and the rows yielded so far. Stepping back is free, because the
trace is already in hand.

**Under it, the database as a table** — every stored row, in key order, as bytes
*and* as a fact, with the range the current scan is walking **shaded across
it**. That is the panel the plan's numbers are about: a seek is a byte prefix
and a scan is a range over the same order, so `[lo, hi)` means nothing against
decoded values and everything against stored keys. The pinned bytes are marked
off from the ones the scan walks, which is the cost model in one place —
everything left of the boundary the seek jumped to, everything right of it the
scan reads.

Four states a row can be in, and between them they are the whole story of a
query: outside the range and never read; inside it and not yet reached; **read
and dropped** by a residual; and **held**, which is where a register is
standing.

The bounds come from `open`, which is where they are computed — recorded on the
frame under the feature and reported by the caller that holds the deadline, so
no signature changes for a feature that is off. `Trace` grew `scanning` and
`fetching` beside `rejected` for it. The hex is unseparated because the page
compares it as a string: `"0000000104"` starts with `"00000001"` and
`"00 00 00 01 04"` does not.

#### What building it found

**A silently wrong answer, in `flatten`.** A constraint written inside a
subquery — `X = (Y where code.File Y; Y = "src/"..)` — was *dropped*, so the
query answered rows the constraint excludes; and a generator bind inside one
(`X = (Y where Y = test.Foo _)`) declined to plan at all, tripping flatten's own
"no plan without a reason" assertion in a debug build and refusing with an empty
sink in a release one. The cause was two paths for one thing: the subquery
inliner carried its own copy of the bind walk that handled only the *alias*
case. Both now go through one `Flattener::bind`, and four corpus entries pin the
combinations that were missing — which is why it survived: the corpus is how the
language surface is specified, and nothing had written these down.

**A reference reads as the fact it names.** `Value`'s serialiser writes a
`FactRef` as the `u64` it is, which is right for a wire and unreadable in a
panel, so the view renders one as `code.File#2`. Not a second codec: nothing
there decodes bytes.

### Movement 5 — the book itself, with the engine in it ✅

`web/` renders **the design book**, not a demo beside it. The pages are
`website/content/`, imported raw and parsed by a TypeScript port of the same
dialect `build.py` renders; the reading order moved to `website/nav.json`, which
both read. Nothing was copied, because two copies of a page is one page that goes
stale.

**The demos are the argument.** A `:::demo <kind>` block in the content is the
engine running where the paragraph that needs it is — `lex`, `parse`, `types`,
`plan`, `run`, `store`, `schema` — editable, so the reader's next question is
answerable by typing it. `build.py` understands the same block and renders the
source with a pointer rather than a typed-out answer, so the generated site stays
true while it is still the published one.

Three things fell out of doing it this way:

- **The module is demanded, not assumed.** A demo triggers the load; everything
  else observes. A page of prose costs no WebAssembly, and a page with a demo
  re-paints its static `sigla` and `schema` blocks with the *lexer* once the
  module lands — which is the beginning of retiring `website/assets/app.js`.
- **A page is a path**, not a fragment: the book is full of `#anchor` links and a
  hash router would have had to own that character. The bundle carries a document
  per route so a path is a file and answers 200, and `dist/404.html` for a path
  nothing knows about — a fallback on its own renders the right page and returns
  404, which is live only to a reader who starts at the root.
- **The two renderers are compared.** The smoke check walks every page in both
  and compares headings, tables, code blocks, callouts and demos — a dialect that
  drifts between Python and TypeScript is a page that reads differently depending
  on which copy you found.

**The publish is switched over.** CI's `site` job builds the module, drives the
bundle in a real Chrome, and then builds it twice — base `/` for the tarball a
release carries, and the repository's own name for the copy Pages serves, since
`SITE_BASE` is compiled into every asset URL and every route. `website/` still
builds strictly: it is the copy that reads with no toolchain, and the renderer
this one is held to.

### Movement 6 — a design system under it ✅

The site was hand-rolled CSS: a stylesheet ported from the generated book, a
split pane, an accordion, a drawer, a search modal and a transport, all written
here. It worked and it looked like it — so the components are now **Astryx**
(`@astryxdesign/core`), Meta's design system, and what is left hand-written is
only what is about *this engine*.

The shell is `AppShell` + `TopNav` + `SideNav`; the on-page contents is
`Outline`, search is a `CommandPalette` over the same index, and a page's blocks
are components: `Heading`, `Text`, `Table`, `Banner`, `Blockquote`, `Divider`,
`CodeBlock`. The workbench is `Layout` + `LayoutPanel` with a `ResizeHandle` on the
database, its sections are `Collapsible`, its transport is a `Toolbar` of
`Button`s and a `Slider`, and the schema is a `Dialog`. The markdown renderer stopped emitting
HTML strings and emits a **tree**, because every block on a page is now a
component rather than a string.

The palette did not change: `src/theme.ts` seeds `defineTheme` with the book's
warm paper and rust accent, and a syntax theme whose colours are the ones
`fjord_inspect::tokens` already decides. `CodeBlock`'s `tokenizer` prop is where
the two systems meet — a `sigla` block on a page that has the module is
tokenized by the engine's own lexer, and the block says which painter it got.

Three things this turned up:

- **A class name is a shared namespace.** The book's authored HTML uses `card`
  and `pill`; the design system's `CodeBlock` carries a `card` variant class. An
  unscoped rule restyled every code block on the site until the book's names went
  under `.authored`.
- **The database table folded around the wrong query.** Re-folding was keyed on
  the step number, and a new query starts at step 0 like the last one did. It is
  keyed on *which predicates matter* now, as well as on the step.
- **The smoke check was testing a phone.** Puppeteer's default window is 800×600,
  and the shell overlays its panels below 1024px — which had been invisible while
  the layout was hand-rolled and unresponsive.
- **A flex column will not shrink past its content.** A code block is wider than
  the measure, so without `min-width: 0` the page scrolled sideways under the
  navigation at tablet widths rather than letting the block scroll inside itself.

### What is left

- **The `WireView`** — frames, blocks and a hex dump annotated by offset — which
  is the one view in the original list nothing has needed yet.
- **A schema handle, if a bigger schema ever makes it hurt.** `compile` re-reads
  the schema on every keystroke, because the module holds no state — two strings
  in, JSON out, and no handle a page has to free. Measured on
  `schemas/code.sigla`: 700–800 µs warm for the whole round trip, which is a
  tenth of a frame, so the statelessness is worth keeping until it is not.
- **Size.** 258 KB is the whole front end plus the schema language; `wasm-opt
  -Oz` takes 34 KB off it and `web/`'s dev-dependencies now carry binaryen so
  the build script finds one. If it matters more later, the lever is splitting
  the module per segment rather than shrinking this one.
- **Retire the hand-written highlighter** in `website/assets/app.js`. `web/`
  paints `sigla` and `schema` blocks with the real lexer once a demo has brought
  the module in, and carries the fallback rules for the languages the engine has
  no opinion about. The generated site keeps its own copy for as long as it is
  the copy that reads with no toolchain — which is what publishing the bundle
  changed about this item, rather than closing it.
- **A virtual import resolver**, so browser schemas are not single-file:
  `syntax::resolve` reads files, and everything else in `fjord-schema` is clean.
- **`ts-rs` behind a feature**, so `web/src/wasm.ts`'s types are generated from
  the view structs instead of stated a second time.
- **Ingest stays impossible in a browser**, and that is not a gap: interning
  needs a real backend and durable id claims.


## Operational gaps

Each is a *specified* absence with the seam that keeps it cheap — none is an oversight.

| Gap | The seam kept |
|---|---|
| `db backup` / `restore` | A Complete database is a tar-able directory; the commands would wrap what operations already documents. Also the row where copy-on-start reader scaling lands |
| `db verify` | Recomputing the content fingerprint is cheap and specified; the two structural at-rest checks to add are I1 and I12 after a crash-and-recover |
| Per-predicate stats, and `:stat` | An **exact** O(1) count per predicate exists unread — per-predicate keyspaces plus insert-only make fjall's `approximate_len()` reliable. Surface as a virtual predicate (the `fjord.db.List` shape); record at `finish` into the versioned sidecar. Spend it on pruning, not join ordering |
| Server-side reference expansion | A **flag on the query message, not a fourth query kind** — expansion stays orthogonal to paging, profiling, counting. Collapses depth-many round trips into one, which is what makes `--expand` usable over TCP; the predicate allowlist is the better dial than depth. The client-side path stays (it is what makes `:expand` retroactive) |
| A **wall-clock deadline** on the cancellation stride, and a byte budget in the chunk accumulator | What is left of this row after the rows-examined ceiling landed. A coarse monotonic read every 4096 rows is free at our row costs, and it is what would bound a *slow* chunk rather than a *large* one — the two differ once a store is remote or a disk is degraded. The byte budget is the other half: a chunk is bounded in rows and not in bytes, so a page of very wide rows is unbounded in memory |
| ~~Rows examined~~ **— done** | Kept visible because its absence shaped the recursion plan. `Executor::with_examined_ceiling`, set by the server per executor: the engine's only limit on *input*, where every other budget counts output and a query whose residuals reject every row produces nothing while reading everything. One executor is one chunk only while a chunk is one plan — a fixpoint driver owes the aggregation |
| Per-stream flow-control windows | Bounded per-stream queues + connection backpressure in the meantime |
| Retention | `db rm` exists and the filesystem is the catalog, so a policy is a caller, not a mechanism. "Keep the newest *n* Complete instances" is the shape |
| Provenance / freeform properties | The sidecar format is versioned; both are descriptive-only under `ops-I4` |
| Shell completions | `fjord completions <shell>` is specified in the CLI design |
| Fair cross-database write scheduling, fair fan-out merge | Both arrive with multi-database work; the fairness that exists is the other axis (`outbound` interleaves streams within a connection) |
| fjall keyspace tuning | **Measure, do not assume.** Options are fixed at creation, so a comparison builds a database per setting; needs a real-scale corpus. Until then fjall's defaults are the answer |
| `hasRefs` precomputed per predicate | Consulted before walking a fact's references; prerequisite for cheap expansion, not an alternative to it |

### A defect, not a gap — a cursor does not name the world it was made in

Everything above is a *specified* absence. What follows is not: two live
[I4](website/content/invariants.md#i4) violations in shipped code, recorded here because they
were found while reviewing recursion and must not be fixed only inside that feature. They are one
defect wearing two faces — **a resume token identifies a plan and nothing else about the world it
read.**

#### The base database

`Executor::resume` checks the cursor's version, its plan fingerprint and its level count. None of
those says *which database*, and the executor says so itself: a test is not re-run on restore
partly because the base is frozen, "against a different database, which is a case the token cannot
detect at all". So a cursor can be replayed against another Complete database with the same schema
and overlapping keys, and the per-level `fact_id` check is all that stands between that and a
wrong answer — a check that passes whenever the saved key exists there too.

**And "the base is frozen" is conditional, which the rest of this plan reads as though it were
not.** `ops-I2` is established by refusing a *write-mode* open of a Complete database; a **read**
session against a **Writable** one is supported, and every chunk takes a fresh `reader()`. So
ingest between two pages changes what the next page sees, and the result is not a refusal but a
hybrid: page 1 from one state, page 2 from another. Recursion makes this worse rather than
different — re-derivation recomputes the whole fixpoint from the changed base.

The fix is the same shape as below and belongs in the same place: **the database's content
fingerprint or instance identity becomes a general cursor stamp**, not a recursion envelope field.
For a Writable database that is not enough, because the identity does not move as facts arrive:
`meta` records a fingerprint at `finish` and holds none at all while the database is Writable —
"absent rather than zero", by deliberate choice, because a fact count on a Writable database would
be a claim rather than a fact.

**A draft of this section answered that by refusing resumable reads until the database is
Complete. That answer is withdrawn, because "resumable" is much larger than it looks.** Resume is
not a paging feature in this server. `run_query` streams *every* result in `CHUNK_ROWS` chunks,
and each chunk takes a **fresh `reader()`** — a fresh snapshot — through `run_chunk`; the `count`
path does the same. So an ordinary unpaged `where`, and a bare `count`, already suspend and resume
internally against a new view of the database each time. Refusing resumable reads on a Writable
database therefore does not refuse paging: it refuses **every server query and every count against
a database that is still being ingested**, before a row description is ever sent. That is most of
what a Writable database is for, and the paragraph proposing it had not noticed it was proposing
it.

**So the generation gets built, and it costs far less than the dismissal assumed.** That dismissal
— "a counter on a live write path is a great deal of machinery" — priced a counter this project
would have to maintain. It does not have to: `FjallStore` holds a `fjall::Snapshot`, which *is* a
position in a sequence, and `Database::visible_seqno` reads the very counter that stamped it. A
reader capture that reads the visible seqno, opens the snapshot, reads it again and keeps the
value only if the two agree names that snapshot exactly, and adds one atomic load to a path that
already takes a lock. It is monotone by construction and coordinates with ingest not at all. The
real cost is a **dependency on a pinned version's internals** — and a draft of this paragraph got
that dependency wrong, claiming only `Snapshot::seqno` was `doc(hidden)`. Both are:
`Snapshot::seqno` and `Database::visible_seqno` alike carry `#[doc(hidden)]` in
`fjall = "=3.1.8"`, so the bracketed
reading buys a *stable* value, not a supported API, and there is no third option that avoids the
internals. The pin is what makes it tolerable, and a fjall bump owes this a check either way.

**A seqno alone is not a world identity, because it does not survive a reopen — and cursors
explicitly do.** `fjall` reconstructs its sequence on open by taking `get_highest_seqno() + 1` over
what it *recovered*, and a Writable database's writes are not necessarily fsynced: `persist` is an
explicit act that `finish` calls, so a crash can lose committed-but-unsynced tail writes. The
recovered sequence is then **lower** than a live cursor's stamp, subsequent ingest reissues those
numbers over different content, and the instance directory is unchanged because a Writable database
carries no identity to change. A stamp of `{ instance, seqno }` matches, and the I4 hole is back —
now behind a crash, which is where the expensive bugs live.

**So the Writable stamp carries an incarnation: `{ instance, incarnation, visible_seqno }`.** The
incarnation is a nonce minted when the handle is opened and held in memory beside it — nothing
persisted, because persisting it is what a crash is allowed to lose. Every reopen therefore mints a
new one and **every Writable cursor from a previous incarnation is refused**, which is
conservative, correct, and free of any reasoning about what the recovery kept. A Complete database
needs none of this: its stamp is the content identity `finish` computed after `persist`, which is
restart-stable by construction, so the common case of a cursor outliving the process keeps working
exactly as it does today. The seqno is still needed *within* an incarnation, where it is the only
thing that moves; the incarnation is needed *across* one, where the seqno can move backwards.

**The rule is then uniform and small: a chunk boundary revalidates the stamp exactly as a page
boundary does.** On a Complete database the stamp cannot move, so nothing is ever refused and
today's behaviour is untouched — which is the common case and the one performance matters in. On a
Writable one, a read that no write crosses completes normally, and a read a write *does* cross is
refused by name, mid-stream, instead of returning a hybrid of two states. Refusing mid-result is
unpleasant; it is strictly better than the current behaviour, which is to return the hybrid and
call it an answer. Holding a single snapshot across the whole request is the obvious alternative
and it is rejected deliberately: releasing at every chunk *is*
[I8](website/content/invariants.md#i8), and pinning a snapshot for the lifetime of a slow client's
stream is the thing that rule exists to prevent.

Its guards: a resume against a *different* same-schema database with overlapping fact ids and
keys; ingest between two `query_page` calls; and — the arm a paged-only framing misses — ingest
between two internal **chunks** of an unpaged streaming query, and between two chunks of a
`count`, each refused by name rather than answered from two states. A recursive program adds a
fourth: re-derivation between count chunks, where the two partial counts come from different
fixpoints.

And a fifth, which only the incarnation catches: **a cursor taken, the handle reopened with the
recovered sequence rewound, and the same sequence numbers reissued over different content** —
refused on the incarnation, with the negative that a Complete database's cursor survives the same
reopen. A test can build this directly by reopening a Writable database whose tail writes were
never `persist`ed, which is the honest reproduction rather than a simulated one.

#### The virtual predicates

The second face, and the one that needs no second database at all.
`catalogue`'s charter says the rows are materialised once per query and shared by every chunk, so
"a `create` between two pages is invisible to the result in flight". That is true of the path it
was written against — the shell's `\more` drains one already-open stream — and **false of
`Connection::page`**, the stateless path a web tier uses. There, each page is a *new request*:
`run_query` re-prepares the query and `prepare` rebuilds the listing from the registry. So a
`create`, `rm` or an interning counter moving between two pages renumbers the listing under a
cursor that is a *position* in it, and the resumed read silently skips or repeats rows. Cursor
validation cannot catch it: the query text and schema are unchanged, so the plan fingerprint
matches.

**The fix is the cursor, not the catalogue.** Carry the listing's generation in the cursor and
refuse a resume across a change, by name — the same shape as every other thing a cursor already
refuses to resume into. Freezing the listing across unrelated requests is the wrong trade: it
would make the server hold state for a path whose whole purpose is not to.

Its guard is a server-level test that mutates the catalogue between two `query_page` calls, for
both an ordinary scan and — once recursion lands — a `Program`. The reason this survived is worth
recording: the differential and the resume property both run over a frozen `MemStore`, where a
mutable source cannot be expressed, so neither could ever have failed.

#### The fetch round trip

A third face, and the one a cursor cannot reach at all. `FETCH` is its own request: it
rematerialises the catalogue from the registry, then resolves each asked-for id against **that**
listing. So a query can return a virtual id from listing L1, the catalogue can change, and a fetch
with an empty cache resolves the id against L2 and answers for a different database. No cursor is
involved, so no cursor stamp can catch it, and clearing a client cache cannot either — the
first-ever resolution is already wrong.

**This is documented behaviour rather than a discovery, and that is exactly why it is written down
here.** `session::fetch`'s own comment says a catalogue row's id "is its position in the listing
that produced it, so a database created or removed between a query and a fetch can move it", and
concludes: "It is a handle into a view, not an identity." A design that says so out loud is not a
bug; a *plan* that then claims item 12's listing digest makes stale virtual ids detectable is
wrong, and it said so until this round. The digest travels in a cursor. A fetch carries no cursor.

**The mechanism, selected rather than listed: the listing digest travels with the rows and comes
back on the fetch.** A result containing virtual ids reports the digest of the listing that minted
them, the client returns it in `FETCH`, and the server refuses by name when the current listing
digests differently. It keeps the property that makes this path worth having — the server holds no
per-result state between requests — where tying fetch to a server-held result would trade exactly
that away, and refusing virtual whole-row references outright would delete `fjord query --expand`
over `fjord.db.List`, a shipped feature, to fix a race in it.

**It does not gate recursion, and the reason is not scheduling convenience.** A plain
`where fjord.db.List {..}` with `--expand` has this race today, in full. Recursion adds no new
surface to it: item 3 refuses `Project::FactRef` of a *local* row, so the only virtual ids a
program can put on the wire are the ones a base scan would have put there anyway. There is no
recursion-shaped half to fix early — unlike the base-database stamp, where a program genuinely
cannot be correct without one — so this is fixed once, for both, in the protocol.

Its guard is the one the cursor tests cannot express: mutate the catalogue between a query
returning a virtual id and the **first** fetch of it, with an expander that has cached nothing, and
require a named refusal rather than a row from the wrong database.

---

## Recursion — query-local relations, magic sets, stratified negation

**Goal.** A query may define named relations in its own text, those relations may refer to
themselves and to each other, and negation over them stays sound. The worked example is the
one five features in [`docs/gitnexus.md`](docs/gitnexus.md) are blocked on:

```sigla
with Reach : { from : src.Decl, to : src.Decl } =
    ( {from = A, to = B} where src.Calls {from = A, to = B} )
  | ( {from = A, to = B} where Reach {from = A, to = M}; src.Calls {from = M, to = B} )

{name = D.name, file = D.module.file} where
  src.SearchByName {name = "encode", to = Seed}; Reach {from = Seed, to = D}
```

**Status: amended over seven rounds of adversarial review; the sixth ended the design loop and
the seventh read it against the code**
([`docs/recursion-plan-adversarial-review.md`](docs/recursion-plan-adversarial-review.md)).
The architecture survived all seven; the boundaries around it did not. **Movement 0 exists because
of that review**, and after the second round it gates *selectively* rather than wholesale:
**Movement 1's routing, snapshot and allocation work is unblocked and should start**, because it
depends on predicate routing and the owned-scan representation and on nothing the compiler
findings touch — but it **must not freeze `RelationDecl`'s field-name representation**, which is
item 15's and is the one representation question the movement touches without owning. What gates
**Movement 3** is item 9 — a program of named rules, which semi-naive and magic both rewrite and
neither can be tested without.

Three findings were outright errors rather than omissions — a wrong invariant citation, an
acceptance criterion written against a wall-clock deadline this project does not have, and a
claim that this engine has no logical rule IR when `syntax::Ast` is one — and all three are
corrected in place rather than footnoted. The second round also found four gaps the first
missed, and the last of them is not about recursion at all: **ephemeral fact ids already escape
to the wire today**, so item 3's rationale describes an existing contract rather than a new
risk, and is written that way below.

**The third round found one uncovered region, three internal contradictions and one missing
generalisation.** The uncovered region is the only one of the five that blocks building:
cancellation is polled in exactly one place, `Deadline::tick`, per row, inside `advance` — so
candidate deduplication, cross-round snapshot merging and canonical-id finalisation observe no
token and no budget at all, and item 14 now owes work units of its own. The contradictions were
item 10's phase order, Movement 4's purity claim and item 4's materialisation contract; the
generalisation was item 7's fallback trigger.

**The fourth round found that three amendments had been scoped to recursion when the defect they
answered was general, and it overturned one of the third round's rejections.** The scoping
failures share a shape — a fix written into the part of the system that noticed the problem — and
each is now widened at the item that made it: item 13's world stamp covers plain cursors and not
only program envelopes; item 7's magic-versus-fallback selection moves to after flattening,
because the failures that force the fallback are produced by semi-naive expansion rather than by
magic generation; and the refusal of resumable reads on a Writable database is withdrawn, because
every server query already resumes internally per chunk and the refusal would have taken the
whole feature. Item 3's open "either" is closed with a selected contract, for the same reason a
plan does not ship a branch nobody owns.

**The fifth round found that two of the fourth round's own amendments were unsound as written, and
one missing compiler phase.** The unsound pair are the two places where a fix reached for the
nearest available value: `visible_seqno` is not a world identity across a reopen, because `fjall`
recovers its sequence from what survived and a Writable database's tail writes need not have been
`persist`ed — so the stamp gains an **incarnation**; and scoping a virtual `FactId` to its listing
does not make `FETCH` sound, because a fetch is its own request that rematerialises the catalogue
and resolves positions against it, which no cursor stamp is present to catch. The missing phase is
**re-stratification of the magic candidate**: magic and supplementary relations change the
dependency graph, and both the unstratifiability fallback Movement 6 promises and semi-naive's own
notion of which occurrences are recursive were reading the *source* program's SCCs. Two narrower
corrections landed with them: the `fjord.db.Interning` refusal is narrowed to cross-request
resume — it had acquired the same over-broad phrasing the Writable refusal was withdrawn for —
and a mid-stream refusal is now owed as a *client* contract, because `next_row` releases a stream
only on `COMPLETE`.

**The sixth round stopped reviewing the design and reviewed the *evidence*, which is what closed
it.** Its finding was that Movement 0 recorded decisions without converting them into anything
falsifiable: fourteen of fifteen items could be satisfied by prose, one item still offered a
choice, and the ledger and this plan each described an intended guard as though it existed. So
Movement 0's acceptance is now a **proof boundary** — every assertion classified green-here,
owned by exactly one named ignored guard, or explicitly evidence of nothing — with a per-item
table, independent models for the three properties that need them, barriers rather than sleeps for
the two concurrency guards, and a completion checklist. Three substantive changes came with it:
item 15 selects a representation (a name-tier type parameter, so a persisted schema *cannot*
hold a local name rather than being validated not to); item 5 gains a history-sensitive work bound,
because the copy guards permit a segmented relation that moves the quadratic cost into reading; and
the world stamp goes into the plain `Cursor` **first**, which deletes the transitional token
format and repairs three live non-recursive defects before recursion is built on them.

**The seventh round read the plan against the code rather than against itself, and found the proof
boundary right in shape and incomplete in two ways.** Neither is an architecture finding: nothing
below moves the `Program`, the overlay, or the claim that the executor does not change. The first
is that five statements a guard would have to be written *against* are ambiguous in the plan
itself, so a guard could be written green over the wrong reading — the accumulated snapshot's
relationship to the delta (item 5b, where the omission decides between a correct fixpoint and one
that never terminates), whether a stratum is one condensed SCC or several (the `Program` shape,
where the loose reading answers a legal forward reference with an empty relation), whether a local
relation has a value side at all (item 4, which three other items had already assumed the answer
to), what canonical rank does when it meets `MAX_FACT_SEQUENCE` (item 3), and that the termination
rule is complete only because `ExprKind::Arith` is today's one value-inventing expression
(Movement 2). The second is that eight assertions were owned by nobody, unprovable as stated, or
provable only of a placeholder: item 5's work bound had no subject in the movement that marks it
green, item 6's "every charge site" is a universal claim with no mechanism behind it, Movement 2
never differentiates its own naive evaluator against the independent model three later movements
lean on, and item 14's blanket impl dissolves I8's structural proof two movements before the
replacement arrives. **Two findings land on work already in flight and are recorded rather than
fixed** — the composite world stamp must length-prefix its variable-length field before a listing
digest is concatenated onto it, and an empty stamp comparing equal to itself is fail-open (item
13). **And Movement 0 is split into four parts**, because as specified it is one diff spanning a
cursor layout, a protocol change, a published type parameter and four pure models, which is the
shape the contract's one-sitting rule exists to forbid.

**What was challenged and rejected is recorded beside the item that provoked it, so a later round
does not re-derive it:** the claim that a versioned cursor layout and a database stamp are
*incompatible* (item 13 — they are sequenced, not simultaneous), the claim that both branches must
be compiled through every phase eagerly (item 7 — retaining the rule set buys the same guarantee
without paying on every successful compile), and the claim that item 15 blocks Movement 1 outright
(the acceptance note below — it blocks one field of one struct), and the claim that the `FETCH`
race blocks recursion (the [fetch round trip](#the-fetch-round-trip) — a plain `--expand` over
`fjord.db.List` has it in full today, and item 3's refusal of `Project::FactRef` on a local row
means a program can put no virtual id on the wire that a base scan would not have). **One earlier
rejection is withdrawn rather than defended**: the third round's ruling that DNF expansion leaves
scan work unchanged was wrong. It counted the innermost level, where the cross product really is
identical, and missed that every *prefix* level is re-entered once per clause. Item 10 now carries
the arithmetic and Movement 3 owes a guard measuring it.

**Query-local rather than schema-declared, decided.** The two forms need the same three
things — a name, a signature, a body — so they differ in *where the declaration lives*, not in
power. Query-local wins the first round because it enters no **database schema** fingerprint,
forces no re-index, and lets an ad-hoc question be asked without a schema change. It still
enters the **program fingerprint** in full, and the distinction is load-bearing: the first
governs what a client and a database must agree about, the second governs what a cursor may
resume into. The cost it accepts is that a query-local relation can never become `stored`, so
it is re-derived per query and **demand seeding is load-bearing rather than an optimisation** —
which is why magic sets are in the first implementation and not deferred behind it.

### The mechanism we cannot copy

Glean's fixpoint writes facts *into the database* mid-query and loops while `firstFreeId`
grows (`Codegen.hs:1412-1465`). That is structurally unavailable here and the reasons are all
load-bearing: `FactStore` is `scan` + `point` with no write, a queried database is sealed, and
`fjord-engine` depends on the seam and never on a backend. Glean also rejects SCCs larger than
one predicate, so mutual recursion is out on that side too — this design goes further than the
reference implementation rather than after it.

### The shape: a `Program` of plans, and an executor that does not change

The seam is the extension point. A derived relation is an in-memory relation the engine owns,
addressed by an engine-local predicate identity, and the executor reads an overlay of frozen
base ∪ derived. Because the two identity spaces are disjoint, the overlay **dispatches** rather
than merges — the same prefix-disjointness argument `ops-I8` already makes for stored
derivation.

```rust
struct Program {
    relations: Box<[RelationDecl]>,   // local identity + declared physical key layout
    strata:    Box<[Stratum]>,        // one condensed SCC each, in topological order
    answer:    Plan,                  // streamed, exactly as today
}

enum Stratum {
    Once(Box<[Rule]>),                            // one non-recursive SCC: run each rule once
    Fixpoint { seed: Box<[Rule]>, step: Box<[Rule]> },
}

struct Rule { plan: Plan, into: LocalPredicate, project: Materialise }
```

**A `Stratum` is exactly one condensed SCC — not a set of them, and the difference is a wrong
answer rather than a scheduling preference.** `Once(Box<[Rule]>)` reads naturally as "the
non-recursive rules, run each once", and gathering several independent SCCs into one stratum is the
obvious implementation of that reading. It is unsound the moment a forward reference exists, which
item 1 declares legal: rules run in source order, a relation declared later in the text has not
been derived when a rule reads it, and the read answers **empty**. One condensed node per stratum
makes that unrepresentable — every edge between predicates is then an edge between strata, and the
topological order over `strata` is the whole of the scheduling rule. It also fixes what item 3's
"finalised at the end of its stratum" means, since a `Once` stratum is then exactly one relation.

**Every phase is "run an ordinary `Plan`, materialise its rows".** A rule's sink is a consumer
of the existing iteratee seam — the `step` callback that already exists — so the fixpoint
driver sits *above* `enumerate` and nothing inside `advance` changes. Three consequences, and
they are the reason this shape was chosen over any other:

- **`Step` keeps its three variants.** The architectural rule holds: no new case in the driver,
  no new case in the cursor, no new obligation on resume per construct.
- **A query with no `with` block compiles to today's `Plan` and runs on today's
  `Executor<S>`**, monomorphised as it is now. It never constructs an overlay and pays nothing.
- **Semi-naive's delta/accumulated distinction is two local relations**, not an `Access`
  change — so a Δ-rule is an ordinary plan and the seek path is untouched.

**What that claim does *not* cover, corrected after review.** "The executor does not change" is
true and remains the reason for this shape. It is not the same as "recursion is cheap to
build". A recursive query additionally needs a predicate catalogue threaded through lowering,
typecheck and flatten; a snapshot discipline for relations that satisfies an *owned* scan seam;
and an explicit materialisation projection. None of those is in the executor, and all of them
are real work — see [what it costs](#what-it-costs).

**The relation store lives in `fjord-engine`, not in `fjord-store-mem`.** Reusing `MemStore`
would grow the `fjord-engine → fjord-store` edge into a backend, which `dependency_closure`
exists to fail — and it would make the engine depend on the differential oracle its own
batteries are judged against.

### Movement 0 — semantics and seams

**The gate, and what it does *not* gate.** Every item below is a decision the rest of the plan
silently assumed and that the code does not currently support. **An earlier draft said "none is
implementation; all are settled-decision work", and that is what stopped this movement being a
proof boundary** — a written answer is a claim, and this section is where the claims are supposed
to become falsifiable. Most items still resolve to a decision plus a guard that fails first; three
resolve to code that must be *green* before anything recursive is built on it (items 12 and 13's
world stamp, the fetch digest, and the terminal-`ERROR` client contract), and one resolves to a
representation change (item 15). What each item owes, and when, is the proof-boundary table at the
end of this section rather than a sentence per item, and the movement itself lands in **four
parts** (0a–0d), for the reason recorded with them. **Do not fix the relation representation,
the id scheme or the grammar before items 1, 2, 4 and 5 are closed** — each of the others depends on
what they decide. **Items 9, 10 and 11 gate Movement 3, not Movement 1**: clause rewriting
needs a program of rules and a settled answer to what a rule *is*, and the relation store needs
none of it. **Item 14 gates Movement 2**, because a driver that cannot run two plans over one
snapshot cannot run a fixpoint at all. The one prerequisite outside this list — a rows-examined
ceiling, without which every limit in item 6 is output-side and blind — **is built**.

1. **Clause union has a surface, an AST and a semantics.** The recursive definition is a union
   of whole rules, each with its own head and conjunctive body — which is *not* what `|` means
   today, where a disjunction is one level with alternative `Source`s that deliberately does not
   distribute over sibling conjuncts. That meaning must not change. The worked example above is
   written in a spelling that **appears to parse under the current grammar** — a disjunction of
   subqueries, `sum '|' sum` with each branch reaching `'(' pattern 'where' stmt_list ')'` — so
   the delta may be semantic rather than syntactic; confirming that is this item's first task,
   and inventing bracket syntax before confirming it is the failure mode. **The confirmation has
   a head start:** a disjunction branch must today be a *fact pattern* — anything else, a
   subquery included, is `nyi/disjunction` — so the worked example parses and is then diagnosed,
   which is evidence for the semantic reading rather than the syntactic one.

   **The four sub-questions, answered rather than listed.** They are not cosmetic: a duplicate
   clause is a no-op for the *answer* and for nothing else. It becomes a second generated rule,
   so it moves the generated rule count, the rule-output-attempt tally, the profile, and — because
   generated rules are fingerprinted — the program fingerprint itself, which decides whether a
   cursor resumes.

   - **Two declarations of one name: reject** (`reject/duplicate-relation`). Schema-first
     resolution gives one name one signature; two declarations have no agreed signature, and
     merging them silently is how a typo becomes a union.
   - **Duplicate clauses within one declaration: retained, never deduplicated.** Dedup is a
     semantic no-op that hides a user error and inserts a normalisation step between the source
     and its fingerprint. Retaining them means the limits and the profile report what actually
     ran.
   - **Canonical order is source order.** Deterministic, a function of the text, and already what
     `Placement::Written` respects.
   - **Forward references and mutual recursion are both legal**, which the rest of this section
     assumes and which nothing in the pipeline needs to be told twice.
2. **A predicate catalogue exists, and `Schema` is left alone.** `PredicateId` is a predicate's
   **position in a dense array** (`schema.rs`: "`predicates` is in id order"), so the reserved
   high band this section first proposed would require a multi-million-element sparse prefix and
   is withdrawn. What replaces it: an engine-side catalogue presenting base and local
   declarations uniformly, consumed by `lower`, `ty`, `flatten`, diagnostics and inspection
   wherever predicate metadata is needed today, leaving the published dense `Schema` and the
   fingerprint embedded in a database untouched. **There are two identity spaces and they must
   not be conflated:** the catalogue's resolution space, and the `FactId` tag space that
   `FactId::new` bounds at `MAX_TAGGABLE_PREDICATE`. If item 3 lands as internal-only, the tag
   space can be reused per query and needs no band at all — but **reused per query is a claim
   about queries, and disjointness still has to hold *within* one.** The bound to check before
   any executable or relation is built, with overflow-safe arithmetic and a named diagnostic:

   ```text
   augmented_predicate_count + generated_local_count <= MAX_TAGGABLE_PREDICATE + 1
   ```

   **`augmented`, not the database's own count.** `fjord-server` appends its catalogue schema
   to every database's, and reserved names sort last so appending moves no stored id — which
   means a server-compiled query sees *more* predicates than the database declares, and local
   tags starting above the stored count would land on `fjord.db.*`. Allocation is deterministic
   and dense within the query; the guards are a catalogue with virtual predicates present, the
   exact last usable tag, and one past it.
3. **Local identities are refused inside compilation, not stopped at the wire.** A local row
   still has a `FactId` — `FactStore::Scan` yields one per row and there is no way not to mint
   it — and semi-naive gives one tuple two of them, one in the accumulated relation and one in
   the delta relation. So "internal-only, enforced at every output boundary" is the wrong
   boundary: a local identity can decide another *derived* key long before any row reaches a
   wire. **Enforced where identity becomes observable instead**, which is four constructs and no
   others: `Project::FactRef`, `SeekKeyPart::RegisterFactId`, `ResidualOp::EqRegisterFactId`,
   and `Source::Fetch` onto a local target.

   **One declaration rule makes three of the four unreachable by construction.** A local
   relation's signature may not name another local relation as a field type. All three
   register-identity forms compare against or follow a *fact-typed field*, and a base schema can
   never name a query-local predicate — so with that rule they arrive only on a hand-built plan,
   which is exactly how `Source::Fetch`'s own cross-predicate check is already treated, and they
   are tested the same way. That leaves **one** refusal needing front-end work:
   `Project::FactRef` of a local row, which is what `X where Reach X` spells.

   **`Project::Value` is a fifth construct that reads a local row's own id, and what makes it
   unreachable is item 4's key-only rule rather than any refusal here.** A value-side field is
   fetched with `point` on the *register's own* `fact_id`, not on a referent's — so a local
   relation with a value side would route a local identity into `point` on the ordinary projection
   path with all four refusals above still passing. Nothing observable escapes even then, but the
   list stops being exhaustive and `Relation::point` becomes a reachable path with no stated
   contract. Item 4 removes the case at its source: a local relation is key-only, so flatten emits
   no `Project::Value` against one, and the fifth construct is refused by what a local relation
   *is* rather than by a check somebody has to remember to write.

   Stated as a limitation rather than discovered as one: a derived tuple cannot reference
   another derived tuple, so path reconstruction is out of the first cut. That is the price of
   internal-only identity, and it is what buys delta-as-a-second-relation in item 5. A fetch
   *through* a local row's field stays legal when that field's declared referent is a base
   predicate — which is the worked `Reach` case exactly, since its fields hold `src.Decl` ids
   and the `Reach` row's own identity is never read.

   **Identities are assigned canonically, in encoded-key order — not in the order tuples
   arrived.** This is not a tidiness preference: `Executor::resume` **hard-compares** the saved
   `fact_id` against the re-derived row's and answers `BadResumeKey` when they differ, and item
   3's refusals do not help, because that check is inside the executor and applies to every
   register whatever the language can observe. So derived identity stability is already a
   requirement of resume, with a badly-named error waiting behind it.

   Assign by insertion order and every one of these becomes a resume-compatibility surface: rule
   scheduling, rectangular versus triangular expansion (item 5b), and the snapshot representation
   (item 5). Assign in encoded-key order and a derived id is a function of *content*, so all
   three become invisible to a cursor and none of them has to be pinned in a fingerprint to stay
   safe. A sorted snapshot has that order to hand already.

   **When it is assigned is not a detail, and the naive reading does not work.** A tuple's rank in
   encoded-key order *changes* as later rounds insert tuples before it, so "assign by rank" cannot
   mean assign at derivation. It means: **the ids of a relation are minted when that relation is
   finalised** — at convergence for an accumulated relation, at the end of its stratum for a
   `Once` one — and every id a rule sees *during* the fixpoint is arbitrary and unobservable,
   which item 3's four refusals are what make safe. That is sound because nothing else can reach
   one: the answer plan scans only finalised relations, and a suspend mid-fixpoint is not
   representable (Movement 4), so a finalised rank is the only derived identity a cursor can ever
   hold. Rank counts from **one**, because sequence 0 is reserved and `FactId::new` refuses it,
   and the relation's cardinality is bounded by `MAX_FACT_SEQUENCE` like any other predicate's —
   **enforced by clamping item 6's retained-facts limit to it at configuration, not by a diagnostic
   at finalisation.** `FactId::new` is fallible and refuses a sequence past the maximum, so the
   alternative is an error variant on a path with no query left to blame, reachable only when an
   operator sets a limit higher than the id space. Clamping refuses earlier, by name, through a
   limit that already exists, and leaves finalisation with no failure to report — which is also
   what stops an `unwrap` being written there on a data path. Versioning the assignment algorithm
   into the envelope is the fallback if canonical assignment turns out to cost too much — it
   makes upgrades refuse cleanly instead of failing obscurely, which is strictly worse than not
   needing to.

   **The rationale must not claim this is a new risk, because it is not.** `Catalogued` already
   mints ordinary `FactId`s for virtual rows, a whole-row `Project::FactRef` over one is
   accepted, `fjord-server::rows` writes the id straight to the wire, and
   `fjord_client::expand::Expander` caches by `FactId` alone for the life of a **shell session**
   — resting, in its own comment, on I11's promise that an id is never reused. But a catalogue
   id is a *position in a listing*, and a new query relists. So: list databases, create one that
   sorts earlier, list again in the same session, and a cached entry answers for the wrong
   database. That is a live identity-scope hole in a shipped feature, not a hypothetical one
   recursion introduces.

   **The choice is made here rather than left as an "either", because an open branch inside a
   settled movement is a decision nobody owns.** The two candidates were: give a virtual id a
   documented lifetime and scope every cache to it, or make virtual identity content-derived so an
   id survives a relisting. **The lifetime wins, and the alternative is not merely less
   convenient — it does not fit.** A `FactId` is a snowflake: a 24-bit predicate tag over a 40-bit
   sequence, minted through `FactId::new`, which reserves sequence 0. Content-derived would mean an
   injective map from a database name into 40 bits with nowhere to keep an allocation table — the
   catalogue materialises its listing per request from a directory walk and persists nothing. A
   hash instead of a map collides, and two databases sharing an id is the same wrong answer by a
   longer route.

   **The selected contract: a virtual `FactId` is valid only within the listing that minted it,
   and nothing may cache one across requests.** `Catalogue::of` assigns the sequence from
   `rows.into_iter().enumerate()` — the id *is* the row's position — so this states what the id has
   always meant rather than restricting it. Three consequences follow, all owed here rather than by
   recursion: [I11](website/content/invariants.md#i11) gains an explicit carve-out, because "an id
   is never reused" is a promise about stored facts that virtual rows do not keep and `Expander`'s
   comment currently cites in its general form; `Expander` drops its cached entries for virtual
   predicates at every request boundary, which needs no new index because a `FactId` carries its
   predicate in its high three bytes; and the *first* resolution of a virtual id needs a mechanism
   of its own, because cache scoping cannot reach it.

   **That third consequence corrects a claim this plan made and could not support.** An earlier
   draft said item 12's listing digest makes a stale virtual id detectable. It does not, outside a
   cursor resume: `FETCH` is a separate request that rematerialises the catalogue and resolves
   positions against the listing it just built, so an expander with an empty cache resolves a
   correctly-scoped id against the wrong listing and no digest is anywhere near the exchange. The
   scoping rule above is still right and still necessary — it is what makes caching *across*
   requests illegal — but it is not sufficient, and the sufficient part is
   [the fetch round trip](#the-fetch-round-trip), which is a protocol change and not a recursion
   one.

   Proved by a test that changes the catalogue between requests while an expander is alive, so the
   aliasing is *observed* rather than reasoned about — and by its negative, a cached entry for an
   ordinary stored fact surviving the same boundary, since clearing everything would be a
   correctness fix that quietly deletes the cache's reason to exist.
4. **Rule heads are reconciled with declared key order.** A local signature's declaration order
   is physical key order; a query's head record is **sorted by name at lowering**
   (`lower.rs:465`). Encoding a projected record straight into a `RelationDecl` therefore puts
   values under the wrong physical fields, and same-typed fields make it silent while the
   relation still scans and decodes consistently — answering reversed edges. The answer is an
   explicit name-to-declared-position materialisation projection, requiring exactly the declared
   field set, rejecting missing, extra and duplicate fields at the declaration's contract layer.

   **Record-only in the first cut, stated as a restriction rather than left to be discovered.**
   `Predicate` holds `key: PredicateTy` — a **bare** type, not a record wrapper — so `int` and a
   union are both legal top-level predicate keys today, and "a local relation reuses the schema
   type grammar in full" therefore promises `with Count : int` and `with T : <A | B>` heads that
   the projection above does not define: a scalar head has no field set to reconcile, and a union
   head needs the head expression to carry a discriminant, which nothing here says how to produce.
   The gap is not academic — left open it admits either an undocumented record-only
   implementation of a wider promise, or an invented wrapper encoding at the head of a relation.
   **So a local relation's top-level type must be a record**, refused by name
   (`reject/non-record-relation`); scalar and union heads are a later cut that owes the projection
   its own definition, and Movement 7's census asserts the refusal so the restriction cannot pass
   silently for the full grammar.

   **And a local relation is *key-only*: the whole declared record is the key, and there is no
   value side.** `Predicate` carries `value: Option<PredicateTy>` and nothing said which a
   `RelationDecl` gets, while three other items had already assumed an answer. Item 3's canonical
   identity is a rank in **encoded-key** order, which is a function of the tuple only if the key
   *is* the tuple; item 5b's `Δ = candidates - A` is a set difference over those same bytes; and a
   value side is read through the row's own id, which is the fifth identity construct item 3 now
   records. Two tuples agreeing on their key and differing in a value would be one tuple to the
   deduplicator, one rank to the allocator, and two rows to a scan — three subsystems disagreeing
   about how many facts there are. The cost is that a derived relation cannot carry a payload
   outside its index, which is cheap here because a local relation is re-derived per chunk rather
   than stored. Structural, not validated: a `RelationDecl` has no value field to set.
5. **The owned-scan representation is settled and costed.** `FactStore::Scan` is an owned
   associated type with no lifetime, so a `BTreeMap::range` iterator cannot be returned from
   `scan(&self)` — `MemStore` copes by cloning the matching range into a `Vec`. Doing that for
   an accumulated relation means a relation-sized clone **per scan open**, and semi-naive opens
   the accumulated and delta relations once per rule per round, with magic adding more. The
   shape to reach for is an immutable `Arc`-backed sorted snapshot per round, which also
   supplies 5b. If the answer is instead a GAT on `FactStore`, that is a workspace-wide seam
   change and gets priced as one rather than assumed.

   **The naive reading of "a snapshot per round" is quadratic, and no limit in item 6 can see
   it.** Rebuild a contiguous accumulated snapshot each round and a chain deriving one tuple per
   round copies 1 + 2 + … + N. Retained bytes measures peak *live* memory, attempts measures rule
   outputs, and Movement 1's allocation guard forbids a relation-sized clone per scan or per open
   — so all three stay green while snapshot construction dominates the runtime.

   **So the representation is persistent or segmented, and a round shares its predecessor's
   storage. The budgeted-rebuild alternative is withdrawn.** A budget bounds the blast radius and
   not the complexity: a contiguous rebuild is still quadratic right up to the point it refuses,
   and refusing is the wrong answer to a legitimately deep chain. Leaving both options open also
   left Movement 1's categorical guard with nothing consistent to guard. That guard is
   chain-shaped, measuring bytes copied **across rounds** rather than per open.

   **And the copy guards do not close the hole they look like they close: a segmented
   implementation can move the quadratic cost from building to *reading*.** Keep one segment per
   round and construction is O(1) per round, opening a scan allocates nothing, and no relation-
   sized clone happens anywhere — every guard above stays green — while `next`, a narrow seek and
   a point lookup each consult an unbounded number of historical segments. The relation becomes
   *history-sensitive*: its read cost depends on how many rounds produced it rather than on what
   it holds, which is the same asymptotic defect wearing the other face. Nothing in the copy
   guards can see it, because nothing is copied.

   **So the criterion is representation-independent and stated over work, not over layout:** a
   relation's read cost is bounded by its **final size**, not by the number of rounds that built
   it. The test builds identical final contents twice — once inserted in a single batch, once
   over N one-tuple rounds — and compares four measurements between them through a counter the
   representation increments: an empty-range seek, a narrow seek, a point lookup, and a full
   scan. The batch-built relation is the oracle; the N-round one may cost a bounded factor more
   and may not grow with N. Retained-byte accounting includes **segment and index metadata**, or
   a segmented representation reports the tuples and hides its own bookkeeping.

   This is deliberately not a vote for persistent over segmented. A segmented representation that
   compacts, or bounds its segment count, passes it; only the one-segment-per-round implementation
   fails, and that is the implementation the present criteria permit.

   **What is green in Movement 0 is the criterion, not a representation, and saying so is what
   keeps this row honest.** Movement 1 builds the relation; a work bound measured before it exists
   can only be measured against a placeholder, which this section's own classification calls
   evidence of nothing. So Movement 0 lands the bound as a **trait-level harness** — parameterised
   over the relation seam and green against a deliberately obvious reference implementation, a
   sorted `Vec` rebuilt per round — and that proves the harness and its oracle and nothing about
   the shipped representation. **The four measurements then belong to Movement 1's acceptance
   list**, where the subject exists. Leaving them here alone is precisely how the
   one-segment-per-round implementation would ship green.

   **The overlay half of this is already answered and should not be re-litigated.**
   `Catalogued<S>` declares `type Scan = Scan<S::Scan>` — an enum sum over the wrapped store's
   scan and its own — so `Overlay<S>` is choosing the *relation* side of a sum whose shape
   ships. What is genuinely open is only the relation's own snapshot.

   **5b. Round visibility is a simultaneous SCC transition, not a per-rule freeze.** "A rule
   must not observe its own insertions" is necessary and not sufficient: freezing per rule lets
   a later rule see an earlier rule's same-round output, which still reaches the least fixpoint
   for positive Datalog but makes round numbers, work limits and profile counts artefacts of
   declaration order. That matters here for a specific reason — **the deferred closure operator
   below defines minimum BFS depth as the round of first derivation**, so an order-dependent
   round number would quietly cost that feature. The transition to write down:

   0. `A_1 = Δ_1 = dedup(seed output)`, and **`A_r` contains `Δ_r` at every round** — accumulated
      is everything derived through round `r`, delta is the part of it that is new. Stated first
      because it is the clause the other seven are unsound without, and because "delta and
      accumulated are distinct relations" invites the opposite reading;
   1. every rule in the SCC reads the same accumulated snapshot `A_r` and delta snapshot `Δ_r`;
   2. a rule with `k` recursive occurrences contributes one delta variant per occurrence;
   3. the selected occurrence reads `Δ_r`; every non-selected recursive occurrence reads `A_r`;
   4. candidates for every predicate in the SCC stream into one shared deduplicator, which compares
      **encoded keys and never identities** — an id minted during a fixpoint is arbitrary (item 3),
      so one tuple wears two of them and an id-keyed set would never converge;
   5. `Δ_(r+1) = candidates - A_r`, visible to every rule only at the next round;
   6. `A_(r+1) = A_r ∪ Δ_(r+1)`; and
   7. the SCC converges when every predicate's next delta is empty.

   **Step 0 fails in two directions at once when it is left implicit, which is why it is a step and
   not a footnote.** Read `A_r` as *excluding* `Δ_r` and step 3 is incomplete — a rule with two
   recursive occurrences never sees both atoms drawn from round `r`, because neither variant offers
   the selected occurrence its round-`r` partner — while step 5 stops terminating, since a tuple
   already in `Δ_r` survives a subtraction that does not contain it and reappears in every later
   delta. Both are silent: the first answers short, the second runs until the round backstop. The
   containment was inferable from one sentence in Movement 3 about a tuple holding two `FactId`s,
   and nowhere else in this section.

   **Step 3 is the rectangular expansion, and that is a choice, not an oversight.** It is
   complete, and it re-derives any tuple whose derivation matches `Δ_r` at two or more atoms —
   wasted work that step 5 deduplicates away, never a wrong answer. The triangular form (`A_r`
   before the selected occurrence, `A_(r-1)` after) removes those duplicates and is deliberately
   *not* taken first. Writing that down is the point: otherwise it lands later as a "bug fix"
   while silently moving the rule-output-attempt counts item 6 budgets and the profile numbers
   5b exists to keep stable.
6. **Resource limits cover work and bytes, not just cardinality — and none of them is
   semantics.** Named, separate limits for retained facts, retained encoded bytes, rows examined,
   rule-output attempts, fixpoint rounds as a defensive backstop, and generated program size
   (adorned, magic and supplementary relations are produced at compile time and are themselves
   unbounded).

   | Limit | Charged over | Scope | Reset | Outcome |
   |---|---|---|---|---|
   | Retained facts | every relation live in the program | one fixpoint derivation | per chunk | `limit/retained-facts` |
   | Retained encoded bytes | the same, **peak live** | one fixpoint derivation | per chunk | `limit/retained-bytes` |
   | Rule-output attempts | every rule of the executable | one fixpoint derivation | per chunk | `limit/rule-attempts` |
   | Fixpoint rounds (backstop) | each SCC | one fixpoint derivation | per chunk | `limit/rounds` |
   | Generated program size | the executable, **incrementally** | one compilation | per compilation | terminal for *mandatory* expansion; **falls back** for the magic rewrite — see item 7 |
   | Rows examined | **one executor** | see below | per executor | `FjordError::ExaminedCeiling` |

   **The classification is performed here rather than promised, and the answer is uniform: all of
   them are deployment policy. None is semantics, none enters the program fingerprint.** An
   earlier draft split them — some semantic and fingerprinted, some policy — and then tried to
   keep the semantic ones optimiser-independent by charging them over the *logical* program. That
   does not work, and the reason is worth keeping because it is the shape of the whole question:
   **magic deliberately derives fewer tuples of the user's own relations.** `P^bf` holds the
   demanded subset of `P`, so "retained facts of the logical program" is a different number
   depending on whether magic ran; attempts and rounds are worse, since defining them
   independently of the optimiser would mean evaluating the unmagicked computation, which is
   precisely what magic exists to avoid.

   So the contradiction is resolved by giving it up. **A limit refuses; it does not change an
   answer**, and a reproducibility claim is about answers. Two consequences follow directly and
   both are improvements:

   - **Magic's guarantee narrows to *static* validity** — parse, typecheck, safety, stratification
     — which is what it should always have said. An optimiser changing whether a query hits a
     resource ceiling is ordinary and unremarkable; an index that makes a query fit a timeout is
     the same phenomenon and nobody calls it a semantic change. An optimiser changing which
     programs are *well-formed* is the thing worth forbidding, and item 7 still forbids it.
   - **What overflows decides whether there is a fallback at all.** Mandatory expansion — the
     semi-naive variants, and item 10's disjunction normalisation — has nothing to fall back
     *to*, so overflowing it is terminal. The magic rewrite is optional by definition, so
     overflowing it falls back to the unmagicked executable that was already in hand.
   - **Nothing falls back at runtime.** A limit reached during derivation is simply a refusal, so
     fallback stays a compile-time decision — which is what keeps the selected executable a pure
     function of source, schema and engine build, and therefore what lets item 13 validate a
     cursor cheaply, before a row description, instead of after a fixpoint.

   The consequence to state rather than hide, the same one rows examined already carries: **a
   resumed request can be refused by a limit its first page was never measured against**, because
   policy can change between two pages and no cursor pins it.

   **Two things a cardinality limit does not bound.** First, *peak live representation*:
   accumulated and delta indexes, per-round snapshot state, candidate-dedup state, magic and
   supplementary relations and their metadata are all live at once, so "retained encoded bytes"
   must either charge the peak or declare and mechanically guard a strict multiplier from logical
   payload to it. Second, *generation itself*: a generated-program limit checked after adornment
   does not stop adornment exhausting memory, so it is enforced incrementally, before each adorned
   relation or rule is allocated. Candidates stream through deduplication and the limit check for
   the same reason — an unbounded per-rule candidate buffer is the materialised-result-set
   anti-pattern under another name, and the anti-pattern list is right to call it one.

   **A budget nothing can bypass, rather than a budget every site is asked to remember.** The
   deferred half of this item read "mechanical proof that every driver and compiler charge site
   uses it", and there is no mechanism by which that is mechanical: a state machine proven correct
   in isolation plus an audit of its callers is the arrangement that fails silently on the
   eleventh site somebody adds. So the budget is a **chokepoint**. Retaining a tuple, allocating a
   generated relation or rule, and copying between round snapshots are reachable only through an
   API that charges, and the types the driver and the generator hold offer no other way to obtain
   one — which is item 15's argument applied a second time, an illegal state that cannot be
   constructed beating one that is rejected, and which turns a coverage claim no test can make
   into a structural one the compiler enforces. The same shape settles the one limit with a hard
   ceiling above it: **retained facts is clamped to `MAX_FACT_SEQUENCE` at configuration** (item
   3), so canonical-id finalisation has no failure to report.

   **Charging per derivation and resetting per chunk is what resume needs**: every chunk
   re-derives the same fixpoint from the same frozen base, so every chunk reaches the same limit
   at the same point, and a resumed read behaves like an uninterrupted one. A ceiling on total
   work **across a paged read is therefore not listed at all**, and its absence is deliberate:
   each page is a new request and the cursor is client-held, so a cumulative counter has nowhere
   to live that a client cannot reset. It is not a policy dial that has yet to be built — it is
   unavailable in a stateless model, and it returns only with a portal, as portal state.

   **The rows-examined row is the one to read twice, and it is the only one already built.**
   It was a prerequisite rather than a decision: **every other limit above is output-side**, so a
   recursive rule that scans an arbitrarily large base and produces zero candidates leaves
   retained facts, bytes, attempts and rounds all reading zero while the work is unbounded. Until
   it existed, such a rule was stoppable only by whoever held the cancellation token.

   It ships as `Executor::with_examined_ceiling`, counted in the tick that already runs per row
   for the cancellation stride and the profile. A listener defaults it from
   `session::EXAMINED_CEILING` — chosen from the measured ~400 ns/row floor so that a chunk is
   bounded at roughly 25 seconds of scanning and still sits about seven times above the largest
   predicate in the published corpus — and an embedding may set a tighter deployment policy.
   `Executor::new` remains unlimited, because an embedded caller reading its own database is
   entitled to no ceiling at all.

   **Its scope is one executor, and "per chunk" is true only while a chunk is one plan.** The
   tally is private state on the `Executor`, and a fixpoint builds a new one per rule per round —
   so N rules would each be entitled to the whole ceiling, and a program could examine N times
   what the server thinks it capped. Nothing is wrong in the shipped code, because there is no
   driver yet to aggregate across; what is wrong is any claim that the ceiling is per chunk for a
   `Program`. **The driver owns one remaining budget** (item 14), seeds each rule's executor from
   it, and decrements it by work actually done — and the acceptance criterion is the aggregate
   one: several rules, each individually under the ceiling, exceeding it only together.

   The mechanical note for whoever builds it: `enumerate` consumes `self`, so there is no
   read-back. `Profile` is the natural carrier — it is already threaded through every chunk and
   already *added into* rather than replaced, so a scalar examined total on it gives the driver
   its decrement and gives `--profile` the per-program figure Movement 8 wants anyway.

   **Checked per row, not on the cancellation stride**, and that is not a preference: the
   `step` path rebuilds its deadline per call, so `since_poll` restarts at zero and a
   stride-checked ceiling would never fire for a caller driving the machine by hand. Polling a
   token earns its stride; a `u64` compare does not need one. **Neither it nor the cancellation
   token reaches the driver's own work**, which is why item 14 now owes work units of its own
   rather than only a budget to hand down.

7. **Magic failure falls back; it never changes what the language accepts *statically*.** The
   qualifier is load-bearing and was added after the limits in item 6 turned out to be policy:
   magic guarantees that a program which parses, typechecks, passes safety and stratifies keeps
   doing so — and guarantees nothing about whether it exhausts a resource, because resource
   exhaustion is not a property of the language. Fallback is therefore **compile-time only**: an
   unstratifiable transformed program, or a rewrite that overflows the generated-program limit
   while the unmagicked executable fits. See Movement 6.

   **The trigger is structural, not a list of error kinds — and a list is what a draft of this
   item wrote.** Enumerating "unstratifiable, or generated-program overflow" leaves every other
   compile-time failure of the transformation outside the guarantee, and one is already reachable:
   **execution-tag exhaustion**. Generated magic and supplementary relations consume the same
   bounded namespace item 2 bounds, so near `MAX_TAGGABLE_PREDICATE` a program whose unmagicked
   form fits can be rejected outright for relations that were optional by definition. That is
   magic deciding what the language accepts, which is the one thing this item forbids.

   So the pipeline is ordered rather than the errors classified: **the unmagicked rules are
   retained, the magic form is carried through every downstream phase, and only a candidate that
   finishes compiling is selected.** Item 6 already calls the fallback "the unmagicked executable
   that was already in hand" — this makes that literal.

   **The selection point is after flattening, not after magic generation, and putting it earlier
   was a hole rather than a wording slip.** Step 6 of item 9's phase order generates magic rules;
   steps 7, 8 and 9 re-analyse the candidate, generate semi-naive variants and flatten. Select at
   step 6 and every failure this item exists to catch lands *after* the decision has been taken: it
   is a supplementary relation's **delta variants** that exhaust the tag namespace, semi-naive
   expansion that pushes a rewrite past the generated-program limit, and the *transformed*
   dependency graph that turns out to hold a negative cycle — none of the three is visible when
   magic generation returns. So the branch is not chosen until step 9 has succeeded for it, and any
   failure at 6 through 9 unique to the magic branch discards the candidate and runs steps 7 to 9
   over the retained unmagicked rules instead.

   **Unstratifiability of the candidate is a member of that set, not a case beside it.** Movement 6
   named it first and treats it as the fallback's motivating example, which is how it came to be
   written as though it were the *only* structural trigger. It is one, and it is caught the same
   way as the resource ones: step 7 fails, the candidate is discarded, no diagnostic is emitted,
   and the unmagicked rules carry on. That is the point of stating the trigger as a pipeline order.

   **What is retained is the rule set, not a second compiled executable.** Compiling both branches
   all the way through 7 and 8 eagerly, as a review proposed, buys exactly the same guarantee and
   pays for it on every *successful* magic compile — which is the case the transformation exists
   for. The unmagicked rules are in hand at step 5 already and cost nothing to keep, and their SCCs
   are step 4's, already computed and unaffected by a rewrite that was discarded; the baseline is
   re-analysed and flattened only if the fallback is actually taken.

   **And "a mandatory failure is terminal" means mandatory in the *baseline* branch.** A failure
   the unmagicked rules produce too — a program that overflows the limit or exhausts the tags with
   no magic in it — is terminal, because there is nothing to fall back to and nothing optional
   caused it. Only a failure unique to the transformed branch falls back. The guarantee then
   follows from the shape of the pipeline, and no compile-time failure mode invented later can
   leak out of a list nobody remembered to extend.
8. **The executable seam is specified**: a `PreparedQuery`/`Executable` sum preserving the exact
   no-`with` fast path, the program fingerprint's coverage, the program profile model, the
   server paging and count path, `reads_virtual` over every rule rather than one plan, and the
   recursive `I8` guard. The token that carries the fingerprint is item 13; the snapshot the
   guard is about is item 14. See Movements 4 and 8.
9. **A program of named rules exists, over the AST this engine already has.** Adornment and
   semi-naive variant generation are *clause* rewrites: they need head arguments, ordered body
   atoms, polarity, variables and spans. `Plan` has erased all of that. **What they do not need
   is a second query IR** — `syntax::Ast` already retains every one of them (`ExprKind::Var`,
   `ExprKind::Fact`, `QueryStmt::Negation`, node ids carrying spans), and inventing a parallel
   logical IR would be a second source of truth for what a query is, the same objection this
   plan already makes to duplicating the type grammar. What is missing is the *program*: several
   named `Query<NodeId>` rules over one syntax store, **plus the answer goal as a distinguished
   rule** — see Movement 6, which cannot seed anything without it.

   **The SIPS seam exists too, and it is `collect`, not `Plan`.** `flatten::Collected` holds the
   statements, `Deps` and head reads, and is built *before* an order is chosen; `Deps` is
   symbol-level `captures`/`reads` with no plan structure in it. So the refactor is to make
   collection runnable over an arbitrary rule body independently of plan emission — after which
   adornment reads the collected statements plus `reorder`'s frontier order, and reconstructs
   nothing. The phase order that falls out:

   1. lower declarations into a program of named rules over the existing syntax tree;
   2. resolve and typecheck the program;
   3. run `collect` per rule for its statements and symbol dependencies;
   4. validate recursive safety (Movement 2's termination rule) and stratify the source program;
   5. **normalise the bodies of the rules about to be rewritten** — item 10, which needs step 4's
      answer and is exactly why it is not step 1;
   6. generate magic rules;
   7. **re-collect, re-SCC and re-stratify the transformed candidate** — its own dependency graph,
      not the source program's;
   8. generate semi-naive variants; then
   9. flatten each executable rule to an ordinary `Plan` — with 6 through 9 run over the magic
      candidate first and **the transformed-versus-unmagicked selection made only once step 9 has
      succeeded for it**, because the failures that force the fallback are produced at 7, 8 and 9
      rather than at 6 (item 7).

   **Step 7 is not bookkeeping, and its absence was a hole rather than an omission.** Magic and
   supplementary relations are new predicates with new edges, so the transformed program's SCCs are
   *not* the source program's: Movement 6 states outright that the rewrite can unstratify a
   stratified program, and nothing downstream of step 6 was in a position to notice. Semi-naive
   needs the answer for a second reason that has nothing to do with negation — a delta variant is
   generated per recursive occurrence, and which occurrences are recursive is a fact about the
   *transformed* SCC membership. Source stratification metadata is insufficient before the
   negation question is even asked.

   **And step 7 is the soundness gate for magic over negation, not only the fallback's trigger.**
   Demand for a negated subgoal is derived from the body of the rule that *contains* the negation,
   so the magic predicate depends on that rule's prefix while the rule depends negatively on the
   predicate the demand is for — the standard route by which a rewrite of a perfectly stratified
   program acquires a negative cycle. Item 11's adornment rule is sound *because* step 7 catches
   that case and discards the candidate; without it the transformed program would be evaluated
   under a stratification that was never valid, and the negation would consult a relation that is
   not total. Recorded here because a step described as bookkeeping is a step a later optimisation
   deletes.

   **The two stratifications differ in what a failure means, which is the whole reason they are
   separate steps.** Step 4 runs over the user's program and a negative cycle there is
   `reject/unstratified`, naming a cycle in the user's own dependency graph. Step 7 runs over a
   program the compiler wrote, and a negative cycle there **emits no diagnostic at all**: it
   discards the candidate and takes the unmagicked fallback, because reporting a cycle the
   optimiser invented as though the user had written one is precisely what Movement 6 forbids. A
   single stratification pass reused for both is how that distinction gets lost.

   Every static refusal in this section is checked at step 4 or earlier, which is what lets a
   diagnostic name a source variable and a span. A language rule checked after step 9 would be
   a language rule living downstream of an optimiser — and normalisation sitting at step 5 means
   no refusal is ever stated over a body the compiler rewrote.
10. **A rule body is normalised to disjunction-free form; a query body is not.** A disjunction
    is one level with one alternative per branch (`flatten.rs`, and every branch must be a fact
    pattern today). Semi-naive is defined "per recursive occurrence", and a level mixing a
    recursive alternative with a base one belongs to neither half of the seed/step split:

    ```sigla
    with P : { x : int } = ( {x = X} where P {x = X} | base.Seed {x = X} )
    ```

    Classified as a step rule, it has no seed, so Δ starts empty and the fixpoint terminates at
    zero. Retain the base alternative inside every delta variant instead and it re-runs every
    round, so a variant produces output without consuming its selected delta — which inflates
    round counts, attempt limits and profiles, and can reach a resource limit the naive
    evaluator never approaches.

    **The rule: a body normalises to the *product* of its disjunctive statements**, before
    adornment and before delta generation. Not one rule per branch: a body is a conjunction, so
    `(A | B); (C | D)` is the four clauses `AC`, `AD`, `BC`, `BD`, and
    anything less answers differently. `conjoined_disjunctions_do_not_multiply` already writes
    the arithmetic down from the other side — "three two-branch disjunctions in conjunction are
    2³ = 8 clauses if the alternation is distributed over the conjunction, and 3 levels of 2
    sources if it is not".

    **When, and to which rules — a draft said "every rule body, at step 1", and that contradicted
    this item's own carve-out.** SCC membership is not known until step 4 of item 9's phase order,
    so a step-1 expansion cannot honour the promise below that non-recursive strata keep today's
    multi-source levels — while retaining everything breaks the seed/step classification this item
    exists to fix. So normalisation is **step 5, after stratification**. The reorder is free, and
    that is the part worth writing down rather than re-deriving: dependency edges are drawn per
    body *occurrence* and a disjunction's branches are occurrences either way, so the SCCs computed
    over the original bodies are the ones expansion would have produced, and expanding afterwards
    adds and removes no occurrence — stratification cannot move underneath it.

    **The expansion set is the rules about to be rewritten, which is *not* the recursive ones.**
    Adornment is a clause rewrite over ordered body atoms, so a magicked *non-recursive* IDB rule
    needs a disjunction-free body exactly as a delta variant does. The set is therefore **every
    rule that is adorned or delta-generated**; "recursive rules only" is the narrower rule that
    would hand a disjunctive body to adornment and rediscover this at step 6.

    **Expansion does cost scan work. The third round of this plan recorded a rejection saying it
    did not; that rejection is withdrawn here rather than quietly deleted.** The reasoning behind
    it was that a multi-source level already enumerates the full cross product — level 0
    concatenates `A` then `B`, level 1 concatenates `C` then `D` per row of it, and
    `AC + AD + BC + BD` is the same four combinations the four clauses run. True of the
    *combinations*, and false of the *scans*, because it counts only the innermost level. Level 0
    is entered once by the multi-source plan and once per clause containing its branch by the
    expanded one:

    | level | multi-source | four clauses |
    |---|---|---|
    | 0 | `a + b` | `2a + 2b` |
    | 1 | `(a+b)(c+d)` | `(a+b)(c+d)` |

    The deepest level is unchanged; every prefix level is multiplied. With `d` two-branch
    disjunctions the factor at depth `k` is `2^(d-k-1)`, so the outermost scan is repeated
    `2^(d-1)` times — bounded by the generated-program limit, since the factor is just the clause
    count, but exponential *within* that bound. And it bites hardest in exactly the case the
    cross-product argument looks safest in: when a later level is empty or highly selective the
    prefix is the whole cost, so the amplification is the query's cost rather than a rounding
    error on it.

    **That makes it a resource consequence, not a bookkeeping one, and it is therefore measured.**
    Rows examined, the examined ceiling, cancellation timing and refusal behaviour all move with
    it: a rewritten rule can reach `EXAMINED_CEILING` where the same rule unexpanded would not, so
    expansion can change a query's *answer* from rows to a refusal. Movement 3 owes a store-spy or
    profile guard over a rule with two disjunctions and an empty third level, asserting the
    measured amplification is the predicted `2^(d-k-1)` and no worse — a number, so a regression
    reports itself instead of appearing as a timeout. Movement 3 rather than 6 because delta
    generation is the first thing that expands a body, so an expanded rule exists there before
    magic does. What expansion moves *besides* scans stands
    as first written: the rule count, the rule-output-attempt tally, the profile and the program
    fingerprint.

    **And this is DNF expansion — at the rule level — which is worth admitting rather than
    defining away.** An earlier draft said it was not, on the grounds that the anti-pattern is
    about the executor's level shape. That distinction is true and it hides the cost: the
    combinatorics are identical, and calling it something else makes the generated-program limit
    look like caution rather than the necessity it is. The anti-pattern stays satisfied because
    a *query* still compiles to levels of sources and no plan is ever DNF-expanded; what expands
    is a rule, into more rules, and the limit in item 6 is what bounds it.

    **Two carve-outs, without which this fix breaks something else.** This is *rule-body*
    normalisation and it reaches only the rules named above: the answer plan, and every rule
    neither adorned nor delta-generated, keep the one-multi-source-level meaning — so Movement 7's
    "query disjunction still means one multi-source level" holds unchanged. And the answer goal,
    now a distinguished rule, is normalised **only to generate demand**: the plan that actually
    streams keeps its disjunctive shape, or `|` would quietly mean something else in exactly the
    query a person wrote.

    The guard belongs on both sides of that line, and today only one side has one: a disjunction
    inside a **non-recursive, unmagicked local relation** must still compile to one multi-source
    level, which `a_disjunction_is_one_level_with_a_source_per_branch` asserts for a query alone.
    Movement 7 owes the local-relation arm.
11. **A predicate reached only through negation still gets demand.** Magic keeps the rules
    demand reaches. A local recursive relation whose only use is a ground `!Blocked {x = X}` in
    a higher stratum therefore derives nothing, and the negation passes for everything — while
    the transformed program stays perfectly stratified, so Movement 6's unstratifiable-fallback
    never fires. Answer equality against a small fixture will not find it either.

    **A negated occurrence is not ground, and a draft of this item claimed it was.** The claim
    was that `reject/unbound-variable` makes every negated occurrence fully bound, so its demand
    is a single tuple. That rule constrains **variables**, and `_` is not one: an omitted field
    *is* a wildcard (`ty.rs` — "an omitted field is a wildcard, so `test.Edge {from = 1}` is any
    edge from 1"), and a wildcard inside a negation is legal and tested
    (`!test.Edge {from = X, to = _}`). So `!Blocked {from = X, to = _}` adorns `bf`, not `bb`,
    and a rewrite that seeds one ground tuple derives an incomplete `Blocked` and lets the
    negation admit rows it should reject.

    **The rule, stated over adornment instead.** Demand for a negated occurrence carries **that
    occurrence's own adornment**, partially bound included, and propagates into the callee's
    defining rules exactly as a positive call of the same adornment does. `magic_Blocked^bf(X)`
    computes precisely the `Blocked` tuples with `from = X`, which is exactly the set the
    negation must consult — sound for a stratified program, and no machinery adornment does not
    already need. Groundness was never what made this work; the adornment string was. What is
    still not propagated is demand *through* the negated subgoal into a nested body, which cannot
    arise because a negated group is itself refused.

    **The retreat is stated as a trigger or it is not stated at all, and a draft of this item
    failed that test.** It read: "for any negatively-reached IDB whose demand is not proven to
    cover what the negation consults, evaluate it unadorned" — a side condition with no decidable
    test behind it, which an implementer settles by never taking it or always taking it, and either
    way the plan has said nothing. There is also nothing left for it to catch. Demand at the
    occurrence's own adornment covers exactly what the negation consults, and the one way the
    rewrite can go wrong — a negative cycle the transformation invented — is caught structurally at
    step 7 of item 9's phase order, which discards the whole candidate. So the retreat is
    **deleted**, and the fallback it was reaching for is the unmagicked one item 7 already defines.
    Recorded rather than removed silently, because "evaluate it unadorned" is a reasonable-sounding
    sentence somebody will propose again.
12. **A virtual predicate is not a frozen base, and stateless resume assumes one.** Movement 4
    rests on the fixpoint being a pure function of a frozen base. True of a Complete database;
    **false of `fjord.db.*`**. `run_query` re-prepares on every request, paged ones included, and
    `prepare` rebuilds the listing from the registry — so between two `query_page` calls the
    catalogue can change, the program fingerprint still validates, and the fixpoint is re-derived
    from different facts. Item 3's identity scoping does not help: stable ids do not freeze
    contents, and a local relation may legally hold a *virtual* reference, since item 3 forbids
    only local-to-local.

    **This is not recursion's defect and must not be fixed only here.** A positional cursor into
    a renumbered listing already skips or repeats rows for an ordinary query — see
    [the cursor defect](#a-defect-not-a-gap--a-cursor-does-not-name-the-world-it-was-made-in).
    Recursion inherits the fix and makes the consequence worse, because re-derivation recomputes
    everything from the changed facts rather than mis-ordering one page.

    The decision: **carry a digest of the materialised listing into the cursor** — as half of the
    composite world stamp item 13 puts in the plain `Cursor`, before any recursion work depends on
    it — **and refuse on mismatch.** A *counter* cannot do this job:
    `Catalog::list()` walks the live directory tree with no snapshot and no registry lock, and
    create, remove and finish each become visible in more than one step, so a counter can stamp a
    listing captured mid-mutation with the same number as a later, different one. A digest over the
    encoded rows the catalogue already builds is self-consistent by construction, needs no
    coordination with the lifecycle at all, and fails in the safe direction: a torn capture hashes
    to something no consistent listing matches, so the resume refuses rather than proceeding on a
    state that never existed. It turns a silent wrong answer into a named refusal and matches what a
    cursor already claims: that it names the world it was made in. Refusing recursive paging over
    virtual predicates outright costs more than the problem is worth. Proven by a **server-level**
    test that mutates the catalogue between `query_page` calls; a generated frozen `MemStore` cannot
    reach this, which is why it has survived. The mutation-between-pages test is necessary and not
    sufficient: it says nothing about whether the listing and its stamp were captured consistently,
    so create, remove and **finish** each need a case *during* capture — `finish` especially, since
    it moves status, facts and bytes at once.

    **The two virtual predicates are not alike, and one of them has no snapshot to number.**
    `materialise` builds the listing and the interning counters together, but the counters are
    read by taking every interning stripe's lock in turn — so that capture is not point-in-time
    even as it happens, and the values move with every write. A generation makes `fjord.db.List`
    resumable; nothing can make `fjord.db.Interning` resumable, because there is no stable thing
    to number. So they split: a generation for the listing, and **`fjord.db.Interning` refused by
    name in a resume that crosses requests**. Numbering a value that thrashes on every ingest
    would produce a cursor that is always stale, which is a refusal with extra steps. Guards for
    counter movement and for mutation *during* capture, not only for a changed listing.

    **"Crosses requests" is the whole of the refusal, and a draft wrote "any resumable or recursive
    read", which is much larger and would have taken the predicate off every execution path.** Every
    server query resumes internally, so read literally that phrasing refuses `fjord.db.Interning` to
    plain `where` and to `count` as well — the same over-broad consequence that got the Writable
    refusal withdrawn one section above, arrived at by the same route.

    **The catalogue and the base database are asymmetric here, and the asymmetry is the reason the
    narrowing is safe rather than a concession.** A chunk boundary takes a fresh `reader()`, so the
    *base* genuinely moves under an unpaged stream and its stamp must be revalidated per chunk. The
    catalogue does not: `prepare` materialises the listing and the counters once, and `run_query`
    clones that same `Arc` into every chunk of the result. Within one request the counters are
    therefore fixed, a fixpoint over them is well defined, and there is nothing to refuse. What is
    genuinely unsafe is stateless paging — `query_page`, where each page is an independently
    prepared request that rematerialises the counters — and that is where the refusal lands, beside
    the listing digest, which travels in a cursor for exactly the same reason.

    **A virtual predicate read only by a rule reads as empty, silently.** `catalogue::reads`
    walks **one plan's** body, and `prepare` asks it about the answer plan alone. In a `Program`
    the answer may read only a local relation while a seed or recursive rule reads `fjord.db.*`
    — no `Catalogue` is built, the bare store is passed, the scan routes to fjall, which has no
    such keyspace, and the relation is *empty*. A wrong answer, not an error. So
    `Executable::reads_virtual` traverses the answer plan **and every generated rule**, and
    Movement 8 owes a program whose virtual predicate appears exclusively in a derivation rule.

13. **The resume token is an envelope; the executor's `Cursor` is left alone.** Item 12 and
    Movement 4 require a cursor to carry a *program* fingerprint and a virtual-snapshot
    generation. A cursor today is `{ version, plan: PlanFingerprint, entries }`;
    `build_cursor` writes the **answer plan's** fingerprint and `resume` validates against that
    same plan. Both ways out of that are wrong on their own: keep the answer-plan fingerprint and
    two programs with byte-identical answer plans but different rules — or different
    magic-versus-fallback selections — accept each other's cursors, which is an
    [I4](website/content/invariants.md#i4) violation with a wrong answer at the end of it;
    substitute the program fingerprint and the unchanged executor rejects every cursor it is
    handed.

    **So neither: wrap it.** A program's token is
    `{ version, program_fingerprint, inner: Cursor }`, validated at the `Executable` layer before
    `Executor::resume` sees the inner bytes. The executor's `Cursor` gains no *program* field, so
    I4's existing proof stands unaltered; program-level validation lands where program-level
    knowledge already lives, beside the server's existing pre-row-description fingerprint check;
    and a query with no `with` block emits a plain `Cursor`, which is the fast path item 8 exists
    to preserve. The trust model is unchanged — every field is compared against a freshly computed
    value, so an envelope field is no more forgeable than the one inside it.

    **The envelope carries its own version, for the reason `CURSOR_VERSION` exists.** A token is
    client-held and outlives the process that made it. Unversioned, the next build reads the old
    layout as the new one and validates fields that mean something else, which is the failure
    `CURSOR_VERSION` documents itself as preventing. It is separate from the inner cursor's
    version because the two move for different reasons: one says what a program's token is, the
    other what an entry is.

    **The world stamp goes into the plain `Cursor` first, before any of this — and two earlier
    drafts of this item had it the other way round.** `Cursor` becomes
    `{ version, plan, world: WorldStamp, entries }`, where
    `WorldStamp = { base: BaseIdentity, listing: Option<ListingDigest> }` is `None`-listed for a
    query that reads no virtual predicate, compared whole and refused whole. One value because the
    two halves answer one question — *which world was this token made in* — and because two
    independently validated fields is precisely how a path ends up carrying one of them and not
    the other.

    **Sequencing it through the envelope was the mistake, and naming the mistake is what keeps it
    from coming back.** The reasoning was that recursion should not disturb the plain path, so the
    stamp would ride in the program envelope now and lift into `Cursor` when the underlying defect
    was fixed. Both halves of that were wrong. The defect is *not* recursion's — a plain
    `where fjord.db.List {..}` paged across a `create` returns a silently short or repeated page
    today, and a cursor replayed against a different same-schema database has always been
    undetectable — so recursion would have been carrying a correct stamp past a broken path in
    order to avoid touching it. And the sequencing buys a **transitional token format that is pure
    cost**: one layout with the stamp in the envelope, a second with it in the cursor, two
    validation paths, two versions to reason about, and a migration between them whose only
    purpose is that the first one existed. Doing it in the other order deletes all of that. The
    envelope then never holds a world field at all, and ends up with exactly the one thing that is
    genuinely Program-specific: a program fingerprint, because a plain plan has no program to
    fingerprint.

    **This changes the engine's resume signature, and that is the price, paid once.** `FactStore`
    is `scan` + `point` and exposes neither an identity nor a listing, so a `Cursor` holding a
    world stamp holds a value the engine cannot recompute in order to compare it.
    `Executor::resume` therefore takes a **caller-supplied world stamp**, and `CURSOR_VERSION`
    bumps — validation happening in the database-owning layer, which is the only layer that can
    compute either half. It is also why the stamp is opaque to the engine: `Catalogued` is a store
    wrapper the executor cannot see past, so the layer that materialised the listing is the only
    one that can digest it.

    **The composite is one value, so its encoding owes what a concatenation does not.** The base
    half's Writable form ends in a variable-length instance id; append a listing digest to that and
    two different worlds can encode identically by moving bytes across the boundary between them —
    and an instance id is a directory name, so the input is user-supplied rather than adversarial
    only in theory. Every variable-length field is length-prefixed, or every one but the last is,
    and the guard is the base half's own extended to the composite: changing one field must move
    the bytes, **and so must shifting a field boundary while keeping the bytes the same**. A
    per-half round-trip cannot see this, because each half is unambiguous on its own.

    **An absent stamp must not be the value that matches everything.** An empty stamp comparing
    equal to itself is what lets the plain path keep working while the database-owning layer is
    built, and it is fail-open: a caller that stamps at neither end is indistinguishable from one
    that stamps correctly, and no test can tell them apart. `resume` taking the stamp as a required
    argument is half the fix; the other half is that the value is a `WorldStamp` with an explicit
    *unstamped* case rather than an empty byte string, so running without one is a keystroke
    somebody made instead of a line nobody wrote. The engine still compares bytes and still knows
    nothing about worlds.

    **What it costs at run time belongs with the fix
    ([the cursor defect](#a-defect-not-a-gap--a-cursor-does-not-name-the-world-it-was-made-in)),
    with one consequence to add there: on a database being ingested into, "a read a write crosses"
    is the common case and not the corner.** A fresh reader per chunk means every streaming query
    and every count against a busy Writable database can now end mid-stream in a named refusal,
    where today it silently answers a hybrid. It is the right answer and it is still a shipped
    behaviour change: whoever lands it owes the release note, and an embedder wanting continuity
    across an ingest owes itself a `finish` first.

    **A review once read "the cursor is left alone" and "the cursor names its world" as
    incompatible.** They were never incompatible, only ordered, and the order has now been
    reversed on its merits: the cursor gains the world stamp and gains no *program* knowledge, and
    "the executor's `Cursor` is left alone" narrows to what it always meant — the executor learns
    nothing about programs. Recorded so a later round does not restore the envelope-first
    sequencing by analogy with the paragraph it replaced.
14. **One base snapshot, owned by the driver, for every rule and every round.** A fixpoint runs
    many ordinary plans, and `FactStore` offers neither `Clone` nor a reader factory while
    `Executor<S>` **owns** its store and `enumerate` takes `self` by value. That signature is not
    incidental: it is [I8](website/content/invariants.md#i8)'s *structural* proof, and its own
    doc says so — every exit path drops the frame stack and the store handle, so no caller can
    park a live iterator across a suspend.

    The smallest resolution is a blanket `impl FactStore for &S` delegating `scan` and `point`,
    letting each rule run as `Executor<&S>` with no seam change and no new trait method. **But
    whichever is chosen, the consequence is the same and has to be written down: I8's structural
    guarantee moves from `Executor` to the program driver.** Dropping an `Executor<&S>` drops a
    reference; the driver becomes the owner, and I8 stops being free.

    **So it is a sealed, engine-private wrapper rather than a blanket impl, and the reason is
    *when* the replacement arrives.** A blanket impl is public and unconditional: from the moment
    it lands, any code anywhere can build an `Executor<&S>`, and `enumerate` consuming `self` —
    which the registry names as I8's proof — stops implying the snapshot was released. The driver
    that restores the guarantee, and the exit-path drop probes that demonstrate it, are Movements 2
    and 4. That is two movements in which a load-bearing invariant holds by convention, entered by
    a change whose own acceptance criterion tests the delegation and not the ownership discipline
    it dissolves. A wrapper the engine owns, constructible only by the driver, buys the same
    monomorphisation with none of that: the seam is widened for nobody else, and the window closes
    because it never opens.

    It is a correctness requirement and not only resource hygiene. "The fixpoint is a function of
    a frozen base" *means* one snapshot for every rule and every round — and with item 12 it
    means one `Catalogued` snapshot for the whole program, not one per rule. So the guard is
    three obligations, not one: one base snapshot observed by every rule and round (not one per
    rule, which would multiply fjall's open-snapshot count), the relation snapshots of Movement
    4, and *that* owner released on every exit path — done, suspend, cancel, limit, unwind.

    **The driver inherits the rows-examined budget for the same reason.** The ceiling is private
    state on an `Executor`, and the driver is about to create one per rule per round, so what is
    a chunk's budget today would silently become a budget *each*. One remaining count lives with
    the snapshot, is seeded into every rule and into the answer plan, and is decremented by work
    actually done — see item 6.

    **And the driver owes work units of its own, because a row budget cannot cover it.**
    Cancellation is polled in exactly one place — `Deadline::tick`, on the examined-rows stride,
    inside `advance` — so *every* mechanism this plan has for stopping is coupled to pulling
    another row from a store. The driver's largest phases pull none: candidate deduplication,
    persistent or segmented snapshot merging across rounds, relation indexing, and **canonical-id
    finalisation**, which is O(|relation|) over an already-derived relation with no executor in it
    at all (item 3). Seeding each child executor from one remaining row budget bounds the scanning
    and leaves all of that untouched, so a large derivation can ignore a cancelled token for the
    whole of it — and the "cancellation mid-fixpoint" criterion in Movement 4 passes green while
    exercising only executor scans.

    So the driver defines and counts its own units — **candidates deduplicated, tuples finalised,
    and bytes copied between round snapshots** — polls the token on the same stride discipline the
    executor uses, and charges item 6's retained-facts and retained-bytes limits at those points.
    None of it is new machinery: `Profile` already carries the executor's examined total across
    chunks, and these are scalars beside it. The arms that discharge it cancel *during*
    deduplication, *during* a snapshot merge and *during* final canonical-id assignment, each with
    no executor live, and each must observe the token within one stride.

15. **A local signature's field names have no representation, and the failure is silent.** The
    plan promises a local relation reuses the schema type grammar in full. It cannot today.
    `PredicateTy::Record` holds `Arc<[(Spur, PredicateTy)]>` and `Alternative::name` is a `Spur`
    — raw interner indices with no tier — while `decode_key` resolves them with
    `Symbol::Schema(*name)`, **unconditionally**. Query-local names live in the per-query
    `Rodeo`, a separate space with its own numbering. So a local signature whose nested record or
    union field names are local either raises `UnknownSymbol` or, worse, resolves to whatever
    schema string happens to share that index and answers with the wrong field name.

    `Symbol` exists for exactly this two-tier problem; `PredicateTy` predates its use. **The
    representation is selected here, and this item no longer offers a choice** — an open
    alternative in the one item that decides what a signature may contain is the last thing a
    foundation should ship with.

    **Selected: the name tier becomes a type parameter.** `PredicateTy<N = Spur>` and
    `Alternative<N = Spur>`, with a persisted schema holding `PredicateTy<Spur>` — today's type,
    byte for byte — and a local relation's signature holding `PredicateTy<Symbol>`. The ten-odd
    sites that today write `Symbol::Schema(*name)` unconditionally become generic over
    `N: Copy + Into<Symbol>`, with `impl From<Spur> for Symbol` supplying `Symbol::Schema`, so
    every existing call site keeps its exact present behaviour and the tier assertion moves to the
    one conversion.

    **The reason it is the parameter and not simply `Symbol` everywhere: an illegal state that
    cannot be constructed beats one that is rejected.** `Schema::new` is infallible and is
    constructed all over the tests and fixtures; make its field type `PredicateTy<Symbol>` and it
    becomes *capable* of holding `Symbol::Local`, at which point the fingerprint walk and
    `decode_key` can misresolve a name again and the only thing standing between them and a wrong
    answer is a validation nobody is obliged to call. With the parameter, a local type cannot be
    stored in a `Schema` without an explicit fallible conversion, and there is no path that
    forgets. The rejected alternative — `Symbol` everywhere plus a fallible `Schema::new` — is
    recorded because it is the one a later reader will propose: it is a smaller diff and a weaker
    guarantee.

    **The restriction alternative is rejected outright**, as this item's earlier draft nearly
    conceded: "a nested field name must already exist in the schema" is not a rule a person can
    hold, and a promise that a local relation reuses the type grammar *in full* cannot be kept by
    a grammar that silently excludes half the names.

    **It carries a new invariant, stated because the type parameter enforces only three of its
    four clauses.** A persisted `Schema` type contains schema-tier names only — structural. A
    local relation's type may contain local-tier names — structural. Two numerically equal `Spur`s
    drawn from the schema and local interners never alias — structural, since the tier travels
    with the name. And **every existing artifact is unchanged**: schema canonical forms,
    fingerprints, stored bytes, wire descriptors and base-query plan fingerprints. That last one
    is not structural, it is a non-regression obligation, and it is the clause a `N = Spur` default
    makes plausible rather than proven.

    Guards, all owed by Movement 0: **local-only names in a nested record and in a union**, each
    resolving to the local text; the adversarial case where a schema `Spur` and a local `Spur`
    hold the **same numeric value over different text**, which is the case that silently answers
    with the wrong field name today and the one a same-interner corpus can never provoke; and the
    full non-regression set for the fourth clause — every corpus entry's schema fingerprint,
    canonical form, stored bytes, descriptor and base plan fingerprint identical across the
    change. The worked example's `from` and `to` are schema names, so they resolve to *something*
    and a corpus built around it stays green over the defect; that is why the census asserts the
    local-only case rather than assuming a generator reaches it.

**Acceptance is a proof boundary, not a reading.** The previous version of this paragraph asked
for "a written answer for each of the fifteen, and a failing test first for items 1 and 4". That
is a documentation gate wearing acceptance criteria: fourteen of the fifteen could be satisfied by
prose, in a repository whose contract is property-first, and the movement whose entire job is to
be the foundation would be the one movement that proves least. Replaced by three classifications
and a table, on the principle that **an ignored test records an obligation and does not
demonstrate a property**:

- **Green here** — implemented and mechanically demonstrated *within this movement*. A claim in
  this class is one the suite would fail without.
- **Deferred, owned** — a compiling, named `#[ignore]`d guard, filed against one later movement
  that unignores it. The obligation is in the ledger, not in a paragraph; it is never described as
  proven.
- **Decided** — a written answer with no code yet. Legitimate, and it is *evidence of nothing*.
  An item may not sit here alone unless the table says so.

Items 1, 2, 4, 5 and **15** gate representation — 15 hardest of all, since a type model that
cannot name a local field decides what a signature may contain. **Items 9, 10 and 11 gate
Movement 3**, because each decides what a rule *is* before anything rewrites one; **item 14 gates
Movement 2**; items 12 and 13 gate Movement 4, and both are filed as defects against the existing
cursor besides. The rest gate the movement that names them. The rows-examined ceiling in item 6
was never a Movement 0 decision — it was missing code, and it has been written; what it still owes
is item 14's aggregation and its driver-side work units.

**Three of these are live defects in shipped, non-recursive behaviour, and they are repaired
*first* rather than alongside.** The plain cursor's world stamp, the virtual `FETCH` digest and
the terminal-`ERROR` client contract are all reachable today with no `with` block anywhere near
them — the ceiling in item 6 already produces an error *after* rows, which is exactly the frame
`Connection::next_row` mishandles. Building recursive resume on top of them means every later I4
failure has two candidate causes. Fixing them first also deletes the transitional token format
item 13 used to describe, which is the concrete payoff rather than a tidiness argument.

**The terminal-`ERROR` client contract — done, first of the three.** `Connection::recv_on` was
already turning an error frame into `Err` before `next_row` or `cancel` ever inspected a `kind`,
so the mishandling was never about the frame — it was that returning that `Err` skipped the same
release `COMPLETE` gets: the stream stayed in `self.open`, `Rows` stayed `Streaming`, and a second
read on it would wait on a stream whose server-side task had already returned. `Rows` gains a
third, terminal `Errored` state; `Connection::recv_stream_frame` releases a claimed stream on its
own `ERROR`, including before a `Rows` exists and on count, while `recv_row_frame` also marks an
open bookmark errored. The originating stream is retained so a session-level error on stream zero
cannot recycle a still-live claimed stream id. Guarded by
`a_mid_stream_error_ends_the_stream_the_way_complete_does` and
`a_cancel_racing_a_terminal_error_leaves_the_connection_working`
(`crates/fjord-client/tests/against_a_server.rs`) for the two post-description positions,
`a_session_error_does_not_recycle_a_query_stream_that_is_still_running` and
`a_session_error_does_not_recycle_a_fetch_stream_that_is_still_running` for the stream-zero
distinction — the latter delays the first positional `FETCHED` reply until after a second fetch,
so recycling the live id is observed as the wrong fact rather than only in bookkeeping — plus
`the_server_ceiling_stops_queries_and_counts_without_leaking_their_streams`: a real server under
an injected deployment ceiling of three, proving both server charge sites and the pre-`Rows`
count path. The terminal-stream guards prove release rather than assuming it by checking that later
work on the same connection **reuses** the errored stream's id; the stream-zero guards prove the
opposite boundary by forcing concurrent work onto another id. The world stamp's base half is done,
below, and the virtual fetch digest is done, after it; only the world stamp's **listing** half — the
`query_page` case item 13 also names — is still open.

**The plain cursor's base identity — done, second of the three, and the world stamp's `base` half
in full.** `Cursor` gains an explicit `WorldStamp::{Unstamped, Stamped}` — the stamped payload is
opaque bytes the database-owning layer computes and the engine only compares, never interprets,
which is what "opaque to the engine" in item 13
above meant literally: `fjord_store_fjall::world::BaseIdentity` is the typed value, `to_bytes`
its wire form, and neither name reaches `fjord-engine`. `Executor::build_cursor` and
`Executor::resume` both gained a `world` parameter (a builder, `with_world_stamp`, for the former,
since a fresh run has nothing to validate against); `resume` checks it third, after the version and
the plan fingerprint and before the empty-cursor shortcut, for the reason the plan fingerprint's own
check already gives — restarting is still an answer to whichever world asked. The explicit
`Unstamped` tag is distinct from `Stamped([])`, so omitting both ends is a choice in the call rather
than an empty default indistinguishable from a correctly supplied stamp. `CURSOR_VERSION` is 3.

Two `BaseIdentity` cases, matching the two-vs-three-field split item 13 settled on:
**`Complete { fingerprint }`** is the content fingerprint `finish` already computed, read off
`Database` once (`Database::mark_complete`, called alongside `seal`) and re-read for free on every
later chunk, since it cannot move. **`Writable { instance, incarnation, visible_seqno }`** is a live
handle's write position, bracket-read around the snapshot a chunk's reader takes
(`FjallDb::reader_stamped`, reading `Database::visible_seqno` — `#[doc(hidden)]`, pinned by the same
fjall version this section already named — before and after `snapshot()` and keeping the reading
only once the two agree), plus a nonce minted fresh by every `FjallDb::open` and held only in
memory. The nonce is what turns "a sequence number recovered from what a crash left durable can be
lower than a live cursor's stamp" from a case to reason about into a case that cannot arise: every
reopen is a new incarnation, so every cursor from a previous one is refused, full stop — proved by a
plain clean reopen (`store::reopening_mints_a_new_incarnation`), not a simulated crash, because the
guarantee does not depend on anything having been lost.

**The virtual fetch digest — done, third of the three, and [the fetch round
trip](#the-fetch-round-trip) in full.** No cursor is involved in a `FETCH`, so this is not the world
stamp: every catalogue table now carries its own `digest: u64` — an FNV-1a hash over its predicate
id and every row's length-prefixed encoded key, in the key order the catalogue already sorts to,
computed once at `materialise` — and `kinds::LISTING_DIGEST` (`l`) is a new frame the server sends
once per non-empty virtual predicate, right after `ROW_DESCRIPTION` and before the first row, exactly
when the prepared plan reads a virtual
predicate. `FETCH`'s payload gains a leading presence byte and an optional digest ahead of the ids it
already carried (`protocol::encode_fetch`/`decode_fetch`, both now `(ids, Option<u64>)`), and the
server's `fetch` handler refuses the whole request with a new `ServerError::StaleListing`
(`ErrorCode::Refused`) when a supplied digest disagrees with the listing it just materialised —
checked before a single id is resolved, never answered partway. The digest frame names its predicate,
so a query reading both virtual tables and a fetch reading one compares like with like; protocol
version 3 marks both that frame and the changed `FETCH` payload. `Rows` carries the digests transparently
(`set_listing_digest`, consumed in `next_row` and `cancel` exactly as `PROFILE` and `RESUME` are, just
earlier); `Connection::fetch` takes the one predicate's optional digest, while
`Expander::expand`/`prefetch` select it from `Rows::listing_digests()` for each predicate-grouped batch.
**One correction the client side
needed and the design note above did not anticipate:** `Expander`'s existing `unexpandable` cache
treats every `ClientError::Server` from a fetch as a permanent, predicate-level refusal — right for "no
such predicate", wrong for "the listing moved", which is a fact about one request and would otherwise
silently disable expansion of a virtual predicate for the rest of the session the first time a listing
changed. `ErrorCode::Refused` is unreachable from `fetch` any other way today, so `prefetch` now
propagates that code as a hard `Err` instead of caching it.

Guarded server-level, over a real socket, mutating the catalogue **between a query's row and the
first fetch of it** with an expander that has cached nothing —
`commands::query::surface::a_catalogue_change_between_a_query_and_its_first_fetch_is_refused`
(`crates/fjord-cli/src/commands/query.rs`) — with
`a_reference_into_a_virtual_predicate_expands` (unchanged, same file) standing as the positive
control a too-broad refusal would otherwise still pass, and the session shown still answering an
ordinary catalogue query afterwards. `fjord-server::catalogue` adds the digest's own properties —
two materialisations of the same listing agree, a changed listing or a changed interning counter
moves it, two listings of equal length but different content do not collide, and one table's digest
is independent of its siblings — while the client/server guards add a query reading both virtual
predicates and cancellation before the first virtual row. `fjord-wire::protocol` proves the frame,
its predicate id and the presence byte: digest zero round-trips as a real answer rather than as an
absence, and a flag byte that is neither 0 nor 1 is refused by name.

**The cursor's listing half — done, and it needed no `{ base, listing }` pair.** `query_page`'s
`with_listing_digest` (`fjord-server::session`) appends `fjord.db.List`'s digest to the same opaque
bytes the base identity already occupies, gated on `reads_listing` rather than on `catalogue.is_some()`
— a query reading only `fjord.db.Interning` still builds a `Catalogue`, from a placeholder empty
listing whose digest is a constant, and folding that in would make such a query look resumable
against a value that can never disagree. `Cursor` still carries one typed
`WorldStamp::{Unstamped, Stamped}` field, not a second one: a moved listing disagrees through the
same `FjordError::CursorWorld` check the base identity already uses, so no cursor field, no new
error variant, and every guard proving the base half keeps passing unchanged. `run_chunk` and
`count_chunk` both build the composite before the executor sees it, so the row path and the count
path resume under the same rule. `fjord.db.Interning` itself has no stable value to digest — the
write path's counters move on every write, not only when a listing changes — so a `QUERY_PAGE`
resuming a query that reads it is refused by name instead, as `ServerError::VolatileResume`, rather
than validated against a stamp that would always disagree.

Proved server-level, over a real socket and a real `FjallDb`, because a generated `(plan, store)`
pair in the engine's own battery holds one store for the whole property and cannot express a store
that changes mid-resume:
`against_a_server::a_write_between_two_pages_of_a_writable_database_is_refused` — seed, page,
write a new fact into the same still-Writable database, resume, and the resumed page errors rather
than silently answering from the state that existed when the second page was requested — with
`paging_a_writable_database_with_no_intervening_write_still_works` as the negative control a
too-broad refusal would otherwise still pass. `fjord-store-fjall` adds
`store::visible_seqno_moves_after_a_write_and_the_snapshot_agrees`, proving the bracket names the
snapshot it pairs with, and `world::*` proves the encoding: identical inputs encode identically,
every `Writable` field alone moves the bytes, and `Complete` and `Writable` can never collide.
`fjord-engine` adds `iter::an_empty_cursor_from_another_world_is_refused`, the world-stamp sibling of
`an_empty_cursor_from_another_plan_is_refused`, and `fixtures::run_with_suspends` — the shared I4
runner every corpus and proptest resume case already goes through — now stamps every run with a
fixed non-empty world, so the whole existing battery exercises the new field for free rather than
leaving it permanently at its untested default.

| item | green before Movement 0 closes | deferred, and to whom |
|---|---|---|
| **1, 9** | a hand-built `Program` AST preserves clause multiplicity and source order; duplicate declarations refuse; forward and mutual references resolve; per-rule `collect` equals today's single-query collection | source spelling and corpus execution — Movement 7 |
| **2** | the schema-first catalogue property against a **dense-array model**, virtual augmentation, deterministic tags, the exact-last and one-past bounds, base-only plans and fingerprints unchanged | generated magic and delta namespace exhaustion — Movement 6 |
| **3** | the four identity-observability refusals over hand-built IR; the canonical-id allocator mapping encoded-key rank to a sequence from one; the permutation property; the virtual fetch and cache repair | driver-level canonicality across rule scheduling and expansion strategy — Movement 3 |
| **4** | the projection property against an independent **string-name model**; non-lexical order; same-typed reversed fields; missing, extra and duplicate fields; scalar and union heads refused by name | the full source corpus — Movement 7 |
| **5** | the key-only rule; the representation contract and the history-sensitive work bound as a **trait-level harness**, green against a deliberately obvious reference relation — evidence about the harness, not about the shipped one | the four read measurements against the real representation — **Movement 1's acceptance list**; the implementation and simultaneous-SCC visibility — Movements 1–3 |
| **6** | a **pure budget state machine**: overflow-safe, monotone, no overshoot, reserve/release peak accounting, exact boundaries, a named outcome per limit; the retained-facts clamp to `MAX_FACT_SEQUENCE` | that charging is a **chokepoint** and not a convention — structural, discharged by the types the driver and generator hold rather than by a coverage claim — Movements 2 and 6 |
| **7** | the pipeline and fallback obligations registered | fault injection at every transformed-candidate phase — Movement 6 |
| **8** | the plain-path dispatch contract registered | the executable, server and inspection proof — Movement 8 |
| **10** | the DNF product checked against an independent **truth-table evaluator**; deterministic lexicographic product order; 2×2 and deeper | the scan-amplification witness — Movement 3 |
| **11** | negative-only and partially-bound cases registered | the magic-versus-unmagicked model property — Movement 6 |
| **12, 13** | **all green**: the plain cursor's world stamp — as a `WorldStamp` with an explicit unstamped case, encoded so a shifted field boundary moves the bytes — mutable-catalogue paging, Writable chunking and count, the **cross-request `fjord.db.Interning` refusal**, the fetch digest, a malformed token, the version, and terminal-`ERROR` client behaviour | the program fingerprint and envelope, and `reads_virtual` over generated rules — Movements 4 and 8 |
| **14** | the sealed reference wrapper passes the seam battery; reference ownership introduces no hidden clone and extends no snapshot lifetime; and the wrapper is **constructible only by the driver**, so I8 stays structural across the two movements before its probes exist | the one-snapshot driver and every exit-path drop probe — Movements 2 and 4 |
| **15** | the representation selected (above); nested local names and the cross-tier collision property green; persisted-schema and protocol non-regression green | **none — this closes here** |

**Where a model is named, it must be simpler than the thing it checks and share no code with it.**
The dense-array tag model, the string-name projection model and the truth-table DNF evaluator are
each written to be obviously correct and slow; a "model" that reuses the implementation's own
allocator, interner or normaliser proves that the code agrees with itself. New generated domains
get canonical `program::proptest` and `relation::proptest` strategies with population censuses,
following `plan::proptest` — the census being the part that stops a generator quietly degenerating
to the trivial case.

**The concurrency guards need barriers, not sleeps.** Listing capture during a `create`, `rm` or
`finish`, and the Writable stamp's bracketed seqno reading, are both interleaving properties. A
timing-based test for either is a test that passes on a fast machine and is deleted after it
flakes twice, so both get deterministic barriers or injected probes at the interleaving point.

**The ledger has to say what is true, and today it does not.** The invariant registry states that
no guard is `#[ignore]`d and names I9's recursive-materialisation guard as the next entry; this
plan states that the guard "is written `#[ignore]`d before the path exists". No such guard is in
the source. Both are describing an intention as though it were a fact, which is the precise habit
this proof boundary exists to break. So Movement 0 writes it — compiling, ignored, owned by
Movement 3 — and the registry's line changes from "no guard is `#[ignore]`d" to naming it and its
owner. The same treatment for every other deferred row above.

**And the ledger needs a marker before it can be read as one.** `cargo test -- --ignored --list`
already names four tests that are not guards — three child processes and a fingerprint printer —
so "the ledger contains exactly the documented obligations" is a sentence nothing can check. A
pending guard is written `#[ignore = "guard: <claim>, owned by Movement N"]`, and a script
partitions the list on that prefix; a guard with no owner named, or an owner naming a movement
that has closed, fails the same way a red test does. Without it the criterion below can be
asserted forever and checked never, which is the habit this section exists to break.

**Movement 0 closes when, and only when:**

- item 15 has one selected representation and no remaining "either" or "whichever";
- every assertion in items 1–15 is classified green-here or owned by **exactly one** named
  `#[ignore]`d guard in a named later movement — none is left as prose alone;
- every green guard passes, with its population census and its positive controls;
- every new error variant is mechanically reachable, and no variant is added ahead of the path
  that provokes it;
- base-only corpus diagnostics, plan fingerprints, storage bytes and schema fingerprints are
  unchanged, and cursor behaviour is unchanged **except** for the intentional versioned world
  stamp;
- the ignored-test ledger contains exactly the documented downstream obligations — no more, and
  no fewer — **checked through the guard marker above**, not read by eye; and
- the plain cursor's world stamp, the virtual fetch digest and the terminal-`ERROR` client
  contract are green **before** any recursive resume work begins.

**And it closes in four parts, because as one it is the diff this repository's contract exists to
forbid.** As specified this movement spans a `Cursor` layout change and a `CURSOR_VERSION` bump,
server world-stamp plumbing, a `FETCH` protocol change, a client `Rows` state machine, a name-tier
type parameter threaded through a published crate, a predicate catalogue, four pure models and
ten-odd ignored guards. "Keep diffs reviewable in one sitting" is not a style rule here — the
dominant failure mode is a large, mostly-correct diff whose wrong tenth is expensive to find — and
this is the movement where that costs most, because every later movement is built on it. The split
is along proof lines, and each part is green before the next starts:

- **0a — the three live defects. Done.** The plain cursor's world stamp, the virtual fetch digest
  and the terminal-`ERROR` client contract, with the server-level I4 arms that prove them, and
  non-regression everywhere else. The world stamp's listing half folds `fjord.db.List`'s digest
  into the same opaque bytes the base identity already occupies rather than adding a cursor field —
  the composite refuses through the executor's existing `CursorWorld` check, unchanged —
  and `fjord.db.Interning`, which has no stable value to digest, is refused by name on a resume
  that crosses requests instead.
- **0b — item 15's name tier.** `PredicateTy<N>`, the nested local-name and cross-tier collision
  properties, and the full non-regression set. It touches the most files and proves the narrowest
  thing, which is exactly why it travels alone.
- **0c — the pure models.** The budget state machine and its chokepoint, the canonical-id
  allocator, the DNF product against a truth-table evaluator, the materialisation projection
  against a string-name model, and the catalogue against a dense-array model with its tag bound.
  Every one is a pure function with an independent oracle, and none needs the executor.
- **0d — the seam and the ledger.** The sealed reference wrapper against the seam battery, the
  guard marker and its script, the registry's amendments (I8's second witness, I9's third escape
  boundary, I11's virtual carve-out), and the deferred guards themselves.

The order is a dependency order rather than a preference: 0a repairs what recursion would
otherwise be built on, 0b decides what a signature may contain, 0c is what 0b's decisions are
checked with, and 0d is the only part that widens a seam.

**Gating is about *completion*, not about starting, and an earlier draft of this paragraph
conflated them.** A movement can be prototyped against hand-authored inputs long before the items
it names are closed; what it cannot do is meet its own acceptance criteria. Two cases where the
difference is material, and where a review was right to call the graph as written false:

- **Movement 2** can build the driver and evaluate hand-built `Program`s under item 14 alone. It
  cannot *complete*: its termination rule is stated over the **AST**, at step 4 of item 9's phase
  order, and the single-query AST cannot represent a multi-rule program at all. So item 14 gates
  Movement 2's start and item 9 gates its acceptance.
- **Movement 1** is unblocked for everything its acceptance criteria measure — routing, snapshot
  construction cost, allocation, fingerprint non-regression, module size — none of which decodes a
  field name. The same review read item 15 as blocking the movement outright; it blocks one field
  of one struct. What Movement 1 must not do is *settle* `RelationDecl`'s field-name
  representation by accident, and saying it that way is what keeps the movement startable.

**Diagnostics this section adds, named here so the corpus gate is not discovered at the end.**
Every new refusal owes a `Code` variant, a corpus entry, and reachability — a variant no test can
provoke is a variant to delete. At least: `Project::FactRef` of a local row (item 3), a local
relation named as a local relation's field type (item 3), execution-tag exhaustion (item 2),
`reject/duplicate-relation` (item 1), the computed-value recurrence (Movement 2),
`reject/unstratified` (Movement 5), a cursor whose program fingerprint or world stamp has moved
(items 12 and 13), a **cross-request** resume naming `fjord.db.Interning` (item 12),
`reject/non-record-relation` (item 4), and one per limit in item 6's table — noting
that the generated-program limit and execution-tag exhaustion are the two entries that sometimes
have no diagnostic at all, because **any** compile-time failure attributable to the magic attempt
falls back (item 7) while the same failure in a mandatory expansion does not.

### Movement 1 — the relation store and the overlay

De-risking first: prove the machine before building a surface for it. Representation follows
Movement 0 items 2 and 5 and is not chosen here — and `RelationDecl`'s **field-name**
representation follows item 15, which this movement must leave open rather than settle by
accident. **Everything this movement's criteria measure is unblocked by the compiler findings**
and it should start first: routing, snapshot construction cost, allocation and module size decode
no field name, and clause rewriting needs none of it.

- `relation::Relation` owes the `FactStore` contract with an **owned** scan over a stable
  snapshot; `relation::Overlay<S>` dispatches by the identity in a seek key's leading
  `PREDICATE_ID_SIZE` bytes.
- **The dispatch is not a new design; it ships.** `Catalogued<S>` already routes `scan` on the
  key's predicate prefix and `point` on `id.predicate()`, mints ordinary predicate-tagged
  `FactId`s for rows that were never stored, and declares `type Scan = Scan<S::Scan>` — the enum
  sum `Overlay` needs. Follow it rather than re-deriving it, and the novelty reduces to the
  relation's own snapshot.
- Guards on **both** sides of the identity namespace: no predicate visible to compilation may
  land in the local space, and no generated program may exhaust it — the item 2 bound over the
  **augmented** count, with a named diagnostic if it does.

**Acceptance:**
- [ ] `Overlay` and `Relation` satisfy the seam's contract **per implementation, not
      differentially** — two stores that leak identically satisfy a differential and are both
      wrong.
- [ ] A scan of a base predicate through an `Overlay` touches the relation store zero times
      (a spy, not an assertion about the code).
- [ ] **No relation-sized clone per seek or per open**, measured — the allocation guard's shape
      (N versus 2N) applied to *derived* scans, not only to base ones.
- [ ] **And no quadratic snapshot construction across rounds**, which the per-open guard above
      cannot see (item 5): a chain-shaped fixture deriving one tuple per round, measuring bytes
      copied and allocated over the whole run rather than per open. A contiguous rebuild per round
      passes every other guard in this movement while dominating the runtime.
- [ ] **And the history-sensitive work bound, against *this* movement's representation** (item 5).
      Movement 0's harness measures a reference relation and therefore says nothing about this one:
      build identical final contents twice — once in a single batch, once over N one-tuple rounds —
      and compare an empty-range seek, a narrow seek, a point lookup and a full scan between them.
      The batch-built relation is the oracle; the N-round one may cost a bounded factor more and
      may not grow with N. The copy guards above cannot stand in for it, because a segmented
      representation copies nothing and moves the same cost into reading.
- [ ] Scan, point **and reference-follow** routing are guarded independently, including a query
      whose catalogue carries virtual predicates, a program reaching the exact last usable tag,
      and one past it.
- [ ] **Non-regression, mechanical:** every corpus entry's plan fingerprint is unchanged, and
      `scan_is_alloc_free_per_row` is unchanged, with this movement merged.
- [ ] The WASM module's size is measured before and after, because `Overlay<S>` is a second
      monomorphisation of the executor — see [what it costs](#what-it-costs).

### Movement 2 — `Program`, the naive driver, and two oracles

Naive before semi-naive, because the naive evaluator is what the rest of this work is
differentiated against.

**The dependency graph and positive SCC construction land here, not in Movement 5.** The phase
order in item 9 puts recursive-safety validation and stratification *before* magic and semi-naive
generation — and this movement's own termination rule is stated over "a predicate in a recursive
SCC", while Movement 3 needs SCC membership to know which occurrences are recursive at all. A
plan that defers all of it to Movement 5 contradicts itself and invites an interim classifier
that is then thrown away. So the split is by *what the analysis is about*: predicates as nodes,
an edge per body occurrence, Tarjan, condense, toposort — here. The **negative** edge rule,
`reject/unstratified` and its corpus entry stay in Movement 5, where they belong with the
diagnostic; full stratification is needed before *magic*, which is Movement 6, so it still lands
in time.

**One consequence for Movement 5's shape, from the phase order's step 7: stratification is run
twice and only one of the two runs owns the diagnostic.** Over the user's program it emits
`reject/unstratified` against a cycle the user wrote; over the magic candidate it emits nothing and
discards the candidate. So what Movement 5 builds is a *reusable* analysis over an arbitrary rule
set, with the diagnostic wired in by its caller rather than baked into the checker — the same
shape this movement's SCC construction already needs for the same reason.

**Two oracles, not one.** A naive evaluator sharing the same `Plan` executor, `Overlay`,
relation encoder and identity allocator cannot catch a bug *in* those — both sides would misread
a local key identically and agree. So the naive program evaluator stays, for semi-naive and
magic differentials, and an **obviously-correct tuple-set model that uses none of that
machinery** is written beside it. That is the method's own rule: an oracle is independent or it
is not an oracle.

**Acceptance:**
- [ ] Hand-built `Program`s in `fixtures` evaluate transitive closure, mutual recursion, and a
      non-recursive stratum.
- [ ] **The naive evaluator agrees with the independent tuple-set model over generated
      `(program, store)` pairs**, with the generator's population asserted. This is the criterion
      the movement was missing, and it is the load-bearing one: everything downstream is
      differentiated *against* the naive evaluator — semi-naive in Movement 3, magic in Movement 6
      — so proving it by worked examples alone and leaving the model unused until Movement 7 rests
      three movements of differentials on an oracle nothing has checked. Both artifacts are built
      here; only the comparison between them was absent.
- [ ] **A stratum is exactly one condensed SCC, demonstrated by the case that fails if it is
      not**: two non-recursive local relations where the reader is declared *before* the relation
      it reads, answering identically to the same program written in dependency order. Item 1 makes
      the forward reference legal and the loose reading of `Once(Box<[Rule]>)` answers it empty, so
      this is a wrong answer no other criterion in this movement or the next reaches.
- [ ] Every limit from Movement 0 item 6 has a named terminal error and a test that provokes it.
      Never a silent truncation.
- [ ] Non-termination is refused by a **decidable static rule**, not by a semantic aspiration.
      "No rule may invent values outside a finite domain drawn from the base" is not checkable —
      it leaves query literals (finite, not from the base), finite images of base-bound
      computations, and wrapping `i64` arithmetic all undefined, so a compiler written against it
      either refuses useful programs inconsistently or falls through to the limits for programs
      this plan promises to *diagnose*. The language does not need a general finite-domain
      analysis, because recursive value invention has exactly one entrance: an arithmetic head
      leaf. Declared signatures fix record shape, so construction cannot grow in depth. The rule:

      > A head field of a predicate in a recursive SCC may not be an arithmetic expression whose
      > transitive variable inputs include a variable bound by a recursive occurrence in that SCC.

      Stated over the **AST**, at step 4 of item 9's phase order — `ExprKind::Arith`, plus any
      variable bound by an arithmetic `QueryStmt::Bind`, walked transitively through bind chains
      and nested record fields. Stating it over `Project::Computed` and `Computed::Register`
      would put a language rule downstream of flatten and cost the diagnostic its span. Literals
      and base-only computations stay legal. It rejects exactly the hand-written `depth : int`
      recurrence the [deferred closure operator](#deferred-the-closure-operator-as-sugar-over-this)
      already names as a trap. The round and fact limits remain the backstop, not the rule.

      **And it is complete only because `ExprKind::Arith` is the one expression that invents a
      value** — `Lit`, `Var`, `Wildcard`, `Prefix`, `Record`, `Access`, `Select`, `Disjunction`,
      `Subquery` and `Fact` all draw from the active domain, so a head field built from them ranges
      over a finite set and the fixpoint terminates. That is a fact about today's grammar, not a
      property of the design. So the checker matches `ExprKind` **exhaustively, with no wildcard
      arm**: the day a value-producing construct is added, this rule fails to compile until
      somebody classifies it, rather than silently admitting a second way not to terminate.
- [ ] The local-reference-cycle case needs no analysis, because Movement 0 item 3 forbids a local
      relation as a local relation's field type — a positive test that the *declaration* is
      refused, not a graph validator.

### Movement 3 — semi-naive

Δ-rules per recursive occurrence, delta and accumulated as distinct local relations, convergence
when Δ is empty. **Gated on Movement 0 item 9**: a delta variant is a clause rewrite, so this
movement cannot be honestly built or tested against hand-built `Program`s alone.

**Delta as a second relation is sound because item 3 made identity unobservable, and for no
other reason.** One tuple held in both the accumulated and the delta relation has two `FactId`s
— different predicate tags, so `point` routes them apart and a reference-follow check rejects
one against the other's declared referent. Nothing in the language may see that, which is what
item 3's four refusals buy. Without them, the alternative is a delta *view* returning the
accumulated relation's canonical id, which fights the overlay's prefix dispatch for a capability
this feature does not need.

**Acceptance:**
- [ ] Model-based: semi-naive answers **exactly** what the naive evaluator answers, over
      generated `(program, store)` pairs. Tier 3, and the generator's population is asserted — a
      strategy that degenerates to non-recursive programs leaves this green and vacuous.
- [ ] The simultaneous SCC transition (Movement 0 item 5b) is asserted, not assumed, and not
      only its weaker half: a rule that observes its own insertions, *and* a rule that observes
      another rule's same-round output, are both caught by tests built to make them.
- [ ] Focused properties for two recursive occurrences in one rule, mutual recursion with
      cross-rule output, one tuple derived through two clauses, and a **permutation of source
      declaration order** answering identically in the same number of rounds. The two-occurrence
      case is built so that **both atoms must be satisfied from the same round** — the derivation
      that is lost when the accumulated snapshot excludes its own delta (item 5b, step 0), and the
      only shape that tells the two readings of `A_r` apart.
- [ ] **The census requires mixed disjunctive levels**, or item 10's hole stays open under a
      green differential: a level mixing a recursive alternative with a base one, a level with
      two recursive alternatives, and either followed by sibling conjuncts. A generator that
      never emits one leaves semi-naive ≡ naive true and vacuous exactly where it matters.
- [ ] **And two or more disjunctive statements in one body**, which is the case that decides
      whether normalisation is a product or a sum: `(A | B); (C | D)` must answer as four clauses.
      One disjunctive statement cannot tell the two readings apart, so a census that stops there
      certifies the wrong rule.
- [ ] **Expansion's scan amplification is measured rather than assumed** (item 10). A store spy or
      profile over a rule with two disjunctions and an empty final level shows the prefix scans
      repeated the predicted `2^(d-k-1)` times at depth `k` and no more, with the innermost level
      unchanged. Answer equality cannot see this — the expanded and unexpanded forms return the
      same rows while examining different numbers of them — and the number is what says whether a
      rewritten rule can reach `EXAMINED_CEILING` where the same rule unexpanded would not.
- [ ] The four identity refusals of item 3 are tested as refusals — the one that is reachable
      from source (`Project::FactRef` of a local row) at its diagnostic, the three that item 3's
      declaration rule makes unreachable on a hand-built plan.
- [ ] Determinism: the same `(program, frozen base)` derives the same tuples **with the same
      identities**, twice — including that no rewrite collection is traversed in hash or interner
      order. This is what Movement 4 rests on.
- [ ] **Identities are canonical, not merely reproducible** (item 3): a finalised relation's ids
      are assigned by rank in encoded-key order, so permuting rule order, switching rectangular
      for triangular expansion, or changing the snapshot representation leaves every id unchanged.
      Determinism covers the ids a run *observes*; canonicality covers the finalised ones, which
      are the only kind a cursor can hold. `Executor::resume`
      hard-compares a saved `fact_id`, so anything less makes each of those a cursor-compatibility
      surface — and a same-build, fresh-process test does not reach that boundary.

### Movement 4 — resume, and I4 re-proved over a `Program`

**Re-derivation is the semantics; a portal is a cache.** The fixpoint is a pure function of the
frozen base — *provided the base is actually frozen*, which item 12 is about and which is not
true of `fjord.db.*` on the `query_page` path. Given that, the executor chapter's recompute rule
already covers it: *anything determined by
the bindings and the frozen base may be recomputed on restore instead of saved.* A recursive
query's cursor therefore stays bytes-only — resume re-derives the strata, then replays the saved
rows against the answer plan.

The cost is honest and stated: O(fixpoint) per page, **paid on every chunk**, not only on every
client-visible page. When the portal lands with ranking, a recursive cursor may name a session
holding the materialised strata — and **a portal miss must never be an error**, only a
re-derivation. That is what keeps a stateless `?page=7` web tier possible for every query, and it
means I4 is proven once, against re-derivation, with the portal validated by a differential
rather than by a second proof.

**Acceptance:**
- [ ] The program fingerprint covers **all** of: the answer plan, every relation declaration and
      its physical layout, its **materialisation projection**, every rule's target and order,
      every generated magic, supplementary, accumulated and delta relation, stratum kind and
      order, the deterministic execution-tag allocation, every operator tag — and **which of the
      transformed or unmagicked-fallback executables was selected**. That last one is the
      load-bearing addition: Movement 6's fallback means one source program can produce two
      executables that differ in what they materialise and where they stop, and a cursor from one
      must not resume into the other. **No limit value appears**, because after item 6 none of
      them is semantics — with the consequence stated there, that a structurally valid cursor can
      still be refused by a policy that moved. Deterministic identities alone are not sufficient.
- [ ] **The selected executable is a pure function of source, schema, engine build and compiler
      policy**, which is what makes the criterion above satisfiable at all: fallback is
      compile-time only (item 7), so a resumed request recompiles to the same selection and the
      comparison can happen before a row description rather than after a fixpoint. A runtime
      fallback would make the selection unknowable until the derivation had run, and the three
      ways out of that are all wrong — reject valid fallback cursors, trust an untrusted token to
      pick the expensive path, or make an invalid cursor cost O(fixpoint).

      **Compiler policy is the fourth input, and a draft of this criterion omitted it — which
      made the claim false rather than incomplete.** The generated-program cap is deployment
      policy by item 6 and enters no fingerprint, yet crossing it is exactly what selects magic
      versus fallback, so one build compiles one query into two different executables under two
      settings. Naming policy as an input is the fix; freezing the cap into the build is not,
      because the cap is a real memory dial and an operator is entitled to it. The consequence is
      one item 6 already accepts: **a policy change refuses a cursor by name**, since the
      selection is fingerprinted, rather than silently resuming into the other executable. Both
      behaviours here are named refusals and neither is a wrong answer, which is why this is a
      wording defect and not a design change.
- [ ] **The fingerprint travels in item 13's envelope, and a cross-program rejection proves it:**
      two programs whose answer plans are byte-identical but whose seed or step rules differ — or
      which differ only in taking magic versus the fallback — must refuse each other's cursors.
      Left on the answer plan's fingerprint alone, they accept them.
- [ ] The fingerprint is **extended the way this repository already does it** — the hand-written
      walk, paired with the single-element mutation table of
      `every_part_of_a_plan_reaches_its_fingerprint`, one mutation per component above, each
      required to produce a distinct value. Not a new canonical serialization of the executable:
      that would be a second artifact to keep in sync with the thing execution consumes, which is
      the failure the mutation table exists to catch. Plus a rebuild in a fresh process yielding
      the same fingerprint, proving identity allocation is stable.
- [ ] `resume_equals_uninterrupted` extended to `Program`s, with the generated interruption
      schedule — suspend at every boundary, in every combination — **and with every limit set
      unreachable, stated in the property rather than left to the fixture.** Limits reset per chunk
      (item 6), so the interruption schedule *is* a budget multiplier: a suspend-everywhere run is
      entitled to many times the work of a suspend-never one, and a generated fixture anywhere near
      a ceiling makes the property disagree for a reason that is not a resume defect. Unstated,
      that arrives later as a flake and is repaired by turning limits off — which deletes the
      coverage quietly, in the property that matters most.
- [ ] **Limits crossed with resume are their own criterion, at a *fixed* schedule**: one
      interruption point, a limit placed to fire before it and a limit placed to fire after it,
      each producing the same named refusal the uninterrupted run produces, and never a short
      answer. That is the honest statement of what per-chunk charging buys, and it is what the
      criterion above stops trying to say at the same time.
- [ ] **I4 has server-level arms for every mutable source** (items 12 and 13). The catalogue is
      mutated between two `query_page` calls; a cursor is replayed against a *different*
      same-schema database with overlapping fact ids and keys; and ingest lands between two chunks
      of a read on a **Writable** database. Each either answers as an uninterrupted run would or
      is refused by name — never silently short, long, reordered, or hybrid. A frozen `MemStore`
      cannot express any of these, which is why the existing property has never failed.
- [ ] **Every one of those arms runs twice: once for a plain query and once for a `Program`.**
      The plain arm is the criterion, not the courtesy — it is what forces the composite world
      stamp of item 13 into the `Cursor` rather than leaving it in a program envelope, and a suite
      that passes for `Program`s alone has fixed catalogue paging for the rare case and left it
      broken for `where fjord.db.List {..}`.
- [ ] **The Writable arms cover internal chunk boundaries, not only client-visible pages.**
      `run_query` and `count` both take a fresh `reader()` per `CHUNK_ROWS` chunk, so an unpaged
      streaming query and a bare count already resume across snapshots. Arms: ingest between two
      chunks of an unpaged stream; ingest between two chunks of a `count`; and, for a `Program`,
      ingest between two count chunks where the two partial counts would otherwise come from
      different fixpoints. Each refused by name mid-stream. Plus the negative that keeps the
      feature alive: the same reads against a Writable database with **no** interleaved write
      complete normally, and against a Complete one take the refusal path zero times.
- [ ] **A mid-stream refusal is a contract, not just a server behaviour, and the client does not
      implement it yet.** The Writable stamp makes "rows, then an error" an *expected* outcome
      rather than an exceptional one, and `Connection::next_row` releases the stream only on
      `COMPLETE`: an `ERROR` frame is raised by `raise_if_error` and propagated with `?`, so the
      stream id stays in `open`, `Rows` stays `Streaming`, and `check_open` lets a caller retry into
      a `recv_on` that waits for a server task which has already ended. Repeated refusals leak
      stream ids, since none is ever returned to `free`. **Movement 0 owes the contract** — it is
      reachable today through the examined ceiling, which errors after rows — and this movement
      re-proves it over a `Program`; in both the Rust client and the external one:
      **`ERROR` is terminal even after a `DATA_ROW`**; the
      stream id is released and reclaimable; `Rows` moves to an ended state that reports the
      refusal rather than looking resumable; the connection stays usable and the next query reuses
      the id; and `count` releases its stream on the same refusal. Tested by driving a refusal
      mid-result and then running another query on the same connection.
- [ ] **A virtual `FactId` does not outlive its listing** (item 3). The catalogue changes between
      two requests while a `fjord_client::expand::Expander` is alive, and no cached entry answers
      for the wrong database; with the negative, that a cached entry for an ordinary stored fact
      survives the same boundary.
- [ ] **I8** — the immutable snapshot is released at suspend — holds for a recursive page
      suspend and for cancellation or error *during* a fixpoint. (`ops-I8` is phased derivation
      and is a different rule; the first draft of this section cited it here by mistake.)
- [ ] **One base snapshot for the whole program** (item 14), asserted rather than assumed: every
      rule of every round observes the same snapshot — not one per rule, which would multiply
      fjall's open-snapshot count — and the driver, which now owns it in the executor's place,
      releases it on every exit path.
- [ ] **I8 needs two further witnesses, because fjall's count cannot see a derived relation.** A local
      relation is an engine-side `Arc` with no storage-engine counterpart, so a suspended program
      could retain every derived tuple while the open-snapshot cross-check reports zero and
      passes. Keep that count for the base reader and add a drop probe around the relation
      snapshot, following `fjord_store::fixtures`' existing `DropProbe`. Both at zero after an
      answer-page suspend, a cancellation mid-fixpoint, a materialisation or limit error, and
      normal completion — with positive controls showing **both** live during execution. The
      registry now says this under [I8](website/content/invariants.md#i8).
- [ ] A suspend mid-fixpoint is not representable, and the refusal is written in terms of the
      mechanism that exists: the cancellation token polled on the examined-rows stride. **There
      is no wall-clock deadline in this executor** — it is still an entry in
      [operational gaps](#operational-gaps), and any criterion here that wants one makes that
      gap a prerequisite rather than assuming it.
- [ ] **Cancellation is observed in the driver's own phases, not only in a scan** (item 14).
      Three arms with no executor live: cancel during candidate deduplication, during a
      cross-round snapshot merge, and during final canonical-id assignment. Each observes the
      token within one stride, and each charges the driver's work units. Without them every
      cancellation criterion in this list is discharged by `Deadline::tick` alone — the one path
      that was never in doubt.

### Movement 5 — stratification

The graph, the SCCs and the toposort are Movement 2's — its termination rule and Movement 3's
occurrence selection both need them, and the phase order puts them before any rewrite. What is
*this* movement's is the half that is about negation: an edge marked negative under `!`, and the
rule that a negative edge **within** an SCC is unstratifiable and draws `reject/unstratified`
naming the cycle, while between components it is fine.

This is what preserves the property the language currently gets for free — *every negation is
evaluated against a relation that is already total* — and it is shared work: stored derivation's
"derived-on-derived via sealed rounds" needs the same topological sort over the same graph.
Stratification metadata is computed early enough for the naive driver to classify seed and
recursive rules.

**Acceptance:**
- [ ] Property: the analysis agrees with an independently written checker over generated
      dependency graphs, including the negative-edge-in-cycle case.
- [ ] `reject/unstratified` is reachable, names a cycle **in the user's dependency graph**, and
      has a corpus entry.
- [ ] A stratified program evaluates each stratum only after every stratum it negates through is
      complete — asserted mechanically, not by construction.

### Movement 6 — magic sets

The demand transformation, because an unseeded `Reach` computes the closure of the whole call
graph for a question about one symbol.

**The demand has no producer until the answer goal is a rule, and that is the difference between
this working and being decorative.** The worked query does not call `Reach` with a literal: `Seed`
comes out of `src.SearchByName`, and `Program` derives every stratum *before* it streams the
answer plan — so at the moment `Reach`'s fixpoint must be seeded, `Seed` does not exist. Standard
magic rewriting answers this by treating the query goal as a distinguished rule and generating
seed rules from its bound prefix, which is why Movement 0 item 9 requires the answer goal in the
program. The first cut generates the non-recursive rule

```text
magic_Reach^bf(Seed) :- src.SearchByName {name = "encode", to = Seed}
```

into a `Stratum::Once`, and the answer plan re-runs that base prefix — a seek. Retaining the
prefix as a supplementary relation is an optimisation and does not belong in the first cut; if it
is ever taken, it is charged to the same memory budget as every other local relation.

- **Adornment.** Each occurrence gets a `b`/`f` string, propagated from the query's use site.
  **The SIPS is already built:** `reorder`'s runnable frontier is a sideways-information-passing
  strategy, and it is greedy-complete for the reason that module documents — reads are
  structural, `bound` only grows. Reuse it rather than inventing a second notion of what is bound
  when.
- **Magic and supplementary magic.** `magic_p^a` holds the demanded bindings; each adorned rule
  gains a magic literal at the front; `sup_i` names shared body prefixes so they are not
  recomputed per magic rule.
- **The rewrite may not change what the language accepts *statically*.** The transformation can unstratify a
  stratified program, and the first draft of this section answered that by refusing the query —
  which makes an internal performance optimisation part of language validity, and reports a
  cycle the *optimiser* invented as though the user had written one. Corrected: **if the
  transformed program is unstratifiable, fall back to evaluating the original stratified
  program**, subject to the Movement 0 limits. Demand seeding may be load-bearing for useful
  performance without being allowed to redefine correctness.
- **Demand is generated for a negated occurrence at that occurrence's adornment, and is not
  propagated through it.** Two claims, and the first draft made only the second — which reads as
  though a negatively-used predicate gets no demand at all, and that is unsound: it derives
  nothing, the negation passes for everything, and the transformed program stays stratified so
  the fallback never fires (item 11). The correction that matters is *which* demand: a negated
  occurrence is **not** ground, because an omitted field is a wildcard and a wildcard inside a
  negation is legal, so `!Blocked {from = X, to = _}` is `bf`. Seed it at its own adornment and
  propagate into the callee's rules as for a positive call. What is not propagated is demand
  through the negated subgoal into a nested body, which cannot arise, because a negated *group*
  is itself still refused.

**Acceptance:**
- [ ] **Static validity preservation, not just answer equality:** every original program that
      parses, typechecks, passes safety and stratifies still does so after the rewrite, and either
      answers identically through magic or takes the defined unmagicked fallback. *Resource*
      outcomes are deliberately outside this claim — magic derives less, so it can succeed where
      the unmagicked program would exhaust a limit, and that is an optimiser doing its job.
      Answer equality alone goes vacuous exactly on the cases the rewrite breaks, because a
      rejected program has no answers to compare.
- [ ] The generator's census asserts mutual recursion, multiple adornments of one predicate,
      multiple answer use sites, the fallback path being taken, and — specifically, because
      "ground negation" is satisfied by a base predicate and proves nothing — **a recursive local
      relation reached only negatively, with an omitted or wildcard position in that negated
      occurrence**. Without the wildcard the census tests the case that was already right.
- [ ] Fallback is exercised by a program the rewrite is known to break, **by a program whose
      rewrite overflows the generated-program limit while its unmagicked executable fits**
      (item 6), **and by a program whose magic and supplementary relations exhaust the
      execution-tag space while the unmagicked form fits under `MAX_TAGGABLE_PREDICATE`** — the
      magic-enabled counterpart to Movement 1's exact-last-tag guard. Neither of the last two has
      an unstratifiability to trigger it, so nothing else in this list reaches them, and a
      terminal error at either would be magic deciding what the language accepts. **All are
      compile-time**: a limit reached during *derivation* is a refusal, not a fallback, because
      magic guarantees static validity only (item 7). The third arm is why item 7 states its
      trigger as a pipeline order rather than a list of error kinds — a list is exactly what left
      it uncovered.
- [ ] **The last two arms fail *downstream* of magic generation, and a fixture that overflows at
      generation does not test them.** Construct each so the magic candidate's rules are produced
      successfully and the failure appears in step 8 or 9 of item 9's phase order — supplementary
      relations whose *delta variants* cross `MAX_TAGGABLE_PREDICATE`, and a rewrite whose
      semi-naive expansion crosses the generated-program limit its unexpanded form fits under.
      That is the shape that proves the selection point is after flattening rather than after
      generation, which is the whole content of item 7's ordering; a fallback suite that only ever
      fails at step 6 passes against the pipeline item 7 rejects.
- [ ] **A source-stratified program whose transformed candidate holds a negative cycle**, which is
      the arm that proves step 7 of the phase order exists. It must fall back with **no
      `reject/unstratified` emitted** — the diagnostic belongs to the user's dependency graph and
      this cycle is the optimiser's — and no transformed SCC or stratum metadata may reach
      execution, since the delta variants generated from it would be generated from a stratification
      that was never valid. Assert both halves: the answer equals the unmagicked program's, and the
      diagnostic stream is empty.
- [ ] **And a mandatory failure stays terminal**: an *unmagicked* program that overflows the
      generated-program limit or exhausts the tag space is refused by name, with no fallback
      attempted, because nothing optional caused it.
- [ ] Seeding is visible in the profile: `Reach` from one symbol examines rows proportional to
      the reachable set, not to the predicate — a guard in the shape of
      `no_page_reads_a_predicate_whole`.
- [ ] **A store spy proves the seed is doing the work**, over a fixture where the seed comes from
      a multi-level base join and unrelated graph components dominate the database: those
      components are never scanned. Result equality on a small fixture cannot discharge this — an
      accidentally unseeded implementation computes the whole closure and still returns exactly
      the right rows.
- [ ] A use site that binds nothing still works — unseeded evaluation is the fallback, not an
      error.

### Movement 7 — the surface

Last, because everything above is testable against hand-built `Program`s and the grammar is the
part most likely to be re-cut once the machine is real. Shape follows Movement 0 item 1.

- `with Name : <type> = <clauses>` before the head, one or more. The **top-level** type is a
  record in the first cut, refused by name otherwise — item 4, which owes scalar and union heads a
  materialisation contract they do not have.
- **The signature is not optional.** A query's record fields are sorted by name at lowering while
  a schema's are declaration order — and declaration order *is* key order. A local relation
  inferred from a sorted head record would have its index design decided alphabetically, and the
  backward relation a bidirectional search needs would be unspellable. The signature also gives
  the typechecker the recursive occurrence's type without a two-pass inference.
- **An unqualified capitalised name in applied position is free.** `Reach {from = X}` is
  unparseable today — juxtaposition is reserved for `QualifiedName branch` — so the position can
  be taken without touching the lexer, and schema-first resolution already forbids shadowing a
  schema name.

**Acceptance:**
- [ ] **The worked example at the top of this section parses, compiles and runs, from source
      text.** Hand-built `Program`s do not discharge this, and neither does a differential
      between two evaluators that share a lowering bug.
- [ ] End-to-end property: source compilation plus execution equals Movement 2's independent
      tuple-set model, over a canonical source generator whose census asserts recursion, mutual
      recursion, multiple clauses, stratified negation, **non-lexical signature order**, a
      **forward reference between two non-recursive local relations** — legal by item 1, and
      answered empty by the loose reading of a `Once` stratum — and multiple adornments — **plus a
      scalar and a union top-level signature, each refused by name** (item 4). A census that
      never emits one lets a record-only implementation pass as an implementation of the full
      type grammar, which is the shape of the gap that was found.
- [ ] Corpus entries classify every new construct, and every new diagnostic code is reachable or
      excused.
- [ ] Query disjunction still means one multi-source level — **and so does a disjunction inside a
      non-recursive, unmagicked local relation**, which is the arm item 10's normalisation could
      erode without any query changing. A test says so for both, because clause union landing
      next to it is exactly how that would erode.

### Movement 8 — the executable seam, and inspection

The consumers, which the first draft under-counted: the server validates a page cursor against
`prepared.plan.fingerprint()`, sizes a `Profile` from one plan, asks catalogue routing whether
one plan reads a virtual predicate, and builds a fresh `Executor` per chunk (`session.rs`). The
shell, count queries, paging refusal before row description, inspection and the WASM demo path
are all `Plan`-shaped too.

- A `PreparedQuery`/`Executable` sum, preserving the **exact** no-`with` fast path and giving
  every consumer one explicit dispatch point.
- A program profile model: stable rule and stratum identities, aggregated across rounds, and
  **re-derivation counted on every chunk**, because it is work actually performed. A feature
  whose most expensive work is invisible to `--profile` contradicts the reason `--profile`
  exists.
- `:plan` over a `Program` shows strata, rules and the magic relations; `fjord-inspect` gains the
  view models; the workbench renders them.

**Acceptance:**
- [ ] Early cursor validation in the server checks the **program** fingerprint — item 13's
      envelope, including the base identity and the listing digest — before any row description
      is sent, and without deriving anything (item 13).
- [ ] `reads_virtual` traverses the answer plan **and every generated rule**, proven by a program
      whose virtual predicate appears *only* in a derivation rule. On today's one-plan test that
      program builds no `Catalogue`, routes to fjall, and answers empty without erroring.
- [ ] A recursive query's profile reports the fixpoint's work, per rule and per stratum, and a
      chunked read reports more total work than an unchunked one — which is the truth, and the
      opposite of the rule for `Plan` resumes.
- [ ] **The count path has criteria of its own, because it is not the row path.** `counting`
      builds its own `Executor`, suspends on the chunk stride and resumes — so a recursive
      executable re-derives the entire fixpoint per chunk to produce one number. Required: a
      recursive count equals the cardinality of full enumeration; every count chunk validates the
      **program** fingerprint before counting; each chunk charges and reports its re-derivation
      work; cancellation and every limit release both the base and the relation snapshot; and a
      multi-chunk count carries a guard documenting the repeated-fixpoint cost, so a later
      optimisation cannot quietly change resume or accounting semantics.
- [ ] The no-`with` path is unchanged, asserted the same mechanical way Movement 1 asserts it.

### What it costs

Stated plainly, because this is the largest single feature in the project's history — bigger
than unions, bigger than the browser build. Revised upward after review.

| Cost | Detail |
|---|---|
| **Movement 0** | Fifteen settled decisions before any implementation: five gating representation, three gating Movement 3, one gating Movement 2, two gating Movement 4 — one of them filed as a defect besides. The first draft of this section assumed all of them, and the sixth round turned them into a proof boundary. It lands in **four parts** (0a–0d), because as one diff it is not reviewable in a sitting |
| **A prerequisite outside the feature** | A **rows-examined ceiling**, because every limit this feature adds is output-side and a recursive rule that scans a huge base and produces nothing evades all of them. **Built** — `Executor::with_examined_ceiling`, counted in the existing per-row tick. It was the only item here that was missing code rather than a missing decision; its scope is one executor, so the driver still owes the aggregation (item 14) |
| **Two defects it inherits** | A cursor names a plan and nothing else about the world it read — not the database, not the listing. Both are live [I4](website/content/invariants.md#i4) holes today, both are recorded in [operational gaps](#operational-gaps), and recursion cannot be correct until they are fixed *there* rather than inside an envelope |
| **A published type that cannot say what a local field is called** | `PredicateTy` carries raw `Spur`s and the codec resolves them as schema symbols unconditionally (item 15). Either that type gains `Symbol` — a workspace-wide change to a published crate — or local signatures are restricted. Not a movement's parenthetical; it decides what a signature may contain |
| **A seam that has to move** | `Executor` owns its store and `enumerate` consumes it, which *is* I8's structural proof. A fixpoint runs many plans, so ownership moves to the driver and I8 stops being free. Through a **sealed engine-private wrapper**, not a blanket `impl FactStore for &S`: a public one lets anything build an `Executor<&S>` from the moment it lands, two movements before the drop probes that replace the guarantee — see item 14 |
| **A predicate catalogue** | Threaded through `lower`, `ty`, `flatten`, diagnostics and inspection — everywhere `Schema::get` and `Schema::find_position` are reached today. Not a module, a seam |
| **New modules in `fjord-engine`** | `relation`, `program`, `stratify`, `magic`, plus a fixpoint driver. `flatten`/`reorder`/`compile` are *reused per rule*, not rewritten |
| **Grammar work in two places, now larger** | The signature is a schema-style type, and the type grammar lives in `fjord-schema::syntax` while the query grammar lives in `fjord-engine`. Clause union lands on the same seam. Sharing the type grammar across two `lelwel` grammars is its own task, not a parenthetical; duplicating it is a second source of truth for what a type is |
| **A snapshot discipline** | An owned scan seam means relations are frozen per round rather than read live — the naive reading is a relation-sized clone per scan open, inside semi-naive's inner loop |
| **Consumer blast radius** | Server paging and count, shell, inspection, WASM, and any public function returning `Plan`. Count is the one that looks free and is not: it chunks and resumes, so it re-derives per chunk for a single number |
| **A second executor monomorphisation** | `Executor<S: FactStore>` is monomorphised, and the server already instantiates it for the stored reader *and* `Catalogued<Reader>`. `Overlay<S>` adds an instantiation per store shape, and the browser adds the `MemStore` one — compile time, native code size, and the **WASM bundle**, which is a stated product constraint. This is not an argument for dynamic dispatch: the per-row virtual call and the allocation it implies are what the current design deliberately avoids. It is an argument for measuring the module before and after, which Movement 1 now owes |
| **Invariants re-proved** | I4 over `Program`s (the expensive one). **I8** for a suspend or an error mid-fixpoint, and it needs a *second witness* — fjall's open-snapshot count cannot see an engine-side relation snapshot. I7 holds because the fixpoint is above `enumerate`. **I9 does not hold for free**: re-scoping its guard to put the fixpoint outside the path it measures would define the problem away, because a materialisation callback runs per rule-output attempt and a duplicate-heavy join would allocate per attempt with the scan-only guard still green. The registry now names a retained derived tuple as a third escape boundary and owes three measurements; **Movement 0 writes the guard `#[ignore]`d and the registry names it and its owner** — today the registry says nothing is ignored and this table said the guard exists, and neither was true |
| **A new accumulation** | The anti-pattern list forbids materialising a *result set*; a relation is not one, but the distinction has to be written down and budgeted across facts, bytes, work and generated program size, or the next reader is right to call it a violation |
| **Test burden** | **Two** oracles — a naive program evaluator and an independent tuple-set model that shares none of the new machinery; four tier-3 properties (semi-naive ≡ naive, magic ≡ unmagicked-or-fallback, source ≡ model, resume ≡ uninterrupted); a stratification checker written twice; determinism of identities; and non-regression guards on fingerprint and allocation for base *and* derived scans |
| **What it does not cost** | The executor: no `Step`, no frame kind, no cursor entry, no branch in `advance`, no change to the seek path. That is still the reason for this shape — and it is a claim about the machine, **not** a claim that the feature is small |

### Deferred: the closure operator, as sugar over this

`(src.Calls | src.Member)+{1,5} {from = Seed, to = B, depth = N}` is the narrow construct that
answers most of what recursion is wanted for, and once the general form exists it is **sugar** —
it desugars to a local relation. Two things it can offer that the general form cannot, which is
why it stays on the list rather than being dismissed:

- **Min-depth is free and inexpressible.** The round at which a tuple is first derived *is* its
  BFS depth. Written by hand as a `depth : int` field it is a trap: `(a,b,1)` and `(a,b,2)` are
  distinct tuples, so the fixpoint **never converges on a cyclic call graph**, and bounding the
  depth to restore termination changes the relation from reachable-nodes to paths — exponential
  where the answer is small. `impact`'s depth grouping should come from the driver.
- **Stratification becomes structural.** A closure expression is total at its point of use, so
  negation over it is stratified by construction and there is no unstratifiable program to
  reject.

## Language backlog

Additive — each is an enum arm, a token, or a compile rule; none reshapes the machine. A
construct may add a `Source`, a `Test`, a residual op or a `Computed` arm — never a `Step`.
**Additive is not the same as small**: anything that touches the resume token or freezes
bytes on disk gets acceptance criteria, not a bullet.

- **Recursion / transitive closure.** Promoted out of this list — it has a design and a
  movement plan of its own
  ([above](#recursion--query-local-relations-magic-sets-stratified-negation)), and it is the
  one item here that was never additive.
- **Aggregation.** `count` exists as a query kind (`--count`) without entering the language;
  aggregation proper materialises, which is the one thing that cannot be made suspend-free.
- **`distinct` via adjacency.** Deduplicating on the witness tuple is provably a no-op;
  deduplicating on the projected row needs O(distinct) cursor state — except when the
  projected fields are a **prefix of the output order**, where duplicates are adjacent and
  one row of state suffices. Compile under that condition, refuse with a named diagnostic
  otherwise. `--count` with distinct is the same mechanism.
- **A sargeable order comparison.** `<`/`>` on a leading key field denotes one contiguous run
  of the key order — unlike a denial there *is* a seek form. Filters today.
- **if-then-else.** `(C; T) | (!C; E)` is the desugaring and needs no machinery.
- **`maybe` / `enum`.** Sugar over a union (built); each waits on a *naming* decision, since
  what they desugar to enters the fingerprint.
- **Arrays / sets.** The multiplicity decision (below) stands: one fact per element until
  stored derivation exists to explode an array into a seekable index. The codec reserves the
  band; length-prefix vs terminator is the real one-way door.
- **`evolves` + query-time projection.** The compatibility checker is structured around a
  canonical-model diff, not just hashes, so field-level compatibility has its seam.
- **Pattern-pushing** — what is left of `pattern = pattern`: a left side that is not a target
  (`gen = gen`, `Y.name = X`). If unification ever lands after disjunction, import Glean's
  rule that it must be branch-local.
- **Intra-row repeated variables** (`{from = X, to = X}`) — needs a same-row `EqField`
  residual; rejected by name until something else wants the operator.
- **Row polymorphism** in the typechecker — an inference capability with no invariant
  attached and nothing waiting on it.
- **Block-local back-references** in the wire format — a pure encoding win (naming a fact by
  ordinal within a block) over a semantics that is decided; deliberately not first.

---

## Settled decisions — recorded so they are not reopened

Each entry is the decision and the reason it went that way. Reopening one means arguing with
the reason, not rediscovering it.

- **Parallel writes to a Writable database — yes, behind a striped merge frontier.** The
  serial writer was never required: `ops-I4`'s identity is a multiset hash over each fact's
  logical form (order-independent by construction), and `ops-I5` asks for one *pipeline*, not
  one thread. What the single thread actually held was I12's write-once half, and that now
  has a mechanism — per-key exclusion striped by `hash(predicate ++ key)`, no lock ordering
  needed because interning is bottom-up and critical sections are never nested. The stripe is
  held across the read *and* the commit (a batch is atomic on recovery, not isolated from
  readers), so per-key exclusion is the weakest sufficient mechanism and a lock-free CAS
  would not do. Commits do not parallelise (fjall's journal mutex) — accepted, because the
  expensive half was the redundant point reads. Do not restore the single writer, and do not
  add a conflict rule that picks a winner — that is the one thing `ops-I4` really forbids.
- **Per-block commits — a `serve` flag, off by default, gated on the durable id claim.** A
  durability trade should be something somebody typed, not a config entry or a create-time
  property (two identical databases must not differ in metadata because of how fast somebody
  wrote them). The nearly-missed failure: a lost batch let the allocator resume *below* a
  stranded id and reissue it, so a surviving reference resolved to the wrong target through a
  `finish` that looked like it checked this — hence ids are claimed in `meta` before use, and
  the worst outcome is back to "cannot seal". The honest statement, everywhere it appears: *a
  crash during ingest may cost the index, never its correctness.*
- **Multiplicity — one fact per element; `nyi/array` names the decision.** Glean's array
  story works *because* stored derivation explodes an array into a seekable index; arrays
  before stored derivation ship the storage win and none of the query mitigation.
- **A client never computes a fingerprint — it carries the number as a constant.** The
  fingerprint is a provenance tag; the byte-identical golden is what actually guards the
  shapes, and it is the stronger check. Generating clients from the schema is the proper end
  state and would change the golden's role in the same breath.
- **Predicate ids belong to the database, not the schema text.** No assignment that is a
  function of the text satisfies reproducibility, layout-independent identity and
  "adding a predicate is compatible" at once — so the map is assigned at create, embedded,
  append-only for life, and **the wire carries names** (once per block), so the numbering
  never leaves the database and a fact file is portable to any database declaring those
  names.
- **A reference on the way in is the target fact, written inline** (or an id the producer
  already holds); stored, a reference is a `FactId` and nothing else. Every id-based
  alternative makes the *producer* keep a map from every entity to its assigned identity plus
  an emission order respecting it. Interning is resolve-or-create, bottom-up, total because a
  reference in a key cannot be cyclic; "already there" is `ops-I5`'s silent dedup and
  "disagrees" is its same-key-different-value reject — no new rules. The cost is stated: a
  reference costs the target's whole fact on the wire, per occurrence.
- **Storage codec and transport codec are distinct, and the transport is bidirectional.**
  They share no bytes; only the inbound direction has a reference that is not an id.
- **Schema compatibility is subset containment.** The only compatible change is adding a
  predicate; any in-place change is Breaking until `evolves` exists — which is what makes the
  check unable to fail the way a richer one can.
- **Primitives — comparisons and arithmetic are built.** A comparison is a byte-compare
  residual (I1 makes encoded order value order); arithmetic is the first producer of
  `Step::Derive`. Angle's primitive surface is narrower than it sounds (15 ops); what sigla
  still lacks is if-then-else and element iteration, and the second is the multiplicity
  decision, not this one.
- **`pattern = pattern` — the gate is the left side's shape alone.** Most of what was filed
  as unification was not: reading before binding is *ordering* (`reorder`), a constant is
  *substitution* (the fold), a place is an *alias*, a prefix is a *constraint* applied by the
  capturing level, a record of targets is *destructuring*. Both spellings of each compile to
  the same plan, pinned by paired corpus entries. Typecheck no longer asks "was this
  mentioned above" — that decided in source order, the one order the query might not have
  used. A literal leaf on the left of a destructuring is refused because it binds nothing and
  would mean `true` where it means the empty relation.
- **Intra-row repeated variables — rejected by name** (`nyi/repeated-variable`). Repeated
  *reads* of an outer variable are ordinary splices; only a repeated *capture* is refused.
- **Cancellation counts rows examined, and the counter belongs to the run.** As a local it
  reset per call and a plan whose rows all matched never polled the token. The bounded
  overrun a stride buys is the intended trade, documented on the constant.
- **`FactRef` has its own fixed-width marker** (`0x51`) — a value's bytes are
  self-describing without the schema and the `Int`/`Fact` distinction is byte-level.
- **The on-disk format version is two numbers** (`codec`, `storage`) in a metadata keyspace,
  checked for **equality** at open; an unstamped database holding facts is refused rather
  than adopted. It makes nothing migratable — a future codec is a different number rather
  than an impossibility. The resume cursor versions separately, against the build.
- **Union types (the one-way doors, taken together):** explicit append-only discriminants
  (I10); a union is a **terminated group** in the codec so `skip` needs no notion of a value
  still owed; a `FieldPath` step at a union position is the expected discriminant, checked
  before any payload read; every union edit is Breaking in `schema diff`, appending included.
- **Negation in a stored derivation will be legal** — see
  [stored derivation](#stored-derivation); the ban Glean carries is a cost of incrementality
  this design does not pay, and reopening `ops-I9` is what would make it unsound.
