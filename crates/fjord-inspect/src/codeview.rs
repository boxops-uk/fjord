//! **The questions a code browser asks**, answered in Rust.
//!
//! Everything else in this crate answers a question *about* the engine — the tokens,
//! the plan, the trace. This answers questions about the **code in the database**:
//! what files are there, what does this one say, what is at this position, where is
//! that defined, who uses it. The page draws the answers; it does not compute them.
//!
//! **That split is the point, and it is a performance claim.** A code browser does
//! per-token work on every line it draws, and doing that in JavaScript over a JSON
//! string means parsing a document to reach numbers the engine already had. So the
//! decoding lives here: [`decode_styles`] turns a `src.FileLineStyles` payload into
//! flat runs, and the boundary hands the page a typed array rather than a string to
//! re-parse.
//!
//! **The vocabulary deliberately stays out.** A run carries its legend *index*, not
//! a class name: `src.FileLineStyles` says the schema defines nothing about the
//! payload's meaning, and a highlighter's vocabulary is a presentation concern with
//! its own lifecycle. Mapping index to colour is the page's, and it is a table
//! lookup rather than decoding.

use fjord_wire::varint;
use serde::Serialize;

/// One coloured run on one line.
///
/// `start` and `length` count in **the encoding's own unit**, which is not always
/// this database's: `roslyn-lsp-1` counts UTF-16 code units — LSP's unit, and
/// `src.FileLine.cstart`'s — while `scip-syntax-1` counts UTF-8 bytes, which is
/// `start`'s. A consumer drawing a run over text converts through that line's
/// `start`/`cstart` pair rather than assuming the two agree.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize)]
pub struct TokenRun {
    /// Where the run begins, from the start of its line.
    pub start: u32,
    pub length: u32,
    /// The legend index — what kind of token, in the encoding's own vocabulary.
    pub kind: u32,
    /// A bitmask in the encoding's own modifier legend. Always zero where the
    /// encoding has no modifiers.
    pub modifiers: u32,
}

/// A style payload format this build can read.
///
/// **Matched exhaustively wherever it is read.** A third producer's encoding is a
/// third arm here, and the day one arrives this fails to compile rather than
/// rendering its bytes as somebody else's vocabulary.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize)]
pub enum StyleEncoding {
    /// The .NET indexer's: LSP `SemanticTokens.data`, five varints per token —
    /// `[deltaLine, deltaStart, length, type, modifiers]`, `deltaStart` relative to
    /// the previous token **on that line**, columns in UTF-16 code units.
    RoslynLsp1,
    /// `scip2fjord`'s: three varints per token — `[start, length, kind]`, `start`
    /// absolute from the line's first byte, both counted in UTF-8 bytes, no
    /// modifiers.
    ScipSyntax1,
}

impl StyleEncoding {
    /// The encoding `config.Setting {dimension = "style-encoding"}` names, or
    /// `None` for a name this build does not know.
    ///
    /// **`None` is not a fault.** `src.FileLineStyles` says a consumer that does not
    /// recognise the encoding renders those lines plain, so an unknown name is an
    /// index this build can browse without colour, never one it refuses.
    #[must_use]
    pub fn named(name: &str) -> Option<Self> {
        match name {
            "roslyn-lsp-1" => Some(Self::RoslynLsp1),
            "scip-syntax-1" => Some(Self::ScipSyntax1),
            _ => None,
        }
    }

    /// How many varints one token costs in this encoding.
    const fn stride(self) -> usize {
        match self {
            Self::RoslynLsp1 => 5,
            Self::ScipSyntax1 => 3,
        }
    }
}

/// Why a payload could not be decoded.
#[derive(Debug, Clone, PartialEq, Eq, Serialize)]
pub enum StyleError {
    /// A varint ran off the end, or did not terminate, or was not minimal.
    BadVarint,
    /// The payload ended part-way through a token — a whole number of tokens is the
    /// only well-formed shape.
    PartialToken { stride: usize, left: usize },
    /// A field wider than the 32 bits a run holds. A column, a length or a legend
    /// index that large is a corrupt payload rather than a large file.
    TooWide(u64),
}

impl core::fmt::Display for StyleError {
    fn fmt(&self, f: &mut core::fmt::Formatter<'_>) -> core::fmt::Result {
        match self {
            Self::BadVarint => write!(f, "a style payload holds a malformed varint"),
            Self::PartialToken { stride, left } => write!(
                f,
                "a style payload ends {left} of {stride} fields into a token"
            ),
            Self::TooWide(value) => {
                write!(
                    f,
                    "a style payload holds {value}, which is wider than a run field"
                )
            }
        }
    }
}

impl core::error::Error for StyleError {}

/// Decode one line's style payload into runs, absolute and in order.
///
/// **The deltas are resolved here rather than at the boundary.** `roslyn-lsp-1`
/// stores each token's start relative to the previous one, which is most of why the
/// payload is small and all of why a consumer cannot use it as it stands.
pub fn decode_styles(encoding: StyleEncoding, payload: &[u8]) -> Result<Vec<TokenRun>, StyleError> {
    let stride = encoding.stride();
    let mut fields = Vec::new();
    let mut rest = payload;

    while !rest.is_empty() {
        let (value, read) = varint::get_u64(rest).map_err(|_| StyleError::BadVarint)?;
        fields.push(u32::try_from(value).map_err(|_| StyleError::TooWide(value))?);
        rest = &rest[read..];
    }

    let left = fields.len() % stride;
    if left != 0 {
        return Err(StyleError::PartialToken { stride, left });
    }

    let mut runs = Vec::with_capacity(fields.len() / stride);
    let mut previous = 0u32;

    for token in fields.chunks_exact(stride) {
        match encoding {
            StyleEncoding::RoslynLsp1 => {
                // `token[0]` is `deltaLine`, always zero because a fact is one line.
                // Read and ignored rather than skipped: it is part of the format, and
                // a payload that used it would be one this cannot render.
                let start = previous.saturating_add(token[1]);
                runs.push(TokenRun {
                    start,
                    length: token[2],
                    kind: token[3],
                    modifiers: token[4],
                });
                previous = start;
            }
            StyleEncoding::ScipSyntax1 => runs.push(TokenRun {
                start: token[0],
                length: token[1],
                kind: token[2],
                modifiers: 0,
            }),
        }
    }

    Ok(runs)
}

#[cfg(test)]
mod tests {
    use super::*;

    fn payload(values: &[u64]) -> Vec<u8> {
        let mut out = Vec::new();
        for &value in values {
            varint::put_u64(&mut out, value);
        }
        out
    }

    /// The deltas the .NET indexer writes, resolved back to the absolute starts it
    /// had before it encoded them. Three tokens at 0, 4 and 12.
    #[test]
    fn roslyn_deltas_resolve_to_the_starts_they_were_made_from() {
        let bytes = payload(&[
            0, 0, 3, 1, 0, // at 0, length 3, keyword
            0, 4, 5, 4, 0, // +4 → at 4, length 5, class name
            0, 8, 2, 3, 1, // +8 → at 12, length 2, identifier, static
        ]);

        assert_eq!(
            decode_styles(StyleEncoding::RoslynLsp1, &bytes).expect("it decodes"),
            [
                TokenRun {
                    start: 0,
                    length: 3,
                    kind: 1,
                    modifiers: 0
                },
                TokenRun {
                    start: 4,
                    length: 5,
                    kind: 4,
                    modifiers: 0
                },
                TokenRun {
                    start: 12,
                    length: 2,
                    kind: 3,
                    modifiers: 1
                },
            ]
        );
    }

    /// SCIP's starts are absolute, and reading them as deltas would pile every token
    /// onto a running total — the one mistake that produces plausible-looking runs.
    #[test]
    fn scip_starts_are_absolute_not_accumulated() {
        let bytes = payload(&[0, 3, 7, 4, 5, 2, 12, 2, 9]);

        assert_eq!(
            decode_styles(StyleEncoding::ScipSyntax1, &bytes).expect("it decodes"),
            [
                TokenRun {
                    start: 0,
                    length: 3,
                    kind: 7,
                    modifiers: 0
                },
                TokenRun {
                    start: 4,
                    length: 5,
                    kind: 2,
                    modifiers: 0
                },
                TokenRun {
                    start: 12,
                    length: 2,
                    kind: 9,
                    modifiers: 0
                },
            ]
        );
    }

    #[test]
    fn an_empty_payload_is_no_runs_rather_than_an_error() {
        assert!(
            decode_styles(StyleEncoding::RoslynLsp1, &[])
                .expect("empty decodes")
                .is_empty()
        );
    }

    #[test]
    fn a_payload_ending_part_way_through_a_token_is_refused() {
        let bytes = payload(&[0, 0, 3, 1]); // four fields of a five-field token

        assert_eq!(
            decode_styles(StyleEncoding::RoslynLsp1, &bytes).unwrap_err(),
            StyleError::PartialToken { stride: 5, left: 4 }
        );
    }

    #[test]
    fn a_malformed_varint_is_refused_by_name() {
        assert_eq!(
            decode_styles(StyleEncoding::RoslynLsp1, &[0x80; 10]).unwrap_err(),
            StyleError::BadVarint
        );
    }

    #[test]
    fn a_field_wider_than_a_run_holds_is_refused() {
        let bytes = payload(&[0, 0, 3, u64::from(u32::MAX) + 1, 0]);

        assert_eq!(
            decode_styles(StyleEncoding::RoslynLsp1, &bytes).unwrap_err(),
            StyleError::TooWide(u64::from(u32::MAX) + 1)
        );
    }

    /// **The injection guard.** Every question here builds a query around a value
    /// from outside, so a quote that closed the literal would let the rest be read
    /// as query. Written as the attack rather than as a spelling check.
    #[test]
    fn a_quote_in_a_search_term_cannot_close_the_literal() {
        let attack = r#"x" ; codemarkup.SearchEntry {nameLowercase = ""#;
        let spelled = literal(attack);

        // One opening quote, one closing quote, and every quote between them escaped
        // — so the literal ends where this function put its end and nowhere else.
        assert!(spelled.starts_with('"') && spelled.ends_with('"'));

        let inner = &spelled[1..spelled.len() - 1];
        for (at, _) in inner.match_indices('"') {
            let preceding = inner[..at].chars().rev().take_while(|&c| c == '\\').count();
            assert_eq!(preceding % 2, 1, "an unescaped quote at {at} in {spelled}");
        }
    }

    /// What [`literal`] spells is what the lexer reads back — checked against the
    /// thing that actually reads it, rather than against my idea of it.
    #[test]
    fn what_literal_spells_is_what_the_lexer_reads_back() {
        for text in [
            "Blocks.cs",
            "weird\"name.cs",
            "back\\slash.cs",
            "tab\there.cs",
            "\u{1}control.cs",
            "Boxops/Fjord/Client#Send().",
        ] {
            assert_eq!(
                fjord_engine::lexer::unescape_str(&literal(text)).as_deref(),
                Ok(text),
                "{text:?} did not survive being spelled and read back"
            );
        }
    }

    /// An encoding this build does not know renders plain, which the schema asks for
    /// — never a refusal to open the file.
    #[test]
    fn an_unknown_encoding_is_none_rather_than_an_error() {
        assert_eq!(
            StyleEncoding::named("roslyn-lsp-1"),
            Some(StyleEncoding::RoslynLsp1)
        );
        assert_eq!(
            StyleEncoding::named("scip-syntax-1"),
            Some(StyleEncoding::ScipSyntax1)
        );
        assert_eq!(StyleEncoding::named("treesitter-whatever-2"), None);
    }
}

// ======================================================================================
// The questions
// ======================================================================================

use fjord_encoding::tuple::Value;

/// How many rows each question will take before it stops.
///
/// Generous, because these bound a *file* rather than a page a reader scrolls: the
/// largest is one row per line of the longest file in the index.
const FILE_CAP: usize = 200_000;
/// What a search box will show. Small on purpose — the seek is cheap and the list is
/// read by a human.
const SEARCH_CAP: usize = 200;

/// One line of a file: its text, where it starts, and how it is coloured.
#[derive(Debug, Clone, PartialEq, Eq, Serialize)]
pub struct BlobLine {
    pub line: i64,
    pub text: String,
    /// The byte offset of the line's first byte.
    pub start: i64,
    /// The same start, counted in the unit `config.Setting "position-encoding"` names
    /// — which is the unit a `roslyn-lsp-1` run's columns are in.
    pub cstart: i64,
    pub runs: Vec<TokenRun>,
}

/// A whole file, ready to draw.
#[derive(Debug, Clone, PartialEq, Eq, Serialize)]
pub struct Blob {
    pub path: String,
    pub lines: Vec<BlobLine>,
    /// What `config.Setting {dimension = "style-encoding"}` said, or `None` where the
    /// database names none or names one this build cannot read. `None` means the
    /// lines carry no runs and render plain — which the schema asks for.
    pub encoding: Option<StyleEncoding>,
}

/// A definition, as an outline row or a jump target.
#[derive(Debug, Clone, PartialEq, Eq, Serialize)]
pub struct Definition {
    pub symbol: String,
    pub path: String,
    pub start: i64,
    pub length: i64,
    /// The union alternative's name — `class_`, `method_`, `other`.
    pub kind: String,
    pub name: String,
    /// The human-readable full name — `Namespace.Type.Method`.
    ///
    /// Stored by the producer rather than derived here, because it is not derivable
    /// from the symbol string without a SCIP descriptor parser. Empty where the
    /// producer wrote nothing, which is the same shape as `name`.
    pub qualified: String,
    /// The symbol that contains this one, or empty where nothing here does.
    ///
    /// **Filled by [`outline`] and by nothing else.** Containment is a `Relation`
    /// rather than a field — the schema is explicit that there is no `container` on
    /// `Definition` or `FileDefinition` — so it costs a query, and the one caller that
    /// wants it is the one drawing a file's symbols as a tree. Empty is therefore two
    /// answers at once: "nothing contains it" and "nobody asked".
    pub container: String,
}

/// A reference: a span in a file that names a symbol.
#[derive(Debug, Clone, PartialEq, Eq, Serialize)]
pub struct Reference {
    pub symbol: String,
    pub path: String,
    pub start: i64,
    pub length: i64,
}

/// A reference whose target is **file-local** — a parameter, a local, a lambda
/// binding: anything the producer has no global name for.
///
/// Span to span inside one file, and that is not a shortcut. A SCIP `local` id is
/// an occurrence ordinal, so it names a different variable in every file and moves
/// whenever one is edited; giving these symbols would mint identities that look
/// global and are not. They are also the majority of the references in a real
/// corpus, so a code view that skipped them would leave most of its text dead.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize)]
pub struct LocalReference {
    pub start: i64,
    pub length: i64,
    /// Where the thing it names is declared, in the same file.
    pub target_start: i64,
    pub target_length: i64,
}

/// What one symbol says about itself — `codemarkup.SymbolInfo`, whole.
#[derive(Debug, Clone, PartialEq, Eq, Serialize)]
pub struct Info {
    /// The declaration as a reader would write it, types and all.
    pub signature: String,
    /// The doc comment, already stripped of its markup by the indexer. Often empty.
    pub doc: String,
    /// `public sealed`, `private static` — the words in front of the declaration.
    pub modifiers: String,
    /// The human-readable full name — `System.IDisposable`.
    pub qualified: String,
    /// The assembly it ships in, as a name and a version — `System.Runtime 10.0.0.0`.
    pub package: String,
    /// What it is — `interface_`, `method_`, `parameter`. The union alternative's name.
    ///
    /// On this predicate as well as on `Definition`, because a symbol nothing here
    /// declares has no `Definition` at all and "what is it" is the first thing asked.
    pub kind: String,
}

/// A search hit.
#[derive(Debug, Clone, PartialEq, Eq, Serialize)]
pub struct Hit {
    pub name: String,
    pub symbol: String,
    pub kind: String,
    pub path: String,
    pub line: i64,
}

/// One directory's immediate entries — **a seek, not a scan**.
///
/// `src.File` is a path relative to the index root, and a directory is a prefix of one
/// rather than a fact of its own: there is nothing to join to and nothing to store. So
/// the question "what is directly under `clients/dotnet/`" is a prefix seek on the
/// leading field of the key, and the answer is that range cut at its next `/`.
///
/// A tree that opens one level at a time asks this once per level, which is what keeps
/// a browser over a million-file index from reading a million paths to draw twelve
/// rows. `prefix` is `""` for the root, or a directory path **with** its trailing `/`.
pub fn children(prefix: &str) -> Result<Vec<Entry>, String> {
    let rows = crate::corpus::values(
        &format!(
            "P where F = src.File P; P = {prefix}..",
            prefix = literal(prefix)
        ),
        FILE_CAP,
    )?;

    // Ordered and deduped by the walk itself: the seek returns keys in order, so a
    // directory's entries arrive together and the last one seen is the only one to
    // compare against.
    let mut out: Vec<Entry> = Vec::new();
    for path in rows.iter().filter_map(as_str) {
        let Some(rest) = path.strip_prefix(prefix) else {
            continue;
        };
        let (name, is_dir) = match rest.split_once('/') {
            Some((head, _)) => (head, true),
            None => (rest, false),
        };
        if name.is_empty() {
            continue;
        }
        if out.last().is_some_and(|last| last.name == name) {
            continue;
        }
        out.push(Entry {
            name: name.to_owned(),
            path: format!("{prefix}{name}"),
            is_dir,
        });
    }

    Ok(out)
}

/// An entry in a directory listing: a file, or a directory that holds more.
#[derive(Debug, Clone, PartialEq, Eq, Serialize)]
pub struct Entry {
    /// The last segment — what a row is labelled with.
    pub name: String,
    /// The whole path from the index root. A directory carries no trailing `/`.
    pub path: String,
    pub is_dir: bool,
}

/// What MSBuild resolved about one project, and what it builds against.
///
/// **The other half of a `.csproj`.** Its text says what somebody wrote; this says what
/// the build made of it — the framework a `<TargetFramework>` resolved to, the version a
/// floating `<PackageReference>` landed on, the graph edges a path string only implies.
/// A viewer showing one without the other is showing half the file.
#[derive(Debug, Clone, PartialEq, Eq, Serialize)]
pub struct Project {
    pub framework: String,
    pub sdk: String,
    pub output: String,
    pub assembly: String,
    pub namespace: String,
    pub platform: String,
    /// Projects this one builds against, as paths.
    pub references: Vec<String>,
    /// Projects that build against it — the direction that decides whether a change is safe.
    pub dependents: Vec<String>,
    /// The source files it compiles.
    pub sources: Vec<String>,
}

/// A package a project asks for: what it resolved to, and what the file wrote.
#[derive(Debug, Clone, PartialEq, Eq, Serialize)]
pub struct PackageRef {
    pub name: String,
    /// The version that was resolved — part of the package's identity.
    pub version: String,
    /// The range the project file asked for, which differs under a floating version.
    pub range: String,
}

/// What the build layer holds about the project a `.csproj` declares.
///
/// `None` where the path names no project — every other file in the index, and a project
/// outside the indexed tree that only an edge mentions.
///
/// # Errors
///
/// If no corpus is loaded, or a query fails.
pub fn project(path: &str) -> Result<Option<Project>, String> {
    let rows = crate::corpus::values(
        &format!(
            "{{row = P.value}} where F = src.File {file}; P = msbuild.Project {{file = F}}",
            file = literal(path)
        ),
        1,
    )?;

    let Some(row) = rows.first().and_then(|row| field(row, "row")) else {
        return Ok(None);
    };

    // Each of these is a `src.MaybeString`, which is a union rather than an empty string:
    // "MSBuild resolved nothing" and "MSBuild resolved the empty string" are different
    // answers, and the schema keeps them apart. A viewer wants one of them, so this is
    // where they stop being different.
    let said = |name: &str| {
        field(row, name)
            .and_then(|value| match value {
                Value::Union { alt, value, .. } if alt == "just" => as_str(value),
                _ => None,
            })
            .unwrap_or_default()
            .to_owned()
    };

    let paths = |query: &str| -> Result<Vec<String>, String> {
        Ok(crate::corpus::values(query, FILE_CAP)?
            .iter()
            .filter_map(as_str)
            .map(ToOwned::to_owned)
            .collect())
    };

    Ok(Some(Project {
        framework: said("targetFramework"),
        sdk: said("sdk"),
        output: said("outputType"),
        assembly: said("assemblyName"),
        namespace: said("rootNamespace"),
        platform: said("platformTarget"),
        references: paths(&format!(
            "Q where F = src.File {file}; P = msbuild.Project {{file = F}}; \
             msbuild.ProjectReference {{from = P, to = T}}; T = msbuild.Project {{file = G}}; \
             G = src.File Q",
            file = literal(path)
        ))?,
        dependents: paths(&format!(
            "Q where F = src.File {file}; P = msbuild.Project {{file = F}}; \
             msbuild.ProjectReferencedBy {{to = P, from = T}}; \
             T = msbuild.Project {{file = G}}; G = src.File Q",
            file = literal(path)
        ))?,
        sources: paths(&format!(
            "Q where F = src.File {file}; P = msbuild.Project {{file = F}}; \
             msbuild.ProjectToSourceFile {{project = P, src = G}}; G = src.File Q",
            file = literal(path)
        ))?,
    }))
}

/// The packages one project asks for, with the range its file wrote.
///
/// # Errors
///
/// If no corpus is loaded, or the query fails.
pub fn packages(path: &str) -> Result<Vec<PackageRef>, String> {
    let rows = crate::corpus::values(
        &format!(
            "{{name = N, version = V, row = R.value}} where F = src.File {file}; \
             P = msbuild.Project {{file = F}}; \
             R = msbuild.PackageReference {{project = P, package = K}}; \
             K = msbuild.Package {{name = N, version = V}}",
            file = literal(path)
        ),
        FILE_CAP,
    )?;

    Ok(rows
        .iter()
        .filter_map(|row| {
            Some(PackageRef {
                name: as_str(field(row, "name")?)?.to_owned(),
                version: as_str(field(row, "version")?)?.to_owned(),
                range: field(row, "row")
                    .and_then(|value| field(value, "range"))
                    .and_then(|value| match value {
                        Value::Union { alt, value, .. } if alt == "just" => as_str(value),
                        _ => None,
                    })
                    .unwrap_or_default()
                    .to_owned(),
            })
        })
        .collect())
}

/// Every file in the index, in the order a scan meets them.
pub fn files() -> Result<Vec<String>, String> {
    Ok(crate::corpus::values("P where F = src.File P", FILE_CAP)?
        .iter()
        .filter_map(as_str)
        .map(str::to_owned)
        .collect())
}

/// One file's lines, with each line's colour runs already resolved.
///
/// **Two seeks and a zip, rather than a join.** The line table and the style table
/// are separate predicates keyed the same way, and a line with no tokens on it writes
/// no style fact at all — so joining them would drop exactly the lines that are
/// plain. They are read apart and matched by line number here.
pub fn blob(path: &str) -> Result<Blob, String> {
    let file = literal(path);

    let lines = crate::corpus::values(
        &format!(
            "{{line = L, row = R.value}} where F = src.File {file}; R = src.FileLine {{file = F, line = L}}"
        ),
        FILE_CAP,
    )?;

    let encoding = style_encoding();

    let mut styles: std::collections::BTreeMap<i64, Vec<TokenRun>> =
        std::collections::BTreeMap::new();

    if let Some(encoding) = encoding {
        let rows = crate::corpus::values(
            &format!(
                "{{line = L, row = S.value}} where F = src.File {file}; S = src.FileLineStyles {{file = F, line = L}}"
            ),
            FILE_CAP,
        )?;

        for row in &rows {
            let (Some(line), Some(payload)) = (
                field(row, "line").and_then(as_int),
                field(row, "row")
                    .and_then(|row| field(row, "styles"))
                    .and_then(as_bytes),
            ) else {
                continue;
            };

            // A payload this build cannot decode leaves that line plain rather than
            // failing the file: one corrupt line is not a reason to refuse to show
            // the other nine hundred.
            if let Ok(runs) = decode_styles(encoding, payload) {
                styles.insert(line, runs);
            }
        }
    }

    let mut blob = Vec::with_capacity(lines.len());

    for row in &lines {
        let (Some(line), Some(text)) = (
            field(row, "line").and_then(as_int),
            field(row, "row")
                .and_then(|row| field(row, "text"))
                .and_then(as_str),
        ) else {
            continue;
        };

        let inner = field(row, "row");

        blob.push(BlobLine {
            line,
            text: text.to_owned(),
            start: inner
                .and_then(|row| field(row, "start"))
                .and_then(as_int)
                .unwrap_or_default(),
            cstart: inner
                .and_then(|row| field(row, "cstart"))
                .and_then(as_int)
                .unwrap_or_default(),
            runs: styles.remove(&line).unwrap_or_default(),
        });
    }

    blob.sort_by_key(|line| line.line);

    Ok(Blob {
        path: path.to_owned(),
        lines: blob,
        encoding,
    })
}

/// The definitions declared in one file — the outline, in position order.
pub fn outline(path: &str) -> Result<Vec<Definition>, String> {
    let rows = crate::corpus::values(
        &format!(
            // **The qualified name is a join, which is what `symbol` trailing the key is
            // for.** `FileDefinition` carries `kind` and `name` and is ordered by file
            // and span, so rows arrive in render order with no join at all; the full
            // name lives on `Definition`, keyed by the symbol this row already names.
            "{{symbol = Sym, span = SP, row = D.value, full = Q.value}} where \
             F = src.File {file}; \
             D = codemarkup.FileDefinition {{file = F, span = SP, symbol = S}}; \
             S = src.Symbol Sym; \
             Q = codemarkup.Definition {{symbol = S, file = F}}",
            file = literal(path)
        ),
        FILE_CAP,
    )?;

    let mut found: Vec<Definition> = rows
        .iter()
        .filter_map(|row| {
            let span = field(row, "span")?;
            Some(Definition {
                symbol: as_str(field(row, "symbol")?)?.to_owned(),
                path: path.to_owned(),
                start: as_int(field(span, "start")?)?,
                length: as_int(field(span, "length")?)?,
                kind: field(row, "row")
                    .and_then(|row| field(row, "kind"))
                    .map_or_else(|| "other".to_owned(), alternative),
                name: field(row, "row")
                    .and_then(|row| field(row, "name"))
                    .and_then(as_str)
                    .unwrap_or_default()
                    .to_owned(),
                qualified: field(row, "full")
                    .and_then(|full| field(full, "qualified"))
                    .and_then(as_str)
                    .unwrap_or_default()
                    .to_owned(),
                container: String::new(),
            })
        })
        .collect();

    found.sort_by_key(|definition| definition.start);

    // **A second question rather than a join.** A top-level type is contained by
    // nothing, and joining `Relation` into the query above would drop its row entirely
    // — the outline would lose exactly the entries every other one hangs off. So the
    // edges come back on their own and are matched up here, where an absent one is an
    // empty string rather than a missing symbol.
    let edges = crate::corpus::values(
        &format!(
            "{{child = Sym, parent = Owner}} where F = src.File {file}; \
             codemarkup.FileDefinition {{file = F, span = SP, symbol = S}}; \
             S = src.Symbol Sym; \
             codemarkup.Relation {{from = C, kind = {{contains = {{}}}}, to = S}}; \
             C = src.Symbol Owner",
            file = literal(path)
        ),
        FILE_CAP,
    )?;

    let held: std::collections::HashMap<&str, &str> = edges
        .iter()
        .filter_map(|row| {
            Some((
                as_str(field(row, "child")?)?,
                as_str(field(row, "parent")?)?,
            ))
        })
        .collect();

    for definition in &mut found {
        if let Some(owner) = held.get(definition.symbol.as_str()) {
            definition.container = (*owner).to_owned();
        }
    }

    Ok(found)
}

/// Every reference in one file, in position order — what a renderer splices links
/// over the text with.
pub fn xrefs(path: &str) -> Result<Vec<Reference>, String> {
    let rows = crate::corpus::values(
        &format!(
            "{{span = SP, symbol = Sym}} where F = src.File {file}; \
             codemarkup.FileXRef {{file = F, span = SP, target = S, role = R}}; S = src.Symbol Sym",
            file = literal(path)
        ),
        FILE_CAP,
    )?;

    let mut found: Vec<Reference> = rows.iter().filter_map(|row| reference(row, path)).collect();

    found.sort_by_key(|found| found.start);
    Ok(found)
}

/// Every file-local reference in one file, in position order.
pub fn local_xrefs(path: &str) -> Result<Vec<LocalReference>, String> {
    let rows = crate::corpus::values(
        &format!(
            "{{span = SP, target = TG}} where F = src.File {file}; \
             codemarkup.FileLocalXRef {{file = F, span = SP, target = TG, role = R}}",
            file = literal(path)
        ),
        FILE_CAP,
    )?;

    let mut found: Vec<LocalReference> = rows
        .iter()
        .filter_map(|row| {
            let span = field(row, "span")?;
            let target = field(row, "target")?;
            Some(LocalReference {
                start: as_int(field(span, "start")?)?,
                length: as_int(field(span, "length")?)?,
                target_start: as_int(field(target, "start")?)?,
                target_length: as_int(field(target, "length")?)?,
            })
        })
        .collect();

    found.sort_by_key(|found| found.start);
    Ok(found)
}

/// Where a symbol is defined. More than one row is legitimate — a C# partial class
/// is declared in two files.
pub fn definitions(symbol: &str) -> Result<Vec<Definition>, String> {
    let rows = crate::corpus::values(
        &format!(
            "{{path = P, row = D.value}} where S = src.Symbol {symbol}; \
             D = codemarkup.Definition {{symbol = S, file = F}}; F = src.File P",
            symbol = literal(symbol)
        ),
        SEARCH_CAP,
    )?;

    Ok(rows
        .iter()
        .filter_map(|row| {
            let inner = field(row, "row")?;
            let span = field(inner, "span")?;
            Some(Definition {
                symbol: symbol.to_owned(),
                path: as_str(field(row, "path")?)?.to_owned(),
                start: as_int(field(span, "start")?)?,
                length: as_int(field(span, "length")?)?,
                kind: field(inner, "kind").map_or_else(|| "other".to_owned(), alternative),
                name: field(inner, "name")
                    .and_then(as_str)
                    .unwrap_or_default()
                    .to_owned(),
                qualified: field(inner, "qualified")
                    .and_then(as_str)
                    .unwrap_or_default()
                    .to_owned(),
                container: String::new(),
            })
        })
        .collect())
}

/// Every use of a symbol, across the whole index — find-references.
pub fn references(symbol: &str) -> Result<Vec<Reference>, String> {
    let rows = crate::corpus::values(
        &format!(
            "{{path = P, span = SP}} where S = src.Symbol {symbol}; \
             codemarkup.SymbolXRef {{target = S, file = F, span = SP}}; F = src.File P",
            symbol = literal(symbol)
        ),
        FILE_CAP,
    )?;

    Ok(rows
        .iter()
        .filter_map(|row| {
            let span = field(row, "span")?;
            Some(Reference {
                symbol: symbol.to_owned(),
                path: as_str(field(row, "path")?)?.to_owned(),
                start: as_int(field(span, "start")?)?,
                length: as_int(field(span, "length")?)?,
            })
        })
        .collect())
}

/// **What a hover card needs**: the signature, the doc comment, and the modifiers.
///
/// Its own predicate rather than a wider `Definition`, and read one symbol at a
/// time, because that is the shape the schema was given for exactly this reason: a
/// signature plus a doc comment is an order of magnitude more bytes than a name and
/// a span, and a panel reads a hundred of the latter for one of these.
///
/// `None` is ordinary rather than an error. Only a symbol this index *defines* has
/// a row here — `System.String` is named by the code and declared outside it, so the
/// card falls back to what the name itself says.
pub fn info(symbol: &str) -> Result<Option<Info>, String> {
    let rows = crate::corpus::values(
        &format!(
            "{{row = I.value}} where S = src.Symbol {symbol}; \
             I = codemarkup.SymbolInfo {{symbol = S}}",
            symbol = literal(symbol)
        ),
        1,
    )?;

    Ok(rows.iter().find_map(|row| {
        let inner = field(row, "row")?;
        Some(Info {
            signature: as_str(field(inner, "signature")?)?.to_owned(),
            doc: field(inner, "doc")
                .and_then(as_str)
                .unwrap_or_default()
                .to_owned(),
            modifiers: field(inner, "modifiers")
                .and_then(as_str)
                .unwrap_or_default()
                .to_owned(),
            qualified: field(inner, "qualified")
                .and_then(as_str)
                .unwrap_or_default()
                .to_owned(),
            package: field(inner, "package")
                .and_then(as_str)
                .unwrap_or_default()
                .to_owned(),
            kind: field(inner, "kind").map_or_else(|| "other".to_owned(), alternative),
        })
    }))
}

/// Case-insensitive **fuzzy prefix** search over the name index.
///
/// **A seek, not a scan.** `codemarkup.SearchEntry` leads with `nameLowercase` for
/// exactly this, and everything a hit renders trails it in the key — so a hit needs
/// no join and no point read. A fuzzy pattern keeps that: it sits on the leading
/// field, where the executor can still turn a live DFA state into a range and seek
/// past the dead bands between them.
pub fn search(prefix: &str) -> Result<Vec<Hit>, String> {
    let lowered = prefix.to_lowercase();

    let rows = crate::corpus::values(
        &format!(
            "{{name = N, symbol = Sym, kind = K, path = P, line = L}} where \
             codemarkup.SearchEntry {{nameLowercase = {pattern}, name = N, kind = K, \
             symbol = S, file = F, line = L}}; S = src.Symbol Sym; F = src.File P",
            pattern = pattern(&lowered)
        ),
        SEARCH_CAP,
    )?;

    Ok(rows
        .iter()
        .filter_map(|row| {
            Some(Hit {
                name: as_str(field(row, "name")?)?.to_owned(),
                symbol: as_str(field(row, "symbol")?)?.to_owned(),
                kind: field(row, "kind").map_or_else(|| "other".to_owned(), alternative),
                path: as_str(field(row, "path")?)?.to_owned(),
                line: as_int(field(row, "line")?)?,
            })
        })
        .collect())
}

/// What the database says its style payloads are, or `None` if it says nothing this
/// build can read.
fn style_encoding() -> Option<StyleEncoding> {
    let rows = crate::corpus::values(
        "V where config.Setting {dimension = \"style-encoding\", value = V}",
        8,
    )
    .ok()?;

    rows.iter()
        .filter_map(as_str)
        .find_map(StyleEncoding::named)
}

fn reference(row: &Value, path: &str) -> Option<Reference> {
    let span = field(row, "span")?;
    Some(Reference {
        symbol: as_str(field(row, "symbol")?)?.to_owned(),
        path: path.to_owned(),
        start: as_int(field(span, "start")?)?,
        length: as_int(field(span, "length")?)?,
    })
}

// ======================================================================================
// Reading a decoded value, and writing one into a query
// ======================================================================================

fn field<'a>(value: &'a Value, name: &str) -> Option<&'a Value> {
    match value {
        Value::Record(fields) => fields
            .iter()
            .find_map(|(held, value)| (held == name).then_some(value)),
        _ => None,
    }
}

fn as_str(value: &Value) -> Option<&str> {
    match value {
        Value::Str(text) => Some(text),
        _ => None,
    }
}

fn as_int(value: &Value) -> Option<i64> {
    match value {
        Value::Int(number) => Some(*number),
        _ => None,
    }
}

fn as_bytes(value: &Value) -> Option<&[u8]> {
    match value {
        Value::Bytes(bytes) => Some(bytes),
        _ => None,
    }
}

/// A union's alternative name — `class_`, `method_` — which is what a page shows and
/// what a stylesheet keys on. `other` carries a string, and that string is the name.
fn alternative(value: &Value) -> String {
    match value {
        Value::Union { alt, value, .. } => match (alt.as_str(), value.as_ref()) {
            ("other", Value::Str(name)) => name.clone(),
            _ => alt.clone(),
        },
        _ => "other".to_owned(),
    }
}

/// Spell `text` as a sigla string literal.
///
/// **Every question here builds a query around a value that came from outside** — a
/// path out of the index, a term somebody typed into a search box. A quote in either
/// would otherwise end the literal and let the rest be read as query, which is the
/// oldest injection there is. The escapes are the lexer's: `\"`, `\\`, the named
/// control ones, and `\u` for anything else below a space.
#[must_use]
/// How a search term is spelled as a pattern, which depends on how much of it there is.
///
/// `~<` rather than `~` because this is a search box: it measures the term against a
/// **prefix** of the stored name rather than the whole of it, so `"parsr"~<1` finds
/// `parser_function`, which no whole-string distance would — a five-character term is
/// never within three edits of a fifteen-character identifier, however well it starts
/// it.
///
/// **The distance has to grow with the term rather than sit still.** A term no longer
/// than its distance is within that distance of the *empty* prefix, and every name
/// starts with the empty prefix — so a fixed `~<1` would make `"a"` match the entire
/// index. Below three characters there is nothing worth being fuzzy about and the
/// exact prefix is both the stricter answer and the cheaper one; past that, one edit
/// covers the typo a short name can carry and two covers a long one. Three is the
/// engine's ceiling and is not reached here: at that width the answers stop being
/// about the term.
fn pattern(lowered: &str) -> String {
    let term = literal(lowered);
    match lowered.chars().count() {
        0..=2 => format!("{term}.."),
        3..=5 => format!("{term}~<1"),
        _ => format!("{term}~<2"),
    }
}

pub fn literal(text: &str) -> String {
    let mut out = String::with_capacity(text.len() + 2);
    out.push('"');

    for c in text.chars() {
        match c {
            '"' => out.push_str("\\\""),
            '\\' => out.push_str("\\\\"),
            '\n' => out.push_str("\\n"),
            '\r' => out.push_str("\\r"),
            '\t' => out.push_str("\\t"),
            '\u{8}' => out.push_str("\\b"),
            '\u{c}' => out.push_str("\\f"),
            c if (c as u32) < 0x20 => out.push_str(&format!("\\u{:04x}", c as u32)),
            c => out.push(c),
        }
    }

    out.push('"');
    out
}
