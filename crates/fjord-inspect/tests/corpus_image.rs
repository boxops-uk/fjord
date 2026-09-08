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
}
