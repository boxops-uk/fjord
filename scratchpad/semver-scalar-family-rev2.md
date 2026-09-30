# A `semver` scalar family — rev 2

| | |
|---|---|
| **Issue** | not filed yet; this document is the plan under review |
| **Area** | `fjord-encoding`, `fjord-schema`, `fjord-wire`, `fjord-store`, `fjord-store-fjall`, `fjord-ingest`, `fjord-engine`, `fjord-inspect`, `fjord-server`, `fjord-cli`, `clients/dotnet` |
| **Precedent** | `bytes`, the last scalar family added — the shape of this work item is that one's |
| **Depends on** | the tagged-literal form, **already built** — `fjord_engine::tagged`, and the query-language page's *Tagged literals*. `semver` adds a row to its table |
| **Invariants** | **I1** (order preservation — this is the whole of the work), **I2** (`skip` walks it), **I3** (the marker table is append-only; the placement is forced), I10 untouched |
| **Fingerprint** | a schema using `semver` is new; nothing existing moves |
| **Format** | **one new marker byte.** `codec` version unchanged — appending a marker is what I3 permits |
| **Protocol** | **a bump.** The wire descriptor gains a tag, so a peer built before this refuses the stream rather than misreading a field |

## Claim

A schema can declare a field holding a **SemVer 2.0.0 version**; it round-trips through sigla,
the storage codec, the wire, a range seek and both JSON renderings; and **its encoded `memcmp`
order is SemVer precedence order**, checked against an independent oracle rather than against
itself.

## Why this is a primitive and not a record

The obvious alternative is `{major: int, minor: int, patch: int, pre: string}`, and it is wrong
in four separate ways — each of them an *ordering* failure, which is why the type has to reach
the codec:

1. **A version with no prerelease sorts *after* one with a prerelease.** `1.0.0-alpha < 1.0.0`.
   An absent string field is the empty string, which sorts *before* every other string. There is
   no assignment of bytes to a `string` field that fixes the direction.
2. **Numeric prerelease identifiers compare numerically.** `1.0.0-2 < 1.0.0-10`. A string
   comparison says the opposite.
3. **Numeric identifiers sort below alphanumeric ones**, whatever their text.
4. **A shorter run of identifiers sorts below a longer one that it prefixes.**
   `1.0.0-alpha < 1.0.0-alpha.1`.

A record orders field-by-field over its fields' own encodings, and none of those four rules is
expressible that way. Order preservation is the entire reason this is a primitive; if a consumer
only ever wanted equality, a `string` would do and this work item would not exist.

## The encoding

**`MARK_SEMVER = 0x54`, appended after `MARK_BYTES = 0x53`, is the only legal placement.** The
argument is `MARK_BYTES`'s own, and it is not re-litigated here: I3 freezes the table on disk,
renumbering after data exists is an AGENTS.md anti-pattern, and the alternative is a `codec`
bump that I15 turns into "every existing database is unopenable". The consequence — `semver`
sorts after `bytes`, which sorts after unions — is unobservable for the same reason as before: a
field has one declared type, a union discriminates by tag before any payload is compared, and a
record's fields are positional, so no query can put a `semver` and anything else on two sides of
one comparison.

**The payload is an escaped run, exactly as `bytes` is.** `MARK_SEMVER`, then
`put_escaped(canonical)`, then the terminator. That buys I2 for free — an escaped run is
self-delimiting, so `skip` needs no case beyond the marker — and it buys order preservation for
free too, because `memcmp` of two escaped runs agrees with `memcmp` of the payloads. All the
design work is therefore in **what `canonical` is**, and the law is that `memcmp` over it equals
SemVer precedence.

```
canonical :=
    uint(major) uint(minor) uint(patch)
    rank                              0x00 if a prerelease is present, 0x01 if not
    [ identifier* 0x00 ]              only when rank is 0x00
    escaped-tail(build)               empty when there is no build metadata

identifier :=
      0x01 uint(n)                    a numeric identifier
    | 0x02 <ascii bytes>  0x00        an alphanumeric identifier

uint(n)   := 0x01                     n == 0
           | (0x01 + len) <minimal big-endian bytes>
```

Four things make that order correctly, and each is a line in the property test rather than a
claim here:

- **`uint` is order-preserving because the width leads.** For a non-negative integer with no
  leading zeros, a shorter big-endian run is always the smaller number, so comparing width first
  and bytes second is comparing magnitude. This is the existing integer encoding's trick without
  the sign half, which `semver` does not need.
- **`rank` puts prerelease below release**, which is rule 1 above and the one a record cannot
  express.
- **The identifier terminator `0x00` sorts below both identifier tags**, which is rule 4: a run
  that is a prefix of another sorts below it.
- **`0x01` (numeric) sorts below `0x02` (alphanumeric)**, which is rule 3; and within a numeric
  identifier `uint` gives rule 2.

### The one place this is *not* SemVer, and it is deliberate

SemVer says build metadata is **ignored** for precedence: `1.0.0+a` and `1.0.0+b` have equal
precedence. That makes SemVer precedence a **partial** order, and a key encoding must be a
**total** one — two distinct keys cannot compare equal, because a `keys` row names exactly one
fact (I12, `ops-I5`).

So the encoding puts build metadata last, after everything precedence cares about, and:

> **Fjord's order is a total refinement of SemVer precedence, and Fjord's equality is stricter
> than SemVer's.** Where SemVer says *less* or *greater*, Fjord agrees. Where SemVer says *equal
> precedence but different build metadata*, Fjord orders by the build string.

That is the right trade — the alternative is to drop build metadata, which loses data a
producer sent — but it is a **footgun with teeth**, and it is the first one in the list below.

### Bounds and rejections

- **Components are `u64`.** SemVer's grammar permits arbitrarily large integers; nothing real
  uses them. A component that does not fit is a **named diagnostic**, never a silent wrap.
- **Leading zeros are rejected, not normalised.** SemVer forbids them in numeric identifiers,
  and a numeric identifier is *stored as a number* here, so a leading zero is unrepresentable
  rather than merely invalid. Rejecting keeps `decode ∘ encode == id` honest; normalising would
  make two input spellings one key and quietly discard the difference.
- **Alphanumeric identifiers are ASCII** by the grammar, so the escaped run's `memcmp` is the
  spec's "ASCII sort order".

## The sigla literal — **rev 2: this no longer belongs to this work item**

rev 1 proposed `v1.2.3` as its own LL(1) alternative, and argued about whether `v1` lexing as an
identifier made that fragile. That argument was the signal: `bytes` had already paid the same tax
once for `0x…`, and a date family would pay it again for ISO 8601, each time with a fresh
ambiguity to reason about and a fresh chance to get it wrong permanently.

That mechanism is now **built** — `fjord_engine::tagged`, one `LId String` alternative in
`primary`, no lexer change, the tag being the type's own name — so `semver` adds a row to
`tagged::FAMILIES` and spells its values

```
semver "1.2.3"    semver "1.2.3-alpha.1"    semver "1.2.3-alpha.1+build.5"
```

The `semver "1.2.3"` corpus entry already exists, classified `Diagnosed(LitUnknownTag)`; the day
this lands it moves to `Supported` rather than being written from scratch.

**What this work item still owes** is what the mechanism says every family owes and nothing more:
a parser from the body text, a canonical printer back to it, and its own `Lit*` codes — one per
malformed form (a missing component, a leading zero, an empty identifier, a non-ASCII identifier,
a component that overflows `u64`) — plus the corpus entries that classify each. No grammar work,
no lexer work, and no `v1` ambiguity to argue about.

It also inherits the mechanism's three rules, and **rule 2 is the one this type exists for**: the
ordering is over the structure, never over the text. `1.0.0-alpha.2` and `1.0.0-alpha.10` order
the wrong way as text, which is the whole reason `semver` is not a `string`.

## The work

**1 · The type.** `PredicateTyNamed::Semver`; `Value::Semver`; `WireValue::Semver`; `Ty::Semver`;
`TySpec::Semver`; `"semver"` in `syntax::lower` (`lower.rs:449`) and `syntax::print`
(`print.rs:231`).

**2 · The codec.** `MARK_SEMVER = 0x54`, `put_semver`/`get_semver` over the existing
`put_escaped`/`get_escaped`, plus the `canonical` encoder and decoder above.

**3 · The compiler names the rest.** §19 of `bench/FINDINGS.md` enumerates the sites a new
`PredicateTyNamed` variant reaches — **21** of them, `fingerprint::type_form`,
`tuple::encode_typed_at`, `wire::value::encode_value`, `store::fact::checked`,
`ingest::intern::resolve`, `server::rows::to_wire` and the two halves of the generator census
among them — and **8** more for `fjord_engine::syntax::Ty`. Run `scripts/check-exhaustive.sh
schema` and `… engine` and work the list; a shorter list than §19's is a regression, not a
saving. (Its first pass on the current tree names four sites: `fingerprint.rs:304`,
`refs.rs:35`, `syntax/print.rs:173` and `:228`.)

**4 · Four independent tag tables, each taking its own next free number** — none derived from
another, and none shared with `Str`, because sharing would fold a version and a string together
in a content identity:

| Table | Where | Next free | `semver` takes |
|---|---|---|---|
| wire descriptor | `fjord-wire/src/desc.rs:30-45` | `… Union 4, Bytes 5` | **6** |
| content identity | `fjord-store-fjall/src/identity.rs:72-86` | `… Union 8, Bytes 9` | **10** |
| plan fingerprint (type) | `fjord-engine/src/plan.rs:1083` | `… Union 4, Bytes 5` | **6** |
| plan fingerprint (value) | `fjord-engine/src/plan.rs:1119` | `… Union 5, Bytes 6` | **7** |

**5 · Rendering — the canonical text, in both renderers.** `"1.2.3-alpha.1+build.5"`, bare, not
`{"$semver": …}`. The `bytes` precedent settles this: both live renderers hold the type at render
time, so a tag would carry nothing the schema does not already say. Unlike `bytes`, the rendered
text is unambiguous to a schema-less reader anyway.

**6 · The .NET client, in lockstep.** `FjordType.Semver` + `FjordValue.Semver`,
`ValueCodec.WriteValue` / read-side cases (`Values.cs:117`, `:276`), and `emit-golden.sh`
re-run. See the footguns below — the client is where most of this work item's risk is.

## .NET footguns

The engine is **strict SemVer 2.0.0** and is not bent toward .NET. Every quirk below therefore
lands on the client, and each is a way a .NET producer can write facts it did not mean to.

1. **Build metadata makes a fact.** `1.0.0+abc123` and `1.0.0+def456` have equal precedence to
   both SemVer and NuGet, and are **two different facts** here — `ops-I5` interns by bytes. The
   natural .NET source of a version string is `AssemblyInformationalVersion`, which is exactly
   the one that carries `+<sha>`. **A producer should strip build metadata unless it means
   something**, or every rebuild sprays a new fact into the index. This is the sharpest of the
   lot because nothing fails; the index just grows.
2. **`System.Version` is not SemVer and must never be implicitly converted.** It is
   `Major.Minor.Build.Revision`, has no prerelease and no build metadata, and — the trap —
   unset components are `-1`, not `0`, so `new Version(1,0)` is **not equal** to
   `new Version(1,0,0)`. Any mapping is lossy both ways; the client should offer an explicit,
   documented conversion or none.
3. **NuGet allows a fourth component; SemVer does not.** `1.2.3.4` is a legal `NuGetVersion`.
   The client must **reject it by name**, not drop the revision — dropping folds `1.2.3.4` and
   `1.2.3.5` into one key, which is a silent data loss wearing `ops-I5`'s clothes.
4. **NuGet compares prerelease labels case-insensitively; SemVer does not.** `1.0.0-Alpha` and
   `1.0.0-alpha` are one version to NuGet and two to SemVer — and they do not merely differ,
   they *order* differently (`A` < `a` in ASCII). A producer that dedupes with NuGet's
   comparer before writing will still emit two facts.
5. **NuGet normalises and SemVer rejects.** NuGet accepts `1.0` and widens it to `1.0.0`,
   strips leading zeros from numeric prerelease identifiers, and drops a zero revision. This
   encoding rejects the leading zero and requires all three components. **Normalise in the
   client, deliberately, and say so** — do not let two spellings become two keys by accident.
6. **`VersionRange` is not a value.** NuGet's range syntax (`[1.0,2.0)`) is a query, not a
   version, and has no place in this type. Range *queries* over a `semver` field fall out of
   I1 for free, which is the point of the whole work item.

## Acceptance criteria

1. **Order matches an independent oracle.** `TySpec::Semver` enters `arb_typed_pair` and
   `PredicateTy::Semver` the `cmp_typed` oracle, after which the existing properties cover it as
   they cover every family. The oracle for the *semantic* half is the `semver` crate as a
   dev-dependency — an independent implementation, never the code under test — with the law
   stated precisely: **where the oracle answers `Less` or `Greater`, encoded `memcmp` agrees;
   where it answers `Equal`, the two differ only in build metadata and Fjord orders by that.**
2. **The generator is proven to draw the family, from the spec.** A census assertion that it
   draws: no prerelease, numeric-only identifiers, alphanumeric identifiers, mixed runs, prefix
   runs, build metadata present and absent, and a `u64`-boundary component. The population comes
   from SemVer 2.0.0's grammar, **not** from the cases this document happened to think of.
3. **`semver_ordering_edges`** — a matrix drawn straight from the spec's own worked example,
   `1.0.0-alpha < 1.0.0-alpha.1 < 1.0.0-alpha.beta < 1.0.0-beta < 1.0.0-beta.2 <
   1.0.0-beta.11 < 1.0.0-rc.1 < 1.0.0`, asserted pairwise at the encoded-bytes level.
4. **The refinement is pinned by a test, not by this paragraph.** `1.0.0+a` and `1.0.0+b` are
   distinct keys, both intern to distinct facts, and their relative order is stable.
5. **Malformed input is refused by name**, one case per diagnostic: missing component, leading
   zero, empty identifier, non-ASCII identifier, `u64` overflow.
6. **No existing golden moves.** `byte_identical_with_the_dotnet_client` and
   `unions_are_byte_identical` green **without** regenerating `clients/dotnet/golden/*.txt`;
   `sample_schema`'s predicate-count assertion unchanged; every shipped schema's fingerprint
   unchanged.
7. **The format stamp does not move**, and a database created before this work item still opens
   — asserted by a fixture, not by argument.
8. **The .NET side round-trips it**, with a golden case emitted by `emit-golden.sh` and asserted
   byte-identical from Rust; plus a client-side test per footgun 3 and 4, proving the refusal and
   the case-sensitivity are deliberate.
9. **The literal is corpus'd**, against the tagged mechanism rather than a bespoke token: a
   `Supported` entry using a `semver "…"` constant in a key, a `Diagnosed` entry per malformed
   body, `every_code_is_reachable_from_the_corpus` green with the new `Lit*` codes, and
   `print::literal` round-tripping through the mechanism's property rather than a test of its own.
10. **The full gate**: `cargo test`, `cargo +1.97.1 clippy --all-targets --workspace -- -D
    warnings`, `cargo +1.97.1 fmt --all --check`, the wasm checks, `scripts/check-guards.py`,
    `scripts/check-docs.py`, `scripts/check-links.py`, and `cd clients/dotnet && dotnet test`
    against a freshly built release binary.
11. **The book says what the type is for**: the storage chapter's marker table, and a
    schema-language paragraph covering what it orders by, that build metadata refines the order
    rather than being ignored, and the .NET footguns by name.
12. **The flag day is walked.** The wire descriptor gains a tag, so this is a protocol bump and
    `clients/dotnet/README.md`'s ordered checklist applies — each client's constant checked
    against its own schema by name.

## Traps

- **`Str` and `Semver` must never share a tag in the identity hash.** Folding them makes two
  databases holding different data hash alike, which is `ops-I4`'s whole business.
- **Do not store the text.** The canonical form is the *parsed* structure; two spellings of one
  version must not be two keys. The corollary is that the printer reconstructs text from
  structure, which is what criterion 9's round-trip checks.
- **Do not normalise on the way in.** Rejecting a leading zero and normalising it are different
  contracts, and only one of them keeps `decode ∘ encode == id`.
- **Do not add a fourth component "because NuGet has one".** That is the moment the core ties
  itself to .NET, and footgun 3 is the client's to own.

## Not in scope

- **Version ranges** as a type or an operator. A range query is a seek over I1's order and needs
  nothing new; NuGet's bracket syntax is a client concern.
- **Arrays** of versions — `nyi/array` is settled and unaffected.
- **Migrating any shipped schema** from `string` to `semver`. That is one predicate's fingerprint
  and belongs in its own work item, deliberately.
