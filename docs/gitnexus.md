# Fjord and GitNexus

> Reference doc. **Read this before claiming Fjord can or cannot answer a code-intelligence
> question.** `glean.md` measures the engine against another database and answers *what can this
> do*; this file measures it against a product and answers *what could be shipped on it*. The two
> pull on different gaps, and the difference is the point.

GitNexus ships seventeen code-intelligence tools. This is a per-feature audit of how many Fjord
could answer, under two assumptions stated up front because they are load-bearing:

1. **An indexer exists** that can populate a database with whatever facts a feature needs. What a
   Roslyn or tree-sitter walk can see is not the subject here; `clients/dotnet`'s indexer already
   demonstrates the shape at twenty-five million facts.
2. **Fuzzy matching lands** as a Levenshtein-DFA **guided seek on the first hop**.

**Read a verdict as being about the engine, not the indexer.** ✅ means the question is a seek or a
join sigla compiles today, given the facts and a schema declared for that question. ◐ means the
shape is answerable but something the language lacks forces the work into the client. ❌ means there
is no spelling, and adding one is not additive.

That ✅ line does real work, and it is drawn deliberately: declaring a predicate keyed for the
question you actually ask is an ordinary schema decision here, not a workaround. Four predicates in
`schemas/code.sigla` exist for exactly that reason and each says so in a comment.

---

## The finding

**6 supported · 9 partial · 2 not supported.**

The tally is the least interesting part. The nine partials fail on a very small number of shared
axes — the table below is mostly the same three gaps counted repeatedly.

| # | Missing capability | Features it holds back | Price |
|---|---|---|---|
| 1 | **Recursion / transitive closure** | `impact`, `trace`, `api_impact`, `pdg_query`, half of `check`, most of `cypher` | A genuine machine reshape ([backlog](../PLAN.md#language-backlog)) |
| 2 | **Ordering by a computed value (ranking)** | `query` outright; the confidence half of `impact` and `check` | A machine reshape — it materialises |
| 3 | **Cross-database query** | `group_sync`, `group_list`, every cross-repo variant of the rest | Above the executor; `ops-I9` untouched ([glean.md §0](glean.md#0-the-question-that-opened-this-file-fact-ids-across-databases)) |
| 4 | No float, no vector distance, no ANN | the semantic half of `query` | A type-model change plus a second index kind |
| 5 | No `distinct` | every "which *files* are affected" answer | Additive under the prefix condition ([glean.md §1.3a](glean.md#13a-why-we-cannot-deduplicate-yet-and-what-would-let-us)) |

Fuzzy search moves exactly one thing: name lookup from prefix-only to edit-distance. It touches
none of the five.

### Verdict table

| # | Feature | Verdict | The deciding fact |
|---|---|---|---|
| 1 | `list_repos` | ✅ | `fjord.db.List` already *is* this |
| 2 | `query` (BM25 + semantic + RRF) | ❌ | Retrieval expressible; scoring and ranking are not |
| 3 | `context` | ✅ | `fjord-viewer`'s `/symbol/{name}`, shipping today |
| 4 | `impact` | ◐ | One hop is a seek; closure is a client-side BFS |
| 5 | `trace` | ◐ | Client-side bidirectional BFS |
| 6 | `detect_changes` | ◐ | Line→decl containment filters rather than seeks; impact half is #4 |
| 7 | `check` | ◐ | Fixed-depth rules yes; reachability rules no |
| 8 | `rename` | ◐ | Graph half is the flagship seek; substring text search is absent |
| 9 | `cypher` | ❌ | Variable-length paths, `ORDER BY`, aggregation, `WITH` |
| 10 | `route_map` | ✅ | A join over indexer facts, keyed both directions |
| 11 | `tool_map` | ✅ | Same shape as #10 |
| 12 | `shape_check` | ✅ | An antijoin — the best fit on the list |
| 13 | `api_impact` | ◐ | #10 composed with #4 |
| 14 | `explain` | ✅ | Persisted flows read back in key order |
| 15 | `pdg_query` | ◐ | One hop yes, transitive no |
| 16 | `group_list` | ◐ | Enumeration yes; group config lives outside the database |
| 17 | `group_sync` | ◐ | Building yes; cross-repo *querying* has no mechanism |

---

## Supported

### 1 · `list_repos` — discover all indexed repositories

`fjord.db.List` (`crates/fjord-server/schemas/catalogue.sigla`) is a virtual predicate over the
store root, keyed `{name, instance, status, facts, bytes, created}`. A store root is
`<name>/<instance>/` where the instance is a ULID
([operations](../website/content/operations.md)), so **repo → name, build → instance** with no
modelling work at all. Every field is a key field on purpose, because a listing exists to be
filtered — by name, by status, by how big something got.

The one mismatch is paging. GitNexus offers `limit/offset`; Fjord offers `--limit` plus an opaque
resume cursor. The cursor is the stronger primitive — it survives the connection and is verified
against the plan fingerprint before use ([glean.md §1.3](glean.md#13-answering-paging-and-inspection))
— but it answers "the next page", not "page N". Offset paging over an immutable database is a
client-side skip, and worth saying rather than papering over.

### 3 · `context` — 360-degree symbol view

This ships. `fjord-viewer` answers a symbol panel with `definition`, `references` and
`definition_span` (`crates/fjord-viewer/src/query.rs`), all seeks. Categorised references are one
seek per category, and the schema pattern for the reverse directions already exists: `src.Extends`
answers "who derives from this", `src.DerivesFrom` answers "what does this derive from", and
`schemas/code.sigla` says in a comment that the second exists *precisely because* a symbol panel
asks the opposite question to the fan-out. `src.AttributeOf` is the same choice made again.

Process participation is a join, given a process predicate keyed both ways. Nothing new is needed.

The trap this feature teaches is worth carrying, because it is invisible from outside. `search` was
once written to **bind** the declaration — `…, to = D}; D = src.Decl {module = M}` — and cost
**30 seconds** where the fetch spelling costs **2 ms**. A row bind *claims* its variable
(`flatten`'s `Claims`), so the statement saying what `D` is must run before anything reading it,
and no reordering rescues that: it is not an ordering question. `src.Decl` scanned its 888,177 rows
and the seek became a residual on each one. **Every feature on this list that reads through a
reference is one spelling away from that cliff.**

### 10 · `route_map` · 11 · `tool_map`

Two predicates keyed in opposite directions and a join between them:

```sigla
{component = C.name, method = R.method, path = R.path, handler = H.name}
  where api.Fetch {component = C, route = R}; api.Handles {route = R, handler = H}
```

Both are the `src.Ref`/`src.FileXRef` pattern — the same edges declared twice so each direction
seeks. The map's order falls out of key order, which is what a route map wants anyway.

### 12 · `shape_check` — response shapes against consumers' accesses

The best fit on the list. "A consumer accesses a property the response shape does not declare" is
an antijoin, and `!` is exactly that:

```sigla
{consumer = A.consumer, prop = P}
  where api.Access {consumer = A, prop = P}; api.Responds {route = A.route, shape = S};
        !api.Field {shape = S, name = P}
```

The negation reads two already-bound registers, binds nothing, takes no cursor entry, and costs a
`Step::Test` drained to its first row — the question is whether a witness exists, not how many. It
sits inside neither `nyi/negation` case: there is no negated *subquery*, and no *generator* inside
the negation's key. The missing array type costs nothing here either, because one fact per property
is the settled multiplicity answer and is what makes the antijoin seek in the first place.

### 14 · `explain` — persisted taint findings

The verdict holds for **persisted** findings, which is what the feature says. A flow stored as one
fact per step, keyed `{flow, index}`, reads back in step order as a seek — structurally identical
to `src.Param {decl, index, name}`, which exists so that a method's parameters come back in order.
An `int` in the middle of a key is the idiom, and it is already in the sample schema.

What is *not* supported is **deriving** flows at query time. That is reachability, and it is #4.

---

## Partial

### 4 · `impact` · 5 · `trace` · 13 · `api_impact` · 15 · `pdg_query`

One gap, four features. sigla has no recursion —
[glean.md §3](glean.md#recursion--glean-has-it-we-do-not) records this as *our* decision rather
than a shared one (Glean has an opt-in semi-naive fixpoint behind `--experimental-recursion`) and
prices it as the one item that is a genuine machine reshape: the loop is driven by facts being
*written* mid-query, `enumerate` has neither an arm that re-runs the body nor a write path, and
holding state across iterations conflicts with [I8](../website/content/invariants.md#i8).

What this costs in practice is less than a ❌ would suggest, which is why these are ◐:

- **Each hop is a seek.** All four edge predicates in `schemas/code.sigla` lead with the end you
  fan out *from* — that is what `{base, type}` buys. A frontier expansion is one query.
- **Blast radius is a client-side BFS**: N round trips for depth N. **Depth grouping is free** —
  the round number *is* the depth, which is exactly what `impact` reports.
- **`trace` wants shortest path**, which needs the client driving the search in any system;
  bidirectional BFS is a client algorithm wherever it runs.
- **Multiple edge kinds in one hop are `|`**, which exists, is flat and n-ary, and is never
  DNF-expanded across sibling conjuncts.
- **The client must dedup its frontier anyway**, so the missing `distinct` costs nothing *here*.

What genuinely does not work is unbounded closure inside one query, and confidence scoring.

### 6 · `detect_changes` — git-diff impact

Three parts, and only the middle one is about the engine.

`git diff` is the client's job. Fjord is immutable: a database is built against one commit and
sealed. That is also the honest limit — there is no incremental re-index and no stacked delta.
[glean.md §1.5](glean.md#15-lifecycle-and-operations) records per-fact ownership as declined, and
[§0](glean.md#stacking-is-not-an-answer-to-this-and-cannot-be-made-into-one) works out that two
independently-built databases can never be stacked, **in Glean either**, because the delta's ids
must be allocated above the base's at create time. So `detect_changes` compares a working tree
against *the commit that was indexed*, not against a continuously updated graph.

Mapping a changed line to the declaration containing it is a range containment — `line <= L` and
`L <= endLine` against `src.DeclSpan`. Comparisons exist and are byte compares, sound because
[I1](../website/content/invariants.md#i1) makes encoded order value order, but they **filter**; a
sargeable order comparison is on the [backlog](../PLAN.md#language-backlog). Inside a file-scoped
seek that is fine — the scan is one file's declarations, not the database's. Worth stating
explicitly, because the same filter over an unscoped predicate is the
56,274-rows-examined-per-row-produced failure `schemas/code.sigla` opens by warning about.

Which processes the changed declarations affect is #4.

### 7 · `check` — structural checks against the graph

Splits three ways, and the split is the useful finding:

- **Fixed-depth structural rules** — "no handler calls a repository directly", "every route has a
  handler", "no public type lacks a doc comment" — are a join plus `!`. Supported.
- **Threshold rules** — "no file declares more than N things" — need a count. `QUERY_COUNT`
  (`--count`) runs the same plan with a counting accumulator, so the count costs no rows over the
  wire and the comparison is the client's. Supported in effect, one round trip per rule.
- **Reachability rules** — "nothing in the domain layer transitively reaches the HTTP layer" — are
  #4.

One sharp edge worth naming: `nyi/repeated-variable` refuses `{from = X, to = X}`, so a
self-reference check — "no type extends itself", "no route handles itself" — has no spelling. It
needs a same-row `EqField` residual, which [`PLAN.md`](../PLAN.md#language-backlog) records as
rejected by name *until something else wants the operator*. This is that something else.

### 8 · `rename` — multi-file coordinated rename

The graph half is the flagship. `src.Ref` leads with `to`, which is what makes find-references a
seek and what `bench/FINDINGS.md` §11 records as making it answerable at all. "Every reference to
this declaration" is one seek, and it is what `fjord_viewer::query::references` already asks.

The text half is where it stops. Prefix search is a range under I1 — the one place the two codecs
genuinely agree with Glean's — but **substring and regex are absent**: no `contains`, no suffix
index, and no `toLower` at read time, which is why `src.SearchByLowerName` exists as a second
stored copy of the same names. A trigram or suffix index is expressible as facts and needs no
language change; it is simply not built, and the cost lands on the indexer.

Fuzzy helps here more than anywhere else on this list, because "find the things spelled nearly like
this" is the discovery step a rename across an unfamiliar codebase actually starts with.

### 16 · `group_list` · 17 · `group_sync`

Enumerating databases is `fjord.db.List`. *Grouping* them is configuration that lives outside any
database, so `group_list` is half a query and half a config read.

`group_sync` is the interesting one, and it deserves a paragraph because Fjord has already thought
this through without building it. Rebuilding a group's Contract Registry is a *write*, and writes
are fine. Querying **across** the group is not:
[glean.md §1.5](glean.md#15-lifecycle-and-operations) marks cross-database query "—", and
[§0](glean.md#0-the-question-that-opened-this-file-fact-ids-across-databases) works out why the
obvious fixes fail — stacking is create-time only, and a fact id is database-local by construction
(our snowflake at least catches a *predicate* mismatch; Glean's bare `uint64_t` catches nothing).

§0 also establishes the shape of the answer, which is why this is ◐ and not ❌:

- **Origin cannot be hidden**, and Glean's own source is the argument — `entityRepo` carries the
  comment *"vital to know which repo this came from"*.
- **A union whose rows are freely comparable cannot contain a `FactId`.** Cross-repo links want a
  content-derived string identity with the repo as its first token — Glean's `SymbolId`, reversible
  by searching rather than by dereferencing.
- **A fan-out needs a merge policy, not a merge.** Glean has three, chosen per call site. Ours
  would need at least the fair interleave, since plain concatenation across forty CI databases
  means the first supplies every row of the first page.
- **Every mechanism sits above the executor**, in a service holding several handles, so `ops-I9`
  stays intact and no layer dimension enters `Access` or `Cursor`.

None of it is built; all of it is designed.

---

## Not supported

### 2 · `query` — process-grouped hybrid search

Four sub-features, failing for four different reasons, which is why this is ❌ rather than ◐.

**Grouping works.** "Process-grouped" is free if the schema declares a predicate keyed by process:
the output stream is ordered by key order, so grouped output falls out. It is the same trick
`src.FileXRef` uses to hand a renderer its cross-references already sorted by line and column.

**BM25 retrieval works; BM25 scoring does not.** Postings as facts — `Posting {term, doc} -> tf` —
are seekable on the leading term, and document length is another predicate. What is missing is the
arithmetic. sigla has `+` and `-` on `i64`, wrapping, and that is all: no division, no `log`, no
float. The type model is five constructors — `Int`, `Str`, `Fact`, `Record`, `Union`
(`crates/fjord-schema/src/schema.rs:44-60`). Glean has no signed integer, so this is a place we are
*wider*, and still nowhere near enough.

**Semantic search fails at the type model.** Vectors need floats, a distance function, and an ANN
index. All three are absent, and the third is not a language feature at all — it is a second index
kind at the storage seam.

**RRF fails at ordering.** Reciprocal rank fusion merges ranked lists, which is a sort. See below.

The thing to carry away: **this is a ranking gap wearing a search gap's clothes.** Candidate
retrieval — structural, prefix, and with the assumption fuzzy — is well within reach. What Fjord
cannot do is put the candidates in an order anyone would call relevance.

### 9 · `cypher` — raw graph queries

Not a subset relationship in either direction, and it should be framed that way rather than as a
missing feature. Fixed-length Cypher patterns map to sigla joins one-for-one — sigla is
Datalog-flavoured, and `MATCH (a)-[:CALLS]->(b)` is `code.Calls {from = A, to = B}`. What does not
map: variable-length paths (`*1..5`) are recursion; `ORDER BY` is ranking; `LIMIT` is a clause
sigla deliberately does not have, because bounding a result is the client's job and `--limit`
leaves the query unchanged; `WITH` and aggregation materialise.

sigla is the peer feature, not a subset of this one. Exposing it is what `fjord query` already is.

---

## The ordering story

The most useful correction this audit makes. **"Ordering" is two unrelated problems, and Fjord's
position on them is opposite.** Filing it as one backlog bullet would hide that half of it is
already built and the other half is the most expensive thing on the list.

### (a) Ordering by a stored attribute — solved, and stronger than it looks

The output stream is lexicographically ordered by the concatenation of the levels' key orders in
nesting order: [I1](../website/content/invariants.md#i1) makes encoded order value order, and
`enumerate` advances every level monotonically over a sorted scan. That order is total,
deterministic and **resume-stable** — resuming from a cursor produces exactly the rows, in exactly
the order, an uninterrupted run would ([executor](../website/content/executor.md)).

[glean.md §1.1](glean.md#11-query-language) records Glean making the weaker promise — `seek`
returns each key *"in no specified order"* — and notes that ours is the stricter one, which
forecloses the key truncation Glean adopted.

What Fjord lacks is not order but a **chosen** order: the order is a consequence of the schema and
the plan, never of the query. The standing answer — *if you want the data in another order, declare
it twice* — is what `src.SearchByName`, `src.FileXRef`, `src.DerivesFrom` and `src.AttributeOf`
already are. It covers more of GitNexus than it first appears: process grouping, route maps,
parameter order, flow step order, and a file's cross-references in render order are all free.

### (b) Ordering by a computed value — ranking — is the real gap, and it is not additive

Top-k by score must see rows it will not emit, so it materialises. That is the property
[glean.md §1.4](glean.md#14-aggregation) already names as the reason aggregation is absent, and it
is the same wall. It also breaks the claim [chapter 5](../website/content/executor.md) is built on:
a suspended query holds one detached row per open level, bytes only, so a page held for an hour
costs what one held for a millisecond does. A partial ranking buffer is state proportional to the
*result*, not to the plan.

**Ranking and recursion are the only two items in this audit that reshape the machine.** Everything
else is an enum arm, a residual op, or a schema decision.

---

## What fuzzy buys, precisely

The assumption is load-bearing and its limits are sharp, so they are worth stating rather than
discovering later.

- **It is a `Source`/`Access` variant, not a `Step`** — exactly what
  [`AGENTS.md`](../AGENTS.md) permits: *a construct may add a `Source`, a `Test`, a residual op or
  a `Computed` arm — never a `Step`*. The architecture is already shaped for it.
- **The `FactStore` seam needs nothing new.** It is `scan(lo, hi)` plus `point(id)`
  (`crates/fjord-store/src/fact_store.rs`); re-opening a scan at the DFA's next candidate key is a
  working guided seek. A `seek_to` on an open scan is an optimisation, not a prerequisite — and it
  would touch both implementations plus the differential oracle.
- **Resume is safe, and the argument should be written down rather than assumed.** The DFA is a
  pure function of the query term, so it re-derives at restore with nothing kept in the cursor; and
  a Levenshtein DFA can be re-entered at an arbitrary key, because it is a function of the
  candidate string rather than of the walk history. So the cursor stays one detached row per level,
  bytes only — [I4](../website/content/invariants.md#i4) and
  [I8](../website/content/invariants.md#i8) untouched. This is the kind of claim this repository
  would want a property test for before believing it.
- **It only helps on a leading key field.** `src.SearchByLowerName` is keyed `{name, to}`, so the
  schema pattern fuzzy needs already exists and already carries the comment explaining why. Fuzzy
  on a non-leading field, or on a variable an earlier level bound, degrades to a residual over a
  full scan — and there is no fuzzy residual op either.
- **It does not help ranking.** Edit distance is a natural relevance score and there is nowhere to
  put it. A fuzzy seek returns candidates in *key* order, not distance order — the ordering gap
  arriving immediately, inside the one feature fuzzy was supposed to rescue.
- **Search-as-you-type needs the anchored operator, and `~` is not it.** Whole-string edit
  distance answers "did they misspell the complete name"; a search box asks "does what they
  have typed so far reach the start of this name". `~<` is that question, and the difference is
  not marginal — over the 148,809-name corpus `"parse_node"~1` answers 5 rows and `"parsr"~<1`
  answers 7,416. Both operators exist, both guide a seek, and the guide stops reading a row the
  moment an anchored prefix accepts, because every key extending it is an answer.

---

## Candidates for the language backlog

Not proposed as entries — a backlog entry is a commitment and this audit should be read first.
Listed so the reading has somewhere to go, roughly by ratio of features unblocked to cost.

| Candidate | Unblocks | Shape |
|---|---|---|
| `distinct` under the prefix condition | the "which files" answer in #4, #6, #8, #13 | Additive; mechanism worked out in [glean.md §1.3a](glean.md#13a-why-we-cannot-deduplicate-yet-and-what-would-let-us), one row of cursor state |
| A fuzzy `Source` | #8, the discovery half of #2 | Additive; the assumption this audit was run under |
| Sargeable `<`/`>` on a leading key field | #6 | Additive; already a backlog bullet |
| A same-row `EqField` residual | `nyi/repeated-variable`, the self-reference check in #7 | Additive; #7 is the first caller to ask for the operator |
| Cross-database fan-out with a merge policy | #16, #17, cross-repo everything | Above the executor; designed in [glean.md §0](glean.md#0-the-question-that-opened-this-file-fact-ids-across-databases), `ops-I9` untouched |
| Recursion / transitive closure | #4, #5, #13, #15, half of #7, part of #9 | **Machine reshape** — no longer a candidate: it has a design and a movement plan ([`PLAN.md`](../PLAN.md#recursion--query-local-relations-magic-sets-stratified-negation)) |
| Ranking / order-by-computed | #2, the confidence half of #4 and #7 | **Machine reshape** — it materialises |

## The things to remember

- **Six of seventeen answer today; nine are held back by three gaps, not nine.** Recursion,
  ranking, and cross-database query account for almost the whole ◐ column.
- **`query` is a ranking problem, not a search problem.** BM25 postings are ordinary seekable
  facts. Treating it as "no full-text search" would send work at the wrong thing.
- **Ordering is half-built, not unbuilt** — and the built half is stronger than Glean's, while the
  unbuilt half is the single most expensive item here.
- **Fuzzy is architecturally cheap and strategically narrow.** It is a `Source`, the seam already
  supports it, resume survives it — and it rescues one feature and a half.
