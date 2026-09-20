//! The **storage tuple codec** — the order-preserving encoding every stored byte
//! goes through, and the faults it can refuse with.
//!
//! Above [`fjord-schema`](fjord_schema) and below everything else: a store
//! writes these bytes, a plan compares against them, and the executor decodes
//! them, but the encoding itself knows about none of that. What it knows is a
//! type and a byte order.
//!
//! Design of record: [chapter 2](../../../web/src/content/storage.mdx), and the three
//! invariants it holds — [I1](../../../web/src/content/invariants.mdx#i1) order-preservation,
//! [I2](../../../web/src/content/invariants.mdx#i2) self-delimiting, and
//! [I3](../../../web/src/content/invariants.mdx#i3) the frozen marker table.
//!
//! This is deliberately **not** the transport codec: rows leaving the executor
//! are framed by a different, non-order-preserving encoding that never touches
//! stored bytes ([chapter 3](../../../web/src/content/storage.mdx#storage-codec-vs-transport-codec)).

pub mod error;
pub mod tuple;
