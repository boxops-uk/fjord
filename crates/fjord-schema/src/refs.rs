//! **Which predicates name which** — the schema's reference graph, and an order over it.
//!
//! A predicate's key or value may hold a `PredicateTy::Fact(target)`, so a schema is a
//! directed graph over its own predicates. Two callers need it and neither is the other's
//! business: [`crate::fingerprint`] has to hash a referent before anything spelling it,
//! and an exporter has to write a fact out after everything it names. Both want the same
//! thing — **every component after the components it reaches** — so it is stated once.
//!
//! # The graph can cycle, and that is why the unit is a component
//!
//! `predicate Node : { parent : Node }` is legal and so is a pair naming each other:
//! nothing in the schema forbids it, because the *facts* are what cannot cycle — a key's
//! bytes do not exist until the facts it references have ids. So a plain topological sort
//! is not available, and the condensation is: [`components`] answers strongly-connected
//! components, each after everything it reaches, and a caller decides what a component
//! larger than one predicate means for it.

use std::collections::{BTreeMap, BTreeSet};

use crate::schema::{PredicateId, PredicateTy, Schema};

/// Whether a predicate's own types mention it.
///
/// A component of one is still a cycle when this is true, and a caller that treats a
/// one-member component as "nothing to order" would be wrong about exactly these.
#[must_use]
pub fn self_referencing(schema: &Schema, id: PredicateId) -> bool {
    references(schema, id).contains(&id)
}

/// Every predicate a predicate's types mention.
#[must_use]
pub fn references(schema: &Schema, id: PredicateId) -> BTreeSet<PredicateId> {
    fn walk(ty: &PredicateTy, into: &mut BTreeSet<PredicateId>) {
        match ty {
            PredicateTy::Fact(target) => {
                into.insert(*target);
            }
            PredicateTy::Record(fields) => {
                for (_, field) in fields.iter() {
                    walk(field, into);
                }
            }
            PredicateTy::Union(alts) => {
                for alt in alts.iter() {
                    walk(&alt.ty, into);
                }
            }
            PredicateTy::Int | PredicateTy::Str | PredicateTy::Bytes => {}
        }
    }

    let mut out = BTreeSet::new();
    if let Some(predicate) = schema.get(id) {
        walk(&predicate.predicate().key, &mut out);
        if let Some(value) = predicate.predicate().value.as_ref() {
            walk(value, &mut out);
        }
    }
    out
}

/// Strongly-connected components over `ids`, **dependencies first**.
///
/// Only `ids` are nodes, and an edge leaving the set is dropped — a caller passing the
/// stored predicates gets an order over those, and a reference to a virtual predicate is
/// not an edge because there is nothing stored behind it to come first.
///
/// Tarjan's, written iteratively: a schema is a data path, and a recursive walk over one
/// deep enough would be a stack overflow where
/// [conventions](https://github.com/boxops-uk/fjord/blob/main/AGENTS.md) requires an error. Tarjan emits each
/// component only after everything it reaches, which is exactly the order both callers
/// need.
#[must_use]
pub fn components(schema: &Schema, ids: &[PredicateId]) -> Vec<Vec<PredicateId>> {
    #[derive(Default, Clone)]
    struct Node {
        index: Option<usize>,
        low: usize,
        on_stack: bool,
    }

    let nodes: BTreeSet<PredicateId> = ids.iter().copied().collect();
    let edges: BTreeMap<PredicateId, Vec<PredicateId>> = nodes
        .iter()
        .map(|id| {
            let mut targets: Vec<PredicateId> = references(schema, *id)
                .into_iter()
                .filter(|target| nodes.contains(target))
                .collect();
            targets.sort();
            (*id, targets)
        })
        .collect();

    let mut state: BTreeMap<PredicateId, Node> =
        nodes.iter().map(|id| (*id, Node::default())).collect();
    let mut stack: Vec<PredicateId> = vec![];
    let mut out: Vec<Vec<PredicateId>> = vec![];
    let mut next = 0usize;

    for root in &nodes {
        if state[root].index.is_some() {
            continue;
        }

        // (node, how many of its edges have been taken)
        let mut work: Vec<(PredicateId, usize)> = vec![(*root, 0)];

        while let Some((node, edge)) = work.pop() {
            if edge == 0 {
                let entry = state.get_mut(&node).expect("known node");
                entry.index = Some(next);
                entry.low = next;
                entry.on_stack = true;
                next += 1;
                stack.push(node);
            }

            let targets = &edges[&node];

            if edge < targets.len() {
                let target = targets[edge];
                work.push((node, edge + 1));

                match state[&target].index {
                    None => work.push((target, 0)),
                    Some(index) => {
                        if state[&target].on_stack {
                            let low = state[&node].low.min(index);
                            state.get_mut(&node).expect("known node").low = low;
                        }
                    }
                }
                continue;
            }

            // Every edge taken: close the node, and propagate its low-link upward.
            if state[&node].low == state[&node].index.expect("visited") {
                let mut group = vec![];
                while let Some(member) = stack.pop() {
                    state.get_mut(&member).expect("known node").on_stack = false;
                    group.push(member);
                    if member == node {
                        break;
                    }
                }
                group.sort();
                out.push(group);
            }

            if let Some((parent, _)) = work.last().copied() {
                let low = state[&parent].low.min(state[&node].low);
                state.get_mut(&parent).expect("known node").low = low;
            }
        }
    }

    out
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::syntax::{lower::lower, parse::parse};

    fn schema_of(source: &str) -> Schema {
        let mut diags = vec![];
        let cst = parse(source, &mut diags).expect("parses");
        let lowered = lower(&cst, &mut diags).expect("lowers");
        assert!(diags.is_empty(), "{diags:?}");
        lowered.schema
    }

    fn all(schema: &Schema) -> Vec<PredicateId> {
        (0..schema.len())
            .map(|index| PredicateId(index as u32))
            .collect()
    }

    fn named(schema: &Schema, order: &[Vec<PredicateId>]) -> Vec<Vec<String>> {
        order
            .iter()
            .map(|group| {
                group
                    .iter()
                    .filter_map(|id| schema.get(*id)?.name().map(str::to_owned))
                    .collect()
            })
            .collect()
    }

    /// **A referent comes out before anything that names it**, which is the whole of
    /// what both callers ask of this.
    ///
    /// Declared in the order that makes the answer wrong if the walk were declaration
    /// order: `Decl` is first and names `File`, which is last.
    #[test]
    fn a_component_follows_everything_it_reaches() {
        let schema = schema_of(
            "schema s {
               predicate Decl : { file : File, name : string }
               predicate Doc : { decl : Decl } -> { text : string }
               predicate File : string
             }",
        );

        let order = named(&schema, &components(&schema, &all(&schema)));
        assert_eq!(
            order,
            vec![
                vec!["s.File".to_owned()],
                vec!["s.Decl".to_owned()],
                vec!["s.Doc".to_owned()]
            ],
            "{order:?}"
        );
    }

    /// **A cycle comes out as one component rather than as an order that cannot exist.**
    ///
    /// The schema allows it — the facts are what cannot cycle — so a caller has to be
    /// handed the group and left to decide, not handed an arbitrary order within it.
    #[test]
    fn predicates_that_name_each_other_are_one_component() {
        let schema = schema_of(
            "schema s {
               predicate A : { b : B } -> { n : int }
               predicate B : { a : A } -> { n : int }
               predicate C : { a : A } -> { n : int }
             }",
        );

        let order = named(&schema, &components(&schema, &all(&schema)));
        assert_eq!(
            order,
            vec![
                vec!["s.A".to_owned(), "s.B".to_owned()],
                vec!["s.C".to_owned()]
            ],
            "{order:?}"
        );
    }

    /// **A predicate that names itself is a component of one that is still a cycle.**
    ///
    /// The size of the group does not say whether there is an order inside it, which is
    /// why [`self_referencing`] is public beside [`components`]: a caller reading only
    /// the length would order these wrongly and never find out.
    #[test]
    fn a_self_reference_is_not_visible_in_the_component_size() {
        let schema = schema_of(
            "schema s {
               predicate Node : { parent : Node, name : string }
             }",
        );

        let node = PredicateId(0);
        let order = components(&schema, &all(&schema));

        assert_eq!(order, vec![vec![node]]);
        assert!(self_referencing(&schema, node));
    }

    /// **An edge leaving the node set is not an edge**, so a caller ordering the stored
    /// predicates is not ordered against something it will never emit.
    #[test]
    fn an_edge_out_of_the_set_is_dropped() {
        let schema = schema_of(
            "schema s {
               predicate Decl : { file : File, name : string }
               predicate File : string
             }",
        );

        let decl = PredicateId(0);
        assert_eq!(components(&schema, &[decl]), vec![vec![decl]]);
    }
}
