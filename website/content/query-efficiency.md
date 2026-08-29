---
title: Query efficiency
description: What a query costs is the rows it examines, not the rows it answers — the four decisions flatten makes per key field, what closes a seek prefix, and how to read the difference back off a plan.
---

Two queries that answer the same rows can differ by five orders of magnitude, and the
difference is never in the answer. It is in how many rows the executor had to look at to find
it. This page is the working rule for keeping that number small, and how to check.

The engine has no statistics and no cost model — `reorder` chooses a loop order for
*legality*, not for speed ([Not built](status.html#not-built) records why). So nothing
recovers a slow spelling for you. What decides the cost is the **schema's field order** and
**what your query pins**, and both are visible in `:plan` before you run anything.

## The unit of cost is a row examined

```text
STEP      EXAMINED
src.Decl  483       full scan
src.Ref   5
488 examined, 5 produced
```

`--profile` prints that, per step. **Examined** is rows pulled off a scan; **produced** is
rows that reached the head. Every filter in this engine sits between the two, so a query that
answers five rows out of a 8.6 M-row predicate does 8.6 M rows of work and looks instant on a
demo database. The ratio is the number to watch, and it is the only one that moves with the
corpus.

:::note Why not "rows returned"
Every other limit in this engine counts output. A scan whose residuals reject every row
produces nothing while doing all the work, so a ceiling that counted rows *answered* would
read zero on exactly the query that needs stopping. That is why the executor's own limit —
[the ceiling](#the-ceiling-is-deployment-policy) — counts input.
:::

## Four decisions, one per key field

`flatten` walks a predicate's key fields **in declaration order** and decides one of four
things about each. The first three narrow what is read; the fourth reads and drops.

| Decision | When | What it costs |
|---|---|---|
| **seek** | The field is a constant, or a constrained range, and every field before it also narrowed | Nothing — the scan opens on a shorter range |
| **splice** | The field's value is in a register an *earlier* level bound | Nothing — this is the join, and it is why the inner loop is not a scan |
| **guide** | The field is a [fuzzy pattern](fuzzy-search.html) — `~` or `~<` — and the fields before it narrowed | One automaton walk per row, and a re-opened scan per dead band |
| **filter** (a residual) | Anything at all, once the seek prefix has closed | A row read, then dropped |

The first three are what "sargeable" means here: the constraint reaches the *key order*, so
the rows that cannot match are never read. A filter answers the same question and reads the
whole range to do it.

## The rule: a field narrows only while everything before it did

A seek is one contiguous run of the key order, so the prefix is built left to right and the
first field that cannot contribute **closes it permanently**. Everything after a closed prefix
filters, however specific it looks.

| At a key field | The prefix | Because |
|---|---|---|
| A literal, or a variable that folded to one | **stays open** | The bytes are known at compile time |
| A variable an **earlier level** bound | **stays open** | Its bytes are in a register — this is the splice |
| A reference to a row an earlier level bound | **stays open** | The field holds an id, so the register's *identity* is spliced — never its key bytes, which are a different value entirely |
| A union alternative — `{text = …}` | **stays open** | A tag is complete, self-delimiting bytes, so the payload's own walk carries on |
| A **complete** record — `{extra = 1, inner = 2}` | **stays open** | The whole wrapped value is known |
| A string prefix — `"a".."` | **ends it, and is the last part** | A range is one run of the order, and nothing after it in the key is that field's bytes |
| A fuzzy pattern — `"ann"~1`, `"ann"~<1` | **ends it, and becomes the guide** | A set of ranges inside one range |
| A **wildcard** — `_` | **closes it** | Nothing is known |
| A **capture** with no constraint on it | **closes it** | The field is an output |
| A **partial** record — `{extra = 1, inner = A}` | **closes it** | A record keeps its wrapper inside a key, so a partial one is not a byte prefix of a complete one |
| A computed value — `Y + 1` | **closes it** (`nyi/value-match`) | A seek compares bytes known before the run |

The two that surprise people are the last two rows and the capture. A capture is the common
case:

```sigla
X where test.Foo {id = X, name = "ann"}      → a scan, then a filter
X where X = test.Foo {id = 1}                → a seek
```

`test.Foo` is keyed `{id, name}`. Asking *which ids are called `ann`* captures `id` at the
leading field, so `name = "ann"` — a constant, on a key field, in a key-only predicate — is
still a residual. There is no spelling of that question that seeks, and that is the honest
answer: **the fix is a second predicate keyed the other way**, not a cleverer query. See
[field order is the index design](schema-language.html#field-order-is-the-index-design).

## Constraints are collected before the order is chosen

`X = "a".."` is a **constraint**, not a filter, and where you write it does not matter:

```sigla
X where test.Name X; X = "a"..               → the same range seek
X where X = "a"..; test.Name X               → as this one
```

Constraints are gathered from the whole body first and then applied by whichever level
*captures* the variable, so the level that binds `X` can seek with it. A constraint applied
afterwards would answer the same rows and read the whole predicate to find them — which is
exactly what happens when the capture is behind a closed prefix and there is no seek left to
narrow.

A variable may carry several, and they are applied by **what each one can do**, not by where
it was written: an exact constant first, then a prefix, then a fuzzy pattern — and where both
fuzzy spellings are present, `~` before `~<`. That is what makes these two the same plan, one
guided seek over the `"an"` bucket:

```sigla
N where test.Name N; N = "an"..; N = "ann"~2
N where test.Name N; N = "ann"~2; N = "an"..
```

## A fuzzy match: guide or filter

`"parse"~1` works in **any** position — on a trailing field, inside a union payload at depth,
on a row reached by a point read through a reference — and so does its anchored sibling
`"parse"~<1`. What changes with position is not whether it answers but whether it can skip
work. What changes with *anchoring* is only which rows are answers, never where the pattern may
sit.

It becomes a **guide** — a `Source::Guided`, walking the key order and seeking past the bands
its dead states prove cannot match — when all three hold:

1. The seek prefix is still open when the walk reaches that field, by the table above; **or**
   the fuzzy pattern is on the very field a prefix range already ended the seek on (`N =
   "pa"..; N = "parse"~2`).
2. Nothing else on that level already took the guide. One automaton drives the walk; a second
   fuzzy pattern on the same level filters, and both still hold. Between `~` and `~<`, the
   whole-string one guides — it is the one whose states go dead on a long key, so it is the
   one with bands to seek past.
3. The level *scans*. A row reached by a fetch is one row already, so there is nothing to walk.

Otherwise it is a `ResidualOp::Fuzzy` and reads every row in the range. Both forms use the same
automaton, cost the same per row, and answer the same rows —
`a_guided_seek_answers_what_a_filtered_scan_answers` is the property that says so, and
`a_guided_prefix_seek_answers_what_a_filtered_scan_answers` says it for the anchored question.

Anchoring adds one thing the whole-string form has no use for: once a prefix accepts, every key
extending it is an answer, so the guide stops reading the row rather than computing a seek
target from it. That is what
`an_accepted_long_key_is_not_decoded_past_its_accepting_prefix` measures.

```sigla
N where test.Name N; N = "ann"~1                     guide  — the field leads the key
N where test.Foo {id = 1, name = N}; N = "ann"~1     guide  — the field before it is pinned
N where test.Bar {id = I};
      test.Foo {id = I, name = N}; N = "ann"~1       guide  — pinned by the outer level
N where test.Foo {id = _, name = N}; N = "ann"~1     filter — the leading field is free
```

The full walkthrough of the automaton, the bounds and the measured saving is
[Fuzzy search](fuzzy-search.html).

## What never seeks

Some things read rows by construction, and no spelling changes that:

| Construct | Why |
|---|---|
| A denial — `X != "a".."`, `X != 3` | "Does not start with `a`" is the two ranges either side of one, and a seek walks one range |
| A comparison — `<` `<=` `>` `>=` | On a leading field this *could* narrow to a run, and the arm is written to be replaced. It filters today |
| A negation — `!(…)` | A `Step::Test`: it binds nothing and probes per row |
| Anything reading `.value` | A value lives in the identity map, and [I6](invariants.html#i6) keeps that out of the scan loop. `.value` is fetched for rows that already survived every filter |
| A repeated variable in one row — `{from = X, to = X}` | Refused by name (`nyi/repeated-variable`) rather than answered slowly |

None of these is a reason to avoid them. A denial *inside* a narrow range costs almost
nothing; the trap is a denial that is the only thing you said.

## Order is yours to choose

`reorder` emits the runnable frontier — a statement runs as soon as everything it reads is
bound, lowest-numbered first — and a query whose written order already works is returned
unchanged. It is a **safety** pass: it makes legal orders legal. Nothing in it prefers the
cheap order, because nothing feeds it a selectivity estimate.

So when two generators could go either way, the one that binds the other's key fields should
be written first:

```sigla
N where test.Bar {id = I}; test.Foo {id = I, name = N}
      r0 <- test.Bar scan
      r1 <- test.Foo seek[id = r0.id, name = _]
```

`test.Bar` is scanned; `test.Foo` is sought once per row of it. Which of the two you want on
the outside is the one that is small, or the one that is already narrowed.

:::warn A join can still be a residual
Being bound is not enough — the field has to be reachable in the key order too:

```sigla
N where test.Named {name = N, of = F}; F = test.Foo {id = 1, name = _}
      r0 <- test.Foo seek[id = 1, name = _]
      r1 <- test.Named scan
           where of == r0#
```

`test.Named` is keyed `{name, of}`. `F` is bound and its id is right there, but `name` is
captured in front of it, so the reference compare is a residual over the whole predicate. Add
what you know about `name` and the same query seeks.
:::

## Read the plan, then the profile

`:plan <query>` in the shell answers without running anything — the client compiles locally
against the schema the server serves. `query --profile` reports what a run actually examined,
per step, to stderr. The plan's vocabulary is small and worth recognising on sight:

| In a plan | Means |
|---|---|
| `scan` | The whole predicate |
| `seek[…]` | A narrowed range; `= _` marks where the prefix stopped |
| `seek~[…]` | A guided walk — a fuzzy automaton decides what is visited inside the range. `~<` inside it is the anchored question |
| `fetch[r0.of]` | One row, by id — the cheapest source there is |
| `where …` | A residual: rows read and dropped |

Live, against the demo schema — the leading field is a reference, so a constant `file` seeks
and a constant `name` behind it filters:

:::demo plan
N where F = code.File "src/lib.rs"; code.Decl {file = F, name = N, line = _}
:::

## The ceiling is deployment policy

A query that examines far more than it produces is bounded rather than trusted. `Executor`
takes a **rows-examined ceiling**, charged per row in the same tick that drives cancellation,
and a run that crosses it ends with `ExaminedCeiling { examined, ceiling }`.

- The **server** sets one: 64,000,000 rows by default, and a `Listener` may set a tighter one
  with `with_examined_ceiling`. It applies to the query and count paths alike.
- An **embedded caller** reading its own database gets none by default. Opting in is one
  builder call.

It is a limit on input, and it is deployment policy: it refuses a run, it never changes an
answer, and it is not part of a plan's fingerprint — so a cursor is not tied to the ceiling
that was in force when it was minted.

:::note Bounding the *result* is a different question
`--limit`, `:more` and a paged web tier cut a result short and hand back a cursor; the query
is unchanged and resumes exactly where it stopped. That is [suspend and
resume](executor.html#the-cursor-bytes-and-nothing-else), not a limit. The ceiling is for the
run that would never have produced the rows in the first place.
:::

## When the answer is the schema

If a question you ask often reads far more rows than it produces, and [the rule
above](#the-rule-a-field-narrows-only-while-everything-before-it-did) says every field it could
narrow on is behind a capture, the query is not the problem. Declare the
same data keyed for that question — the second predicate is the index, and this design makes
you write it down rather than inferring it.

Field order is the largest effect measured anywhere in this project: a seek on the leading
field against the same question asked behind a capture differs by orders of magnitude, and
[Performance](performance.html#field-order-is-the-largest-effect-measured-anywhere) has the
numbers.
