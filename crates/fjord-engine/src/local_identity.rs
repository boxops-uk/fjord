//! Detection of signatures and plans that expose a query-local row identity.
//!
//! Local identities may not enter projections, comparisons, seeks, or fetch targets.
//! Fetching through a local row onto a base predicate remains legal.

use fjord_schema::schema::{PredicateId, PredicateTyNamed};

use crate::plan::{
    Address, Plan, Project, Residual, ResidualOp, SeekKey, SeekKeyPart, Source, Step, Test,
};

/// One place a local row's identity would be observable.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum Escape {
    /// `X where Reach X` — the whole-row projection, and the one of the four that
    /// needs front-end work rather than being unreachable by construction.
    ProjectFactRef {
        address: Address,
        predicate: PredicateId,
    },
    /// A seek splicing a local register's id into a key.
    SeekKeyFactId {
        address: Address,
        predicate: PredicateId,
    },
    /// The same compare, once the seek prefix has closed.
    ResidualFactId {
        address: Address,
        predicate: PredicateId,
    },
    /// A point read whose declared referent is a local relation.
    FetchOntoLocal { predicate: PredicateId },
}

/// A local relation's signature names another local relation as a field type.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct LocalFieldType {
    pub predicate: PredicateId,
}

/// **A local relation's signature may not name a local relation as a field type.**
///
/// The rule that makes three of the four escapes unreachable, and the reason a derived
/// tuple cannot reference another derived tuple — stated as a limitation rather than
/// discovered as one. Path reconstruction is out of the first cut; that is the price of
/// internal-only identity, and what buys delta-as-a-second-relation.
///
/// # Errors
///
/// [`LocalFieldType`] naming the first local referent found, at any depth through
/// records and union alternatives.
pub fn reject_local_field_type<N>(
    ty: &PredicateTyNamed<N>,
    is_local: &impl Fn(PredicateId) -> bool,
) -> Result<(), LocalFieldType> {
    match ty {
        PredicateTyNamed::Int | PredicateTyNamed::Str => Ok(()),

        PredicateTyNamed::Fact(predicate) => {
            if is_local(*predicate) {
                return Err(LocalFieldType {
                    predicate: *predicate,
                });
            }
            Ok(())
        }

        PredicateTyNamed::Record(fields) => fields
            .iter()
            .try_for_each(|(_, field)| reject_local_field_type(field, is_local)),

        // A payload is a field like any other — a union alternative holding a local
        // referent would otherwise be the one shape this rule did not reach.
        PredicateTyNamed::Union(alternatives) => alternatives
            .iter()
            .try_for_each(|alt| reject_local_field_type(&alt.ty, is_local)),
    }
}

/// Every place `plan` would make a local row's identity observable.
///
/// Reports every escape in one pass.
#[must_use]
pub fn escapes(plan: &Plan, is_local: &impl Fn(PredicateId) -> bool) -> Vec<Escape> {
    let mut found = vec![];
    let bound = local_registers(plan, is_local);

    for step in plan.body.iter() {
        let sources: &[Source] = match step {
            Step::Level(level) => &level.sources,
            Step::Test(Test::Absent(sources)) => sources,

            // Neither reaches a row's identity, which is what makes the list of four
            // exhaustive rather than merely long. A `Computed` reads *fields* and other
            // derived slots — `Computed::Register` names a derived bind's output, not a
            // row — so no arm of it can name a `FactId`.
            Step::Derive(_) | Step::Test(Test::Compare { .. }) => &[],
        };

        for source in sources {
            match source {
                // A guide narrows what a seek visits and names no register, so
                // it can carry no identity out of a local row — the seek key and
                // the residuals are still the whole of what escapes.
                Source::Seek { access, residuals }
                | Source::Guided {
                    access, residuals, ..
                } => {
                    seek_key_escapes(&access.seek_key, &bound, &mut found);
                    residual_escapes(residuals, &bound, &mut found);
                }

                Source::Fetch {
                    predicate_id,
                    residuals,
                    ..
                } => {
                    // The declared referent decides this; following a base-typed field
                    // through a local row remains legal.
                    if is_local(*predicate_id) {
                        found.push(Escape::FetchOntoLocal {
                            predicate: *predicate_id,
                        });
                    }
                    residual_escapes(residuals, &bound, &mut found);
                }
            }
        }
    }

    project_escapes(&plan.head, &bound, &mut found);
    found
}

/// Which registers hold a row of a local relation.
///
/// A level's `binds` are the level's and not a source's — every alternative binds the
/// same variables — so a disjunction with any local branch can leave a local row in the
/// register, whichever branch ran.
fn local_registers(
    plan: &Plan,
    is_local: &impl Fn(PredicateId) -> bool,
) -> Vec<(Address, PredicateId)> {
    let mut local = vec![];

    for step in plan.body.iter() {
        let Step::Level(level) = step else { continue };

        let Some(predicate) = level
            .sources
            .iter()
            .map(Source::predicate_id)
            .find(|id| is_local(*id))
        else {
            continue;
        };

        local.extend(level.binds.iter().map(|address| (*address, predicate)));
    }

    local
}

fn seek_key_escapes(key: &SeekKey, bound: &[(Address, PredicateId)], found: &mut Vec<Escape>) {
    // Both part-carrying keys, and matched together on purpose: a bounded seek is
    // an ordinary seek with a range after its parts, so a splice hidden in one
    // would escape unseen if this only knew about the other.
    let (SeekKey::Composite(parts) | SeekKey::Bounded { parts, .. }) = key else {
        return;
    };

    for part in parts.iter() {
        if let SeekKeyPart::RegisterFactId(address) = part
            && let Some(predicate) = local_at(*address, bound)
        {
            found.push(Escape::SeekKeyFactId {
                address: *address,
                predicate,
            });
        }
    }
}

fn residual_escapes(
    residuals: &[Residual],
    bound: &[(Address, PredicateId)],
    found: &mut Vec<Escape>,
) {
    for residual in residuals {
        if let ResidualOp::EqRegisterFactId(address) = &residual.op
            && let Some(predicate) = local_at(*address, bound)
        {
            found.push(Escape::ResidualFactId {
                address: *address,
                predicate,
            });
        }
    }
}

fn project_escapes(project: &Project, bound: &[(Address, PredicateId)], found: &mut Vec<Escape>) {
    match project {
        Project::Lit(_) | Project::RegisterField { .. } | Project::Computed(_) => {}

        // Reads the register's own id, which is the whole of the refusal.
        Project::FactRef(address) => {
            if let Some(predicate) = local_at(*address, bound) {
                found.push(Escape::ProjectFactRef {
                    address: *address,
                    predicate,
                });
            }
        }

        // Fetched with `point` on the register's own `fact_id`. Key-only local
        // relations make this unreachable from lowering, so a hand-built occurrence
        // is reported.
        Project::Value { address, .. } => {
            if let Some(predicate) = local_at(*address, bound) {
                found.push(Escape::ProjectFactRef {
                    address: *address,
                    predicate,
                });
            }
        }

        Project::Record(fields) => {
            for (_, field) in fields.iter() {
                project_escapes(field, bound, found);
            }
        }
    }
}

/// The local predicate a register holds a row of, if it holds one.
fn local_at(address: Address, bound: &[(Address, PredicateId)]) -> Option<PredicateId> {
    bound
        .iter()
        .find_map(|(bound, predicate)| (*bound == address).then_some(*predicate))
}

#[cfg(test)]
mod tests {
    use std::sync::Arc;

    use fjord_schema::schema::AlternativeNamed;

    use super::*;
    use fjord_encoding::tuple::Value;
    use fjord_schema::schema::Symbol;

    use crate::plan::{Access, FieldPath, Level, SeekKey};

    /// Predicate 9 and up are the query's own; everything below is the schema's.
    fn is_local(id: PredicateId) -> bool {
        id.0 >= 9
    }

    const BASE: PredicateId = PredicateId(1);
    const LOCAL: PredicateId = PredicateId(9);

    fn scan(predicate: PredicateId, bind: usize) -> Step {
        Step::Level(Level::seek(
            Access {
                predicate_id: predicate,
                seek_key: SeekKey::Prefix(Box::new([])),
            },
            Box::new([Address::new(bind)]),
            Box::new([]),
        ))
    }

    fn plan_of(body: Vec<Step>, head: Project) -> Plan {
        Plan {
            nvars: 4,
            body: body.into_boxed_slice(),
            head,
        }
    }

    // ---- the one refusal needing front-end work ------------------------------

    /// `X where Reach X` — the whole-row projection of a derived tuple.
    #[test]
    fn projecting_a_local_row_whole_is_an_escape() {
        let plan = plan_of(vec![scan(LOCAL, 0)], Project::FactRef(Address::new(0)));

        assert_eq!(
            escapes(&plan, &is_local),
            vec![Escape::ProjectFactRef {
                address: Address::new(0),
                predicate: LOCAL,
            }]
        );
    }

    /// The negative control: projecting a base row is the ordinary query case.
    #[test]
    fn projecting_a_base_row_whole_is_not_an_escape() {
        let plan = plan_of(vec![scan(BASE, 0)], Project::FactRef(Address::new(0)));

        assert!(escapes(&plan, &is_local).is_empty());
    }

    /// A projection nested in a record is reached, or a head of one field would be
    /// the way out.
    #[test]
    fn a_local_projection_inside_a_record_is_reached() {
        let name = Symbol::Schema(lasso::Rodeo::default().get_or_intern("n"));
        let plan = plan_of(
            vec![scan(LOCAL, 0)],
            Project::Record(Box::new([(name, Project::FactRef(Address::new(0)))])),
        );

        assert_eq!(escapes(&plan, &is_local).len(), 1);
    }

    // ---- the three that are unreachable once the declaration rule holds -------

    #[test]
    fn splicing_a_local_id_into_a_seek_is_an_escape() {
        let plan = plan_of(
            vec![
                scan(LOCAL, 0),
                Step::Level(Level::seek(
                    Access {
                        predicate_id: BASE,
                        seek_key: SeekKey::Composite(Box::new([SeekKeyPart::RegisterFactId(
                            Address::new(0),
                        )])),
                    },
                    Box::new([Address::new(1)]),
                    Box::new([]),
                )),
            ],
            Project::Lit(Value::Int(1)),
        );

        assert_eq!(
            escapes(&plan, &is_local),
            vec![Escape::SeekKeyFactId {
                address: Address::new(0),
                predicate: LOCAL,
            }]
        );
    }

    #[test]
    fn comparing_a_local_id_in_a_residual_is_an_escape() {
        let plan = plan_of(
            vec![
                scan(LOCAL, 0),
                Step::Level(Level::seek(
                    Access {
                        predicate_id: BASE,
                        seek_key: SeekKey::Prefix(Box::new([])),
                    },
                    Box::new([Address::new(1)]),
                    Box::new([Residual {
                        path: FieldPath::field(0),
                        op: ResidualOp::EqRegisterFactId(Address::new(0)),
                    }]),
                )),
            ],
            Project::Lit(Value::Int(1)),
        );

        assert_eq!(
            escapes(&plan, &is_local),
            vec![Escape::ResidualFactId {
                address: Address::new(0),
                predicate: LOCAL,
            }]
        );
    }

    #[test]
    fn fetching_onto_a_local_target_is_an_escape() {
        let plan = plan_of(
            vec![
                scan(BASE, 0),
                Step::Level(Level {
                    sources: Box::new([Source::Fetch {
                        reference: Address::new(0),
                        path: FieldPath::field(0),
                        predicate_id: LOCAL,
                        residuals: Box::new([]),
                    }]),
                    binds: Box::new([Address::new(1)]),
                }),
            ],
            Project::Lit(Value::Int(1)),
        );

        assert_eq!(
            escapes(&plan, &is_local),
            vec![Escape::FetchOntoLocal { predicate: LOCAL }]
        );
    }

    /// The fetch check is on the declared referent rather than on the register followed
    /// from. A fetch *through* a local
    /// row's field is legal when that field names a base predicate: the local row's own
    /// identity is never read, and refusing it would take the example this whole
    /// section exists for.
    #[test]
    fn fetching_through_a_local_row_onto_a_base_target_is_legal() {
        let plan = plan_of(
            vec![
                scan(LOCAL, 0),
                Step::Level(Level {
                    sources: Box::new([Source::Fetch {
                        reference: Address::new(0),
                        path: FieldPath::field(0),
                        predicate_id: BASE,
                        residuals: Box::new([]),
                    }]),
                    binds: Box::new([Address::new(1)]),
                }),
            ],
            Project::Lit(Value::Int(1)),
        );

        assert!(escapes(&plan, &is_local).is_empty());
    }

    /// A disjunction binds one register from several alternatives, so one local branch
    /// is enough — whichever branch ran, the register may hold a local row.
    #[test]
    fn one_local_alternative_makes_the_register_local() {
        let plan = plan_of(
            vec![Step::Level(Level {
                sources: Box::new([
                    Source::Seek {
                        access: Access {
                            predicate_id: BASE,
                            seek_key: SeekKey::Prefix(Box::new([])),
                        },
                        residuals: Box::new([]),
                    },
                    Source::Seek {
                        access: Access {
                            predicate_id: LOCAL,
                            seek_key: SeekKey::Prefix(Box::new([])),
                        },
                        residuals: Box::new([]),
                    },
                ]),
                binds: Box::new([Address::new(0)]),
            })],
            Project::FactRef(Address::new(0)),
        );

        assert_eq!(escapes(&plan, &is_local).len(), 1);
    }

    /// Everything is reported at once.
    #[test]
    fn every_escape_is_reported_rather_than_the_first() {
        let plan = plan_of(
            vec![
                scan(LOCAL, 0),
                Step::Level(Level::seek(
                    Access {
                        predicate_id: BASE,
                        seek_key: SeekKey::Composite(Box::new([SeekKeyPart::RegisterFactId(
                            Address::new(0),
                        )])),
                    },
                    Box::new([Address::new(1)]),
                    Box::new([Residual {
                        path: FieldPath::field(0),
                        op: ResidualOp::EqRegisterFactId(Address::new(0)),
                    }]),
                )),
            ],
            Project::FactRef(Address::new(0)),
        );

        assert_eq!(escapes(&plan, &is_local).len(), 3);
    }

    // ---- the declaration rule ------------------------------------------------

    #[test]
    fn a_local_field_type_is_refused() {
        let ty: PredicateTyNamed<&str> = PredicateTyNamed::Record(Arc::from(vec![
            ("from", PredicateTyNamed::Fact(BASE)),
            ("to", PredicateTyNamed::Fact(LOCAL)),
        ]));

        assert_eq!(
            reject_local_field_type(&ty, &is_local),
            Err(LocalFieldType { predicate: LOCAL })
        );
    }

    /// Every field names a base predicate.
    #[test]
    fn a_signature_of_base_referents_is_accepted() {
        let ty: PredicateTyNamed<&str> = PredicateTyNamed::Record(Arc::from(vec![
            ("from", PredicateTyNamed::Fact(BASE)),
            ("to", PredicateTyNamed::Fact(BASE)),
        ]));

        assert_eq!(reject_local_field_type(&ty, &is_local), Ok(()));
    }

    /// A union payload is a field like any other; missing this arm would leave one
    /// shape the rule did not reach.
    #[test]
    fn a_local_referent_in_a_union_payload_is_refused() {
        let ty: PredicateTyNamed<&str> = PredicateTyNamed::Union(Arc::from(vec![
            AlternativeNamed {
                name: "a",
                disc: 1,
                ty: PredicateTyNamed::Int,
            },
            AlternativeNamed {
                name: "b",
                disc: 2,
                ty: PredicateTyNamed::Fact(LOCAL),
            },
        ]));

        assert_eq!(
            reject_local_field_type(&ty, &is_local),
            Err(LocalFieldType { predicate: LOCAL })
        );
    }

    /// Nested to any depth, or a record inside a record is the way out.
    #[test]
    fn a_local_referent_nested_deeply_is_refused() {
        let inner: PredicateTyNamed<&str> =
            PredicateTyNamed::Record(Arc::from(vec![("deep", PredicateTyNamed::Fact(LOCAL))]));
        let ty: PredicateTyNamed<&str> =
            PredicateTyNamed::Record(Arc::from(vec![("outer", inner)]));

        assert_eq!(
            reject_local_field_type(&ty, &is_local),
            Err(LocalFieldType { predicate: LOCAL })
        );
    }
}
