//! `fjord fact <db> <id>…` — what a fact id names.
//!
//! **The read-path twin of `--expand`, for an id you already have.** A row hands you
//! `#4:1` and until now there was nothing to spend it on: the protocol has carried the
//! question since v4 (`F`/`f`), the client has used it to expand references, and no
//! command asked it directly. This is that command.
//!
//! A *command* rather than sigla syntax, and deliberately. A query names a fact by its
//! key, because a key is the logical form the content hash is computed over and the one
//! thing that survives a rebuild — `ops-I4` says in as many words that ids are
//! "descriptive, never identity", so a *saved* query pinned to `#4:1` would break
//! against a database the project calls unchanged. Asking at a prompt is a different
//! act from writing it down, and keeping the two apart is what stops an id reaching a
//! stored derivation.

use std::sync::Arc;

use fjord_client::Mode;
use fjord_schema::{
    id::FactId,
    schema::{PredicateId, Schema},
};
use fjord_wire::{Desc, protocol::Found};

use crate::{CliError, cli::RowFormat, commands::Target, rows::Sink};

/// An id as a person writes one: `code.Decl#1`, the spelling every renderer prints.
///
/// `#4:1` is taken too — the predicate's *number* and its sequence, which is what a row
/// prints when the schema cannot name the predicate, and therefore the one form that is
/// always available to paste back.
///
/// # Errors
///
/// A message naming the shape, because this is the one place a typo is likely and a
/// parser error would say less than an example does.
pub fn parse(schema: &Schema, text: &str) -> Result<FactId, String> {
    let (predicate, sequence) = text
        .rsplit_once('#')
        .ok_or_else(|| format!("`{text}` is not a fact id — they are written `code.Decl#1`"))?;

    // `#4:1` — a leading `#` leaves the predicate side empty and puts its number after
    // the colon. Checked first, because the sequence half of that form is `4:1` and
    // would fail to parse as a number before ever reaching this. Kept because it is
    // what a row prints against a schema that cannot name the predicate, and refusing
    // to read back what we just wrote would be perverse.
    if predicate.is_empty() {
        let (number, sequence) = sequence_after_colon(text)?;
        return FactId::new(PredicateId(number), sequence)
            .map_err(|why| format!("`{text}` is not a fact id: {why}"));
    }

    let sequence: u64 = sequence
        .parse()
        .map_err(|_| format!("`{sequence}` is not a sequence number, in `{text}`"))?;

    let (id, _) = schema
        .find_position(predicate)
        .ok_or_else(|| format!("`{predicate}` is not a predicate in this database's schema"))?;

    FactId::new(id, sequence).map_err(|why| format!("`{text}` is not a fact id: {why}"))
}

/// The `#predicate:sequence` form, where both halves are numbers.
fn sequence_after_colon(text: &str) -> Result<(u32, u64), String> {
    let body = text.strip_prefix('#').unwrap_or(text);
    let (predicate, sequence) = body
        .split_once(':')
        .ok_or_else(|| format!("`{text}` is not a fact id — they are written `code.Decl#1`"))?;

    Ok((
        predicate
            .parse()
            .map_err(|_| format!("`{predicate}` is not a predicate number, in `{text}`"))?,
        sequence
            .parse()
            .map_err(|_| format!("`{sequence}` is not a sequence number, in `{text}`"))?,
    ))
}

/// Ask the server what each id names, and print the keys in the order asked.
///
/// # Errors
///
/// Whatever connecting, fetching or writing reports. A virtual id — one of the
/// `fjord.db.*` catalogue rows — is refused by the server rather than answered, because
/// those ids are positions in a listing and this command has no listing to name.
pub fn run(target: &Target, written: &[String], format: RowFormat) -> Result<(), CliError> {
    let mut connection = super::query::connect(target, Mode::ReadOnly)?;
    let schema = Arc::new(connection.served_schema()?);

    // Resolved here rather than by clap, because `code.Decl#1` names a predicate and
    // only the schema knows what that is.
    let ids = written
        .iter()
        .map(|text| parse(&schema, text))
        .collect::<Result<Vec<_>, _>>()
        .map_err(|why| CliError::BadFactId { why })?;
    let ids = &ids[..];

    let found = connection.fetch(&schema, ids, None)?;
    let stdout = std::io::stdout();
    let mut out = stdout.lock();

    for (id, answer) in ids.iter().zip(found) {
        match answer {
            Found::Key(key) => {
                // Rendered through the same sink a query's rows go through, so an id
                // and a row that mentions it read the same way — and `--format` means
                // here what it means there.
                let desc = key_desc(&schema, id.predicate());
                let mut sink = Sink::naming(
                    &mut out,
                    format,
                    desc.as_ref().unwrap_or(&Desc::Str),
                    Arc::clone(&schema),
                )?;
                sink.row(&key)?;
                sink.end()?;
            }

            // **Not an error.** An id that names nothing is an answer about the
            // database, and printing it beside the ones that resolved is more use than
            // failing the whole command on the first gap.
            Found::Missing => eprintln!("{} names no fact", show(&schema, *id)),
            Found::Unstored => eprintln!(
                "{} is a row nothing stores — a catalogue id is a position in a listing, \
                 and this command has no listing to read it against",
                show(&schema, *id)
            ),
        }
    }

    Ok(())
}

/// The one spelling, shared with every renderer.
fn show(schema: &Schema, id: FactId) -> String {
    crate::rows::fact_id(Some(schema), id)
}

/// A predicate's key, as a descriptor — the shape the fetched value has.
fn key_desc(schema: &Schema, predicate: PredicateId) -> Option<Desc> {
    let key = &schema.get(predicate)?.predicate().key;
    Desc::of(schema, key).ok()
}
