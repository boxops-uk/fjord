//! **A peer does not choose how much stack the decoder uses.**
//!
//! A nested reference carries a whole fact, whose key may carry another. A schema whose
//! reference graph cycles — `Node { parent : Node }`, or `csharp.Class` and `Interface`
//! naming each other, which is every shipped index — lets that repeat as far as the
//! sender likes, and the decoder followed it with its own recursion.
//!
//! Twenty kilobytes on a socket was enough to overflow the stack, which aborts the
//! **process**: not the stream, not the database, the server. A message must not be able
//! to do that, so the depth is counted and refused.

use fjord_schema::{
    id::FactId,
    schema::{Predicate, PredicateId, PredicateTy, Schema},
};
use fjord_wire::{WireFact, WireRef, WireValue, value};
use lasso::Rodeo;
use std::sync::Arc;

/// `s.Node : { parent : Node }` — a predicate that names itself, which sigla allows and
/// the shipped schemas use.
fn self_referencing() -> Schema {
    let mut rodeo = Rodeo::new();
    let name = rodeo.get_or_intern("s.Node");
    let parent = rodeo.get_or_intern("parent");

    Schema::new(
        rodeo.into_reader(),
        Arc::from([Predicate {
            name,
            key: PredicateTy::Record(Arc::from([(parent, PredicateTy::Fact(PredicateId(0)))])),
            value: None,
        }]),
    )
}

/// A chain of `deep` nested references, bottoming out in an id.
///
/// Built iteratively so that composing the fixture cannot be what overflows.
fn a_chain(deep: usize) -> WireFact {
    let mut fact = WireFact {
        predicate: PredicateId(0),
        key: WireValue::Record(Box::from([WireValue::Ref(WireRef::Id(
            FactId::new(PredicateId(0), 1).expect("an id"),
        ))])),
        value: None,
    };

    for _ in 0..deep {
        fact = WireFact {
            predicate: PredicateId(0),
            key: WireValue::Record(Box::from([WireValue::Ref(WireRef::Nested(Box::new(fact)))])),
            value: None,
        };
    }

    fact
}

/// **A chain past the limit is refused, and the process is still running to say so.**
///
/// The bytes are composed by hand rather than by [`value::to_bytes`], and they have to
/// be: the encoder keeps the same bound, so this library cannot produce the message a
/// hostile peer sends. One level of nesting is exactly one byte — the `REF_NESTED`
/// form, as a varint — so a legal chain with more of its own leading byte in front is
/// the same message, deeper.
#[test]
fn a_chain_deeper_than_the_limit_is_refused() {
    let schema = self_referencing();

    let legal = value::to_bytes(&schema, &a_chain(value::MAX_NESTED_DEPTH)).expect("it encodes");
    let shallower = value::to_bytes(&schema, &a_chain(value::MAX_NESTED_DEPTH - 1)).expect("ditto");

    // Stated rather than assumed: one level is one byte, and it is the leading one.
    assert_eq!(legal.len(), shallower.len() + 1);
    assert_eq!(&legal[1..], &shallower[..]);

    let mut deeper = vec![legal[0]; 1_000];
    deeper.extend_from_slice(&legal);

    let refused = value::decode_fact(&deeper, &schema, PredicateId(0));

    assert!(
        matches!(
            refused,
            Err(fjord_wire::WireError::TooDeep { max }) if max == value::MAX_NESTED_DEPTH
        ),
        "{refused:?}"
    );
}

/// **And a chain at the limit still decodes**, so the bound is a bound and not a ban.
///
/// Asserted at exactly the limit rather than comfortably inside it: a fence checked only
/// from far away is a fence nobody has found the position of, and an off-by-one here
/// would refuse data an indexer legitimately sends.
#[test]
fn a_chain_at_the_limit_still_decodes() {
    let schema = self_referencing();
    let deepest = a_chain(value::MAX_NESTED_DEPTH);

    let bytes = value::to_bytes(&schema, &deepest).expect("the fixture encodes");
    let (decoded, _) = value::decode_fact(&bytes, &schema, PredicateId(0)).expect("it decodes");
    assert_eq!(decoded, deepest);
}

/// **A producer is told by its own library**, rather than by the far end or by a crash.
///
/// The encoder keeps the bound too, so a client composing a fact too deep to send finds
/// out while composing it — and cannot overflow its own stack doing so.
#[test]
fn composing_a_chain_past_the_limit_is_refused_too() {
    let schema = self_referencing();
    let refused = value::to_bytes(&schema, &a_chain(value::MAX_NESTED_DEPTH + 1));

    assert!(
        matches!(
            refused,
            Err(fjord_wire::WireError::TooDeep { max }) if max == value::MAX_NESTED_DEPTH
        ),
        "{refused:?}"
    );
}
