//! **The shell, and nothing else.**
//!
//! Every export takes a `&str` and returns a `String` of JSON, and there is no
//! logic here — a function that needs a branch belongs in [`fjord_inspect`],
//! where the host suite covers it. What cannot be covered there is exactly what
//! is left here: the `wasm_bindgen` boundary.
//!
//! The boundary is `serde_json` in [`fjord_inspect`] and `JSON.parse` on the
//! other side — the encoder is deliberately *not* here, so the JSON a browser
//! receives is byte for byte the JSON the host suite asserts on. A string
//! because payloads are query-sized and a string is debuggable: a failing view
//! can be pasted into a terminal. `serde-wasm-bindgen` is the upgrade if
//! profiling ever asks for it; pre-empting it buys nothing.
//!
//! What a browser cannot do, stated so it is not filed as a gap: **ingest**,
//! because interning needs a real backend and durable id claims. Everything
//! else runs here, lexing to executing: the queries answer against a `MemStore`
//! holding the demo database, through the same executor the server runs.
//!
//! **Schema `import` does work**, and it used to be listed above. Resolution is
//! one algorithm over a source provider, and the filesystem is only one
//! implementation of it — so a browser embedder hands
//! `fjord_schema::syntax::resolve::resolve_from` an ordered list of
//! `(name, text)` and gets the union, with `--no-default-features` making "no
//! filesystem" a compile error rather than a promise.

use wasm_bindgen::prelude::{JsError, wasm_bindgen};

/// Lex `source` as sigla and answer the [token view](fjord_inspect::Tokens) as
/// JSON.
///
/// Never fails: an unreadable byte is a token plus a diagnostic, so a page
/// gets an answer for every keystroke including the half-typed ones.
#[wasm_bindgen]
#[must_use]
pub fn tokens(source: &str) -> String {
    fjord_inspect::tokens_json(source)
}

/// Parse `source` as sigla and answer the [tree view](fjord_inspect::Tree) as
/// JSON.
///
/// Never fails either: a refusal is a tree with no root and the diagnostics
/// that say why, and a recovered parse carries both a tree and the faults it
/// recovered from — which is what a half-typed query looks like.
#[wasm_bindgen]
#[must_use]
pub fn tree(source: &str) -> String {
    fjord_inspect::tree_json(source)
}

/// Lex `source` as a **schema** and answer the token view as JSON.
///
/// A second lexer, not a second reading of the first: the schema language has
/// comments and namespaces where sigla has neither.
#[wasm_bindgen]
#[must_use]
pub fn schema_tokens(source: &str) -> String {
    fjord_inspect::schema_tokens_json(source)
}

/// Read `source` as a schema and answer the
/// [schema view](fjord_inspect::SchemaView) as JSON.
#[wasm_bindgen]
#[must_use]
pub fn schema(source: &str) -> String {
    fjord_inspect::schema_json(source)
}

/// Read a **set** of schema sources as one schema and answer the
/// [schema view](fjord_inspect::SchemaView) as JSON.
///
/// `sources` is a JSON array of `[name, text]` pairs, the entry first, and its
/// `import`s are followed through the rest. A string in and a string out like
/// every export here, because a browser has no filesystem to keep a set of files
/// in — and resolution needs none.
#[wasm_bindgen]
#[must_use]
pub fn schema_set(sources: &str) -> String {
    fjord_inspect::schema_set_json(sources)
}

/// Compile `query` against `schema` through the whole front end — lex, parse,
/// lower, typecheck, flatten, reorder — and answer the
/// [lowered view](fjord_inspect::Lowered) as JSON.
///
/// Two strings in, because the module holds no state: a browser has no
/// filesystem to keep a schema in, and a handle would be a lifetime to manage
/// across a boundary that cannot express one. Schemas are small and compiling
/// one is microseconds.
#[wasm_bindgen]
#[must_use]
pub fn compile(schema: &str, query: &str) -> String {
    fjord_inspect::lowered_json(schema, query)
}

/// Run `query` against the demo database and answer the rows, with what
/// reading them cost.
#[wasm_bindgen]
#[must_use]
pub fn run(schema: &str, query: &str) -> String {
    fjord_inspect::rows_json(schema, query)
}

/// **Trace `query`** — the whole run, one transition at a time.
///
/// The executor is a state machine whose every loop iteration is one
/// transition, so this is that loop driven a step at a time, with the machine's
/// registers read between steps. The whole run comes back at once: a page folds
/// the changes and scrubs a local array, forwards and backwards, rather than
/// asking again per step.
#[wasm_bindgen]
#[must_use]
pub fn trace(schema: &str, query: &str) -> String {
    fjord_inspect::trace_json(schema, query)
}

/// Walk one candidate through the same Levenshtein automaton a guided fuzzy
/// seek uses, returning its capped edit-distance row after each character.
#[wasm_bindgen]
#[must_use]
pub fn fuzzy(term: &str, candidate: &str, distance: u8, anchored: bool) -> String {
    fjord_inspect::fuzzy_json(term, candidate, distance, anchored)
}

/// **Every stored row of the demo database**, as bytes and as a fact, in the
/// order a scan meets them.
///
/// The bytes are the point: a seek is a byte prefix and a scan is a range over
/// the same order, so a scan's bounds mean nothing against decoded values and
/// everything against these.
#[wasm_bindgen]
#[must_use]
pub fn database(schema: &str) -> String {
    fjord_inspect::database_json(schema)
}

/// Load the page's corpus — the JSONL a `fjord export` writes.
///
/// **Nothing here interns a fact**, because nothing compiled to WebAssembly can: the
/// write funnel reaches the fjall backend by name. It does not need to. A file whose
/// references only ever name earlier lines needs *counting* rather than interning — each
/// line takes the next sequence for its predicate, and a reference resolves through the
/// ids already handed out.
///
/// So the ids differ from the database the file came from, and that costs nothing: a
/// content identity is a multiset hash over each fact's logical form.
///
/// Answers a JSON report rather than throwing: a refused corpus is an ordinary thing for
/// a page to have to show, and the commonest reason is a schema that has moved since the
/// asset was built.
#[wasm_bindgen]
#[must_use]
pub fn load_corpus_jsonl(text: &str, schema: &str) -> String {
    fjord_inspect::corpus::load_jsonl_json(text, schema)
}

/// What is loaded, without loading anything.
#[wasm_bindgen]
#[must_use]
pub fn corpus_status() -> String {
    fjord_inspect::corpus::loaded_json()
}

/// Run `query` against the loaded corpus, and answer the rows with what reading
/// them cost — [`run`]'s answer, over the index rather than the demo database.
#[wasm_bindgen]
#[must_use]
pub fn corpus_run(query: &str) -> String {
    fjord_inspect::corpus::rows_json(query)
}

/// The schema the site opens with — `schemas/demo.sigla`, the database in the
/// page rather than the code index `schemas/dotnet.sigla` describes.
#[wasm_bindgen]
#[must_use]
pub fn sample_schema() -> String {
    fjord_inspect::SCHEMA.to_owned()
}

/// The queries the site opens with, as JSON.
///
/// From the module rather than the page because they are *tested* there:
/// `every_sample_compiles_clean` is what stops the site shipping an example the
/// language would refuse.
#[wasm_bindgen]
#[must_use]
pub fn samples() -> String {
    fjord_inspect::samples_json()
}

/// The version of Fjord this module was built from, for a page that wants to
/// say what it is running.
#[wasm_bindgen]
#[must_use]
pub fn version() -> String {
    env!("CARGO_PKG_VERSION").to_owned()
}

// ======================================================================================
// The code browser's client surface
// ======================================================================================
//
// **Typed, not JSON.** Everything above answers a string a page must `JSON.parse`,
// which is right for a panel that shows one answer and wrong for a browser that
// redraws on every scroll: parsing a document to reach numbers the engine already
// had is work nobody asked for. These are `wasm_bindgen` types instead, so
// `wasm-bindgen` writes the `.d.ts` and TypeScript sees a real interface.
//
// **And the blob is a handle rather than a copy.** A file is opened once and stays
// in the module; the page pulls the lines it is drawing. A windowed viewer draws
// forty lines of a thousand, so shipping the thousand across the boundary to draw
// forty is the cost this shape exists to avoid.

use fjord_inspect::codeview;

/// A definition — an outline row, or where a symbol is declared.
#[wasm_bindgen(getter_with_clone)]
pub struct Definition {
    pub symbol: String,
    pub path: String,
    /// A byte offset into the file, in the unit `position-encoding` names.
    pub start: i32,
    pub length: i32,
    /// The union alternative's name — `class_`, `method_` — or an `other` payload.
    pub kind: String,
    pub name: String,
    /// The human-readable full name — `Namespace.Type.Method`, empty if unwritten.
    pub qualified: String,
    /// The symbol that contains this one. Filled by `outline`, empty elsewhere.
    pub container: String,
}

/// What MSBuild resolved about one project, and what it builds against.
#[wasm_bindgen(getter_with_clone)]
pub struct Project {
    pub framework: String,
    pub sdk: String,
    pub output: String,
    pub assembly: String,
    pub namespace: String,
    pub platform: String,
    /// Projects this one builds against, as paths.
    pub references: Vec<String>,
    /// Projects that build against it.
    pub dependents: Vec<String>,
    /// The source files it compiles.
    pub sources: Vec<String>,
}

/// A package a project asks for: what it resolved to, and what the file wrote.
#[wasm_bindgen(getter_with_clone)]
pub struct PackageRef {
    pub name: String,
    pub version: String,
    pub range: String,
}

/// An entry in a directory listing: a file, or a directory that holds more.
#[wasm_bindgen(getter_with_clone)]
pub struct Entry {
    pub name: String,
    pub path: String,
    pub is_dir: bool,
}

/// A span in a file that names a symbol.
#[wasm_bindgen(getter_with_clone)]
pub struct Reference {
    pub symbol: String,
    pub path: String,
    pub start: i32,
    pub length: i32,
}

/// What one symbol says about itself, for a hover card.
#[wasm_bindgen(getter_with_clone)]
pub struct Info {
    pub signature: String,
    pub doc: String,
    pub modifiers: String,
    /// The human-readable full name — `System.IDisposable`.
    pub qualified: String,
    /// The assembly it ships in, as a name and a version — `System.Runtime 10.0.0.0`.
    pub package: String,
    /// What it is — the union alternative's name, or an `other` payload.
    pub kind: String,
}

/// A search hit.
#[wasm_bindgen(getter_with_clone)]
pub struct Hit {
    pub name: String,
    pub symbol: String,
    pub kind: String,
    pub path: String,
    pub line: i32,
}

/// **One file, held in the module.** Opened once; the page pulls what it draws.
#[wasm_bindgen]
pub struct Blob {
    inner: codeview::Blob,
}

#[wasm_bindgen]
impl Blob {
    #[wasm_bindgen(getter)]
    #[must_use]
    pub fn path(&self) -> String {
        self.inner.path.clone()
    }

    /// How many lines the file has.
    #[wasm_bindgen(getter)]
    #[must_use]
    pub fn lines(&self) -> usize {
        self.inner.lines.len()
    }

    /// What the database says its style payloads are, or `undefined` where it says
    /// nothing this build can read — in which case every line's runs are empty and
    /// the file renders plain, which is what the schema asks for.
    #[wasm_bindgen(getter)]
    #[must_use]
    pub fn encoding(&self) -> Option<String> {
        self.inner.encoding.map(|encoding| {
            match encoding {
                codeview::StyleEncoding::RoslynLsp1 => "roslyn-lsp-1",
                codeview::StyleEncoding::ScipSyntax1 => "scip-syntax-1",
            }
            .to_owned()
        })
    }

    /// One line's text, **one-based** as every line number in the schema is.
    #[must_use]
    pub fn text(&self, line: usize) -> Option<String> {
        self.at(line).map(|held| held.text.clone())
    }

    /// The byte offset of a line's first byte.
    #[must_use]
    pub fn start(&self, line: usize) -> Option<i32> {
        self.at(line).map(|held| held.start as i32)
    }

    /// One line's colour runs, flat: **four numbers per run** — start, length, kind,
    /// modifiers — in order and non-overlapping.
    ///
    /// A `Uint32Array` rather than an array of objects because a large file has
    /// thousands of runs and a page redraws them on every scroll: one allocation
    /// instead of one per run. `start` and `length` count in the *encoding's* unit,
    /// which `encoding` names and which is not always this database's.
    #[must_use]
    pub fn runs(&self, line: usize) -> Vec<u32> {
        self.at(line).map_or_else(Vec::new, |held| {
            held.runs
                .iter()
                .flat_map(|run| [run.start, run.length, run.kind, run.modifiers])
                .collect()
        })
    }

    fn at(&self, line: usize) -> Option<&fjord_inspect::codeview::BlobLine> {
        self.inner.lines.get(line.checked_sub(1)?)
    }
}

/// Every file in the loaded index.
///
/// # Errors
/// If no corpus is loaded, or the query fails.
#[wasm_bindgen]
pub fn files() -> Result<Vec<String>, JsError> {
    codeview::files().map_err(|problem| JsError::new(&problem))
}

/// Open one file: its lines, and each line's colour runs already resolved.
///
/// # Errors
/// If no corpus is loaded, or the query fails.
#[wasm_bindgen]
pub fn open(path: &str) -> Result<Blob, JsError> {
    codeview::blob(path)
        .map(|inner| Blob { inner })
        .map_err(|problem| JsError::new(&problem))
}

/// The definitions declared in one file, in position order.
///
/// # Errors
/// If no corpus is loaded, or the query fails.
#[wasm_bindgen]
pub fn outline(path: &str) -> Result<Vec<Definition>, JsError> {
    codeview::outline(path)
        .map(|found| found.into_iter().map(Definition::from).collect())
        .map_err(|problem| JsError::new(&problem))
}

/// Every reference in one file, in position order — what a renderer splices links
/// over the text with.
///
/// # Errors
/// If no corpus is loaded, or the query fails.
#[wasm_bindgen]
pub fn xrefs(path: &str) -> Result<Vec<Reference>, JsError> {
    codeview::xrefs(path)
        .map(|found| found.into_iter().map(Reference::from).collect())
        .map_err(|problem| JsError::new(&problem))
}

/// Where a symbol is defined. More than one is legitimate — a C# partial class is
/// declared in two files.
///
/// # Errors
/// If no corpus is loaded, or the query fails.
#[wasm_bindgen]
pub fn definitions(symbol: &str) -> Result<Vec<Definition>, JsError> {
    codeview::definitions(symbol)
        .map(|found| found.into_iter().map(Definition::from).collect())
        .map_err(|problem| JsError::new(&problem))
}

/// Every use of a symbol across the whole index.
///
/// # Errors
/// If no corpus is loaded, or the query fails.
#[wasm_bindgen]
pub fn references(symbol: &str) -> Result<Vec<Reference>, JsError> {
    codeview::references(symbol)
        .map(|found| found.into_iter().map(Reference::from).collect())
        .map_err(|problem| JsError::new(&problem))
}

/// What the build layer holds about the project a `.csproj` declares.
///
/// `undefined` for every file that is not a project, which is most of them.
///
/// # Errors
/// If no corpus is loaded, or a query fails.
#[wasm_bindgen]
pub fn project(path: &str) -> Result<Option<Project>, JsError> {
    codeview::project(path)
        .map(|found| found.map(Project::from))
        .map_err(|problem| JsError::new(&problem))
}

/// The packages one project asks for, with the range its file wrote.
///
/// # Errors
/// If no corpus is loaded, or the query fails.
#[wasm_bindgen]
pub fn packages(path: &str) -> Result<Vec<PackageRef>, JsError> {
    codeview::packages(path)
        .map(|found| found.into_iter().map(PackageRef::from).collect())
        .map_err(|problem| JsError::new(&problem))
}

/// One directory's immediate entries — a prefix seek over `src.File`.
///
/// `prefix` is `""` for the root, or a directory path with its trailing `/`. A tree
/// that opens a level at a time asks this once per level rather than reading every
/// path in the index to draw a dozen rows.
///
/// # Errors
/// If no corpus is loaded, or the query fails.
#[wasm_bindgen]
pub fn children(prefix: &str) -> Result<Vec<Entry>, JsError> {
    codeview::children(prefix)
        .map(|found| found.into_iter().map(Entry::from).collect())
        .map_err(|problem| JsError::new(&problem))
}

/// The signature, doc comment and modifiers of one symbol, for a hover card.
///
/// `undefined` where this index only *names* the symbol rather than declaring it,
/// which is ordinary: the card then says what the name alone can say.
///
/// # Errors
/// If no corpus is loaded, or the query fails.
#[wasm_bindgen]
pub fn symbol_info(symbol: &str) -> Result<Option<Info>, JsError> {
    codeview::info(symbol)
        .map(|found| found.map(Info::from))
        .map_err(|problem| JsError::new(&problem))
}

/// Case-insensitive prefix search over the name index.
///
/// # Errors
/// If no corpus is loaded, or the query fails.
#[wasm_bindgen]
pub fn search(prefix: &str) -> Result<Vec<Hit>, JsError> {
    codeview::search(prefix)
        .map(|found| found.into_iter().map(Hit::from).collect())
        .map_err(|problem| JsError::new(&problem))
}

impl From<codeview::Project> for Project {
    fn from(found: codeview::Project) -> Self {
        Self {
            framework: found.framework,
            sdk: found.sdk,
            output: found.output,
            assembly: found.assembly,
            namespace: found.namespace,
            platform: found.platform,
            references: found.references,
            dependents: found.dependents,
            sources: found.sources,
        }
    }
}

impl From<codeview::PackageRef> for PackageRef {
    fn from(found: codeview::PackageRef) -> Self {
        Self {
            name: found.name,
            version: found.version,
            range: found.range,
        }
    }
}

impl From<codeview::Entry> for Entry {
    fn from(found: codeview::Entry) -> Self {
        Self {
            name: found.name,
            path: found.path,
            is_dir: found.is_dir,
        }
    }
}

impl From<codeview::Info> for Info {
    fn from(found: codeview::Info) -> Self {
        Self {
            signature: found.signature,
            doc: found.doc,
            modifiers: found.modifiers,
            qualified: found.qualified,
            package: found.package,
            kind: found.kind,
        }
    }
}

impl From<codeview::Definition> for Definition {
    fn from(found: codeview::Definition) -> Self {
        Self {
            symbol: found.symbol,
            path: found.path,
            start: found.start as i32,
            length: found.length as i32,
            kind: found.kind,
            name: found.name,
            qualified: found.qualified,
            container: found.container,
        }
    }
}

impl From<codeview::Reference> for Reference {
    fn from(found: codeview::Reference) -> Self {
        Self {
            symbol: found.symbol,
            path: found.path,
            start: found.start as i32,
            length: found.length as i32,
        }
    }
}

impl From<codeview::Hit> for Hit {
    fn from(found: codeview::Hit) -> Self {
        Self {
            name: found.name,
            symbol: found.symbol,
            kind: found.kind,
            path: found.path,
            line: found.line as i32,
        }
    }
}

/// **One file's references, shaped for hit-testing.**
///
/// A code view asks "what is under the cursor" on every mouse move, and there are
/// hundreds of references in a file. Answering that with an array of objects means
/// hundreds of boundary crossings per move, because each field of a `wasm_bindgen`
/// struct is a getter that crosses — so this is the flat shape instead: two typed
/// arrays and a parallel list of names, fetched once when the file opens. The
/// search is then a binary search over an `Int32Array`, entirely in the page.
#[wasm_bindgen]
pub struct FileRefs {
    spans: Vec<i32>,
    symbols: Vec<String>,
    locals: Vec<i32>,
}

#[wasm_bindgen]
impl FileRefs {
    /// `[start, length]` per reference, ordered by `start` — so a binary search
    /// over even indices finds the span a byte offset falls in.
    #[wasm_bindgen(getter)]
    #[must_use]
    pub fn spans(&self) -> Vec<i32> {
        self.spans.clone()
    }

    /// The symbol each span names, parallel to `spans` — entry `n` belongs to the
    /// span at `spans[n * 2]`.
    #[wasm_bindgen(getter)]
    #[must_use]
    pub fn symbols(&self) -> Vec<String> {
        self.symbols.clone()
    }

    /// `[start, length, targetStart, targetLength]` per **file-local** reference,
    /// ordered by `start`. These have no symbol: the answer is a span in this same
    /// file, which is what a jump to a parameter's declaration needs.
    #[wasm_bindgen(getter)]
    #[must_use]
    pub fn locals(&self) -> Vec<i32> {
        self.locals.clone()
    }
}

/// Every reference in one file — global and file-local — ready to hit-test.
///
/// # Errors
/// If no corpus is loaded, or either query fails.
#[wasm_bindgen]
pub fn refs(path: &str) -> Result<FileRefs, JsError> {
    let global = codeview::xrefs(path).map_err(|problem| JsError::new(&problem))?;
    let local = codeview::local_xrefs(path).map_err(|problem| JsError::new(&problem))?;

    let mut spans = Vec::with_capacity(global.len() * 2);
    let mut symbols = Vec::with_capacity(global.len());

    for found in global {
        spans.push(found.start as i32);
        spans.push(found.length as i32);
        symbols.push(found.symbol);
    }

    let mut locals = Vec::with_capacity(local.len() * 4);
    for found in local {
        locals.push(found.start as i32);
        locals.push(found.length as i32);
        locals.push(found.target_start as i32);
        locals.push(found.target_length as i32);
    }

    Ok(FileRefs {
        spans,
        symbols,
        locals,
    })
}
