//! **One fact encoding, not two** — the claim
//! [operations §8](https://github.com/boxops-uk/fjord/blob/main/website/content/operations.md) makes about the wire and the
//! fact file, checked rather than asserted.
//!
//! An integration test rather than a unit one for two reasons. It spans `value`,
//! `block` and `frame`, so it belongs to none of them; and it exercises the crate
//! from outside, which is the position a client is in — the one place a `pub` that
//! should not be, or a type a caller cannot construct, shows up.

use fjord_schema::{
    id::FactId,
    schema::{Predicate, PredicateId, PredicateTy, Schema},
};
use fjord_wire::{
    FrameKind, StreamId, WireFact, WireRef, WireValue, block, decode_block,
    decode_frame, encode_block, encode_frame,
};
use lasso::Rodeo;
use std::sync::Arc;

/// A two-predicate code index: files, and declarations that reference one.
fn schema() -> Schema {
    let mut rodeo = Rodeo::new();
    let (file, decl) = (
        rodeo.get_or_intern("src.File"),
        rodeo.get_or_intern("src.Decl"),
    );
    let (f_file, f_line, f_name) = (
        rodeo.get_or_intern("file"),
        rodeo.get_or_intern("line"),
        rodeo.get_or_intern("name"),
    );

    Schema::new(
        rodeo.into_reader(),
        Arc::from(vec![
            Predicate {
                name: file,
                key: PredicateTy::Str,
                value: None,
            },
            Predicate {
                name: decl,
                // Sorted by name, as everywhere: file, line, name.
                key: PredicateTy::Record(
                    vec![
                        (f_file, PredicateTy::Fact(PredicateId(0))),
                        (f_line, PredicateTy::Int),
                        (f_name, PredicateTy::Str),
                    ]
                    .into(),
                ),
                value: None,
            },
        ]),
    )
}

fn decl(file: WireRef, line: i64, name: &str) -> WireFact {
    WireFact {
        predicate: PredicateId(1),
        key: WireValue::Record(
            vec![
                WireValue::Ref(file),
                WireValue::Int(line),
                WireValue::Str(name.to_owned()),
            ]
            .into(),
        ),
        value: None,
    }
}

fn file(path: &str) -> WireFact {
    WireFact {
        predicate: PredicateId(0),
        key: WireValue::Str(path.to_owned()),
        value: None,
    }
}

/// **The same bytes ride a frame and sit in a file.**
///
/// One block is encoded once. It is then read two ways: as the payload of a
/// `CopyData` frame, the way a write stream carries it, and as a run of a fact file,
/// the way the bulk path will. Both yield the same facts, because there is one
/// encoding — which is what stops the file format and the wire format drifting into
/// two things that have to be kept in step by hand.
#[test]
fn a_block_is_the_same_bytes_on_a_socket_and_in_a_file() {
    let schema = schema();

    // A declaration whose reference is *nested* — what an indexer sends, holding no
    // ids at all — and one whose reference is an id, from a producer that has one.
    let nested = decl(
        WireRef::Nested(Box::new(file("store/keys.py"))),
        12,
        "key_of",
    );
    let by_id = decl(
        WireRef::Id(FactId::new(PredicateId(0), 3).expect("an id")),
        48,
        "Store.put",
    );

    let facts = vec![nested, by_id];

    let mut block_bytes = vec![];
    encode_block(&mut block_bytes, &schema, PredicateId(1), &facts).expect("a block");

    // On the wire: the block is a CopyData frame's payload, and nothing else.
    let mut wire = vec![];
    encode_frame(&mut wire, FrameKind::COPY_DATA, StreamId(1), &block_bytes).expect("a frame");

    let (header, payload, used) = decode_frame(&wire).expect("the frame decodes");
    assert_eq!(header.kind, FrameKind::COPY_DATA);
    assert_eq!(used, wire.len());
    assert_eq!(
        payload,
        &block_bytes[..],
        "a frame carries the block verbatim"
    );

    let (from_wire, _) = decode_block(payload, &schema).expect("the block decodes");

    // In a file: the same block bytes, read as themselves. A file is a run of blocks
    // walked by the lengths their headers declare — `--emit` writes one, and the
    // byte-identical golden reads it.
    let file_bytes = block_bytes.clone();
    let (from_file, used) = decode_block(&file_bytes, &schema).expect("the block decodes");
    assert_eq!(used, file_bytes.len(), "the block is the whole file");

    assert_eq!(from_wire, facts);
    assert_eq!(from_file, facts);
}

/// The overhead is what the format says it is, and small against a real block —
/// worth pinning, because a marker and a header are the price paid for splittability
/// and the trade is only good while it stays this size.
#[test]
fn framing_overhead_is_thirty_bytes_a_block_and_nine_a_frame() {
    let schema = schema();

    assert_eq!(block::OVERHEAD, 30);
    assert_eq!(fjord_wire::frame::HEADER_LEN, 9);

    let facts: Vec<WireFact> = (0..100)
        .map(|n| {
            decl(
                WireRef::Id(FactId::new(PredicateId(0), 1).expect("an id")),
                n,
                "some_declaration_name",
            )
        })
        .collect();

    let mut block_bytes = vec![];
    encode_block(&mut block_bytes, &schema, PredicateId(1), &facts).expect("a block");

    let payload = block_bytes.len() - block::OVERHEAD;
    assert!(
        block::OVERHEAD * 50 < payload,
        "framing is {} B against {payload} B of facts, which is more than 2% overhead",
        block::OVERHEAD
    );
}
