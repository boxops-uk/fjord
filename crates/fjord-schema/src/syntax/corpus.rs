//! **The schema surface as data** — the audit table, executable so it cannot drift.
//!
//! `fjord_engine::corpus` does this for `sigla` and earned its keep immediately:
//! running it before touching the grammar gave the audit empirically, and six entries
//! that did not parse turned out to be exactly the six constructs that step added. This
//! is the same table for the schema DSL.
//!
//! # What an entry claims, and when
//!
//! Each entry carries the classification it should *end up* with, and the gate checks
//! as much of that as the compiler can currently answer:
//!
//! | | must |
//! |---|---|
//! | [`Verdict::Lowers`] | parse, and lower to a schema with no diagnostics |
//! | [`Verdict::Diagnosed`] | **parse**, and then draw exactly that code |
//! | [`Verdict::SyntaxError`] | not parse |
//!
//! The middle row is the whole of permissive-early: a deferred construct is a thing the
//! grammar accepts so that lowering can name it. A gate that only checked *some*
//! diagnostic came out would pass for a compiler that reported the wrong one, which is
//! exactly the drift a code exists to prevent — so the assertion is on the **set** of
//! codes, and an entry that draws a second unexpected one fails too.

use super::diag::Code;

/// What a source is expected to come to.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Verdict {
    /// Accepted, all the way to a `Schema`.
    Lowers,
    /// Parses, and is then refused by name.
    Diagnosed(Code),
    /// Not in the language at all.
    SyntaxError,
}

/// One row of the table.
#[derive(Debug, Clone, Copy)]
pub struct Entry {
    /// What the construct is called, in prose.
    pub about: &'static str,
    pub source: &'static str,
    pub verdict: Verdict,
    /// **The files this entry's `import`s resolve against**, as `(name, text)`.
    ///
    /// Empty for a single-source entry, which is almost all of them: `source` is then
    /// parsed and lowered directly. A non-empty set makes the entry a *resolution*
    /// case, and there was no way to state one until resolution stopped needing a
    /// filesystem — which is why no cross-file behaviour was in this table at all.
    pub imports: &'static [(&'static str, &'static str)],
    /// What the entry source is **called**, for a spanning entry.
    ///
    /// It has to be a name an `import` could match, because a cycle imports the entry
    /// back — and `about` is prose.
    pub name: &'static str,
}

impl Entry {
    /// Every source this entry resolves over, the entry first.
    ///
    /// # Panics
    ///
    /// If a spanning entry has no name, which would make its own file unimportable.
    pub fn sources(&self) -> impl Iterator<Item = (&str, &str)> {
        assert!(
            self.imports.is_empty() || !self.name.is_empty(),
            "`{}` spans files and has no name",
            self.about
        );

        std::iter::once((self.name, self.source))
            .chain(self.imports.iter().map(|(name, text)| (*name, *text)))
    }
}

const fn entry(about: &'static str, source: &'static str, verdict: Verdict) -> Entry {
    Entry {
        about,
        source,
        verdict,
        imports: &[],
        name: "",
    }
}

/// An entry whose `import`s resolve against `imports`, the entry itself first and
/// named `name`.
const fn spanning(
    about: &'static str,
    name: &'static str,
    source: &'static str,
    imports: &'static [(&'static str, &'static str)],
    verdict: Verdict,
) -> Entry {
    Entry {
        about,
        source,
        verdict,
        imports,
        name,
    }
}

/// The table.
pub const CORPUS: &[Entry] = &[
    // ---- the surface that works -------------------------------------------------
    entry(
        "a scalar key",
        "schema src { predicate File : string }",
        Verdict::Lowers,
    ),
    entry(
        "a record key, whose field order is the key order",
        "schema src { predicate File : string\n          predicate Module : { file : File, name : string } }",
        Verdict::Lowers,
    ),
    entry(
        "a value side",
        "schema src { predicate Module : string\n          predicate Decl : { module : Module, name : string } -> string }",
        Verdict::Lowers,
    ),
    entry(
        "a reference to another namespace",
        "schema src { predicate Decl : string }\n         schema a { predicate P : { d : src.Decl } }",
        Verdict::Lowers,
    ),
    entry(
        "a named type, which is sugar with no identity of its own",
        "schema src { type Position = { line : int, col : int }\n          predicate At : { where : Position } }",
        Verdict::Lowers,
    ),
    entry(
        "an import, which names a namespace and never a path",
        "schema a { import lang.rust }",
        Verdict::Lowers,
    ),
    entry(
        "several blocks in one file — a namespace is open across files",
        "schema a { predicate P : string }\nschema b { predicate Q : string }",
        Verdict::Lowers,
    ),
    entry(
        "a comment",
        "# what this is\nschema src { predicate File : string }",
        Verdict::Lowers,
    ),
    entry(
        "an empty record",
        "schema src { type Unit = {} }",
        Verdict::Lowers,
    ),
    entry(
        "a nested record inside a key",
        "schema src { predicate File : string\n          predicate Ref : { at : { line : int, col : int }, file : File } }",
        Verdict::Lowers,
    ),
    // ---- across files: what resolution comes to ----------------------------------
    //
    // None of this was in the table before, for a mechanical reason rather than an
    // oversight: stating a cross-file case needed a filesystem, and a corpus that wrote
    // temp directories is a corpus nobody runs. `resolve_from` is what made these cheap.
    spanning(
        "an import brings in what it names, and a reference crosses the boundary",
        "app.sigla",
        "schema app { import base\n predicate Use : { of : base.Thing } }",
        &[("base.sigla", "schema base { predicate Thing : string }")],
        Verdict::Lowers,
    ),
    spanning(
        "a named type moved into an imported file — the split every later schema rests on",
        "app.sigla",
        "schema app { import base\n predicate P : { flag : base.Flag } }",
        &[(
            "base.sigla",
            "schema base { type Flag = { no : {} = 0 | yes : {} = 1 } }",
        )],
        Verdict::Lowers,
    ),
    // **Both of these need the imported file to declare the namespace the import
    // named** — otherwise the namespace check below fires first, which is itself the
    // point of having it. A file holds several blocks, so `other.sigla` declares the
    // namespace it is fetched as *and* a second block in `app`, which is where the
    // second definition of `app.P` lives.
    spanning(
        "**identical** redeclaration across two files, which is still a redeclaration",
        "app.sigla",
        "schema app { import other\n predicate P : int }",
        &[(
            "other.sigla",
            "schema other { predicate Q : int }\nschema app { predicate P : int }",
        )],
        Verdict::Diagnosed(Code::RejectRedeclaration),
    ),
    spanning(
        "a genuinely different redeclaration across two files",
        "app.sigla",
        "schema app { import other\n predicate P : int }",
        &[(
            "other.sigla",
            "schema other { predicate Q : int }\nschema app { predicate P : string }",
        )],
        Verdict::Diagnosed(Code::RejectRedeclaration),
    ),
    spanning(
        "a file whose namespace is not the one the import named",
        "app.sigla",
        "schema app { import ob\n predicate P : { t : ob.Thing } }",
        &[("ob.sigla", "schema base { predicate Thing : string }")],
        Verdict::Diagnosed(Code::RejectNamespaceMismatch),
    ),
    spanning(
        "a cycle of imports, which dedup by identity makes harmless",
        "a.sigla",
        "schema a { import b\n predicate A : { b : b.B } }",
        &[("b.sigla", "schema b { import a\n predicate B : string }")],
        Verdict::Lowers,
    ),
    spanning(
        "a diamond, where the shared file is read once",
        "app.sigla",
        "schema app { import left\n import right }",
        &[
            (
                "left.sigla",
                "schema left { import base\n predicate L : { b : base.B } }",
            ),
            (
                "right.sigla",
                "schema right { import base\n predicate R : { b : base.B } }",
            ),
            ("base.sigla", "schema base { predicate B : string }"),
        ],
        Verdict::Lowers,
    ),
    // ---- unions (8.6) -------------------------------------------------------------
    entry(
        "a union, with the explicit discriminants I10 requires",
        "schema src { type T = { a : int = 0 | b : string = 1 }\n predicate P : T }",
        Verdict::Lowers,
    ),
    entry(
        "tags out of order, and neither of them a position",
        "schema src { predicate P : { what : { num : int = 3 | text : string = 0 } } }",
        Verdict::Lowers,
    ),
    entry(
        "a single-alternative union, which needs the trailing `|` to be one at all",
        "schema src { predicate P : { only : string = 0 | } }",
        Verdict::Lowers,
    ),
    entry(
        "an alternative with no payload type, which is the empty record",
        "schema src { type Colour = { red = 0 | green = 1 } \n predicate P : Colour }",
        Verdict::Lowers,
    ),
    entry(
        "an alternative whose payload is a reference",
        "schema src { predicate File : string\n predicate Ref : { at : { file : File = 0 | line : int = 1 } } }",
        Verdict::Lowers,
    ),
    entry(
        "an alternative with no discriminant, which I10 will not have",
        "schema src { type T = { a : int | b : string = 1 }\n predicate P : T }",
        Verdict::Diagnosed(Code::RejectMissingDiscriminant),
    ),
    entry(
        "two alternatives sharing a tag — I10's within-a-schema half",
        "schema src { type T = { a : int = 1 | b : string = 1 }\n predicate P : T }",
        Verdict::Diagnosed(Code::RejectDuplicateDiscriminant),
    ),
    entry(
        "two alternatives sharing a name",
        "schema src { type T = { a : int = 0 | a : string = 1 }\n predicate P : T }",
        Verdict::Diagnosed(Code::RejectDuplicateAlternative),
    ),
    // ---- deferred: parses now, refused by name ----------------------------------
    entry(
        "an array — the multiplicity decision, settled as not yet",
        "schema src { predicate P : [string] }",
        Verdict::Diagnosed(Code::NyiArray),
    ),
    entry(
        "a set",
        "schema src { predicate P : set string }",
        Verdict::Diagnosed(Code::NyiSet),
    ),
    entry(
        "maybe, which is sugar over a union",
        "schema src { predicate P : maybe string }",
        Verdict::Diagnosed(Code::NyiMaybe),
    ),
    entry(
        "an enumeration",
        "schema src { type Colour = enum { red | green } }",
        Verdict::Diagnosed(Code::NyiEnum),
    ),
    entry(
        "evolves, which P0 does not have",
        "schema a evolves b",
        Verdict::Diagnosed(Code::NyiEvolves),
    ),
    entry(
        "a stored derivation",
        "schema src { predicate P : string -> string stored }",
        Verdict::Diagnosed(Code::NyiDerivation),
    ),
    entry(
        "a standalone derive",
        "schema src { derive P stored }",
        Verdict::Diagnosed(Code::NyiDerivation),
    ),
    // ---- meaningless, rather than deferred ---------------------------------------
    entry(
        "a discriminant on a record field, where it means nothing",
        "schema src { type R = { a : int = 0, b : string } }",
        Verdict::Diagnosed(Code::RejectDiscriminantOnRecordField),
    ),
    entry(
        "two definitions of one name",
        "schema src { predicate P : string\n predicate P : int }",
        Verdict::Diagnosed(Code::RejectRedeclaration),
    ),
    entry(
        "a named type that expands into itself",
        "schema src { type A = B\n type B = A\n predicate P : A }",
        Verdict::Diagnosed(Code::RejectTypeCycle),
    ),
    entry(
        "a type that names nothing",
        "schema src { predicate P : Nowhere }",
        Verdict::Diagnosed(Code::RejectUnknownName),
    ),
    // ---- not in the language ------------------------------------------------------
    entry(
        "a declaration outside any block",
        "predicate P : string",
        Verdict::SyntaxError,
    ),
    entry(
        "a versioned schema name, which Angle has and this does not",
        "schema src.1 { predicate P : string }",
        Verdict::SyntaxError,
    ),
    entry(
        "an import naming a path rather than a namespace",
        "schema a { import \"lang/rust.sigla\" }",
        Verdict::SyntaxError,
    ),
    entry(
        "a lowercase predicate name",
        "schema src { predicate file : string }",
        Verdict::SyntaxError,
    ),
];

#[cfg(test)]
mod tests {
    use super::{
        super::{lower::lower, parse::parse},
        *,
    };

    /// Every code an entry draws, sorted and deduplicated.
    ///
    /// A **spanning** entry goes through resolution, which reports by rendering rather
    /// than into a sink — so its codes are read back out of the rendered block, where
    /// `codespan` writes them as `error[reject/…]`. That is the form a person sees, and
    /// reading the code out of it is what keeps this gate asserting on the taxonomy
    /// rather than on wording.
    fn codes(entry: &Entry) -> Vec<String> {
        let mut codes: Vec<String> = if entry.imports.is_empty() {
            let mut diags = vec![];
            if let Some(cst) = parse(entry.source, &mut diags) {
                // The schema itself is not the question here — the diagnostics are.
                let _ = lower(&cst, &mut diags);
            }
            diags.into_iter().filter_map(|d| d.code).collect()
        } else {
            match crate::syntax::resolve::resolve_from(entry.sources()) {
                Ok(_) => vec![],
                Err(rendered) => {
                    let found: Vec<String> = rendered
                        .lines()
                        .filter_map(|line| {
                            let open = line.find("error[")? + "error[".len();
                            let close = line[open..].find(']')? + open;
                            Some(line[open..close].to_owned())
                        })
                        .collect();

                    // **A refusal that carries no code has to fail loudly.** Resolution
                    // reports some faults as plain strings — an import nothing answers,
                    // a source that cannot be read — and returning an empty set for
                    // those would read as "lowered cleanly", which is how an entry
                    // whose fixture is simply wrong passes without asserting anything.
                    if found.is_empty() {
                        vec![format!("<no code in: {}>", rendered.trim())]
                    } else {
                        found
                    }
                }
            }
        };

        codes.sort();
        codes.dedup();
        codes
    }

    /// **The gate**: every entry comes to exactly what it says it does.
    #[test]
    fn every_entry_is_classified_as_the_table_says() {
        for entry in CORPUS {
            let Entry {
                about,
                source,
                verdict,
                ..
            } = entry;

            match verdict {
                Verdict::Lowers => assert!(
                    codes(entry).is_empty(),
                    "`{about}` should lower cleanly, and drew {:?}:\n  {source}",
                    codes(entry)
                ),
                Verdict::Diagnosed(code) => assert_eq!(
                    codes(entry),
                    vec![code.as_str().to_owned()],
                    "`{about}` should draw exactly `{}`:\n  {source}",
                    code.as_str()
                ),
                // Its own gate below: a source that does not parse has no lowering to
                // ask about, and the two claims fail for different reasons.
                Verdict::SyntaxError => {}
            }
        }
    }

    /// The parse half, kept separate: "it parses" is the claim that lets a deferred
    /// construct be reported by name instead of by caret, and it is worth failing on
    /// its own rather than inside a diagnostic mismatch.
    #[test]
    fn every_entry_parses_as_classified() {
        for Entry {
            about,
            source,
            verdict,
            ..
        } in CORPUS
        {
            let mut diags = vec![];
            let tree = parse(source, &mut diags);
            let parsed = tree.is_some() && diags.is_empty();

            match verdict {
                Verdict::Lowers | Verdict::Diagnosed(_) => assert!(
                    parsed,
                    "`{about}` should parse, and did not:\n  {source}\n  {diags:?}"
                ),
                Verdict::SyntaxError => {
                    assert!(!parsed, "`{about}` should not parse, and did:\n  {source}")
                }
            }
        }
    }

    /// **Every code has a worked example.** A code with no entry is a refusal nobody
    /// has written down the shape of, which is how a diagnostic comes to name a
    /// construct that cannot actually be written.
    #[test]
    fn every_code_is_reachable_from_the_corpus() {
        for code in Code::ALL {
            assert!(
                CORPUS
                    .iter()
                    .any(|entry| entry.verdict == Verdict::Diagnosed(*code)),
                "no corpus entry expects `{}`",
                code.as_str()
            );
        }
    }

    /// The table is not all one answer — a gate over a corpus of a single verdict
    /// would pass for a parser that did nothing else.
    #[test]
    fn the_corpus_covers_every_verdict() {
        let has = |f: fn(&Verdict) -> bool| CORPUS.iter().any(|e| f(&e.verdict));

        assert!(has(|v| matches!(v, Verdict::Lowers)));
        assert!(has(|v| matches!(v, Verdict::Diagnosed(_))));
        assert!(has(|v| matches!(v, Verdict::SyntaxError)));
    }
}
