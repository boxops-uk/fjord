//! **The sample schema** — `schemas/demo.sigla`, parsed, and the name lookups the rest
//! of this crate's tests, benchmarks and instruments resolve against it.
//!
//! **It is not a default, and there is no longer such a thing.** Until 0.0.1 there *was*
//! a built-in schema: what a database got when `create` was not given a path. That made a
//! default decide how every stored row of somebody's database decoded, and made the
//! artifact a property of which build of the tool created it. `--schema` is required now,
//! and what is left here is a **fixture** — one worked example, in one place, so an
//! instrument cannot declare its own and end up measuring a database it could not have
//! written.
//!
//! **Nothing here states a schema.** `schemas/demo.sigla` is the single statement, in the
//! language `fjord create --schema` takes, and this module is the two lines that parse it
//! plus the name lookups. What is left to guard is therefore not "does the vector still
//! say what it said" but "does the *file* still declare what the rest of the tree names" —
//! which is what `tests` below asks.
//!
//! **Why this file and not the shipped set.** `schemas/dotnet.sigla` is what a real
//! producer writes: sixty-seven predicates across five layers, keyed for questions a code
//! browser asks. It is the wrong fixture for an instrument. A benchmark wants a corpus it
//! can generate to any size and a shape a reader can hold in their head; the browser
//! playground wants a schema that lowers in one file; the corpus tests want every
//! construct the language has, exactly once. `demo.sigla` is all three, and it is the
//! schema the site opens with — so a number measured here is a number a reader can
//! reproduce in a browser tab.

use std::sync::LazyLock;

use fjord_schema::{
    schema::{PredicateId, Schema},
    syntax,
};

/// The schema itself, as text.
///
/// **The file is the schema**, and it is a file a person can read, diff, and pass to
/// `fjord create --schema` — which is exactly what the scripts and the integration suites
/// do. Compiled in here so a bench does not have to find it on disk.
///
/// A *list* of one, and resolved rather than lowered directly, because the resolving path
/// is the one production takes: `demo.sigla` imports nothing today and an entry that grew
/// an import would otherwise be read as a schema quietly missing everything it brought in.
/// The multi-file case has its own test, `the_embedded_reader_follows_imports`.
const SOURCES: &[(&str, &str)] = &[(
    "schemas/demo.sigla",
    include_str!("../../../schemas/demo.sigla"),
)];

/// The schema everything here resolves names against: **a small code index**, which is
/// the canonical shape for a fact database — one fact per thing, and everything about a
/// thing pointing at it rather than repeating it.
///
/// **There are no id constants, and that is the point.** An id is a *position*, and
/// positions come from sorting the schema's names, so a constant would be a second
/// statement of something the schema already decides — wrong the first time somebody adds
/// a predicate that sorts earlier. Ask [`id`] by name. Nothing outside this process ever
/// sees one anyway: a block header carries the predicate's *name*, so a client keeps no
/// table to fall out of step.
///
/// **Eleven predicates, one per construct the type model can hold.** What each is here to
/// show:
///
/// | predicate | shows |
/// |---|---|
/// | `code.File` | a **scalar key** — a path is one string and needs no record |
/// | `code.Decl` | a record key with a **reference leading it**, and a **scalar value side**, so `D.value` has something to read |
/// | `code.Ref` | two references to one predicate, which is what a graph edge is |
/// | `code.Span` | a **nested record** in a key, spliced into it rather than framed |
/// | `code.Extent` | a **record value side**, which is the shape a scalar one cannot hold |
/// | `code.Kind` · `code.KindOf` | a **union in a key**, and the same data keyed the other way — because a predicate leads with one field and both questions are worth a seek |
/// | `code.Resolves` | a union carrying a payload of **every kind**: none, a reference, a different reference, and a record |
/// | `code.Digest` | **`bytes`**, which the language will not look inside, ordered by `memcmp` |
/// | `code.Extends` | **a keyword as a field name** — `type` is how a named type is declared and is still legal here |
/// | `code.Note` | a **single-alternative union**, which needs the trailing `\|` to be one at all |
///
/// **Why the field order decides the seeks, and why it is declared rather than derived.**
/// A record's fields are stored in the order `schemas/demo.sigla` lists them, that order
/// *is* the key order, and a query can only narrow on a leading run of it. So `code.Decl`
/// leads with `file` because "this file's declarations" is the question worth a seek, and
/// `line` trails so a window is a range on the last key field. Lowering preserves
/// declaration order for exactly this reason — it does not sort a record's fields.
///
/// Nothing sorts these slices: `flatten` walks the schema's own slice by index and looks
/// each query field up by name, and `fjord_store::fact`'s
/// `the_encoding_order_is_the_declared_order` pins that. An **alphabetical** habit makes
/// the physical key order a *consequence* of naming, which is how a declaration comes to
/// lead with a line number — the most expensive key in an index, by accident.
///
/// The order is chosen per predicate and stated in `tests::KEY_ORDER`, which is the guard:
/// a field list that changes silently answers a different question.
pub fn schema() -> Schema {
    /// Parsed once. `Schema` is `Arc`-backed, so handing out clones is a refcount bump
    /// rather than a re-parse — which matters because every connection asks for one.
    static SCHEMA: LazyLock<Schema> = LazyLock::new(|| resolve_or_panic(SOURCES));

    SCHEMA.clone()
}

/// The predicate a name denotes in the sample schema.
///
/// # Panics
///
/// If the schema does not declare it, which is a bug in the caller rather than input:
/// every name passed here is a literal in this repository.
#[must_use]
pub fn id(name: &str) -> PredicateId {
    schema()
        .find_position(name)
        .map(|(id, _)| id)
        .unwrap_or_else(|| panic!("`schemas/demo.sigla` declares no `{name}`"))
}

/// Resolve an embedded schema, or explain why the build is broken.
///
/// A schema compiled into the binary is not input — it ships with the program — so a
/// failure here is a bug rather than a bad file, and the panic carries the rendered
/// reason so it says which line of which file.
///
/// **The resolving path, not `lower` directly**, and the diagnostics asserted on are
/// the *resolved* ones: an unanswered import is then a failure here rather than a
/// schema quietly missing what it imported.
pub(crate) fn resolve_or_panic(sources: &[(&str, &str)]) -> Schema {
    match syntax::resolve::resolve_from(sources.iter().copied()) {
        Ok(resolved) => resolved.schema,
        Err(reason) => panic!("the embedded schema does not resolve:\n{reason}"),
    }
}

#[cfg(test)]
mod tests {
    use fjord_schema::schema::PredicateTy;

    use super::*;

    /// **The schema declares what it is supposed to declare.**
    ///
    /// This used to check six hand-written id constants against their names, which was
    /// the right guard while a position was written down twice. The positions are gone —
    /// `id` asks the schema — so what is left to check is the *membership*: that the file
    /// still holds the predicates the rest of the tree names, and that asking
    /// for one by name answers with it.
    #[test]
    fn the_schema_declares_what_the_tree_names() {
        let schema = schema();

        assert_eq!(
            schema.len(),
            11,
            "`demo.sigla` is eleven predicates — at least one per construct the type \
             model can hold, which is what makes it the fixture"
        );

        for name in [
            "code.File",
            "code.Decl",
            "code.Ref",
            "code.Span",
            "code.Extent",
            "code.Kind",
            "code.KindOf",
            "code.Resolves",
            "code.Digest",
            "code.Extends",
            "code.Note",
        ] {
            assert_eq!(
                schema.get(id(name)).and_then(|p| p.name()),
                Some(name),
                "`{name}` is not where the schema says it is"
            );
        }
    }

    /// **The .NET demo states its schema independently, and must still agree with the
    /// file the server parses.**
    ///
    /// The golden records the fingerprint `Boxops.Fjord.Demo` computed from its own
    /// declarations. `byte_identical_with_dotnet` compares that against a *third*
    /// statement in Rust, which is what makes the codec argument; what neither checks is
    /// whether either agrees with the schema the **server** actually serves, because that
    /// one is parsed from a file and nothing else reads it.
    ///
    /// **It names the file rather than trusting `schema()`.** The two are the same
    /// today — both are `demo.sigla` — and naming it is what keeps this a claim about the
    /// *server's* copy: if the fixture and the demo ever part company again, as they did
    /// while the indexer moved to `dotnet.sigla`, comparing the golden against whatever
    /// `schema()` returned would compare two unrelated numbers and pass by coincidence.
    ///
    /// Regenerate with `./clients/dotnet/emit-golden.sh` when that schema moves on
    /// purpose; both sides move together, which is the point.
    #[test]
    fn the_dotnet_demos_schema_is_the_file_the_server_parses() {
        const GOLDEN: &str = include_str!("../../../clients/dotnet/golden/blocks.txt");
        const DEMO: &str = "schemas/demo.sigla";

        let recorded = GOLDEN
            .lines()
            .find_map(|line| line.strip_prefix("schema-fingerprint "))
            .map(str::trim)
            .and_then(|hex| u64::from_str_radix(hex, 16).ok())
            .expect("the golden names a schema fingerprint");

        let root = std::path::PathBuf::from(concat!(env!("CARGO_MANIFEST_DIR"), "/../.."));
        let served = syntax::resolve::resolve(&root.join(DEMO), &[root.join("schemas")])
            .unwrap_or_else(|reason| panic!("{DEMO} does not resolve:\n{reason}"));

        assert_eq!(
            fjord_schema::fingerprint::of(&served.schema),
            recorded,
            "`{DEMO}` and the .NET demo's declaration have drifted — the demo would be \
             refused at the handshake"
        );
    }

    /// **Every stored key, flat, in the order its bytes go down in.**
    ///
    /// A nested record is spliced into its parent's key rather than framed, so this is
    /// the physical key and `at.line` is a position in it exactly as `to` is. Read it as
    /// the index design: a query narrows on a **leading run** of these fields and filters
    /// on the rest, so the first name in each row is the question that predicate is fast
    /// at, and everything after it is a tie-break.
    ///
    /// The build layer's four are alphabetical because they were written that way and
    /// nothing has measured a reason to disagree — they are thousands of rows, not
    /// millions. That is a different statement from the two that were changed, and it is
    /// here so the next reader can tell a decision from an inheritance.
    const KEY_ORDER: &[(&str, &[&str])] = &[
        ("code.Decl", &["file", "name", "line"]),
        ("code.Ref", &["from", "to"]),
        ("code.Span", &["decl", "at.line", "at.col"]),
        ("code.Extent", &["decl"]),
        ("code.Kind", &["decl", "what"]),
        ("code.KindOf", &["what", "decl"]),
        ("code.Resolves", &["at", "to"]),
        ("code.Digest", &["file"]),
        ("code.Extends", &["type", "base"]),
        ("code.Note", &["decl", "text"]),
    ];

    /// **What a record *value* side holds, in declaration order.**
    ///
    /// Separate from [`KEY_ORDER`] because the two are different claims. A key's order is
    /// the index design — it decides what a query can narrow on. A value's order decides
    /// nothing about seeking and everything about *decoding*: a value is encoded
    /// positionally against its declared type, so two fields swapped here reinterprets
    /// every stored row of that predicate, and `nyi/value-field` means a consumer takes
    /// the whole record or none of it.
    const VALUE_ORDER: &[(&str, &[&str])] = &[
        (
            "code.Extent",
            &["from.line", "from.col", "to.line", "to.col"],
        ),
        ("code.Digest", &["sha256"]),
    ];

    /// **A record's fields are stored in the order this file declares them.**
    ///
    /// A field list one swap out of order encodes fine, stores fine, and answers a
    /// different question — a predicate narrows on its leading fields, so `{base, type}`
    /// typed the other way round silently indexes the derived type instead of the base.
    /// Nothing else in the tree would notice.
    ///
    /// **This used to assert the fields were *sorted*, and that guard was worse than it
    /// looked.** It caught a swap only where the intended order happened to be
    /// alphabetical, and everywhere else it enforced the accident: `src.Decl` led with a
    /// line number and `src.Ref` with a column, which cost 56,274 rows examined per row
    /// produced on an ordinary join and made find-references unanswerable
    /// ([findings §2 and §11](../../../bench/FINDINGS.md)). A guard that pins the *intended*
    /// order catches the same swap and cannot enforce an accident, because somebody has
    /// to write the intention down.
    #[test]
    fn every_record_lists_its_fields_in_the_intended_order() {
        let schema = schema();

        fn walk(ty: &PredicateTy, schema: &Schema, prefix: &str, into: &mut Vec<String>) {
            let PredicateTy::Record(fields) = ty else {
                return;
            };

            for (field, ty) in fields.iter() {
                let name = schema.interner().resolve(*field).expect("a field name");
                let path = if prefix.is_empty() {
                    name.to_owned()
                } else {
                    format!("{prefix}.{name}")
                };

                if matches!(ty, PredicateTy::Record(_)) {
                    walk(ty, schema, &path, into);
                } else {
                    into.push(path);
                }
            }
        }

        for index in 0..schema.len() {
            let predicate = schema.get(PredicateId(index as u32)).expect("in range");
            let name = predicate.name().expect("a name");

            let mut key = Vec::new();
            walk(&predicate.predicate().key, &schema, "", &mut key);

            let expected = KEY_ORDER.iter().find(|(p, _)| *p == name).map(|(_, k)| *k);

            match expected {
                Some(expected) => assert_eq!(
                    key, expected,
                    "`{name}`'s stored key is not the one KEY_ORDER declares, \
                     which changes what it narrows on"
                ),
                None => assert!(
                    key.is_empty(),
                    "`{name}` has a record key and no entry in KEY_ORDER"
                ),
            }

            // **A record value side is declared order too, and the source layer is the
            // first thing here to have one.** It never seeks, so this is not the index
            // design — it is the *codec*: a value is encoded positionally against the
            // declared type, so swapping two fields of one silently reinterprets every
            // stored row of that predicate. `nyi/value-field` means a consumer reads the
            // whole value or none of it, which is what makes a silent swap total.
            let mut value = Vec::new();
            if let Some(ty) = &predicate.predicate().value {
                walk(ty, &schema, "", &mut value);
            }

            let expected = VALUE_ORDER
                .iter()
                .find(|(p, _)| *p == name)
                .map(|(_, v)| *v);

            match expected {
                Some(expected) => assert_eq!(
                    value, expected,
                    "`{name}`'s stored value is not the one VALUE_ORDER declares, \
                     so every row of it decodes into different fields"
                ),
                None => assert!(
                    value.is_empty(),
                    "`{name}` has a record value side and no entry in VALUE_ORDER"
                ),
            }
        }

        for (name, _) in KEY_ORDER {
            assert!(
                (0..schema.len()).any(|index| schema
                    .get(PredicateId(index as u32))
                    .and_then(|p| p.name())
                    == Some(name)),
                "KEY_ORDER names `{name}`, which is not in the schema"
            );
        }
    }

    /// Every reference has to name a predicate that is in the schema — the one way an
    /// appended predicate can still break an existing one is by being pointed at from
    /// a type whose target was mistyped.
    #[test]
    fn every_reference_points_somewhere_that_exists() {
        let schema = schema();

        fn walk(ty: &PredicateTy, len: usize, name: &str) {
            match ty {
                PredicateTy::Fact(target) => assert!(
                    (target.0 as usize) < len,
                    "`{name}` references predicate {}, and the schema holds {len}",
                    target.0
                ),
                PredicateTy::Record(fields) => {
                    for (_, field) in fields.iter() {
                        walk(field, len, name);
                    }
                }
                PredicateTy::Union(alts) => {
                    for alt in alts.iter() {
                        walk(&alt.ty, len, name);
                    }
                }
                PredicateTy::Int | PredicateTy::Str | PredicateTy::Bytes => {}
            }
        }

        for index in 0..schema.len() {
            let id = PredicateId(index as u32);
            let predicate = schema.get(id).expect("in range");
            let name = predicate.name().expect("a name");

            walk(&predicate.predicate().key, schema.len(), name);

            if let Some(value) = predicate.predicate().value.as_ref() {
                walk(value, schema.len(), name);
            }
        }
    }
}
