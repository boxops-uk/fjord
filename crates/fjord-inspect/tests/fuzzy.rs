use fjord_inspect::{FuzzyWalk, fuzzy, fuzzy_json};

#[test]
fn a_walk_exposes_the_automaton_row_after_every_character() {
    let walk = fuzzy("cat", "cut", 1).expect("the worked example is within the bounds");

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
    let walk = fuzzy("cat", "zzzz", 1).expect("the worked example is within the bounds");

    assert!(walk.steps[1].live);
    assert!(!walk.steps[2].live);
    assert_eq!(walk.steps[2].consumed, "zz");
    assert_eq!(walk.steps.len(), 3, "a dead state cannot come back to life");
}

#[test]
fn the_json_boundary_serialises_the_view_not_the_engine_state() {
    let json: serde_json::Value =
        serde_json::from_str(&fuzzy_json("cat", "cut", 1)).expect("the view is JSON");

    assert_eq!(json["term"], "cat");
    assert_eq!(json["steps"][2]["input"], "u");
    assert_eq!(json["steps"][3]["accepts"], 1);
}

#[test]
fn an_unsupported_walk_is_refused_instead_of_clamped() {
    assert!(FuzzyWalk::new("cat", "cut", 4).is_none());
}
