//! **Item 10 of the recursion plan: a rule body normalises to the *product* of
//! its disjunctive statements, before adornment and before delta generation.**
//!
//! A body is a conjunction of statements, and a statement may be a disjunction —
//! one flat level of alternative branches, never nested (`flatten.rs` keeps a
//! query's own disjunction that shape and this module does not change it: see
//! the module doc's carve-out below). Semi-naive is defined "per recursive
//! occurrence", so a level mixing a recursive alternative with a base one
//! belongs to neither half of the seed/step split — normalising to a
//! disjunction-free body first is what gives every generated clause exactly one
//! reading. `(A | B); (C | D)` is the four clauses `AC, AD, BC, BD`, and
//! anything that does not multiply — concatenating branches, or keeping the
//! multi-source level — answers a different question.
//!
//! **This module is the transformation and its oracle, nothing about *when* it
//! runs.** The plan places it at step 5 of item 9's phase order, after
//! stratification and restricted to rules that are adorned or delta-generated —
//! both pipeline-integration facts for Movement 3, not properties [`dnf_product`]
//! itself has an opinion about. Generic over the atom type for the same reason:
//! nothing here needs to know what a `Source` or a `Stmt` is, only that a body is
//! a sequence of statements and a statement is a sequence of branches.
//!
//! **The carve-out this module must not touch.** A *query* body's disjunction
//! keeps meaning one multi-source level —
//! `a_disjunction_is_one_level_with_a_source_per_branch` and
//! `conjoined_disjunctions_do_not_multiply` in `flatten.rs` pin exactly that for
//! today's non-recursive language. Nothing in this module is wired into
//! `flatten.rs`; it exists so Movement 3 has a proven transformation to call for
//! the rules item 10 actually names, without disturbing the query path those two
//! guards already protect.

/// Every clause obtainable by choosing exactly one branch from each of `body`'s
/// statements, in the order a nested loop over `body` — outermost statement
/// first, innermost (last) statement fastest-varying — would visit them.
///
/// `body[i]` is statement *i*'s branches: length one for an ordinary
/// non-disjunctive statement (nothing to multiply, just carried through), and
/// length zero for a statement that can never hold — a `Never` branch that
/// reached here undropped, say — which collapses the **whole** product to no
/// clauses at all, exactly as a cartesian product over an empty factor does.
///
/// An empty `body` produces exactly one clause: the empty conjunction. That is
/// the identity a fold over multiplication needs, and it is also the right
/// answer read as English — a rule with no statements has nothing to case-split
/// on, so it normalises to the one trivially-true clause rather than to zero.
#[must_use]
pub fn dnf_product<T: Clone>(body: &[Vec<T>]) -> Vec<Vec<T>> {
    body.iter().fold(vec![Vec::new()], |clauses, branches| {
        let mut expanded = Vec::with_capacity(clauses.len() * branches.len());
        for clause in &clauses {
            for branch in branches {
                let mut extended = clause.clone();
                extended.push(branch.clone());
                expanded.push(extended);
            }
        }
        expanded
    })
}

/// The number of clauses [`dnf_product`] produces for a body shaped like
/// `sizes` — the product of every statement's branch count, **not** their sum.
/// Exposed so a caller (or a test) can state the expected count without
/// building the bodies just to measure them, and so the "multiply, do not add"
/// claim has one place it is computed rather than one per call site.
#[must_use]
pub fn clause_count(sizes: &[usize]) -> usize {
    sizes.iter().product()
}

#[cfg(test)]
mod tests {
    use super::*;
    use proptest::prelude::*;

    // ---- the golden worked example, verbatim from the plan -------------

    #[test]
    fn the_plans_own_worked_example_expands_to_four_clauses_in_order() {
        let body = vec![vec!["A", "B"], vec!["C", "D"]];

        assert_eq!(
            dnf_product(&body),
            vec![
                vec!["A", "C"],
                vec!["A", "D"],
                vec!["B", "C"],
                vec!["B", "D"],
            ]
        );
    }

    // ---- named edge cases ------------------------------------------------

    #[test]
    fn an_empty_body_is_one_empty_clause() {
        let body: Vec<Vec<&str>> = vec![];
        assert_eq!(dnf_product(&body), vec![Vec::<&str>::new()]);
    }

    #[test]
    fn a_statement_with_no_branches_collapses_the_whole_product() {
        let body = vec![vec!["A"], vec![], vec!["C", "D"]];
        assert_eq!(dnf_product(&body), Vec::<Vec<&str>>::new());
    }

    #[test]
    fn a_body_with_no_real_disjunction_is_one_clause() {
        let body = vec![vec!["A"], vec!["B"], vec!["C"]];
        assert_eq!(dnf_product(&body), vec![vec!["A", "B", "C"]]);
    }

    #[test]
    fn a_single_disjunction_is_its_own_branches_one_clause_each() {
        let body = vec![vec!["A", "B", "C"]];
        assert_eq!(dnf_product(&body), vec![vec!["A"], vec!["B"], vec!["C"]]);
    }

    #[test]
    fn clause_count_multiplies_not_adds() {
        // Three two-branch disjunctions in conjunction: 2*2*2 = 8, not 2+2+2 = 6
        // — `conjoined_disjunctions_do_not_multiply`'s own arithmetic, restated
        // here as the count this module's transformation actually produces.
        assert_eq!(clause_count(&[2, 2, 2]), 8);
        let body = vec![vec!["a1", "a2"], vec!["b1", "b2"], vec!["c1", "c2"]];
        assert_eq!(dnf_product(&body).len(), 8);
    }

    // ---- the oracle: a truth table, decoded by row number ---------------
    //
    // Independent of the fold above in the way that matters: it does not build
    // clauses incrementally at all. Row `r` of the "truth table" is decoded
    // directly from `r`'s digits in mixed radix `body[0].len(), body[1].len(),
    // ...` — the same arithmetic a person filling in a truth table by hand
    // would use, counting with the last column fastest.

    fn truth_table_row<T: Clone>(body: &[Vec<T>], mut row: usize) -> Vec<T> {
        let mut indices = vec![0usize; body.len()];
        for i in (0..body.len()).rev() {
            let width = body[i].len();
            indices[i] = row % width;
            row /= width;
        }
        body.iter()
            .zip(indices)
            .map(|(branches, idx)| branches[idx].clone())
            .collect()
    }

    fn truth_table<T: Clone>(body: &[Vec<T>]) -> Vec<Vec<T>> {
        let total: usize = body.iter().map(Vec::len).product();
        (0..total).map(|row| truth_table_row(body, row)).collect()
    }

    /// **The census.** The oracle property tolerates a degenerate draw: a body holding
    /// an empty branch list collapses the whole product, and one whose statements each
    /// have a single branch is one clause. Both agree with the oracle trivially, so if
    /// the strategy ever drew only those the property would stay green and say nothing
    /// about multiplication at all.
    #[test]
    fn the_generator_reaches_a_real_product() {
        use proptest::{
            strategy::{Strategy, ValueTree},
            test_runner::TestRunner,
        };

        const RUNS: usize = 2_000;

        let mut runner = TestRunner::deterministic();
        let strategy = prop::collection::vec(prop::collection::vec(any::<u8>(), 0..4), 0..5);
        let (mut collapsed, mut one_clause, mut real_product) = (0, 0, 0);

        for _ in 0..RUNS {
            let body = strategy.new_tree(&mut runner).unwrap().current();

            if body.iter().any(Vec::is_empty) {
                collapsed += 1;
            } else if body.iter().all(|branches| branches.len() == 1) {
                one_clause += 1;
            } else {
                real_product += 1;
            }
        }

        assert!(
            real_product > RUNS / 10,
            "the generator rarely draws a body with two branches anywhere, so the \
             oracle property is mostly comparing empty products: {real_product} of {RUNS}"
        );
        assert!(collapsed > 0, "no draw reached the collapsing case");
        assert!(one_clause > 0, "no draw reached the single-clause case");
    }

    proptest! {
        /// The real product and the truth-table oracle agree on the exact
        /// sequence of clauses, not merely on the set of them — the stronger
        /// claim, and the one that also pins the order the worked example
        /// above checks by hand.
        #[test]
        fn agrees_with_the_truth_table_oracle(
            body in prop::collection::vec(
                prop::collection::vec(any::<u8>(), 0..4),
                0..5,
            )
        ) {
            let real = dnf_product(&body);
            let oracle = truth_table(&body);
            prop_assert_eq!(real, oracle);
        }

        /// The clause count is always the product of the branch counts,
        /// whatever the branches are — never their sum, and never anything
        /// that depends on the branch *values* rather than their counts.
        #[test]
        fn clause_count_matches_the_product_of_branch_counts(
            body in prop::collection::vec(
                prop::collection::vec(any::<bool>(), 0..4),
                0..5,
            )
        ) {
            let sizes: Vec<usize> = body.iter().map(Vec::len).collect();
            prop_assert_eq!(dnf_product(&body).len(), clause_count(&sizes));
        }

        /// Every clause the product produces picks its statement-`i` element
        /// from statement `i`'s own branch list — a generated clause can never
        /// smuggle in a branch from the wrong statement.
        #[test]
        fn every_clause_draws_position_i_from_statement_is_own_branches(
            body in prop::collection::vec(
                prop::collection::vec(1u32..1000, 1..4),
                1..5,
            )
        ) {
            for clause in dnf_product(&body) {
                prop_assert_eq!(clause.len(), body.len());
                for (i, value) in clause.iter().enumerate() {
                    prop_assert!(body[i].contains(value));
                }
            }
        }
    }
}
