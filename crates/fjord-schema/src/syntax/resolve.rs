//! **Imports** — an entry file, everything it names, and the union they make.
//!
//! [Operations §7](https://github.com/boxops-uk/fjord/blob/main/website/content/operations.md) settles the rules and this
//! is the transcription:
//!
//! - **An import names a namespace, never a path** (`import lang.rust`). How a namespace
//!   is found is a resolver's business, and this resolver's answer is the obvious one:
//!   `lang.rust` is `lang/rust.sigla` under a root.
//! - **Roots are searched in order, first match wins.** The entry file's own directory
//!   is searched first, so a self-contained directory of schemas needs nothing
//!   configured; `schema_path` supplies the rest.
//! - **Imports are edges with concatenation semantics** — take the transitive closure,
//!   dedup by file identity, union the blocks. A namespace is open across files, so the
//!   union is simply the text put end to end.
//! - **Cycles are harmless by construction.** Dedup by identity means a file already
//!   read is not read again, so `a` importing `b` importing `a` terminates with two
//!   files. Diamonds dedup for free, and there is nothing to detect and nothing to
//!   refuse.
//! - **The real error is genuine redeclaration**: two *different* definitions of one
//!   fully-qualified name, as against the same file reached twice. Lowering the union
//!   already reports that by name — the dedup above is what makes it mean what it says.
//! - **Transitive visibility is accepted rather than fought.** An import is not an
//!   encapsulation boundary: what `a` imports, anything importing `a` can see. Angle
//!   works this way too, and documenting it is cheaper than a scoping rule nobody asked
//!   for.

//! # Two entry points, one algorithm
//!
//! Everything above is about *what* resolution does, and none of it is about where the
//! text came from. [`resolve`] reads files; [`resolve_from`] takes a list of
//! `(name, text)` pairs. Both run one walk over a private provider trait, so two
//! algorithms cannot drift, and `resolving_from_memory_matches_resolving_from_disk` is
//! what says they have not.
//!
//! **The in-memory form is for holding sigla *text* without a filesystem, which is a
//! narrower need than it sounds.** A client does not resolve at all — a database
//! embeds its schema when it is created and serves it already resolved, so a peer
//! receives a [`crate::schema::Schema`] rather than source. What needs this
//! are the three places that hold source and have nowhere to put it: the diagnostic
//! corpus, whose multi-file fixtures would otherwise need a temp directory per case;
//! `include_str!`-embedded schemas like the CLI's sample; and a browser editing sigla,
//! which is authoring rather than consuming. The provider trait itself is private,
//! because nothing outside this module has ever needed a third provider.
//!
//! The filesystem provider is behind the default-on `fs` feature, which is what makes
//! "the embedded path touches no filesystem" mechanical rather than a promise: with
//! `--no-default-features` it is not compiled, so a call to it from the embedded path
//! is a compile error.

use std::collections::BTreeSet;
#[cfg(feature = "fs")]
use std::path::{Path, PathBuf};

use crate::{
    schema::Schema,
    syntax::{diag, lower, parse},
};

/// The extension a namespace's file has.
pub const EXTENSION: &str = "sigla";

/// An entry file, everything it imports, and what they come to together.
pub struct Resolved {
    /// Every source that went into it, in the order they were read — the entry first,
    /// named the way its provider names it: a path from the filesystem, an import name
    /// from an embedded set.
    ///
    /// What `schema check` prints, and what says *where* a schema came from when two
    /// roots hold a namespace of the same name.
    pub files: Vec<String>,
    /// Their union, as one source — what was lowered.
    ///
    /// **Not what a database embeds**: that is [`print`](super::print::print) of the
    /// schema below, which is the same declarations with the comments, the file
    /// boundaries and the writing order taken out. This is what a diagnostic points
    /// into, and what a person is shown when they ask what resolution came to.
    pub source: String,
    pub schema: Schema,
}

/// One source a resolver read.
struct Source {
    /// What a diagnostic names — a path from the filesystem, an import name from an
    /// embedded set.
    name: String,
    text: String,
    /// **The dedup key, and it differs by provider.** `canonicalize` for the
    /// filesystem, because two roots may spell one file two ways and a diamond reaches
    /// it twice; the import name for an embedded set, which has no paths to
    /// canonicalise. Reading one source twice would turn every declaration in it into a
    /// redeclaration of itself.
    identity: String,
}

/// Where a resolver's sources come from.
trait SchemaSources {
    /// The source for an import name, or `None` if this provider has none.
    ///
    /// **The search order is part of the contract**, because it decides which of two
    /// sources claiming one import name is used: the filesystem searches the entry
    /// file's own directory first and then the roots, and an embedded set searches its
    /// list in order. First match wins in both.
    ///
    /// # Errors
    ///
    /// A rendered reason when the source was located and could not be read.
    fn find(&self, import: &str) -> Result<Option<Source>, String>;

    /// Where this provider looked, for the message when nothing declares an import.
    fn searched(&self) -> String;
}

/// An ordered list of `(name, text)` sources, the entry first — the shape an embedder
/// has after a handful of `include_str!`s.
///
/// The dedup identity is the **name**, so two entries with one name are one source.
struct MemorySources<'a> {
    sources: Vec<(&'a str, &'a str)>,
}

impl<'a> MemorySources<'a> {
    pub fn new(sources: impl IntoIterator<Item = (&'a str, &'a str)>) -> Self {
        Self {
            sources: sources.into_iter().collect(),
        }
    }

    /// The entry: the first source in the list.
    ///
    /// # Errors
    ///
    /// When the list is empty, because there is then nothing to resolve.
    pub fn entry(&self) -> Result<(&'a str, &'a str), String> {
        self.sources
            .first()
            .copied()
            .ok_or_else(|| "no schema sources at all".to_owned())
    }
}

impl SchemaSources for MemorySources<'_> {
    fn find(&self, import: &str) -> Result<Option<Source>, String> {
        // **By the namespace the source declares its own name to be**, matched against
        // the import text — the same mapping the filesystem makes, minus the directory
        // walk: `lang.rust` is the source named `lang.rust` or `lang/rust.sigla`.
        let wanted = relative_name(import);

        Ok(self
            .sources
            .iter()
            .find(|(name, _)| *name == import || *name == wanted)
            .map(|(name, text)| Source {
                name: (*name).to_owned(),
                text: (*text).to_owned(),
                identity: (*name).to_owned(),
            }))
    }

    fn searched(&self) -> String {
        if self.sources.is_empty() {
            "no sources at all".to_owned()
        } else {
            self.sources
                .iter()
                .map(|(name, _)| *name)
                .collect::<Vec<_>>()
                .join(", ")
        }
    }
}

/// `lang.rust` → `lang/rust.sigla`, as a name rather than a path.
fn relative_name(namespace: &str) -> String {
    format!("{}.{EXTENSION}", namespace.replace('.', "/"))
}

/// Resolve an in-memory set of sources, the entry first.
///
/// The form for source held in memory rather than on disk — an `include_str!`ed
/// schema, a corpus fixture that spans files, a browser editing sigla. Same algorithm
/// as [`resolve`].
///
/// # Errors
///
/// A rendered diagnostic for anything resolution refuses, and for an empty list.
pub fn resolve_from<'a>(
    sources: impl IntoIterator<Item = (&'a str, &'a str)>,
) -> Result<Resolved, String> {
    let sources = MemorySources::new(sources);
    let entry = sources.entry()?;
    resolve_with(entry, &sources)
}

/// Resolve `entry` — a name and its text — following its imports through `sources`.
///
/// # Errors
///
/// A rendered reason: a source that cannot be read, an import nothing resolves, a
/// syntax error in any source, or anything lowering refuses about the union — a
/// redeclaration most of all.
fn resolve_with(entry: (&str, &str), sources: &impl SchemaSources) -> Result<Resolved, String> {
    let mut files: Vec<String> = vec![];
    let mut texts: Vec<String> = vec![];
    let mut seen: BTreeSet<String> = BTreeSet::new();

    // The frontier, as (source, who asked for it). The second half is the whole of a
    // useful "unresolved import" message: a namespace with no source is only ever a
    // problem in the source that named it.
    let mut pending: Vec<(Source, Option<String>)> = vec![(
        Source {
            name: entry.0.to_owned(),
            text: entry.1.to_owned(),
            identity: entry.0.to_owned(),
        },
        None,
    )];

    while let Some((source, _asked_by)) = pending.pop() {
        // **Dedup by identity, not by name** — see [`Source::identity`].
        if !seen.insert(source.identity.clone()) {
            continue;
        }

        let Source { name, text, .. } = source;
        let mut diags = vec![];

        let Some(cst) = parse::parse(&text, &mut diags) else {
            return Err(diag::render(&name, &text, &diags));
        };
        if !diags.is_empty() {
            return Err(diag::render(&name, &text, &diags));
        }

        for namespace in lower::imports(&cst) {
            let found = sources.find(&namespace)?.ok_or_else(|| {
                format!(
                    "{name}: nothing on the schema path declares `{namespace}` — looked for \
                     `{}` in {}",
                    relative_name(&namespace),
                    sources.searched()
                )
            })?;

            pending.push((found, Some(name.clone())));
        }

        texts.push(text);
        files.push(name);
    }

    union_and_lower(files, texts)
}

/// The union of every source read, lowered as one schema.
fn union_and_lower(files: Vec<String>, sources: Vec<String>) -> Result<Resolved, String> {
    // **One source is itself; several are a union**, and the difference is what a
    // diagnostic can honestly point at. A schema with no imports is lowered under its
    // own name with its own line numbers — the common case, and the one where a caret
    // is worth most. Several have to be lowered together (that is what makes a
    // cross-file reference resolve and a cross-file redeclaration an error), so they
    // get a header apiece and a name that says the union is what was read.
    let (name, source) = if files.len() == 1 {
        (files[0].clone(), sources.concat())
    } else {
        let name = format!(
            "<resolved schema: {} and {} more>",
            files[0],
            files.len() - 1
        );

        let text = files
            .iter()
            .zip(&sources)
            .map(|(path, source)| format!("# ---- {path}\n{source}\n"))
            .collect::<String>();

        (name, text)
    };

    let schema = super::read(&name, &source)?;

    Ok(Resolved {
        files,
        source,
        schema,
    })
}

/// Resolve `entry` against `roots`.
///
/// # Errors
///
/// A rendered reason: a file that cannot be read, an import nothing resolves, a syntax
/// error in any file, or anything lowering refuses about the union — a redeclaration
/// most of all.
#[cfg(feature = "fs")]
pub fn resolve(entry: &Path, roots: &[PathBuf]) -> Result<Resolved, String> {
    // The entry file's own directory first, then the configured roots. A schema that
    // sits beside the ones it imports is the common case and should need no setup.
    let mut search: Vec<PathBuf> = entry
        .parent()
        .filter(|parent| !parent.as_os_str().is_empty())
        .map(Path::to_path_buf)
        .into_iter()
        .collect();
    search.extend(roots.iter().cloned());

    let text = std::fs::read_to_string(entry)
        .map_err(|source| format!("{}: {source}", entry.display()))?;

    resolve_with((&entry.display().to_string(), &text), &FsSources { search })
}

/// The filesystem provider: an import name is a path under one of the roots.
#[cfg(feature = "fs")]
struct FsSources {
    /// Searched in order, first match wins.
    pub search: Vec<PathBuf>,
}

#[cfg(feature = "fs")]
impl SchemaSources for FsSources {
    fn find(&self, import: &str) -> Result<Option<Source>, String> {
        let Some(path) = find(import, &self.search) else {
            return Ok(None);
        };

        let text = std::fs::read_to_string(&path).map_err(|source| {
            format!("cannot read `{import}` from {}: {source}", path.display())
        })?;

        Ok(Some(Source {
            name: path.display().to_string(),
            // **`canonicalize`, not the path as written.** Two roots may spell one file
            // two ways, and a diamond reaches it twice.
            identity: std::fs::canonicalize(&path)
                .unwrap_or_else(|_| path.clone())
                .display()
                .to_string(),
            text,
        }))
    }

    fn searched(&self) -> String {
        if self.search.is_empty() {
            "no roots at all (set `schema_path`)".to_owned()
        } else {
            self.search
                .iter()
                .map(|root| root.display().to_string())
                .collect::<Vec<_>>()
                .join(", ")
        }
    }
}

/// `lang.rust` → `lang/rust.sigla`.
#[cfg(feature = "fs")]
fn relative(namespace: &str) -> PathBuf {
    let mut path = PathBuf::new();
    for segment in namespace.split('.') {
        path.push(segment);
    }
    path.set_extension(EXTENSION);
    path
}

/// The first root holding `namespace`'s file.
#[cfg(feature = "fs")]
fn find(namespace: &str, roots: &[PathBuf]) -> Option<PathBuf> {
    let relative = relative(namespace);

    roots
        .iter()
        .map(|root| root.join(&relative))
        .find(|candidate| candidate.is_file())
}

#[cfg(test)]
mod tests {
    use super::*;

    /// Write `files` into a scratch directory and resolve the first of them.
    fn resolving(files: &[(&str, &str)]) -> (tempfile::TempDir, Result<Resolved, String>) {
        let dir = tempfile::tempdir().expect("a scratch directory");

        for (name, source) in files {
            let path = dir.path().join(name);
            if let Some(parent) = path.parent() {
                std::fs::create_dir_all(parent).expect("a directory");
            }
            std::fs::write(path, source).expect("it writes");
        }

        let entry = dir.path().join(files[0].0);
        let resolved = resolve(&entry, &[]);

        (dir, resolved)
    }

    fn names(schema: &Schema) -> Vec<String> {
        (0..schema.len())
            .filter_map(|index| {
                schema
                    .get(crate::schema::PredicateId(index as u32))?
                    .name()
                    .map(str::to_owned)
            })
            .collect()
    }

    /// The multi-file cases below, as data, so the differential can run every one of
    /// them rather than a hand-picked subset.
    const CASES: &[(&str, &[(&str, &str)])] = &[
        (
            "an import brings in what it names",
            &[
                (
                    "main.sigla",
                    "schema app { import src\n predicate Use : { of : src.File } }",
                ),
                ("src.sigla", "schema src { predicate File : string }"),
            ],
        ),
        (
            "a dotted namespace",
            &[
                ("main.sigla", "schema app { import lang.rust }"),
                (
                    "lang/rust.sigla",
                    "schema lang.rust { predicate Crate : string }",
                ),
            ],
        ),
        (
            "a cycle",
            &[
                (
                    "a.sigla",
                    "schema a { import b\n predicate A : { b : b.B } }",
                ),
                ("b.sigla", "schema b { import a\n predicate B : string }"),
            ],
        ),
        (
            "a diamond",
            &[
                ("main.sigla", "schema app { import left\n import right }"),
                (
                    "left.sigla",
                    "schema left { import base\n predicate L : string }",
                ),
                (
                    "right.sigla",
                    "schema right { import base\n predicate R : string }",
                ),
                ("base.sigla", "schema base { predicate B : string }"),
            ],
        ),
        (
            "a name declared twice, in two files",
            &[
                (
                    "main.sigla",
                    "schema app { import other\n predicate P : int }",
                ),
                ("other.sigla", "schema app { predicate P : int }"),
            ],
        ),
        (
            "an import nothing answers",
            &[("main.sigla", "schema app { import nosuch }")],
        ),
        (
            "a syntax error in an imported file",
            &[
                ("main.sigla", "schema app { import broken }"),
                ("broken.sigla", "schema broken { predicate }"),
            ],
        ),
        (
            "a type moved into an imported file — fingerprint-identical to a local one",
            &[
                (
                    "b.sigla",
                    "schema b { import base\n predicate P : { flag : base.Bool } }",
                ),
                (
                    "base.sigla",
                    "schema base { type Bool = { no : {} = 0 | yes : {} = 1 } }",
                ),
            ],
        ),
    ];

    /// **One algorithm, and this is the evidence.** For every multi-file case, resolve
    /// it twice — once through `FsSources` over a real directory, once through
    /// `resolve_from` over the same text in memory — and assert the two agree on every
    /// predicate id, every name, every per-predicate fingerprint, the schema
    /// fingerprint, and the diagnostics, in order.
    ///
    /// A shared implementation is the means; this is what says the two have not
    /// drifted. What it deliberately does *not* compare is `Resolved::files`, which is
    /// each provider's own naming — a path from one, an import name from the other.
    #[test]
    fn resolving_from_memory_matches_resolving_from_disk() {
        use crate::fingerprint;

        // A differential where every case took one branch would compare nothing on the
        // other, and the identity comparison is the half that matters most.
        let (mut resolved, mut refused) = (0, 0);

        for (what, files) in CASES {
            let (_dir, from_disk) = resolving(files);
            let from_memory = resolve_from(files.iter().copied());

            match (from_disk, from_memory) {
                (Ok(disk), Ok(memory)) => {
                    resolved += 1;
                    assert_eq!(
                        names(&disk.schema),
                        names(&memory.schema),
                        "{what}: different predicates"
                    );
                    assert_eq!(
                        disk.schema.len(),
                        memory.schema.len(),
                        "{what}: different predicate counts"
                    );

                    let identity = |schema: &Schema| {
                        let identity = fingerprint::identity(schema);
                        (identity.schema(), identity.predicates().clone())
                    };

                    assert_eq!(
                        identity(&disk.schema),
                        identity(&memory.schema),
                        "{what}: the same declarations resolved to different identities"
                    );
                }

                // **Diagnostics too, in the same order**, because a message that
                // reads one way from a file and another from memory is a message an
                // embedder cannot act on.
                (Err(disk), Err(memory)) => {
                    refused += 1;
                    // Two clauses are the provider's own by design and are removed
                    // rather than the comparison being loosened: how it *names* a
                    // source (an absolute temp path against the name the embedder
                    // gave), and `searched()`'s account of where it looked (a
                    // directory against a list of names). Everything else must match
                    // character for character.
                    let reason = |rendered: &str| {
                        rendered
                            .replace(&format!("{}/", _dir.path().display()), "")
                            .lines()
                            .map(|line| match line.split_once(" in ") {
                                Some((before, _)) => before.to_owned(),
                                None => line.to_owned(),
                            })
                            .collect::<Vec<_>>()
                    };

                    assert_eq!(
                        reason(&disk),
                        reason(&memory),
                        "{what}: refused for different reasons"
                    );
                }

                (disk, memory) => panic!(
                    "{what}: one path resolved and the other did not\n  disk:   {disk:?}\n  memory: {memory:?}",
                    disk = disk.map(|r| r.files),
                    memory = memory.map(|r| r.files),
                ),
            }
        }

        assert!(resolved >= 4, "only {resolved} cases resolved");
        assert!(refused >= 3, "only {refused} cases were refused");
    }

    /// An import is an edge, and the union is what lowers — including a reference that
    /// crosses the file boundary, which is the reason resolution exists at all.
    #[test]
    fn an_import_brings_in_what_it_names() {
        let (_dir, resolved) = resolving(&[
            (
                "main.sigla",
                "schema app { import src\n predicate Use : { of : src.File } }",
            ),
            ("src.sigla", "schema src { predicate File : string }"),
        ]);

        let resolved = resolved.expect("it resolves");
        assert_eq!(resolved.files.len(), 2);
        assert_eq!(names(&resolved.schema), ["app.Use", "src.File"]);
    }

    /// A namespace of several segments is a path of several segments.
    #[test]
    fn a_dotted_namespace_is_a_directory() {
        let (_dir, resolved) = resolving(&[
            ("main.sigla", "schema app { import lang.rust }"),
            (
                "lang/rust.sigla",
                "schema lang.rust { predicate Crate : string }",
            ),
        ]);

        assert_eq!(
            names(&resolved.expect("it resolves").schema),
            ["lang.rust.Crate"]
        );
    }

    /// **A cycle is harmless**, because dedup is by file identity: `a` imports `b`
    /// imports `a` terminates with two files and no complaint. There is no cycle check
    /// here, and this test is what says one is not needed.
    #[test]
    fn a_cycle_of_imports_terminates() {
        let (_dir, resolved) = resolving(&[
            (
                "a.sigla",
                "schema a { import b\n predicate A : { b : b.B } }",
            ),
            ("b.sigla", "schema b { import a\n predicate B : string }"),
        ]);

        let resolved = resolved.expect("it resolves");
        assert_eq!(resolved.files.len(), 2);
        assert_eq!(names(&resolved.schema), ["a.A", "b.B"]);
    }

    /// A diamond reads the shared file once. Reading it twice would make every
    /// declaration in it a redeclaration of itself, which is the error this dedup
    /// exists to *not* raise.
    #[test]
    fn a_diamond_reads_the_shared_file_once() {
        let (_dir, resolved) = resolving(&[
            ("main.sigla", "schema app { import left\n import right }"),
            (
                "left.sigla",
                "schema left { import base\n predicate L : string }",
            ),
            (
                "right.sigla",
                "schema right { import base\n predicate R : string }",
            ),
            ("base.sigla", "schema base { predicate B : string }"),
        ]);

        let resolved = resolved.expect("it resolves");
        assert_eq!(resolved.files.len(), 4, "base is read once, not twice");
        assert_eq!(names(&resolved.schema), ["base.B", "left.L", "right.R"]);
    }

    /// **Genuine redeclaration is the real error** — two different definitions of one
    /// fully-qualified name, which no dedup can excuse.
    #[test]
    fn two_definitions_of_one_name_are_refused() {
        let (_dir, resolved) = resolving(&[
            (
                "main.sigla",
                "schema app { import other\n predicate P : string }",
            ),
            ("other.sigla", "schema app { predicate P : int }"),
        ]);

        let Err(failed) = resolved else {
            panic!("one name, two definitions");
        };
        assert!(failed.contains("app.P"), "{failed}");
    }

    /// An import nothing answers says which file asked and where it looked.
    #[test]
    fn an_unresolved_import_says_what_it_looked_for() {
        let (_dir, resolved) = resolving(&[("main.sigla", "schema app { import lang.rust }")]);

        let Err(failed) = resolved else {
            panic!("there is no such namespace");
        };
        assert!(failed.contains("lang.rust"), "{failed}");
        assert!(failed.contains("lang/rust.sigla"), "{failed}");
    }

    /// A syntax error is reported against the **file** it is in, not against the union,
    /// which is the reason each file is parsed on its own first.
    #[test]
    fn a_syntax_error_names_the_file_it_is_in() {
        let (_dir, resolved) = resolving(&[
            ("main.sigla", "schema app { import broken }"),
            ("broken.sigla", "schema broken { predicate }"),
        ]);

        let Err(failed) = resolved else {
            panic!("it does not parse");
        };
        assert!(failed.contains("broken.sigla"), "{failed}");
    }
}
