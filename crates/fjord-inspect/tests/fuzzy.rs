use fjord_inspect::{FuzzyWalk, fuzzy, fuzzy_json};

#[test]
fn a_walk_exposes_the_automaton_row_after_every_character() {
    let walk = fuzzy("cat", "cut", 1, false).expect("the worked example is within the bounds");

    assert_eq!(walk.columns, ["∅", "c", "ca", "cat"]);
    assert_eq!(walk.steps.len(), 4);
    assert_eq!(walk.steps[0].row, [0, 1, 2, 2]);
    assert_eq!(walk.steps[1].row, [1, 0, 1, 2]);
    assert_eq!(walk.steps[2].row, [2, 1, 1, 2]);
    assert_eq!(walk.steps[3].row, [2, 2, 2, 1]);
    assert_eq!(walk.steps[3].accepts, Some(1));
    assert!(walk.steps[3].live);
}

#[test]
fn a_walk_marks_the_first_prefix_that_no_extension_can_rescue() {
    let walk = fuzzy("cat", "zzzz", 1, false).expect("the worked example is within the bounds");

    assert!(walk.steps[1].live);
    assert!(!walk.steps[2].live);
    assert_eq!(walk.steps[2].consumed, "zz");
    assert_eq!(walk.steps.len(), 3, "a dead state cannot come back to life");
}

#[test]
fn the_json_boundary_serialises_the_view_not_the_engine_state() {
    let json: serde_json::Value =
        serde_json::from_str(&fuzzy_json("cat", "cut", 1, false)).expect("the view is JSON");

    assert_eq!(json["term"], "cat");
    assert_eq!(json["steps"][2]["input"], "u");
    assert_eq!(json["steps"][3]["accepts"], 1);
}

#[test]
fn an_unsupported_walk_is_refused_instead_of_clamped() {
    assert!(FuzzyWalk::new("cat", "cut", 4, false).is_none());
}

/// The anchored walk stops at the **first** accepting prefix, because every
/// extension of it matches too — so the page teaching `~<` shows a walk that ends
/// where the answer was decided rather than where the candidate ran out.
#[test]
fn an_anchored_walk_stops_at_the_first_accepting_prefix() {
    let whole = fuzzy("cat", "cattle", 1, false).expect("within the bounds");
    let anchored = fuzzy("cat", "cattle", 1, true).expect("within the bounds");

    // Whole-string: `cattle` is three edits from `cat`, so the walk runs until the
    // state dies and the last step accepts nothing.
    assert!(whole.steps.last().expect("a step").accepts.is_none());

    // Anchored: the walk ends at the **first** accepting prefix, which is `ca` at
    // one edit — before the term is even fully consumed. That is the case worth
    // showing on the page: acceptance is a property of a prefix, not of the end.
    let last = anchored.steps.last().expect("a step");
    assert_eq!(last.consumed, "ca");
    assert_eq!(last.accepts, Some(1));
    assert_eq!(anchored.steps.len(), 3, "the walk read past its answer");

    // The rows themselves are the same machine; only where it stops differs.
    for (a, b) in anchored.steps.iter().zip(whole.steps.iter()) {
        assert_eq!(a.row, b.row);
    }
}

/// A short term may accept the **empty** prefix, so the inspection view must stop
/// at the start state exactly as the executor does. Reading even one character
/// here would make the browser teach a different decision point and decode cost
/// from the matcher it claims to expose.
#[test]
fn an_anchored_walk_that_accepts_empty_reads_no_candidate() {
    let walk = fuzzy("a", "anything", 1, true).expect("within the bounds");

    assert_eq!(walk.steps.len(), 1, "the walk read past the empty prefix");
    let start = walk.steps.first().expect("the start state");
    assert_eq!(start.at, 0);
    assert_eq!(start.input, None);
    assert_eq!(start.consumed, "");
    assert_eq!(start.accepts, Some(1));
}

#[test]
fn the_json_boundary_says_which_question_was_asked() {
    let anchored: serde_json::Value =
        serde_json::from_str(&fuzzy_json("cat", "cattle", 1, true)).expect("the view is JSON");
    let whole: serde_json::Value =
        serde_json::from_str(&fuzzy_json("cat", "cattle", 1, false)).expect("the view is JSON");

    assert_eq!(anchored["anchored"], true);
    assert_eq!(whole["anchored"], false);
}
