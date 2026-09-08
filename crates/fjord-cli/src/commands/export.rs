//! `fjord export <db> --to <path>`.
//!
//! A database as a **store image**: every row, with the id it already has, in the
//! shape [`fjord_store_mem::MemStore`] is rebuilt from.
//!
//! **This exists because nothing in a browser can intern a fact.** `fjord-ingest`
//! depends on the fjall backend by name — interning claims ids durably and writes
//! through a batch — so the write funnel is not reachable from a WebAssembly build,
//! and a corpus cannot be assembled in the page. It does not need to be: ids are
//! assigned once, and a database indexed here already holds the answer. This is how
//! the answer travels.
//!
//! **Offline only, and that is `ops-I1` rather than a limitation.** Reading every row
//! means opening fjall, and one process owns a root — so a server holding it is a
//! refusal with something to do about it, never a second opener.

use std::path::Path;

use fjord_schema::fingerprint;
use fjord_store_fjall::catalog::{Intent, Selector};
use fjord_store_mem::dump::{self, OwnedRow};

use crate::{
    CliError,
    commands::{self, Route, Target},
};

/// What an export did, for the line the tool prints.
pub struct Exported {
    pub rows: usize,
    pub bytes: usize,
    pub fingerprint: u64,
}

/// # Errors
///
/// [`CliError::RootHeld`] or a server refusal if this process may not open the root,
/// [`CliError::NoEmbeddedSchema`] for a database carrying no schema copy to key its
/// rows against, and whatever scanning or writing the file reports.
pub fn run(root: &Path, target: &Target, to: &Path) -> Result<Exported, CliError> {
    // Routed like every other command that opens the store, so "a server holds this
    // root" is answered by the rule rather than by a second opener finding out.
    let catalog = match commands::route(root, target)? {
        Route::Local(catalog, _lock) => catalog,
        Route::Server(_) => return Err(CliError::ExportNeedsTheRoot),
    };

    let entry = catalog.resolve(&Selector::parse(&target.database)?, Intent::Read)?;

    // **The embedded copy, never a schema passed in.** A predicate is looked up by
    // position, so keying an image against any other schema would record every row
    // under whatever type sits at that position — silently. `finish` refuses a passed
    // schema for the same reason.
    let schema = fjord_store_fjall::schema_doc::read(&entry.path)?.ok_or_else(|| {
        CliError::NoEmbeddedSchema {
            name: target.database.clone(),
        }
    })?;

    let db = entry.open_store()?;
    // One reader, so every predicate is scanned against one snapshot: an image of a
    // database that moved under the walk would hold rows from two states.
    let reader = db.reader();
    let rows = dump::rows_of(&reader, u32::try_from(schema.len()).unwrap_or(u32::MAX))?;
    let fingerprint = fingerprint::of(&schema);
    let image = dump::write(fingerprint, rows.iter().map(OwnedRow::as_row));

    std::fs::write(to, &image)?;

    Ok(Exported {
        rows: rows.len(),
        bytes: image.len(),
        fingerprint,
    })
}
