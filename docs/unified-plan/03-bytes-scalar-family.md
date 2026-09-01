# W3 · A `bytes` scalar family

| | |
|---|---|
| **Issue** | [#38](https://github.com/boxops-uk/fjord/issues/38) Part 1 — `[02]` |
| **Area** | `fjord-encoding`, `fjord-schema`, `fjord-wire`, `fjord-store`, `fjord-ingest`, `fjord-engine`, `fjord-server`, `fjord-cli`, `fjord-inspect`, `clients/dotnet` |
| **Depends on** | **W2** — the gap is closed first, so this lands as a compiler-guided change rather than five sequential runtime failures |
| **Blocks** | nothing. W6's `src.FileLineStyles` wants it and does **not** wait for it |
| **Invariants** | **I3** (the marker table is append-only — the placement is forced, see below), I1 (order preservation), I2 (`skip` walks any typed value), I10 untouched |
| **Fingerprint** | a schema using `bytes` is new; nothing existing moves |
| **Format** | **one new marker byte.** `codec` version unchanged — appending a marker is what I3 permits |

## Claim

A schema can declare a field holding **uninterpreted bytes**; it round-trips through sigla, the
storage codec, the wire, a range seek and both JSON renderings; its encoded order is `memcmp` order
over the payload; and no existing golden byte moves.

## Why the codec is nearly free, and why the placement is not a choice

`put_str` is `MARK_STRING` followed by `put_escaped(s.as_bytes())` (`tuple.rs:476`); `get_str` is
`get_escaped` then `from_utf8` (`:481`). **`put_bytes` is the same minus the validation — and not
performing that validation is precisely what the type is.**

The soundness that matters is order preservation, and the escape scheme already has it over
arbitrary bytes: a `0x00` in the payload becomes `0x00 0xFF`, a bare `0x00` terminates, so `memcmp`
of two encoded strings agrees with `memcmp` of the payloads. A length prefix would **not** — it
sorts by length first, which is not the order anybody means. `test_str_nul_ordering_edges`
(`tuple.rs:2276`) was already testing this property; it just happened to be testing it through a
type that cannot express `0xFF`.

**`MARK_BYTES = 0x53`, appended after `MARK_UNION = 0x52`, is the only legal placement** — and the
issue frames this as a wart to fix *"while `0.x` still lets the format move"*. It does not.
`MARK_UNION`'s own doc comment states the rule (`tuple.rs:25-41`): *"Appended after `MARK_FACT_REF`,
which is what I3 permits and the only thing it permits: the table above it does not move."* I3
freezes the table on disk, AGENTS.md lists renumbering markers after data exists as an
anti-pattern, and 0.1.0 is released. The alternative is not a tidier table — it is a `codec` version
bump, and I15 checks the stamp for **equality** at open, so every database written by 0.1.0 becomes
unopenable. **Take the wart, and write down why it is not negotiable**, so nobody re-opens it.

The consequence is that `bytes` sorts after unions rather than beside strings, and it is
**unobservable on the read path**: a field has one declared type, a union discriminates by tag
before any payload is compared, and a record's fields are positional. There is no query that can
compare a `bytes` with a `string`.

## The work

**1 · The type.** `PredicateTyNamed::Bytes`; `Value::Bytes`; `WireValue::Bytes`; `Ty::Bytes`;
`TySpec::Bytes`. After W2 the compiler names every site.

**2 · The codec.** `MARK_BYTES = 0x53`, `put_bytes`/`get_bytes` over `put_escaped`/`get_escaped`;
`skip` needs no new case beyond the marker, because an escaped run is self-delimiting (I2).

**3 · Four independent tag tables**, each taking its own next free number — none derived from
another, and none shared with `Str`, because sharing would fold `Str("ab")` and `Bytes(b"ab")`
together in a content identity:

| Table | Where | Existing | `bytes` takes |
|---|---|---|---|
| wire descriptor | `fjord-wire/src/desc.rs:30-37` | `Int 0, Str 1, Fact 2, Record 3, Union 4` | **5** |
| content identity | `fjord-store-fjall/src/identity.rs:72-82` | `1,2,3,4,5,6,7,8` | **9** |
| plan fingerprint (type) | `fjord-engine/src/plan.rs:951` | `Int 0 … Union 4` | **5** |
| plan fingerprint (value) | `fjord-engine/src/plan.rs:981` | `Null 0, Int 1, Str 2, FactRef 3, Record 4, Union 5` | **6** |

**4 · Rendering — a bare lowercase hex string, in both renderers.** *Not* `{"$bytes": …}`.

The tagged form was proposed on the grounds that *"a `Value` is serialised without its type"* — and
that is true only of `impl Serialize for Value`, which is **dead code this plan deletes** (W2). Both
live renderers hold the type at render time: `fjord_cli::rows::json` takes a `Desc`
(`rows.rs:306`), which after this work item carries `TAG_BYTES`, and `fjord_inspect::value::json`
takes a `&Schema` (`value.rs:18`). Every consumer that can interpret the field has the schema too.

The one case that loses is a reader of *detached* JSON text with no schema, for whom
`"00ffff0080c0"` is indistinguishable from a `string` whose content happens to be hex. That is
stated in the book rather than paid for by every consumer, on every row, forever. Hex rather than
base64 because `fjord-encoding` has no base64 dependency and a hex byte pair is readable in a
terminal.

Note this is not the union precedent: a union's `{"alt": payload}` carries information the schema
does **not** have — which alternative this row took. A bytes tag would carry none.

**5 · Ordering allowed, and the `0x…` literal lands in this work item.** `Ty::Bytes` is accepted by
`Checker::compare` (`ty.rs:179-210`). sigla gains a `0x…` token — **required, not deferred** — so a
`bytes` value is reachable as a query constant and not only by binding it out of a field. Without
it, `fjord_engine::print::literal` becomes the one place that emits text sigla cannot parse back,
and a digest lookup (`FileDigest {digest = 0x…}`) is inexpressible.

It is its own LL(1) alternative in the lelwel grammar, never a widening of the string rule — which
would make every existing string literal ambiguous. Being a new literal, it owes the same things
every literal owes: corpus entries for the valid and malformed forms (an odd digit count, a bare
`0x`, a non-hex digit), a `Lit*` diagnostic for each, and a print round-trip.

**6 · The .NET client, in lockstep.** `FjordType.Bytes` + `FjordValue.Bytes` records,
`ValueCodec.WriteValue`/read-side cases (`clients/dotnet/Boxops.Fjord.Client/Values.cs:118`), and
`emit-golden.sh` re-run. Adding the type without the C# side is a trap for whoever writes the first
C# producer that wants it, and R4e already walks this checklist — the two share it (W14).

## Acceptance criteria

1. **Order matches an independent oracle, for bytes.** `TySpec::Bytes` enters `arb_typed_pair`
   (`tuple.rs:1828`) and `PredicateTy::Bytes` enters the `cmp_typed` oracle (`:1741`), after which
   the existing properties cover bytes as they cover every family: order-matches-oracle, round-trip,
   `Value` ord agreement, and `skip_walks_any_typed_value` (`:3235` — I2 for a field a reader may not
   understand). **No new law is written for what the existing ones already say.**
2. **The generator is proven to draw the family.** `the_generator_draws_bytes_including_non_utf8` —
   a census assertion, because a leaf the generator never reaches is a law that passes vacuously,
   and adding a variant while forgetting the strategy looks identical to proving it correct. W2's
   criterion 5 is the general form; this is its first instance.
3. **`bytes_ordering_edges`** — a matrix over payloads a `String` cannot hold (`0x00`, `0xFF`,
   lone surrogates' UTF-8 encodings, `0x80`/`0xC0` continuation bytes), asserting encoded `memcmp`
   order equals payload `memcmp` order at every pair.
4. **`bytes_holds_what_a_string_cannot`** — `0x00 0xFF 0xFF 0x00 0x80 0xC0` survives a full
   round trip: sigla → `create` → the wire → the storage codec → a range seek → the printer,
   through the real binary, as an integration test.
5. **The renderers agree.** W2's criterion 6 extended: the bare string `"00ffff0080c0"` from both
   `fjord_cli::rows::json` and `fjord_inspect::value::json`, asserted equal — and asserted
   **untagged**, so the decision is pinned by a test rather than by this paragraph.
6. **No existing golden moves.** `cargo test -p fjord-client byte_identical` and
   `unions_are_byte_identical` green **without regenerating** `clients/dotnet/golden/*.txt`;
   `schemas/code.sigla`'s fingerprint unchanged; `sample_schema.rs`'s 27-predicate assertion
   unchanged. A `bytes` family that moves an existing byte is a defect in this work item.
7. **The .NET side round-trips it.** A new golden case covering a `bytes` field, emitted by
   `emit-golden.sh` and asserted byte-identical from Rust — the same construction as the union
   golden (`unions_are_byte_identical_with_the_dotnet_client`, `byte_identical_with_dotnet.rs:740`).
8. **The format stamp does not move.** `codec`/`storage` versions unchanged, and a database created
   before this work item still opens — asserted by an existing-database fixture, not by argument.
9. **The full gate**: `cargo test`, `cargo +1.97.1 clippy --all-targets --workspace -- -D warnings`,
   `cargo +1.97.1 fmt --all`, `cargo check -p fjord-engine --target wasm32-unknown-unknown`,
   `python3 scripts/check-guards.py`, `python3 website/build.py --strict`.
10. **The literal is corpus'd.** A `Supported` entry using a `0x…` constant in a key, and a
    `Diagnosed` entry for each malformed form; `every_code_is_reachable_from_the_corpus` green with
    the new `Lit*` codes; and `print::literal` round-trips — what it emits, sigla parses.
11. **The book says what the type is for.** One paragraph in the storage chapter's marker table and
    one in the schema-language page: what `bytes` is, that it is not validated, that it sorts after
    unions and why that is I3 rather than taste, the `0x…` literal, and the one JSON consumer that
    cannot tell it from a string without the schema.

## Traps

- **`Str` and `Bytes` must never share a tag in the identity hash.** Folding them makes two
  different databases hash alike, which is `ops-I4`'s whole business.
- **The wire descriptor gaining `TAG_BYTES = 5` makes a client upgrade mandatory**: a peer built
  before this refuses the stream rather than reading the field as a `string` and handing its caller
  non-UTF-8. That is the right behaviour, it is **a protocol bump**, and it is accepted — the
  protocol version exists for exactly this and the `.NET` client ships a version with it (W14).
- **Do not add a `bytes` literal to sigla by widening the string rule.** A `0x…` rule is its own
  LL(1) alternative; overloading the string token would make every existing string literal
  ambiguous.

## Not in scope

- Arrays. `nyi/array` is a settled decision (PLAN.md, *Multiplicity*) and `bytes` is not a way
  round it: a packed varint table in a `bytes` field is opaque to the query engine by design.
- Migrating `src.FileLineStyles` from `string` to `bytes` — W6 ships the ASCII form, and the
  migration is one predicate's fingerprint later, deliberately.
