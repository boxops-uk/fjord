//! A Levenshtein automaton walk, as a page can teach it.
//!
//! The automaton's fixed-size state remains an engine internal. This view copies
//! only the meaningful row after each transition, so the browser can explain
//! the same machine the guided seek uses without making that state a JSON
//! contract.

use fjord_engine::levenshtein::Automaton;
use serde::Serialize;

/// One automaton transition, including the start state at `at == 0`.
#[derive(Debug, Clone, PartialEq, Eq, Serialize)]
pub struct FuzzyStep {
    pub at: usize,
    /// The character consumed by this transition; absent for the start state.
    pub input: Option<String>,
    pub consumed: String,
    /// The capped edit distance to every prefix named by [`FuzzyWalk::columns`].
    pub row: Vec<u8>,
    /// Whether some extension of `consumed` can still match.
    pub live: bool,
    /// The exact distance when `consumed` is already a match.
    pub accepts: Option<u8>,
}

/// A complete, bounded walk of one candidate through the fuzzy matcher.
#[derive(Debug, Clone, PartialEq, Eq, Serialize)]
pub struct FuzzyWalk {
    pub term: String,
    pub candidate: String,
    pub distance: u8,
    pub cap: u8,
    /// The term prefixes each cell in a row measures against.
    pub columns: Vec<String>,
    pub steps: Vec<FuzzyStep>,
}

impl FuzzyWalk {
    /// Build the view, or refuse the same unsupported term and distance the
    /// query front end refuses by name.
    #[must_use]
    pub fn new(term: &str, candidate: &str, distance: u8) -> Option<Self> {
        let automaton = Automaton::new(term, distance)?;
        let mut columns = vec!["∅".to_owned()];
        let mut prefix = String::new();
        for c in term.chars() {
            prefix.push(c);
            columns.push(prefix.clone());
        }

        let mut state = automaton.start();
        let mut steps = vec![view_step(&automaton, &state, 0, None, String::new())];
        let mut consumed = String::new();

        for (at, input) in candidate.chars().enumerate() {
            consumed.push(input);
            state = automaton.step(&state, input);
            steps.push(view_step(
                &automaton,
                &state,
                at + 1,
                Some(input),
                consumed.clone(),
            ));
            if !automaton.live(&state) {
                break;
            }
        }

        Some(Self {
            term: term.to_owned(),
            candidate: candidate.to_owned(),
            distance,
            cap: distance + 1,
            columns,
            steps,
        })
    }
}

/// Walk one candidate through the automaton.
#[must_use]
pub fn fuzzy(term: &str, candidate: &str, distance: u8) -> Option<FuzzyWalk> {
    FuzzyWalk::new(term, candidate, distance)
}

/// The same view, already JSON. An unsupported walk is `null`, never clamped.
#[must_use]
pub fn fuzzy_json(term: &str, candidate: &str, distance: u8) -> String {
    serde_json::to_string(&fuzzy(term, candidate, distance)).expect("a fuzzy walk serialises")
}

fn view_step(
    automaton: &Automaton,
    state: &fjord_engine::levenshtein::State,
    at: usize,
    input: Option<char>,
    consumed: String,
) -> FuzzyStep {
    FuzzyStep {
        at,
        input: input.map(String::from),
        consumed,
        row: state.row().to_vec(),
        live: automaton.live(state),
        accepts: automaton.accepts(state),
    }
}
