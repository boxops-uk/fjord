---
title: Fuzzy search, step by step
description: A gentle, executable walkthrough of the Levenshtein DFA — its rows, transitions, accepting and dead states — then where a fuzzy match may sit in a query, what it refuses, and what guiding a seek actually saves.
---

Fuzzy search answers a small question: **is this stored string within a chosen number of
single-character edits of my term?** In sigla, `"parse"~1` allows one insertion, deletion,
or substitution. Leaving the number off also means one edit.

Fjord answers that question with a deterministic finite automaton, or **DFA**. The name can
sound more forbidding than the machine is. Its state is one short row of numbers; each input
character turns that row into exactly one next row. The examples below use the automaton from
the query engine itself. Use **next** and **previous** to work each one through.

## 1. Begin with the empty candidate

Each column stands for a prefix of the search term. Before the candidate has supplied any
characters, matching `∅` costs nothing, matching `c` needs one deletion, and matching longer
prefixes needs more deletions.

The allowed distance here is one, so the row is **capped at two**. Two means “two or more”:
once a cost is outside the limit, its exact size cannot change any decision the automaton
makes. Capping is what gives the machine a finite number of possible states.

:::demo dfa
{"term":"cat","candidate":"c","distance":1}
:::

## 2. Consume one character at a time

For every new character, each cell chooses the cheapest of three ways to arrive there:
substitute the new character, delete a term character, or insert the new character. A matching
pair costs nothing; a different pair costs one.

Step through `cat`. Watch the zero move diagonally across the row as each candidate character
matches the next term character. The final cell is the distance to the **whole** term. When it
is at most the limit, the current prefix is an accepting state.

:::demo dfa
{"term":"cat","candidate":"cat","distance":1}
:::

This is deterministic: a row plus the next character has only one possible next row. The row
contains everything the machine needs to know about the prefix already consumed; it never
needs the earlier rows again.

## 3. One row covers all three edit kinds

A substitution appears when the candidate takes a different character at the same position.
Here `u` replaces `a`. The walk still ends at distance one, so `cut` is accepted for `cat~1`.

:::demo dfa
{"term":"cat","candidate":"cut","distance":1}
:::

An insertion is one extra candidate character. The cheapest path holds its place in the term
while it consumes that character; the later matching characters can then carry on.

:::demo dfa
{"term":"cat","candidate":"cart","distance":1}
:::

A deletion is the mirror image: the candidate omits a term character. The row can advance
through that term character without consuming another candidate character.

:::demo dfa
{"term":"cat","candidate":"ct","distance":1}
:::

There are not three special-case matchers. The minimum used to build every cell accounts for
all three edits at once, including combinations when the allowed distance is two or three.

## 4. Live states are the key to skipping work

An accepting state says the current prefix matches **now**. A live state asks a different
question: could *some extension* of this prefix still match? It is live while at least one
cell remains within the allowed distance.

Walk `zzzz` until the demonstration stops. After `zz`, every cell is beyond one edit. No later
suffix can undo those differences, so the state is dead. The remaining characters are not
read because a dead state can never become live again.

:::demo dfa
{"term":"cat","candidate":"zzzz","distance":1}
:::

That is more than an early rejection. Stored strings are in key order, so all keys beginning
with the same dead prefix sit together. The executor can seek past that whole band and reopen
the scan at the next prefix whose DFA transition is live. It finds that next character from
the term's own alphabet plus one representative mismatch; it does not enumerate Unicode.

## 5. Ask the same machine a different question

Everything above measures the distance to the **whole** stored string, and for a search box
that is the wrong question. `parse` is five characters; `parse_node` is ten. Five deletions
separate them, so `"parse"~3` does not find it — not because the spelling is wrong, but
because the tail is long.

`~<` asks whether **some prefix** of the candidate is within the distance. Nothing about the
machine changes: the same rows, the same transitions, the same dead states. What changes is
*where the answer is read*. A whole-string match asks the final cell once, at the end; an
anchored match asks it after every character and stops at the first one that says yes.

Walk `cattle` against `cat~<1` and watch it stop early. `ca` is one edit from `cat`, so the
answer is settled three characters in — and it settles for `cattle`, `catalogue` and
`cathedral` alike, because every extension of a matching prefix matches too.

:::demo dfa
{"term":"cat","candidate":"cattle","distance":1,"anchored":true}
:::

That last sentence is the load-bearing one. Under `~` a longer key eventually goes dead and
the executor seeks past the band; under `~<` a longer key is an *answer*, so once a prefix
accepts there is nothing to skip and nothing left to read. A four-kilobyte identifier that
accepts on its fifth character costs five characters.

**Anchored is not substring.** The term still has to reach the start of the stored string.
`"parsr"~<1` finds `parser_function` and does not find `my_parser_function` — which is what
keeps the pattern a set of ranges the automaton can seek between, rather than a scan.

One consequence is worth stating rather than discovering: a term no longer than its distance is
within that distance of the **empty** prefix, and every string starts with the empty prefix. So
`"a"~<1` matches the whole predicate. The language leaves it legal, because it is precisely
what a search box asks on the first keystroke.

## 6. Put the guide on a key

A fuzzy pattern can guide storage only when it reaches the next string field in key order.
In this schema, `search.Name` has one string key field, so the compiler places a **guided**
access in the plan. Change the term or distance and the real compiler replans it immediately.

:::demo plan
schema search {
  predicate Name : string
}
---
N where search.Name N; N = "parse"~1
:::

Results still arrive in stored key order, not ranked by edit distance. The DFA decides which
parts of that order may contain answers; it is not a scoring or sorting stage. An ordinary
prefix constraint can also anchor the range first, keeping a wider fuzzy search inside a
known part of the predicate.

## 7. Run it as part of a real query

The demonstration database keeps declaration names after their file reference in the key.
That means this spelling cannot guide the leading edge of the scan: the compiler uses the
same fuzzy automaton as a residual check on each declaration row instead. Step the machine to
watch rows reach that check and either survive or be rejected. The DFA state table follows the
highlighted database candidate. When the executor reaches that fuzzy check, the shared transport
descends into a second tier: each click advances one DFA transition while the outer executor is
paused. After the last character, the DFA returns its accept-or-reject decision and the transport
continues through executor transitions. The two counters always say which machine is moving.

:::demo run guided
N where code.Decl {file = _, name = N, line = _}; N = "parse"~1
:::

The distinction is physical, not semantic. A guide and a residual answer the same fuzzy
question; a guide can additionally avoid reading key ranges that its dead states prove cannot
match. Neither path fetches a fact's value just to inspect a key field.

## 8. Where a fuzzy match can sit

A fuzzy pattern works in **any** position, and `~` and `~<` sit in exactly the same places. On
a trailing key field, inside a union payload at depth, on a row reached by a point read through
a reference — the question is always answered, and always by the automaton above. What position
decides is only whether the automaton can *skip*: whether it guides the scan, or filters rows
the scan produced anyway.

It becomes a guide when three things hold, and the first is the one that moves:

1. **The seek prefix is still open when the key walk reaches that field.** Every field before
   it is pinned to exact bytes — a literal, a variable an outer level already bound, a
   reference, a union tag. A wildcard or a free capture in front of it closes the prefix, and a
   closed prefix cannot be reopened. There is one extra way in: a fuzzy pattern on the very
   field a prefix range already ended the seek on, which is the anchored spelling in §5.
2. **Nothing else on that level already took the guide.** One automaton drives the walk. A
   second fuzzy pattern on the same level filters, and both still have to hold. Where the two
   are `~` and `~<`, the whole-string one takes it: its automaton is the one that goes dead on
   a long key, so it is the one with bands to seek past.
3. **The level scans.** A row reached by a fetch is one row already, so there is nothing to
   walk past.

| Written | What the compiler builds |
|---|---|
| `N where test.Name N; N = "ann"~1` | a guide — the field leads the key |
| `N where test.Foo {id = 1, name = N}; N = "ann"~1` | a guide — the field in front of it is pinned |
| `N where test.Bar {id = I}; test.Foo {id = I, name = N}; N = "ann"~1` | a guide — pinned by the outer level's register |
| `N where test.Name N; N = "an"..; N = "ann"~2` | a guide, over the `"an"` bucket only |
| `N where test.Name N; N = "ann"~<1` | a guide — anchoring changes the question, not the position |
| `N where test.Name N; N = "ann"~1; N = "anno"~<1` | a guide and a residual: `~` walks, `~<` filters |
| `N where test.Foo {id = _, name = N}; N = "ann"~1` | a residual — the leading field is free |
| `T where test.Label {id = _, what = {text = T}}; T = "a"~1` | a residual, on a payload behind an unpinned field |
| `X where test.Link {at = _, of = F}; X = F.name; X = "ann"~1` | a residual, on the fetched row |

Which one you got is in the plan and nowhere else: `seek~[…]` is a guide, `where … ~1 …` is a
residual. The wider rule this is one case of — what closes a seek prefix, and what to do when
it closes too early — is [Query efficiency](query-efficiency.html).

The playground opens with **a fuzzy match** among its samples — a misspelt path against
`code.File`, whose key *is* the string, so it compiles to a guided access over the real demo
database. Step it and watch the automaton reject `docs/guide.md` and seek past it. Point the
same pattern at a declaration's name instead and the plan becomes a residual, because `Decl`'s
key leads with a reference: the same question, a different plan, one edit away in the box.

### The bounds, and what is refused

Both limits are checked at **typecheck**, before the compiler chooses between a guide and a
residual. That is deliberate: a limit held by the guide alone would refuse a query on a leading
field and answer the same query on a trailing one, which is the physical plan leaking into what
the language means.

| Written | Answer |
|---|---|
| `N = "ann"~`, `N = "ann"~<` | one edit — either operator with no number |
| `N = "ann"~4`, `N = "ann"~<0` | `reject/fuzzy-distance` — the range is `1` to `3`, for both |
| a term of 64 characters or more | `reject/fuzzy-term` — 63 is the most an automaton is built for |
| `N = "a"~<1` | **everything** — a term no longer than its distance matches the empty prefix. Legal, and documented in §5 |
| `N != "ann"~1`, `N != "ann"~<1` | `nyi/fuzzy-denial` — meaningful, deferred by name; one claim, not two |
| a fuzzy match against `.value` | `nyi/value-match` — a value is fetched per row, and residuals run inside the scan |

Distance stops at three because past it the automaton is live over so much of the key space
that a guided scan is a full scan wearing a hat. The term length is what keeps the DP row a
fixed-size value that a transition can copy without allocating.

## 9. What the guide actually saves

Per row, every form costs the same and none depends on how long the stored string is: the walk
stops after `|term| + distance + 1` characters, because a candidate longer than that is already
too many deletions away, and the field is decoded one character at a time so a 4 KiB identifier
rejected on its fourth character costs four characters. An anchored match has the tighter bound
of the two — it stops at its accepting prefix, so the same 4 KiB identifier costs five
characters when it *matches*. The automaton is built once when the level opens, not once per
row.

What the guide adds is the dead-state case: it backtracks to the longest still-live prefix,
asks for the smallest character above this row's that keeps it live, and re-opens the scan at
the smallest key that could still match. A state that is live but not yet accepting does *not*
seek — its matching extensions are the very next keys.

Over a predicate of 148,809 identifier-shaped names (`examples/e2e_fuzzy`, `MemStore`):

| Query | Answers | Rows a filter reads | Rows the guide reads | Hops |
|---|---|---|---|---|
| `"parse_node"~1` | 5 | 148,809 | 44 | 39 |
| `"parse_node"~2` | 51 | 148,809 | 175 | 124 |
| `"parse_node"~3` | 482 | 148,809 | 539 | 57 |
| `"nosuchname"~1` | 0 | 148,809 | 19 | 19 |
| `"pa"..` then `"parse_node"~2` | 51 | 7,416 | 156 | 105 |
| `"parse"~<1` | 7,416 | 148,809 | 7,432 | 16 |
| `"parsr"~<1` | 7,416 | 148,809 | 7,432 | 16 |
| `"parse_node"~<1` | 470 | 148,809 | 500 | 30 |
| `"parse_node"~<2` | 933 | 148,809 | 966 | 33 |
| `"pa"..` then `"parse"~<1` | 7,416 | 7,416 | 7,416 | 0 |

Three rows to read twice.

`"pa"..` then `"parse_node"~2` is the anchoring pair: a prefix narrows the range before the
guide starts, so the guide has far less left to skip. The two do the same job, and the second
one to run gets whatever the first left.

`"parsr"~<1` answers 7,416 rows on a term that is *misspelt*. Under `~` it answers none —
which is the whole reason the operator exists, stated as a number.

`"pa"..` then `"parse"~<1` reads exactly its answers and hops zero times, and that is not a
coincidence: the anchor's range and the anchored term's answer set are the same set of keys
here, so the guide accepts every row it meets and never has cause to seek. It is the shape a
search box produces, and the cheapest thing the machine does.

The anchored rows read more than the whole-string ones because they *answer* more: 5% of the
predicate rather than 0.03%. The share that matters is the one between "answers" and "rows the
guide reads", and it stays within a few dozen rows in every line above.

A hop is a re-opened scan, and it is not free: roughly ten rows' worth on fjall. So the guide
pays where the dead bands are wide, which is the usual case for a long term at a small
distance, and pays least where an anchor has already done the narrowing.

:::note What the guarantee actually is
`doubling_the_predicate_does_not_double_the_rows_examined` is the guard: as the predicate
grows 46 k → 228 k, the filtered scan grows with it and the guided count moves 40 → 47. A
guided source that answered correctly by reading the whole predicate would pass every other
test in the tree and be worthless, so that property is the one the feature is held to.
`…_when_anchored` is its twin, and there is a second one for `~<` alone:
`a_guided_prefix_seek_never_seeks_past_an_accepting_prefix` reads a band of two hundred keys
that all match and asserts the scan was opened **once**.
:::

## 10. Stop and resume without storing DFA state

A guided scan may suspend after any returned row. Its cursor stores the ordinary key position,
not an automaton row. On resume, the executor replays that key from the DFA's start state to
recover exactly the row it needs. The transition is a pure function of the term, distance,
and consumed characters, so the cursor format needs no fuzzy-search exception.

Use the same run again, moving backwards as well as forwards. The executor reconstruction, DFA
table, and explanation follow the trace rather than relying on a hidden live iterator.

:::demo run guided
N where code.Decl {file = _, name = N, line = _}; N = "parse"~1
:::

So a guided source takes the same cursor entry a plain seek takes, is a witness for a negation
exactly as a seek is, and nothing downstream of the row can tell the two apart. That is the
whole claim: a guide is a scan that reads less, not a new kind of thing for the rest of the
machine to know about.
