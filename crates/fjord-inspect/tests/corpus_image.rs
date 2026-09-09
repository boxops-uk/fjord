//! **An exported image of a real index, loaded and queried.**
//!
//! The batteries beside this one prove the load path against the database this
//! crate writes in Rust. That database is twelve facts chosen by hand; this is the
//! other question — whether a *real* index, twenty thousand facts written by Roslyn
//! against `schemas/dotnet.sigla`, survives the same trip.
//!
//! **Ignored, because the image is not in the repository and should not be.** It is
//! built by indexing a checkout, which needs MSBuild, a server and a minute — so it
//! is named by an environment variable and run on purpose:
//!
//! ```sh
//! ./clients/dotnet/index-repo.sh clients/dotnet/Boxops.Fjord.Client/Boxops.Fjord.Client.csproj code --styles
//! fjord --data-dir /tmp/fj-index/db finish 'code#net10.0'
//! fjord --data-dir /tmp/fj-index/db export 'code#net10.0' --to /tmp/fj-index/client.fjmem
//!
//! FJORD_CORPUS_IMAGE=/tmp/fj-index/client.fjmem \
//! FJORD_CORPUS_SCHEMA=schemas/dotnet.sigla \
//!     cargo test -p fjord-inspect --test corpus_image -- --ignored --nocapture
//! ```

use fjord_inspect::corpus;

#[test]
#[ignore = "not a guard: needs an exported image, built by indexing a checkout — see the module note"]
fn a_real_index_loads_from_its_image_and_answers() {
    let image_path =
        std::env::var("FJORD_CORPUS_IMAGE").expect("FJORD_CORPUS_IMAGE names the image to load");
    let schema_path = std::env::var("FJORD_CORPUS_SCHEMA")
        .expect("FJORD_CORPUS_SCHEMA names the schema it was written against");

    let image = std::fs::read(&image_path).expect("the image reads");

    // Composed the way the indexer composes it: `dotnet.sigla` reaches five files
    // by import, and following an import is sigla's job.
    let root = std::path::PathBuf::from(concat!(env!("CARGO_MANIFEST_DIR"), "/../.."));
    let resolved =
        fjord_schema::syntax::resolve::resolve(&root.join(&schema_path), &[root.join("schemas")])
            .expect("the schema resolves");
    let source = fjord_schema::syntax::print::print(&resolved.schema);

    let loaded = corpus::load(&image, &source);
    assert!(loaded.ok, "the image was refused: {:?}", loaded.problem);

    println!(
        "loaded {} rows, schema {}",
        loaded.rows,
        loaded.fingerprint.as_deref().unwrap_or("?")
    );

    // **Three questions a code-browsing UI actually asks**, each a different shape:
    // a whole-predicate scan, a join through a reference, and the search index.
    for query in [
        "P where F = src.File P",
        "{name = N, line = Ln} where codemarkup.SearchEntry {name = N, file = F, line = Ln}",
        "{path = P, line = L} where codemarkup.Definition {symbol = S, file = F}; \
         F = src.File P; src.FileLine {file = F, line = L}",
    ] {
        let answered = corpus::rows(query);

        assert!(
            answered.diagnostics.is_empty(),
            "{query}\n{:#?}",
            answered.diagnostics
        );
        assert!(!answered.rows.is_empty(), "{query} answered nothing");

        println!(
            "  {} row(s), {} examined — {query}",
            answered.rows.len(),
            answered.examined_total
        );
    }

    // ---- the code browser's own questions, over the same corpus ----------------

    use fjord_inspect::codeview;

    let files = codeview::files().expect("files answers");
    assert!(!files.is_empty(), "the index holds no files");
    println!(
        "\nfiles: {} — {:?}",
        files.len(),
        &files[..3.min(files.len())]
    );

    // The largest file, because the interesting failures are about size and order.
    let (path, blob) = files
        .iter()
        .map(|path| (path.clone(), codeview::blob(path).expect("a blob answers")))
        .max_by_key(|(_, blob)| blob.lines.len())
        .expect("some file is largest");

    println!(
        "blob {path}: {} lines, encoding {:?}",
        blob.lines.len(),
        blob.encoding
    );

    assert!(!blob.lines.is_empty(), "{path} has no lines");
    assert_eq!(
        blob.encoding,
        Some(codeview::StyleEncoding::RoslynLsp1),
        "the .NET indexer writes roslyn-lsp-1"
    );

    // **Lines arrive in order and none is missing.** A blob assembled from two
    // seeks and a zip can silently drop or reorder, and either renders as a file
    // that is subtly not the file.
    for (at, line) in blob.lines.iter().enumerate() {
        assert_eq!(
            line.line,
            at as i64 + 1,
            "{path} line {} arrived at position {at}",
            line.line
        );
    }

    // **Every run lies inside the line it colours**, and runs do not overlap. A
    // delta resolved wrongly produces runs that pile up or run off the end, and
    // both look plausible until you draw them.
    let mut coloured = 0;
    for line in &blob.lines {
        let width = line.text.chars().map(char::len_utf16).sum::<usize>() as u32;
        let mut previous_end = 0;

        for run in &line.runs {
            assert!(
                run.start >= previous_end,
                "{path}:{} run at {} overlaps the one ending at {previous_end}",
                line.line,
                run.start
            );
            assert!(
                run.start + run.length <= width,
                "{path}:{} run {}..{} runs past the line's {width} units",
                line.line,
                run.start,
                run.start + run.length
            );
            previous_end = run.start + run.length;
        }

        coloured += line.runs.len();
    }
    println!("  {coloured} coloured runs, all within their lines and in order");
    assert!(coloured > 0, "{path} decoded no runs at all");

    let outline = codeview::outline(&path).expect("an outline answers");
    println!("  outline: {} definitions", outline.len());
    assert!(!outline.is_empty(), "{path} declares nothing");

    let xrefs = codeview::xrefs(&path).expect("xrefs answer");
    println!("  xrefs: {} references", xrefs.len());

    // Go to definition, then find references, on a real symbol from the outline.
    let symbol = &outline[0].symbol;
    let definitions = codeview::definitions(symbol).expect("definitions answer");
    let references = codeview::references(symbol).expect("references answer");

    println!(
        "  {} → {} definition(s), {} reference(s)",
        outline[0].name,
        definitions.len(),
        references.len()
    );
    assert!(
        !definitions.is_empty(),
        "{symbol} is in the outline but has no definition"
    );

    // **A term too short to be fuzzy is still exact.** One character is within one
    // edit of the empty prefix, and every name starts with that — so a fuzzy `"f"`
    // would answer with the whole index rather than with anything about `f`.
    let hits = codeview::search("f").expect("search answers");
    println!("  search \"f\": {} hit(s)", hits.len());
    assert!(!hits.is_empty(), "nothing in this index starts with f");
    for hit in &hits {
        assert!(
            hit.name.to_lowercase().starts_with('f'),
            "{:?} is not a prefix hit for \"f\"",
            hit.name
        );
    }

    // **The typo a search box exists to forgive**, built from a name this index
    // actually holds rather than from one hardcoded here: take a long enough name,
    // mistype a letter in the middle of its opening, and the hit has to come back.
    // The exact prefix could not have found it — which is asserted, so that this
    // still fails if `~<` silently degrades to `..`.
    let long = hits
        .iter()
        .find(|hit| hit.name.chars().count() >= 8)
        .expect("some name in this index is eight characters or more");
    let opening: String = long.name.to_lowercase().chars().take(7).collect();
    let mistyped: String = opening
        .chars()
        .enumerate()
        .map(|(i, c)| {
            if i == 4 {
                if c == 'x' { 'y' } else { 'x' }
            } else {
                c
            }
        })
        .collect();

    assert!(
        !long.name.to_lowercase().starts_with(&mistyped),
        "{mistyped:?} is still a prefix of {:?}; the typo did not take",
        long.name
    );

    let forgiving = codeview::search(&mistyped).expect("a mistyped search answers");
    println!(
        "  search {mistyped:?} (for {:?}): {} hit(s)",
        long.name,
        forgiving.len()
    );
    assert!(
        forgiving.iter().any(|hit| hit.symbol == long.symbol),
        "{:?} was not found by {mistyped:?}, so the search is not fuzzy",
        long.name
    );
}
