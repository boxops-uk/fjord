//! **A cursor's world stamp** — what the database-owning layer says the base looked
//! like when a chunk was read, encoded to the opaque bytes
//! [`fjord_engine::iter::Cursor`] carries and compares.
//!
//! [I4](../../../website/content/invariants.md#i4) says a resume equals an uninterrupted
//! run; a cursor that names only a plan, a layout version and a level count cannot
//! keep that promise against a *different* database, the *same* database reopened,
//! or a Writable database a write has crossed since the cursor was made — the
//! engine has no way to notice any of the three, because `FactStore` is `scan` +
//! `point` and exposes neither an identity nor a listing.
//!
//! So this is computed here, one layer up, and handed to the engine as bytes it
//! never interprets. Two cases, and they answer different questions:
//!
//! - **`Complete`** — the content fingerprint `finish` already computed
//!   ([`identity::compute`](crate::identity::compute)), read from the sidecar. It
//!   cannot move: a Complete database is immutable forever, so a cursor stamped
//!   with it either matches now and forever or never will.
//! - **`Writable`** — `{ instance, incarnation, visible_seqno }`. The instance is
//!   the directory's own id; the sequence is fjall's own write counter, read
//!   through [`FjallDb::reader_stamped`](crate::store::FjallDb::reader_stamped) at
//!   the moment the snapshot a chunk reads was taken, so a write that lands between
//!   two chunks moves it and the next chunk's stamp disagrees. The incarnation is
//!   what a sequence number alone cannot be: `fjall` recovers its counter from
//!   whatever survived a reopen, which can be **lower** than a live cursor's
//!   stamp when the tail was written but never `persist`ed — so a bare sequence
//!   comparison could let a stale cursor land on reissued numbers over different
//!   content. A nonce minted fresh by every [`FjallDb::open`](crate::store::FjallDb::open)
//!   and held only in memory sidesteps reasoning about what a reopen recovered:
//!   every cursor from a previous incarnation is refused, unconditionally.

/// fjall's own write-position counter — see
/// [`FjallDb::reader_stamped`](crate::store::FjallDb::reader_stamped). Named here
/// rather than written as a bare `u64` because the two `Writable` fields it sits
/// beside are also `u64` and mean something entirely different.
pub type VisibleSeqno = u64;

/// What a database's base looked like when a chunk was read against it.
///
/// Compared for exact equality once encoded — see [`to_bytes`](Self::to_bytes) — so
/// the two variants can never accidentally agree: a `Writable` database that later
/// becomes `Complete` must not have an old `Writable`-tagged stamp read as a match
/// for its new `Complete` one, which is why the encoding tags the variant rather
/// than concatenating fields that happen to differ in width.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum BaseIdentity {
    /// `finish`'s content fingerprint. Never moves.
    Complete { fingerprint: u64 },
    /// A live handle's write position, bracketed at the moment a reader's snapshot
    /// was taken, and the incarnation that makes a reopen refuse rather than guess.
    Writable {
        instance: Box<str>,
        incarnation: u64,
        visible_seqno: VisibleSeqno,
    },
}

const TAG_COMPLETE: u8 = 1;
const TAG_WRITABLE: u8 = 2;

impl BaseIdentity {
    /// Encode to the bytes a [`Cursor`](fjord_engine::iter::Cursor) carries and
    /// compares — dull and self-describing, like every other wire-adjacent layout
    /// in this codebase: a tag, then fixed-width fields, then the instance id last
    /// because it is the only variable-length one.
    #[must_use]
    pub fn to_bytes(&self) -> Box<[u8]> {
        match self {
            BaseIdentity::Complete { fingerprint } => {
                let mut out = Vec::with_capacity(9);
                out.push(TAG_COMPLETE);
                out.extend_from_slice(&fingerprint.to_le_bytes());
                out.into_boxed_slice()
            }
            BaseIdentity::Writable {
                instance,
                incarnation,
                visible_seqno,
            } => {
                let instance = instance.as_bytes();
                let mut out = Vec::with_capacity(17 + instance.len());
                out.push(TAG_WRITABLE);
                out.extend_from_slice(&incarnation.to_le_bytes());
                out.extend_from_slice(&visible_seqno.to_le_bytes());
                out.extend_from_slice(instance);
                out.into_boxed_slice()
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn complete(fingerprint: u64) -> BaseIdentity {
        BaseIdentity::Complete { fingerprint }
    }

    fn writable(instance: &str, incarnation: u64, visible_seqno: VisibleSeqno) -> BaseIdentity {
        BaseIdentity::Writable {
            instance: instance.into(),
            incarnation,
            visible_seqno,
        }
    }

    /// The baseline every other case in this module is a variation of: two values
    /// built the same way encode the same, so a comparison at the engine's side of
    /// the opaque-bytes seam is comparing the thing this type actually means.
    #[test]
    fn identical_identities_encode_identically() {
        assert_eq!(complete(7).to_bytes(), complete(7).to_bytes());
        assert_eq!(
            writable("db-a", 1, 5).to_bytes(),
            writable("db-a", 1, 5).to_bytes()
        );
    }

    /// A Complete database's stamp is content only — it must not see the instance
    /// id, which is a directory name and not part of what `finish` hashed.
    #[test]
    fn two_complete_databases_with_the_same_content_encode_the_same() {
        assert_eq!(complete(42).to_bytes(), complete(42).to_bytes());
    }

    /// Every field a `Writable` stamp carries is load-bearing: changing any one of
    /// them alone must move the encoding, or a mismatch on that field would go
    /// unnoticed at the byte-comparison the engine actually performs.
    #[test]
    fn every_writable_field_moves_the_encoding() {
        let base = writable("db-a", 1, 5);
        assert_ne!(
            base.to_bytes(),
            writable("db-b", 1, 5).to_bytes(),
            "instance"
        );
        assert_ne!(
            base.to_bytes(),
            writable("db-a", 2, 5).to_bytes(),
            "incarnation"
        );
        assert_ne!(
            base.to_bytes(),
            writable("db-a", 1, 6).to_bytes(),
            "visible_seqno"
        );
    }

    /// A `Complete` stamp and a `Writable` one must never collide, whatever their
    /// fields happen to be — the tag byte is what makes that structural rather than
    /// a property of the sizes chosen today.
    #[test]
    fn complete_and_writable_never_collide() {
        assert_ne!(complete(1).to_bytes(), writable("x", 1, 1).to_bytes());

        // The adversarial case: a fingerprint whose bytes, read as an instance
        // string, are what a `Writable` stamp with everything else zeroed would
        // encode — a collision that only the tag byte stands between.
        let fingerprint = u64::from_le_bytes(*b"AAAAAAAA");
        let instance = "AAAAAAAA";
        assert_ne!(
            complete(fingerprint).to_bytes(),
            writable(instance, 0, 0).to_bytes()
        );
    }
}
