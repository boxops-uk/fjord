//! **Tagged literals** — `tag "body"`, one grammar alternative for every scalar
//! family that has a canonical text form.
//!
//! A family whose values are text with a grammar — `bytes` and its hex digits today,
//! a version or a timestamp next — used to need a token of its own: a lexer rule, an
//! LL(1) alternative, an ambiguity argument, and a spelling that could never be
//! withdrawn once it shipped. Paid per family, that is a tax on the one artifact
//! where a mistake is permanent.
//!
//! This is that syntax written once. `LId String` is a single `primary` alternative —
//! no new token, because both halves already exist and the whitespace between them is
//! already skipped — and the tag is resolved *here*, at lowering, against the table
//! below. A family then owes a parser, a canonical printer and its own diagnostic
//! codes, and owes the grammar nothing.
//!
//! # Three rules a family must follow
//!
//! 1. **The literal is text; the stored value is structure.** Two spellings of one
//!    value must lower to one value, or `ops-I5` interns them as two facts.
//! 2. **The ordering is over the structure, never over the text.** ISO 8601 is the
//!    trap this exists to warn about: lexical order is chronological order only over a
//!    normalised subset, and `…+01:00` is an hour *earlier* than `…Z` while sorting
//!    *later*.
//! 3. **Say per form whether a spec's "invalid" means reject or normalise**, because
//!    that is what decides whether `decode ∘ encode == id` holds over the input text
//!    or only over the value.
//!
//! # Why the tag is not a keyword
//!
//! It is an ordinary lowercase identifier, resolved against this table and nowhere
//! else. Reserving one word per family would grow the language's keyword vocabulary
//! every time a type is added, and would turn a field named `semver` into a parse
//! error in a schema with no versions in it.

use crate::lexer::{self, LiteralError};
use crate::syntax::Literal;

/// What a tag resolves to: the parser that turns a body into a value.
///
/// A function pointer rather than a trait object because a family is a compile-time
/// fact — the table is exhaustive at build time, and a family that is not in it does
/// not exist.
type Parse = fn(&str) -> Result<Literal, LiteralError>;

/// Every tag a scalar family has claimed, and what parses its body.
///
/// **Sorted, and the suggestion machinery depends on nothing but membership.** Adding
/// a family is one row; it needs no grammar change, no token, and no ambiguity
/// analysis.
const FAMILIES: &[(&str, Parse)] = &[("bytes", parse_bytes)];

/// `bytes "00ff"` — the same payload `0x00ff` names.
///
/// The tagged spelling is an *alias*: it lowers to the literal the `0x…` form lowers
/// to, and the printer keeps emitting `0x…`. So nothing downstream learns that two
/// spellings exist, which is rule 1 applied to the family that already had a literal
/// before this mechanism did.
fn parse_bytes(body: &str) -> Result<Literal, LiteralError> {
    lexer::parse_hex_digits(body).map(|payload| Literal::Bytes(payload.into()))
}

/// The parser for `tag`, or `None` if no family claims it.
#[must_use]
pub fn family(tag: &str) -> Option<Parse> {
    FAMILIES
        .iter()
        .find(|(name, _)| *name == tag)
        .map(|(_, parse)| *parse)
}

/// The closest claimed tag to `tag`, for a diagnostic to suggest.
///
/// **Bounded by the same edit distance a misspelled name is**, so a suggestion here
/// means what a suggestion anywhere else in the engine means. Without the bound an
/// unclaimed tag would always be "corrected" to whichever family happened to be
/// nearest, which is worse than saying nothing: `semver` is not a misspelling of
/// `bytes`.
#[must_use]
pub fn nearest(tag: &str) -> Option<&'static str> {
    // Smallest distance first, so the answer is the *closest* family rather than
    // whichever the table happens to list first.
    (1..=crate::levenshtein::MAX_DISTANCE).find_map(|distance| {
        FAMILIES
            .iter()
            .map(|(name, _)| *name)
            .find(|name| *name != tag && crate::levenshtein::within(tag, name, distance))
    })
}

/// Every claimed tag, for a diagnostic that lists what is available.
pub fn claimed() -> impl Iterator<Item = &'static str> {
    FAMILIES.iter().map(|(name, _)| *name)
}

#[cfg(test)]
mod tests {
    use super::*;

    /// The table is the extension point, so its shape is asserted rather than assumed:
    /// a family that is in it resolves, and one that is not does not exist.
    #[test]
    fn a_claimed_tag_resolves_and_an_unclaimed_one_does_not() {
        assert!(family("bytes").is_some());
        assert!(family("semver").is_none(), "no family claims this yet");
        assert!(family("").is_none());
    }

    /// A suggestion is only offered inside the engine's own edit-distance bound —
    /// otherwise every unclaimed tag would be "corrected" to the nearest family.
    #[test]
    fn a_suggestion_is_bounded_by_edit_distance() {
        assert_eq!(nearest("bytez"), Some("bytes"));
        assert_eq!(nearest("byte"), Some("bytes"));
        assert_eq!(
            nearest("semver"),
            None,
            "not a misspelling of anything here"
        );
        assert_eq!(nearest("bytes"), None, "an exact match is not a suggestion");
    }
}
