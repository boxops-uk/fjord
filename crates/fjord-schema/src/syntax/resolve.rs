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

/// The `import` that asked for a source, so a namespace mismatch is reported where it
/// was written rather than where it is felt.
struct Asked {
    import: String,
    by_name: String,
    by_text: String,
    span: crate::syntax::parser::Span,
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

    /// The entry: the first source in the list, keyed the way [`find`] keys every
    /// other one.
    ///
    /// [`find`]: SchemaSources::find
    ///
    /// # Errors
    ///
    /// When the list is empty, because there is then nothing to resolve.
    pub fn entry(&self) -> Result<Source, String> {
        let (name, text) = self
            .sources
            .first()
            .copied()
            .ok_or_else(|| "no schema sources at all".to_owned())?;

        Ok(Source {
            name: name.to_owned(),
            text: text.to_owned(),
            identity: name.to_owned(),
        })
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
        self.sources
            .iter()
            .map(|(name, _)| *name)
            .collect::<Vec<_>>()
            .join(", ")
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

/// Resolve `entry` — a source and its identity — following its imports through
/// `sources`.
///
/// # Errors
///
/// A rendered reason: a source that cannot be read, an import nothing resolves, a
/// syntax error in any source, or anything lowering refuses about the union — a
/// redeclaration most of all.
fn resolve_with(entry: Source, sources: &impl SchemaSources) -> Result<Resolved, String> {
    let mut files: Vec<String> = vec![];
    let mut texts: Vec<String> = vec![];
    let mut seen: BTreeSet<String> = BTreeSet::new();

    // The frontier, as (source, who asked for it). The second half is the whole of a
    // useful "unresolved import" message: a namespace with no source is only ever a
    // problem in the source that named it — and it is what lets a namespace mismatch
    // point at the `import` rather than at every use site downstream.
    let mut pending: Vec<(Source, Option<Asked>)> = vec![(entry, None)];

    while let Some((source, asked_by)) = pending.pop() {
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

        // **The file found is the file meant.** A source is located from the import
        // name alone — nothing above looks at the `schema <name>` head — so without
        // this the mismatch surfaces as `reject/unknown-name` at every reference into
        // the namespace and as nothing at the import that caused it.
        if let Some(asked) = &asked_by {
            let declared = lower::namespaces(&cst);

            if !declared.contains(&asked.import) {
                let says = if declared.is_empty() {
                    "no namespace at all".to_owned()
                } else {
                    format!(
                        "`{}`",
                        declared
                            .iter()
                            .map(String::as_str)
                            .collect::<Vec<_>>()
                            .join("`, `")
                    )
                };

                return Err(diag::render(
                    &asked.by_name,
                    &asked.by_text,
                    &[diag::Code::RejectNamespaceMismatch.at(
                        asked.span.clone(),
                        format!(
                            "`{name}` declares no namespace `{}` — it declares {says}",
                            asked.import
                        ),
                    )],
                ));
            }
        }

        for (namespace, span) in lower::imports(&cst) {
            let found = sources.find(&namespace)?.ok_or_else(|| {
                format!(
                    "{name}: nothing on the schema path declares `{namespace}` — looked for \
                     `{}` in {}",
                    relative_name(&namespace),
                    sources.searched()
                )
            })?;

            pending.push((
                found,
                Some(Asked {
                    import: namespace,
                    by_name: name.clone(),
                    by_text: text.clone(),
                    span,
                }),
            ));
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

    resolve_with(
        Source {
            name: entry.display().to_string(),
            text,
            identity: identity(entry),
        },
        &FsSources { search },
    )
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
            identity: identity(&path),
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

/// A path as a dedup key: **`canonicalize`, not the path as written.** Two roots may
/// spell one file two ways, and a diamond reaches it twice.
///
/// **Every path put in `seen` goes through here, the entry's included.** Key the entry
/// by the spelling the caller used and the set holds two key spaces: an entry reached
/// again through an import — a cycle — is read a second time, and every declaration in
/// it becomes a redeclaration of itself. A relative entry path and a symlinked root are
/// both enough; an absolute path under an already-canonical directory is not, which is
/// why no test that builds one can see it.
#[cfg(feature = "fs")]
fn identity(path: &Path) -> String {
    std::fs::canonicalize(path)
        .unwrap_or_else(|_| path.to_path_buf())
        .display()
        .to_string()
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

    /// `a` imports `b` imports `a`, written into `dir` — the shape that reads the entry
    /// twice when the entry's dedup key is not the key `find` hands back.
    fn cycle_in(dir: &Path) {
        std::fs::write(
            dir.join("a.sigla"),
            "schema a { import b\n predicate A : { b : b.B } }",
        )
        .expect("it writes");
        std::fs::write(
            dir.join("b.sigla"),
            "schema b { import a\n predicate B : string }",
        )
        .expect("it writes");
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
                // `other.sigla` declares the namespace it is fetched as **and** a
                // second block in `app`; without the first this is a namespace
                // mismatch rather than the redeclaration this case is about.
                (
                    "other.sigla",
                    "schema other { predicate Q : int }\nschema app { predicate P : int }",
                ),
            ],
        ),
        (
            "a file whose namespace is not the one the import named",
            &[
                (
                    "main.sigla",
                    "schema app { import ob\n predicate P : { t : ob.Thing } }",
                ),
                ("ob.sigla", "schema base { predicate Thing : string }"),
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

    /// **The entry's dedup key has to be the key every other source's is.** `find`
    /// canonicalises, so a frontier seeded with the path as the caller spelled it holds
    /// two key spaces in one set: the entry reached again through an import — a cycle —
    /// is read a second time, and every declaration in it becomes a redeclaration of
    /// itself.
    ///
    /// A relative path is the spelling a person types. Every other case here builds an
    /// absolute path under a Linux tempdir, which is already canonical, so none of them
    /// can see this.
    #[test]
    fn a_relative_entry_path_is_deduped_like_every_other_source() {
        // Inside the working directory, so the relative spelling is well defined
        // without this test changing a directory the rest of the suite shares.
        let here = std::env::current_dir().expect("a working directory");
        let dir = tempfile::TempDir::new_in(&here).expect("a scratch directory");
        cycle_in(dir.path());

        let entry = Path::new(dir.path().file_name().expect("a name")).join("a.sigla");
        let resolved = resolve(&entry, &[]).expect("a cycle through a relative entry");

        assert_eq!(resolved.files.len(), 2, "{:?}", resolved.files);
        assert_eq!(names(&resolved.schema), ["a.A", "b.B"]);
    }

    /// The same claim through the other spelling that is not canonical: an absolute
    /// path whose directory is a symlink. `canonicalize` follows it and the entry's own
    /// spelling does not.
    #[test]
    fn an_entry_reached_through_a_symlink_is_deduped_like_every_other_source() {
        let dir = tempfile::tempdir().expect("a scratch directory");
        std::fs::create_dir(dir.path().join("real")).expect("a directory");
        cycle_in(&dir.path().join("real"));
        std::os::unix::fs::symlink(dir.path().join("real"), dir.path().join("link"))
            .expect("a symlink");

        let entry = dir.path().join("link").join("a.sigla");
        let resolved = resolve(&entry, &[]).expect("a cycle through a symlinked directory");

        assert_eq!(resolved.files.len(), 2, "{:?}", resolved.files);
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
        // `other.sigla` declares the namespace it is fetched as **and** a second block
        // in `app` — a file holds several blocks, and without the first one this is a
        // namespace mismatch rather than a redeclaration.
        let (_dir, resolved) = resolving(&[
            (
                "main.sigla",
                "schema app { import other\n predicate P : string }",
            ),
            (
                "other.sigla",
                "schema other { predicate Q : int }\nschema app { predicate P : int }",
            ),
        ]);

        let Err(failed) = resolved else {
            panic!("one name, two definitions");
        };
        assert!(failed.contains("app.P"), "{failed}");
    }

    /// **Ids are assigned by sorted fully-qualified name, with `fjord.*` last** — not
    /// by file position, and not by declaration order.
    ///
    /// A `sort_by` in `lower` with no test naming it, and every schema file added from
    /// here on relies on it: adding a predicate whose name sorts early does not *append*
    /// an id, it **inserts** one and renumbers everything above. Existing databases keep
    /// the map they embedded (I13), the wire carries names, and the one place it is not
    /// free is a `FactId`'s tag — which is the database's numbering, so a consumer
    /// decoding a returned reference against a hardcoded table reads the wrong
    /// predicate.
    #[test]
    fn predicate_ids_are_assigned_by_sorted_qualified_name() {
        // The *file* order and the sorted order disagree at every position, and the
        // reserved namespace is written first — in the entry, which is read first — so
        // that "last" is a claim rather than an accident of where it was put.
        let (_dir, resolved) = resolving(&[
            (
                "main.sigla",
                "schema fjord.db { predicate List : string }\n\
                 schema zeta { import alpha\n import mid\n predicate Z : string }",
            ),
            ("alpha.sigla", "schema alpha { predicate A : string }"),
            ("mid.sigla", "schema mid { predicate M : string }"),
        ]);

        let resolved = resolved.expect("it resolves");

        assert_eq!(
            names(&resolved.schema),
            ["alpha.A", "mid.M", "zeta.Z", "fjord.db.List"],
            "ids follow the sorted qualified name, not the order the files were read"
        );

        // The reserved half of the rule, which the sort alone would put between
        // `alpha.A` and `mid.M`: a server adding a predicate it answers itself must
        // move no id a database has already written into its keyspaces.
        assert_eq!(
            names(&resolved.schema).last().map(String::as_str),
            Some("fjord.db.List")
        );

        // And the entry file's own predicate is *not* first, which is the reading the
        // sorted rule is most often confused with.
        assert_ne!(
            names(&resolved.schema).first().map(String::as_str),
            Some("zeta.Z")
        );
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

    /// **The first root on the schema path wins**, and the second half is what says the
    /// order is read at all: the same two roots, reversed, resolve the other file.
    #[test]
    fn the_first_root_that_declares_a_namespace_is_the_one_read() {
        let dir = tempfile::tempdir().expect("a scratch directory");
        let root = |name: &str, predicate: &str| {
            let root = dir.path().join(name);
            std::fs::create_dir_all(&root).expect("a directory");
            std::fs::write(
                root.join("base.sigla"),
                format!("schema base {{ predicate {predicate} : string }}"),
            )
            .expect("it writes");
            root
        };

        let (one, two) = (root("one", "One"), root("two", "Two"));

        // The entry sits in a directory of its own, so what is under test is the rule
        // about roots rather than the one about the entry's own directory.
        let away = dir.path().join("away");
        std::fs::create_dir_all(&away).expect("a directory");
        let entry = away.join("main.sigla");
        std::fs::write(&entry, "schema app { import base }").expect("it writes");

        let first = resolve(&entry, &[one.clone(), two.clone()]).expect("it resolves");
        assert_eq!(names(&first.schema), ["base.One"]);

        let reversed = resolve(&entry, &[two, one]).expect("it resolves");
        assert_eq!(names(&reversed.schema), ["base.Two"]);
    }

    /// **The entry file's own directory is searched before the roots**, which is what
    /// makes a self-contained directory of schemas need nothing configured.
    #[test]
    fn the_entry_directory_is_searched_before_the_roots() {
        let dir = tempfile::tempdir().expect("a scratch directory");
        let elsewhere = dir.path().join("elsewhere");
        std::fs::create_dir_all(&elsewhere).expect("a directory");
        std::fs::write(
            elsewhere.join("base.sigla"),
            "schema base { predicate Elsewhere : string }",
        )
        .expect("it writes");

        let home = dir.path().join("home");
        std::fs::create_dir_all(&home).expect("a directory");
        std::fs::write(
            home.join("base.sigla"),
            "schema base { predicate Home : string }",
        )
        .expect("it writes");
        let entry = home.join("main.sigla");
        std::fs::write(&entry, "schema app { import base }").expect("it writes");

        let resolved = resolve(&entry, &[elsewhere]).expect("it resolves");
        assert_eq!(names(&resolved.schema), ["base.Home"]);
    }

    /// First match wins in an embedded set too, and by the list's own order: two
    /// sources of one name are one source, and it is the first.
    #[test]
    fn the_first_source_of_a_name_is_the_one_read() {
        const ENTRY: (&str, &str) = ("main.sigla", "schema app { import base }");
        const ONE: (&str, &str) = ("base.sigla", "schema base { predicate One : string }");
        const TWO: (&str, &str) = ("base.sigla", "schema base { predicate Two : string }");

        let first = resolve_from([ENTRY, ONE, TWO]).expect("it resolves");
        assert_eq!(names(&first.schema), ["base.One"]);

        let reversed = resolve_from([ENTRY, TWO, ONE]).expect("it resolves");
        assert_eq!(names(&reversed.schema), ["base.Two"]);
    }

    /// An empty set has no entry, and says so rather than resolving to an empty schema.
    #[test]
    fn an_empty_set_of_sources_is_refused() {
        let empty: [(&str, &str); 0] = [];

        let Err(failed) = resolve_from(empty) else {
            panic!("there is nothing to resolve");
        };
        assert!(failed.contains("no schema sources"), "{failed}");
    }
}
