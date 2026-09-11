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
    // The name a line gave itself travels with its fact, because grouping below
    // reorders them and a name matched back by position would follow the wrong fact.
    let mut batch: Vec<(PredicateId, WireFact, Option<String>)> = Vec::new();
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

            let (predicate, fact, named) = seen
                .line(&schema, trimmed)
                .map_err(|why| at_line(file, at + 1, &why))?;

            batch.push((predicate, fact, named));
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
    batch: &mut Vec<(PredicateId, WireFact, Option<String>)>,
    written: &mut Written,
) -> Result<(), CliError> {
    if batch.is_empty() {
        return Ok(());
    }

    // **Grouped, because a block is a run of one predicate.** Each fact keeps the name
    // its line gave it, so the ids that come back can be attributed without relying on
    // the read order surviving the grouping — which it does not.
    let mut grouped: std::collections::BTreeMap<u32, Vec<(WireFact, Option<String>)>> =
        std::collections::BTreeMap::new();

    for (predicate, fact, named) in batch.drain(..) {
        grouped.entry(predicate.0).or_default().push((fact, named));
    }

    // Sent in this order, so the ids come back in it.
    let sending: Vec<(PredicateId, Vec<WireFact>)> = grouped
        .iter()
        .map(|(id, facts)| {
            (
                PredicateId(*id),
                facts.iter().map(|(fact, _)| fact.clone()).collect(),
            )
        })
        .collect();

    let blocks: Vec<(PredicateId, &[WireFact])> = sending
        .iter()
        .map(|(id, facts)| (*id, facts.as_slice()))
        .collect();

    // **Asked for, because a later line may reference one of these.**
    let (sent, ids) = connection.write_blocks_reporting_ids(&blocks)?;

    let named_in_send_order = grouped.into_iter().flat_map(|(id, facts)| {
        facts
            .into_iter()
            .map(move |(_, named)| (PredicateId(id), named))
    });

    for ((predicate, named), id) in named_in_send_order.zip(&ids) {
        if let Some(named) = named {
            seen.promote(&named, predicate, *id);
        }
    }

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
