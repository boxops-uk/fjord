//! `fjord write <db> <file.jsonl>`.
//!
//! **The simple way in, and deliberately not the fast one.** A producer writing at volume
//! speaks the protocol through a client library, where a fact is encoded once and a block
//! carries hundreds of them. This reads a text file a person or an agent wrote, one JSON
//! object per line, and sends it over the same socket everything else does — so it is a
//! client like any other and the server cannot tell the difference.
//!
//! The format, and the reason a reference names an earlier line, are [`crate::jsonl`]'s.

use std::path::{Path, PathBuf};

use fjord_client::Mode;
use fjord_schema::schema::PredicateId;
use fjord_wire::WireFact;

use crate::{CliError, commands::Target, jsonl};

/// What a write came to, for the line the tool prints.
pub struct Written {
    pub lines: u64,
    pub created: u64,
    pub deduped: u64,
}

/// **Facts held before a flush.** A block is a run of one predicate, so the reader groups
/// as it goes and sends when the batch is full — which keeps a large file from being
/// entirely resident, without making a block per line.
const BATCH: usize = 10_000;

/// # Errors
///
/// [`CliError::NoServer`] if nothing is listening — a write needs a server, because
/// interning is the backend's. [`CliError::Diagnosed`] for a line that does not fit the
/// schema, naming the file and the line number, or whatever the server reports.
pub fn run(target: &Target, files: &[PathBuf]) -> Result<Written, CliError> {
    // **Asked first, on a session of its own.** A connection encodes against the schema
    // it was opened with, so the shapes have to be settled before the writer exists.
    let schema = {
        let mut asking = crate::commands::query::connect(target, Mode::ReadOnly)?;
        std::sync::Arc::new(asking.served_schema()?)
    };

    let mut connection = crate::commands::query::connect_with(
        target,
        Mode::ReadWrite,
        std::sync::Arc::clone(&schema),
    )?;

    let mut seen = jsonl::Seen::default();
    let mut batch: Vec<(PredicateId, WireFact)> = Vec::new();
    let mut written = Written {
        lines: 0,
        created: 0,
        deduped: 0,
    };

    for file in files {
        let text = std::fs::read_to_string(file)
            .map_err(|why| CliError::Diagnosed(format!("fjord: {}: {why}\n", file.display())))?;

        for (at, line) in text.lines().enumerate() {
            // A blank line is nothing, and a `#` line is a note somebody left themselves.
            // Neither is an error in a file a person is expected to edit.
            let trimmed = line.trim();
            if trimmed.is_empty() || trimmed.starts_with('#') {
                continue;
            }

            let (predicate, fact) = seen
                .line(&schema, trimmed)
                .map_err(|why| at_line(file, at + 1, &why))?;

            batch.push((predicate, fact));
            written.lines += 1;

            if batch.len() >= BATCH {
                flush(&mut connection, &mut seen, &mut batch, &mut written)?;
            }
        }
    }

    flush(&mut connection, &mut seen, &mut batch, &mut written)?;
    Ok(written)
}

/// Send what has been read, one block per predicate.
fn flush(
    connection: &mut fjord_client::Connection,
    seen: &mut jsonl::Seen,
    batch: &mut Vec<(PredicateId, WireFact)>,
    written: &mut Written,
) -> Result<(), CliError> {
    if batch.is_empty() {
        return Ok(());
    }

    // **Grouped, because a block is a run of one predicate** — and the order within a
    // group is the order the lines were read in, which is the order the ids come back in.
    let mut grouped: std::collections::BTreeMap<u32, Vec<WireFact>> =
        std::collections::BTreeMap::new();

    for (predicate, fact) in batch.iter() {
        grouped.entry(predicate.0).or_default().push(fact.clone());
    }

    let blocks: Vec<(PredicateId, &[WireFact])> = grouped
        .iter()
        .map(|(id, facts)| (PredicateId(*id), facts.as_slice()))
        .collect();

    // **Asked for, because a later line may reference one of these.** Grouping reorders
    // the facts, so what is handed back is matched against the order actually sent rather
    // than against the order they were read.
    let (sent, ids) = connection.write_blocks_reporting_ids(&blocks)?;

    let in_send_order: Vec<(PredicateId, WireFact)> = grouped
        .into_iter()
        .flat_map(|(id, facts)| facts.into_iter().map(move |fact| (PredicateId(id), fact)))
        .collect();

    seen.written(&in_send_order, &ids);
    batch.clear();

    written.created += sent.created;
    written.deduped += sent.deduped;
    Ok(())
}

/// A refusal, with the file and line it is about.
///
/// Rendered rather than returned as a sentence, because a line number is the whole of
/// what makes a refusal actionable in a file somebody is editing by hand.
fn at_line(file: &Path, line: usize, why: &str) -> CliError {
    CliError::Diagnosed(format!("fjord: {}:{line}: {why}\n", file.display()))
}
