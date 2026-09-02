# Decisions, corrections, and the risks this plan carries

Three lists. The first is the fourteen decisions put for review and **answered** — recorded with
their consequence so the reasoning survives the sprint; the second is what the plan found to be wrong
in the issues (recorded here so nobody re-derives it); the third is what remains risky after
everything lands. **All of them are answered now**: D11 closed the one left open underneath D5, D12
is the largest of them — `code.sigla` retires, which cancels R4 and reorders everything after it —
and D13 takes the style payload out of the schema's hands entirely.

---

## 1 · Decisions taken

All fourteen were put with a recommendation and all fourteen are answered. Recorded with their
consequence, so the reasoning survives the sprint.

| | Decision | Consequence, and where it lands |
|---|---|---|
| **D1** | **Ship all five language schemas as defaults**, supporting the **C# surface first** | W9. `csharp` + `msbuild` are the supported pair and are exercised end to end; `typescript`, `npm` and `bundle` ship as declared, checked, fingerprint-recorded schemas whose producers are a consumer's. None is embedded in a binary |
| **D2** | **Keep the single whole-schema fingerprint constant.** The default schema is not expected to move often; when it does, a client version bump is the cost | W14. The per-predicate handshake claim is **declined** and the reason recorded. Both schema changes in this plan are therefore flag days, executed by W14's checklist |
| **D3** | **`bytes` is a protocol bump — accepted. JSON renders a bare lowercase hex string, untagged** | W3. The tagged form's argument was that *"a `Value` is serialised without its type"*, which is true only of the dead `impl Serialize for Value` that W2 deletes. Both live renderers hold the type: `rows::json` takes a `Desc` carrying `TAG_BYTES`, `inspect::value::json` takes a `&Schema`. The loss — a schema-less reader cannot tell `"00ff"` from a string — is documented, not paid for on every row |
| **D4** | **The `0x…` literal lands with `bytes`**, not later | W3. Its own LL(1) alternative, never a widening of the string rule; corpus entries for the malformed forms; `print::literal` round-trips |
| **D5** | **Delete `src.Line`.** No production consumers, so the schema gets the right shape rather than a wart plus a deprecation note | W6 becomes **Breaking** — eight added, one removed, every survivor byte-identical. Seven migration sites, all named. Re-baselines §1's line-table figures. The question left open underneath it — whether `fjord-viewer` is being retired — is answered by **D11** |
| **D6** | **Accept both of review #34's narrowed asks as stated** | W13. Cross-producer descriptor agreement and C6's origin requirement are recorded in revision 2's *"what this plan does not prove"* list rather than solved speculatively |
| **D7** | **No logical fact-bytes field in `FJORD_META`** | W12. One honest on-disk number is what a packaging step asserts against |
| **D8** | **The `bytes` prototype is closed-source and unavailable — re-derive** | W3 stands on its own criteria. The issue's account of five sequential runtime failures is evidence for W2, not a patch we can apply |
| **D9** | **Delete `--syntax-only`.** The indexer requires successful resolution | W13's **R3.7 removes the mode** rather than specifying it — 40 occurrences across seven files. **Three published measurements (§1, §14, §15) were taken with it and become unreproducible**, which is recorded and re-run in R7 |
| **D10** | **One sprint; the stated order is fine** | README's sequencing stands. The seven load-bearing orderings still hold inside it — in particular the two flag days stay separate commits |
| **D12** | **`code.sigla` retires**, and the C# indexer writes the new set. There are no consumers, so the freedom to break it exists now and will not later — the schema move goes first, before the runs that merely make the indexer better | [W15](15-retire-code-sigla.md). **R4 is cancelled**: it would spend a flag day giving `src.Decl` the semantic identity `csharp.Method`'s `docId` key already has, in a schema being deleted. R4.0 and R4c survive, re-aimed. Five new runs S1–S5 replace it, and **R7 must run after S5** because every published read number is keyed to predicates this deletes |
| **D11** | **`fjord-viewer` is retired, now, before W6.** A browser application replaces it — React + Vite, a WebAssembly client and a JS wrapper | W11 is re-cut from "fix the viewer" to "what the new viewer needs from this side", and the crate is deleted before the flag day rather than migrated through it. **W6 loses two of its seven migration sites**; R9 loses its stated gate and needs another; and the protocol grows a **WebSocket listener**, because a browser can open neither a Unix socket nor raw TCP and those are the whole of `Transport` today |
| **D13** | **`src.FileLineStyles` is `bytes`, and fjord defines no style vocabulary.** A producer writes what its tokeniser already emits and names the format in `config.Setting {dimension = "style-encoding"}` | Re-cuts **W6 D3** (which shipped `styles` as a run-length `string`) and **W6 c7**, and re-cuts **W15's S1**. The kind table, its `fjord-1` revision and the fold from Roslyn's 67 `ClassificationTypeNames` down into it are all **deleted** rather than deferred: a schema agnostic about a payload has no business shipping the payload, and the fold discarded distinctions a real tokeniser had already made. The dimension `style-vocabulary` is renamed, because it named half the thing. The round-trip property moves to the producer, where the format now lives |
| **D14** | **The Glean comparison is retired, and Fjord stands on its own.** The capability ledger, the read-path comparison plan, the batch-format translation, the script that ran both systems and the `--glean-out` flag all go | The comparison was two documents and a translation, and every number it produced was already void: it was measured over `src.Decl` in a corpus `code.sigla` took with it. What stays is **provenance** — the citations that explain a rule, because deleting those loses the reasoning rather than the comparison. `csharp.sigla` still says it is a translation of Glean's schema and `codemarkup`'s vocabularies still name their source, which is what W8's risk 4 depends on: their discriminants are frozen *because* they are citations, and a reader who thought we invented the numbering would feel free to renumber it. `bench/FINDINGS.md` §15 and §17 are **annotated rather than deleted** — a ledger that deletes entries claims a measurement was never taken — and §17's finding survives them because it is about this system: the 3.5× was our own memory pressure |

## 2 · Corrections this plan makes to the issues

Verified against the tree, each with its anchor. None of them changes an issue's conclusion; three
change the work.

| # | The issue says | The tree says |
|---|---|---|
| C1 | `syntax::print::same_ty`'s caller `compatible` is dead code, so its wildcard costs nothing today (#38) | There is no `fn compatible`. The caller is `equivalent` (`print.rs:136`), with **exactly one production call site**: `recoverable()` (`catalog.rs:875-885`), run by `Catalog::create`. Everything else is a test. So the issue's *first* claim — that this would make `create` reject a valid schema — was right for a schema **built programmatically**, and its retraction was too broad. It fails closed, so it is not a correctness bug, but it must be restructured, not deleted |
| C2 | five silent wildcard sites (#38) | **six** — `fjord_encoding::tuple::encode_typed_at` (`tuple.rs:762`, wildcard `:824`) reports a scalar mismatch as `BadRecord` |
| C3 | `#[deny(clippy::wildcard_enum_match_arm)]` is optional regression insurance (#38) | The lint fires only on a **single-enum** scrutinee and every one of the six sites matches a **tuple**. Measured: 160 sites workspace-wide (120 hand-written, 85 in `fjord-engine`) and **not one is a site from this defect**. The lint becomes useful only *after* the restructure |
| C4 | `MARK_BYTES = 0x53` is "a permanent wart, and the reason to decide while `0.x` still lets the table move" (#38) | The table does not move at any version. I3 freezes it, `MARK_UNION`'s own comment says appending is *"the only thing I3 permits"*, and I15 checks the format stamp for **equality** at open — so the alternative is not a tidier table, it is every 0.1.0 database becoming unopenable. Take the wart |
| C5 | predicate ids are positional, "the entry file's own predicates first" (#41) | Ids are assigned by **sorted fully-qualified name**, `fjord.*` last (`lower.rs:155-171`). The issue's example is consistent with both readings only because `app` sorts before `base2`. The hazard is real and **sharper**: adding `src.FileDigest` *inserts* an id rather than appending |
| C6 | `FJORD_META.bytes` may mean "logical fact bytes", and `fjord list` "already knows the honest number" while `du` disagrees by 2.6× (#43) | `bytes` is `identity::directory_size()` — the on-disk size at sealing, journals included. Both numbers are on-disk and should agree; the reporter's discrepancy is a **third** thing, most likely a journal fjall reclaimed on a later open |
| C7 | the sealed artifact carries journals that "look reclaimable" (#43) | Worse: **`finish` never flushes memtables** (`seal()` calls `persist` and `compact`, and `flush_to_tables` is `#[cfg(test)]`), so the data is *only* in the journal. Measured here: 208,000 facts → **60 KB of tables and 29 MB of journal** |
| C8 | issue #37's fix needs "`fixtures.rs` gaining a two-alternative union" | The shared fixture **already has one**, declared twice in two separately-allocated `Arc`s (`fjord-store/src/fixture.rs:27-30`, `:230`, `:241`) with matching values in both. The regression test is a corpus entry and **no fixture change** |
| C9 | Run 8a is "largely an accessibility change" for `IBlockTarget`, `FactSink`, `IFactWriter` | **`IFactWriter` does not exist.** Writer concurrency is a raw `Thread[]` (`FactSink.cs:62`). Two are accessibility changes; the third is an extraction |
| C10 | `src.sigla` is "8 predicates" (#39 appendix A header) | It lists **nine**, and `index.sigla`'s own total only adds up with nine: 9+1+10+16+31+14+22+35 = 138. Verify at implementation |

And one correction the plan makes to **itself**: revision 2 declared the `ops-I4`/`ops-I5` slip
corrected and then carried it twice — in its own C5 row, and in the Glean transcription C5 cited,
retired since under D14.
**Both landed**, in W10: the conflict-reject rule is `ops-I4`, as `invariants.md:408` has it, and
`ops-I5` is the one-write-funnel rule it was being confused with.

---

## 3 · Risks carried after all of this lands

1. **Two published measurements become unreproducible, deliberately.** D9 deletes the mode §1, §14
   and §15 were measured with, and D5 replaces the predicate §1's largest row counts. Both are
   re-run in R7 on a named corpus; until then the tree carries figures no command can reproduce, and
   `FINDINGS` must say so at each of them rather than leaving a reader to find out.
2. **The indexer owes a UTF-8 offset it has never computed.** `src.FileLine.start` is a byte offset;
   the indexer counts UTF-16 code units everywhere today (`GleanFacts.cs:297`). Getting `start`,
   `bytes` and `cstart` mutually consistent per line, for CRLF and for non-BMP source, is the one
   piece of new producer arithmetic in this plan, and it is exactly where an off-by-one hides.
3. **W12 may need an upstream change.** fjall reclaims journals only inside its own flush worker,
   gated on a hardcoded 64 MB threshold, with no public API to force it. If a material residual
   survives the flush fix, the options are an upstream request or a documented residual — and the
   third option (writing sealed tables into a fresh instance) is a much larger change that should
   not be reached for before the measurement.
4. **Two vocabularies freeze on landing.** `codemarkup.Kind` and `codemarkup.Role` sit in *keys*, so
   I10 freezes their discriminants the day W8 ships. `other : string = 0` is the only valve, and a
   transcription slip is permanent. W8's criterion 5 is the mitigation, not a cure.
5. **118 predicates arrive unpopulated**, and D1 supports the C# surface first — so `typescript`,
   `npm` and `bundle` ship with their two headline joins exercised (W9 c3) and nothing else. An
   unexercised predicate is a name in a file, and three namespaces of them is the shape of this
   risk.
6. **The style payload's forward-compatibility rule is load-bearing and untested by anyone but
   us** (D13). What lets a producer be richer than a reader is now one rule one layer up: *an
   unrecognised `style-encoding` renders those lines plain*. It is cheaper to honour than the kind
   table it replaces — a consumer compares one string — but nothing in this repository consumes
   styles at all, so the rule has no reader-side guard here. The producer's own encoder is covered
   (`SemanticTokensTests`); the contract between two parties is not.
7. **R9's gate is new and has never been run.** It was "the viewer answers `/symbol/{name}`", and
   D11 retired the viewer; the replacement is a fixed set of `codemarkup` queries against a
   converted index, asserted with rows. It is a better gate — it tests the converter rather than a
   UI — but it is *written* rather than *proven*, and a gate nobody has run is a gate whose rows
   might turn out to be the wrong ones to ask for. R9 depends on **W8** now, not on W11.
8. **Every published read measurement dies with `code.sigla`** (D12), and not merely as a
   re-baseline. §1's key-order finding, §2's join costs, §6's 67 q/s mix and §11's ~6,000 q/s are
   measured over `src.Decl`, `src.Ref` and `src.Line` in a corpus S5 deletes — and the replacement
   is a different *shape*, not the same corpus renamed: `csharp` splits one declaration predicate
   into a dozen and `codemarkup` re-keys the same facts again, so one corpus's fact count goes up
   rather than staying still. R7 re-runs them **after S5**, and until then the record carries
   figures no command can reproduce. This repository has been here once already, over
   `--syntax-only` (D9), and got through it by writing it down at each figure rather than by
   pretending.
9. **`nyi/value-field` shapes four schemas.** Every "this is in the key because a value cannot be
   projected" decision in W6, W8 and W9 becomes redundant the day value-field projection lands, and
   the keys will already be wide. That is the right trade today and worth knowing it was a trade.
