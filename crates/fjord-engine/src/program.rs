//! Named query-local relation declarations and their rules.
//!
//! A program owns one syntax arena for its answer and every rule. Duplicate relation
//! declarations are refused; clauses and declarations retain source order.

use fjord_schema::schema::{LocalInterner, PredicateTyNamed, Schema, Symbol};

use crate::{
    diag::Diagnostics,
    flatten::rule_dependencies,
    reorder::Deps,
    syntax::{Ast, NodeId, Query},
};

/// A relation's position in a program's declaration list.
///
/// Its own type rather than a bare index, because a program holds two lists a `usize`
/// could address — the declarations and the rules — and one read as the other resolves
/// to a relation nobody named.
#[derive(Debug, Clone, Copy, PartialEq, Eq, PartialOrd, Ord, Hash)]
pub struct LocalPredicate(usize);

impl LocalPredicate {
    #[must_use]
    pub fn index(self) -> usize {
        self.0
    }
}

/// A local relation's declaration: a name and the signature its rules must produce.
///
/// The signature is `PredicateTyNamed<Symbol>` rather than the published
/// [`PredicateTy`](fjord_schema::schema::PredicateTy), which is `PredicateTyNamed<Spur>`
/// — the name tier is what makes a persisted schema structurally unable to hold a
/// query-local name, rather than validated not to.
#[derive(Debug, Clone)]
pub struct RelationDecl {
    pub name: Symbol,
    pub signature: PredicateTyNamed<Symbol>,
}

/// One unresolved source clause, accepted after all declarations are known.
#[derive(Clone)]
pub struct Clause {
    pub into: Symbol,
    pub query: Query<NodeId>,
}

/// One resolved rule in a program.
#[derive(Clone)]
pub struct Rule {
    into: LocalPredicate,
    query: Query<NodeId>,
}

impl Rule {
    #[must_use]
    pub fn target(&self) -> LocalPredicate {
        self.into
    }

    #[must_use]
    pub fn query(&self) -> &Query<NodeId> {
        &self.query
    }
}

/// A local relation's top-level type is not a record.
///
/// **Stated as a restriction rather than left to be discovered.** `Predicate` holds
/// `key: PredicateTy` — a *bare* type, not a record wrapper — so `int` and a union are
/// both legal top-level predicate keys, and "a local relation reuses the schema
/// type grammar in full" would therefore promise `with Count : int` and
/// `with T : <A | B>` heads that the materialisation projection does not define: a
/// scalar head has no field set to reconcile, and a union head needs the head
/// expression to carry a discriminant, which nothing says how to produce. Left open it
/// admits either an undocumented record-only implementation of a wider promise, or an
/// invented wrapper encoding at the head of a relation.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct NonRecordHead;

/// **A local relation's top-level type must be a record**, and its whole declared
/// record is the key.
///
/// Scalar and union heads are a later cut that owes the projection its own definition.
///
/// # Errors
///
/// [`NonRecordHead`] for a top-level type that is not a record.
pub fn reject_non_record_head<N>(signature: &PredicateTyNamed<N>) -> Result<(), NonRecordHead> {
    match signature {
        PredicateTyNamed::Record(_) => Ok(()),
        _ => Err(NonRecordHead),
    }
}

/// Why a program could not be built.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum ProgramError {
    /// `reject/duplicate-relation` — two declarations of one name.
    DuplicateRelation { name: Symbol },
    /// `reject/non-record-relation` — a scalar or union head.
    NonRecordRelation { name: Symbol },
    /// A rule derives into a relation the program does not declare. Structural rather
    /// than a language rule: it can only arise from a hand-built program.
    UndeclaredTarget { name: Symbol },
    /// A rule's node ids belong to a different syntax arena.
    ForeignSyntaxTree { rule: usize },
}

/// Several named rules over one syntax store, plus the answer goal.
pub struct Program {
    relations: Box<[RelationDecl]>,
    rules: Box<[Rule]>,
    /// The answer goal and the syntax arena every rule is checked against.
    ast: Ast,
}

/// A program whose declarations and syntax arena are fixed, ready for rules.
///
/// This is the public construction seam for a non-empty program: relation identities
/// are resolved only after the complete declaration list is known, then handed to
/// [`finish`](Self::finish) with the rules that use them.
pub struct ProgramBuilder {
    relations: Box<[RelationDecl]>,
    ast: Ast,
}

impl ProgramBuilder {
    /// Resolve a relation against the complete declaration list.
    #[must_use]
    pub fn resolve(&self, name: Symbol) -> Option<LocalPredicate> {
        resolve(&self.relations, name)
    }

    /// The answer query, for callers deriving rule bodies from the same arena.
    #[must_use]
    pub fn answer(&self) -> &Query<NodeId> {
        self.ast.query()
    }

    /// Resolve every clause target and finish construction.
    ///
    /// # Errors
    ///
    /// [`ProgramError::UndeclaredTarget`] if a clause names no declaration, or
    /// [`ProgramError::ForeignSyntaxTree`] if a clause body belongs to another AST.
    pub fn finish(self, clauses: impl Into<Box<[Clause]>>) -> Result<Program, ProgramError> {
        let clauses = clauses.into();
        let mut rules = Vec::with_capacity(clauses.len());

        for (index, clause) in clauses.into_vec().into_iter().enumerate() {
            let Some(into) = resolve(&self.relations, clause.into) else {
                return Err(ProgramError::UndeclaredTarget { name: clause.into });
            };
            if !self.ast.owns(&clause.query) {
                return Err(ProgramError::ForeignSyntaxTree { rule: index });
            }
            rules.push(Rule {
                into,
                query: clause.query,
            });
        }

        Ok(Program {
            relations: self.relations,
            rules: rules.into_boxed_slice(),
            ast: self.ast,
        })
    }
}

impl Program {
    /// Fix the declarations and syntax arena before resolving any rule target.
    ///
    /// # Errors
    ///
    /// [`ProgramError::DuplicateRelation`] for two declarations of one name, or
    /// [`ProgramError::NonRecordRelation`] for a non-record signature.
    pub fn builder(
        relations: impl Into<Box<[RelationDecl]>>,
        ast: Ast,
    ) -> Result<ProgramBuilder, ProgramError> {
        let relations = relations.into();

        validate_relations(&relations)?;
        Ok(ProgramBuilder { relations, ast })
    }

    /// Build a program in one call when its rule targets are already resolved.
    ///
    /// # Errors
    ///
    /// Any [`ProgramError`] that [`builder`](Self::builder) or
    /// [`ProgramBuilder::finish`] can report.
    pub fn new(
        relations: impl Into<Box<[RelationDecl]>>,
        clauses: impl Into<Box<[Clause]>>,
        ast: Ast,
    ) -> Result<Program, ProgramError> {
        Self::builder(relations, ast)?.finish(clauses)
    }

    /// The declarations, in source order.
    #[must_use]
    pub fn relations(&self) -> &[RelationDecl] {
        &self.relations
    }

    /// Every rule, in source order, with duplicate clauses retained.
    #[must_use]
    pub fn rules(&self) -> &[Rule] {
        &self.rules
    }

    /// The rules deriving into one relation, in source order.
    pub fn rules_for(&self, into: LocalPredicate) -> impl Iterator<Item = &Rule> {
        self.rules.iter().filter(move |rule| rule.into == into)
    }

    /// A name's relation, wherever it was declared.
    ///
    /// Resolution is independent of where the declaration appeared in source order.
    #[must_use]
    pub fn resolve(&self, name: Symbol) -> Option<LocalPredicate> {
        resolve(&self.relations, name)
    }

    /// The answer goal — streamed after the local rules materialise.
    #[must_use]
    pub fn answer(&self) -> &Query<NodeId> {
        self.ast.query()
    }

    /// The shared syntax store.
    #[must_use]
    pub fn ast(&self) -> &Ast {
        &self.ast
    }

    /// Collect one rule's statements and symbol dependencies — the SIPS seam.
    ///
    /// The same `collect` every query goes through, run over a rule body instead of over
    /// the store's own query. Reports into `diagnostics` rather than returning them.
    #[must_use]
    pub fn collect_rule(
        &self,
        rule: usize,
        schema: &Schema,
        interner: &mut LocalInterner,
        diagnostics: &mut Diagnostics,
    ) -> Option<Deps> {
        let rule = self.rules.get(rule)?;

        rule_dependencies(&self.ast, &rule.query, schema, interner, diagnostics)
    }
}

fn resolve(relations: &[RelationDecl], name: Symbol) -> Option<LocalPredicate> {
    relations
        .iter()
        .position(|decl| decl.name == name)
        .map(LocalPredicate)
}

fn validate_relations(relations: &[RelationDecl]) -> Result<(), ProgramError> {
    // The first duplicate in declaration order is the one the diagnostic names.
    for (index, decl) in relations.iter().enumerate() {
        if relations[..index]
            .iter()
            .any(|prior| prior.name == decl.name)
        {
            return Err(ProgramError::DuplicateRelation { name: decl.name });
        }
    }

    for decl in relations {
        if reject_non_record_head(&decl.signature).is_err() {
            return Err(ProgramError::NonRecordRelation { name: decl.name });
        }
    }

    Ok(())
}

#[cfg(test)]
mod tests {
    use std::sync::Arc;

    use fjord_schema::schema::PredicateId;

    use super::*;
    use crate::{corpus, cst::CstNode, flatten::dependencies, lower::lower, parse::parse, ty};

    /// Lower one query against the corpus schema — the shared syntax store every rule
    /// below indexes into.
    fn lowered(source: &str) -> (Ast, LocalInterner, Schema) {
        let schema = corpus::schema();
        let mut interner = LocalInterner::new(schema.interner().clone());
        let mut diagnostics = Diagnostics::new();

        let cst = parse(source, &mut diagnostics).expect("a tree");
        let ast = lower(
            &CstNode::new(&cst),
            &schema,
            &mut interner,
            &mut diagnostics,
        );
        let _typed = ty::check(&ast, &schema, &interner, &mut diagnostics);
        assert!(!diagnostics.has_errors(), "{source:?} must typecheck");

        (ast, interner, schema)
    }

    fn named(interner: &mut LocalInterner, name: &str) -> Symbol {
        interner.get_or_intern(name)
    }

    /// `{ from : src.Decl, to : src.Decl }`.
    fn signature(interner: &mut LocalInterner) -> PredicateTyNamed<Symbol> {
        let from = named(interner, "from");
        let to = named(interner, "to");

        PredicateTyNamed::Record(Arc::from(vec![
            (from, PredicateTyNamed::Fact(PredicateId(0))),
            (to, PredicateTyNamed::Fact(PredicateId(0))),
        ]))
    }

    fn decl(interner: &mut LocalInterner, name: &str) -> RelationDecl {
        RelationDecl {
            name: named(interner, name),
            signature: signature(interner),
        }
    }

    // ---- declaration and clause decisions -----------------------------------

    /// Two declarations of one name have no agreed signature, and merging them
    /// silently is how a typo becomes a union.
    #[test]
    fn two_declarations_of_one_name_are_refused() {
        let (ast, mut interner, _) = lowered("{a = X} where test.Foo {name = X, id = 1}");
        let reach = named(&mut interner, "Reach");
        let relations = vec![decl(&mut interner, "Reach"), decl(&mut interner, "Reach")];

        assert_eq!(
            Program::new(relations, vec![], ast).map(|_| ()),
            Err(ProgramError::DuplicateRelation { name: reach })
        );
    }

    /// Two relations with *different* names are the ordinary case, and a refusal that
    /// fired on them would forbid mutual recursion outright.
    #[test]
    fn two_declarations_of_different_names_are_accepted() {
        let (ast, mut interner, _) = lowered("{a = X} where test.Foo {name = X, id = 1}");
        let relations = vec![decl(&mut interner, "Reach"), decl(&mut interner, "Calls")];

        assert!(Program::new(relations, vec![], ast).is_ok());
    }

    /// **Duplicate clauses are retained, never deduplicated.** A dedup is a no-op for
    /// the answer and moves the generated rule count, the profile and the program
    /// fingerprint — which is what decides whether a cursor resumes.
    #[test]
    fn duplicate_clauses_are_retained() {
        let (ast, mut interner, _) = lowered("{a = X} where test.Foo {name = X, id = 1}");
        let body = ast.query().clone();
        let reach = named(&mut interner, "Reach");
        let relations = vec![decl(&mut interner, "Reach")];
        let rules = vec![
            Clause {
                into: reach,
                query: body.clone(),
            },
            Clause {
                into: reach,
                query: body,
            },
        ];

        let program = Program::new(relations, rules, ast).expect("a program");
        let target = program.resolve(reach).expect("Reach is declared");

        assert_eq!(program.rules().len(), 2);
        assert_eq!(program.rules_for(target).count(), 2);
    }

    /// **Canonical order is source order**, so `rules_for` hands them back in the order
    /// they were written rather than in whatever order a map iterated.
    #[test]
    fn rules_keep_source_order() {
        let (ast, mut interner, _) = lowered("{a = X} where test.Foo {name = X, id = 1}");
        let body = ast.query().clone();
        let shorter = Query::new(*body.head(), Box::new([body.body()[0]]));
        let (reach, calls) = (named(&mut interner, "Reach"), named(&mut interner, "Calls"));
        let relations = vec![decl(&mut interner, "Reach"), decl(&mut interner, "Calls")];

        // Interleaved on purpose: grouping by relation must not reorder within one.
        let rules = vec![
            Clause {
                into: reach,
                query: body.clone(),
            },
            Clause {
                into: calls,
                query: body.clone(),
            },
            Clause {
                into: reach,
                query: shorter,
            },
        ];

        let program = Program::new(relations, rules, ast).expect("a program");

        let written: Vec<usize> = program
            .rules()
            .iter()
            .map(|rule| rule.target().index())
            .collect();
        assert_eq!(written, vec![0, 1, 0]);

        let reach = program.resolve(reach).expect("Reach is declared");
        let reach: Vec<usize> = program
            .rules_for(reach)
            .map(|rule| rule.query().body().len())
            .collect();
        assert_eq!(reach, vec![body.body().len(), 1]);
    }

    /// A rule deriving into a relation nobody declared can only come from a hand-built
    /// program, and is refused there rather than resolving to whatever sat at that
    /// index.
    #[test]
    fn a_rule_into_an_undeclared_relation_is_refused() {
        let (ast, mut interner, _) = lowered("{a = X} where test.Foo {name = X, id = 1}");
        let absent = named(&mut interner, "Absent");
        let relations = vec![decl(&mut interner, "Reach")];
        let rules = vec![Clause {
            into: absent,
            query: ast.query().clone(),
        }];

        assert_eq!(
            Program::new(relations, rules, ast).map(|_| ()),
            Err(ProgramError::UndeclaredTarget { name: absent })
        );
    }

    /// Node ids are arena positions. Accepting a query cloned from a different AST
    /// either reads unrelated nodes at the same positions or indexes past the store.
    #[test]
    fn a_rule_from_another_syntax_tree_is_refused() {
        let (ast, mut interner, _) = lowered("{a = X} where test.Foo {name = X, id = 1}");
        let (foreign, _, _) = lowered("{a = X} where test.Foo {name = X, id = 1}");
        let reach = named(&mut interner, "Reach");
        let relations = vec![decl(&mut interner, "Reach")];
        let rules = vec![Clause {
            into: reach,
            query: foreign.query().clone(),
        }];

        assert_eq!(
            Program::new(relations, rules, ast).map(|_| ()),
            Err(ProgramError::ForeignSyntaxTree { rule: 0 })
        );
    }

    // ---- declaration-wide name resolution -----------------------------------

    /// **A forward reference resolves.** Names are resolved from the whole declaration
    /// list, so a rule naming a relation declared after it is not a special case.
    #[test]
    fn a_forward_reference_resolves() {
        let (ast, mut interner, _) = lowered("{a = X} where test.Foo {name = X, id = 1}");
        let body = ast.query().clone();
        let relations = vec![decl(&mut interner, "First"), decl(&mut interner, "Later")];
        let later = named(&mut interner, "Later");

        let builder = Program::builder(relations, ast).expect("valid declarations");
        let target = builder.resolve(later).expect("the later declaration");
        let program = builder
            .finish(vec![Clause {
                into: later,
                query: body,
            }])
            .expect("a program");

        assert_eq!(program.resolve(later), Some(target));
        assert_eq!(program.rules_for(target).count(), 1);
    }

    /// Both rule targets resolve before either rule is built.
    #[test]
    fn both_rule_targets_resolve_before_rules_are_built() {
        let (ast, mut interner, _) = lowered("{a = X} where test.Foo {name = X, id = 1}");
        let body = ast.query().clone();
        let relations = vec![decl(&mut interner, "Even"), decl(&mut interner, "Odd")];
        let (even, odd) = (named(&mut interner, "Even"), named(&mut interner, "Odd"));

        let builder = Program::builder(relations, ast).expect("valid declarations");
        let even_target = builder.resolve(even).expect("Even is declared");
        let odd_target = builder.resolve(odd).expect("Odd is declared");
        let program = builder
            .finish(vec![
                Clause {
                    into: even,
                    query: body.clone(),
                },
                Clause {
                    into: odd,
                    query: body,
                },
            ])
            .expect("a program");

        assert_eq!(program.resolve(even), Some(even_target));
        assert_eq!(program.resolve(odd), Some(odd_target));
        assert_eq!(program.rules_for(even_target).count(), 1);
        assert_eq!(program.rules_for(odd_target).count(), 1);
    }

    /// A name nobody declared resolves to nothing rather than to a neighbour.
    #[test]
    fn an_undeclared_name_resolves_to_nothing() {
        let (ast, mut interner, _) = lowered("{a = X} where test.Foo {name = X, id = 1}");
        let relations = vec![decl(&mut interner, "Reach")];
        let absent = named(&mut interner, "Absent");

        let program = Program::new(relations, vec![], ast).expect("a program");

        assert_eq!(program.resolve(absent), None);
    }

    // ---- per-rule dependency collection -------------------------------------

    /// **Per-rule collection equals single-query collection.** The refactor that
    /// makes `collect` runnable over an arbitrary rule body must not change what it
    /// collects for the body every query already had — otherwise downstream code is
    /// reading a different query than the one that shipped.
    #[test]
    fn per_rule_collection_equals_the_single_query_collection() {
        for source in [
            "{a = X} where test.Foo {name = X, id = 1}",
            "{a = X, b = Y} where test.Edge {from = X, to = Y}",
            "{a = X} where test.Foo {name = X, id = Y}; !test.Bar {id = Y}",
            "{a = X} where test.Foo {name = X, id = Y}; X = \"a\"..",
        ] {
            let (ast, mut interner, schema) = lowered(source);
            let mut diagnostics = Diagnostics::new();

            let single = dependencies(&ast, &schema, &mut interner, &mut diagnostics)
                .expect("a collectable query");

            let body = ast.query().clone();
            let reach = named(&mut interner, "Reach");
            let relations = vec![decl(&mut interner, "Reach")];
            let program = Program::new(
                relations,
                vec![Clause {
                    into: reach,
                    query: body,
                }],
                ast,
            )
            .expect("a program");

            let per_rule = program
                .collect_rule(0, &schema, &mut interner, &mut diagnostics)
                .expect("a collectable rule");

            assert_eq!(single, per_rule, "collection diverged for {source:?}");
        }
    }

    /// The negative control: a rule whose body is a *different* query collects
    /// differently, so the equality above is not passing because `collect_rule` ignores
    /// what it is handed.
    #[test]
    fn a_different_rule_body_collects_differently() {
        let (ast, mut interner, schema) =
            lowered("{a = X, b = Y} where test.Foo {name = X, id = Y}; test.Bar {id = Y}");
        let mut diagnostics = Diagnostics::new();

        let whole = dependencies(&ast, &schema, &mut interner, &mut diagnostics)
            .expect("a collectable query");

        // One statement of the two, as its own rule.
        let head = *ast.query().head();
        let first = Query::new(head, Box::from(vec![ast.query().body()[0]]));

        let reach = named(&mut interner, "Reach");
        let relations = vec![decl(&mut interner, "Reach")];
        let program = Program::new(
            relations,
            vec![Clause {
                into: reach,
                query: first,
            }],
            ast,
        )
        .expect("a program");

        let shorter = program
            .collect_rule(0, &schema, &mut interner, &mut diagnostics)
            .expect("a collectable rule");

        assert_ne!(whole, shorter);
    }

    // ---- record-only and key-only relations ---------------------------------

    /// A scalar head has no field set for the materialisation projection to reconcile.
    #[test]
    fn a_scalar_head_is_refused() {
        let (ast, mut interner, _) = lowered("{a = X} where test.Foo {name = X, id = 1}");
        let count = named(&mut interner, "Count");
        let relations = vec![RelationDecl {
            name: count,
            signature: PredicateTyNamed::Int,
        }];

        assert_eq!(
            Program::new(relations, vec![], ast).map(|_| ()),
            Err(ProgramError::NonRecordRelation { name: count })
        );
    }

    /// A union head would need the head expression to carry a discriminant, which
    /// nothing here says how to produce.
    #[test]
    fn a_union_head_is_refused() {
        let (ast, mut interner, _) = lowered("{a = X} where test.Foo {name = X, id = 1}");
        let tagged = named(&mut interner, "Tagged");
        let alt = named(&mut interner, "one");
        let relations = vec![RelationDecl {
            name: tagged,
            signature: PredicateTyNamed::Union(Arc::from(vec![
                fjord_schema::schema::AlternativeNamed {
                    name: alt,
                    disc: 1,
                    ty: PredicateTyNamed::Int,
                },
            ])),
        }];

        assert_eq!(
            Program::new(relations, vec![], ast).map(|_| ()),
            Err(ProgramError::NonRecordRelation { name: tagged })
        );
    }

    /// The negative control: the `Reach` signature is a record and is accepted.
    #[test]
    fn a_record_head_is_accepted() {
        let (ast, mut interner, _) = lowered("{a = X} where test.Foo {name = X, id = 1}");
        let relations = vec![decl(&mut interner, "Reach")];

        assert!(Program::new(relations, vec![], ast).is_ok());
    }

    /// **A local relation is key-only, and that is structural rather than checked.**
    /// `Predicate` carries `value: Option<PredicateTy>`; `RelationDecl` carries no value
    /// field at all, so two tuples agreeing on their key and differing in a value — one
    /// tuple to the deduplicator, one rank to the allocator, and two rows to a scan —
    /// is unrepresentable rather than refused. A `value` field appearing here would
    /// make identity allocation and set difference disagree.
    #[test]
    fn a_relation_declaration_has_no_value_side() {
        let source = include_str!("program.rs");
        let declaration = source
            .split("pub struct RelationDecl {")
            .nth(1)
            .expect("the declaration is in this file")
            .split('}')
            .next()
            .expect("the declaration is brace-delimited");

        assert!(
            !declaration.contains("value"),
            "RelationDecl grew a value side: {declaration}"
        );
    }
}
