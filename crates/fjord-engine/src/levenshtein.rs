//! The **Levenshtein automaton** that guides a seek.
//!
//! Written here rather than taken from `fst` or `regex-automata`: the engine's
//! dependency closure is a guarded property because it has to build for
//! `wasm32-unknown-unknown`, and a fuzzy match needs four operations, not a
//! regular-expression engine.
//!
//! The state is the **Ukkonen-capped dynamic-programming row** against the term
//! rather than a compiled DFA. Three consequences, and each is load-bearing:
//!
//! - It is a fixed-size `Copy` value, so a transition allocates nothing
//!   ([I9](../../../website/content/invariants.md#i9)).
//! - It is **re-entrant at any string** — replay the candidate from
//!   [`Automaton::start`] — which is the whole of why a guided scan needs no
//!   cursor state of its own ([I4](../../../website/content/invariants.md#i4)).
//! - There is no per-query construction cost to repay, which matters because a
//!   query answering three rows would never repay one.
//!
//! The trap this module exists to avoid is in [`Automaton::next_live_char`]:
//! enumerating Unicode to find the next character worth seeking to. It never has
//! to. Every character *not in the term* drives the same transition — they are
//! all mismatches — so the candidate set is the term's own characters plus one
//! representative of everything else.

/// The longest term an automaton is built for.
///
/// A bound rather than a `Vec` because the row is the state and the state is
/// copied per character; 63 characters is far past any identifier a search box
/// receives, and the refusal is a diagnostic rather than a truncation.
pub const MAX_TERM_CHARS: usize = 63;

/// The largest edit distance accepted. Beyond 3 the automaton is live over so
/// much of the key space that a guided scan is a full scan wearing a hat.
pub const MAX_DISTANCE: u8 = 3;

const MAX_ROW: usize = MAX_TERM_CHARS + 1;

/// One row of the edit-distance matrix: the state of a walk.
///
/// `cells[i]` is the distance between the input consumed so far and the term's
/// first `i` characters, **capped** at `distance + 1` — the cap is what makes two
/// walks that are both already too far apart compare equal, and so what keeps the
/// state space finite.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct State {
    cells: [u8; MAX_ROW],
    len: u8,
}

impl State {
    /// The capped edit-distance row represented by this state.
    #[must_use]
    pub fn row(&self) -> &[u8] {
        &self.cells[..self.len as usize]
    }
}

#[derive(Debug, Clone)]
pub struct Automaton {
    term: Box<[char]>,
    /// The term's characters, sorted and deduplicated — the only characters whose
    /// transition differs from every other character's.
    alphabet: Box<[char]>,
    distance: u8,
}

impl Automaton {
    /// Build for `term` at edit distance `distance`.
    ///
    /// `None` for a term longer than [`MAX_TERM_CHARS`] or a distance above
    /// [`MAX_DISTANCE`]; the caller turns that into a named diagnostic, because a
    /// silently truncated term answers a question nobody asked.
    #[must_use]
    pub fn new(term: &str, distance: u8) -> Option<Automaton> {
        if distance > MAX_DISTANCE {
            return None;
        }

        let term: Box<[char]> = term.chars().collect();
        if term.len() > MAX_TERM_CHARS {
            return None;
        }

        let mut alphabet = term.to_vec();
        alphabet.sort_unstable();
        alphabet.dedup();

        Some(Automaton {
            term,
            alphabet: alphabet.into(),
            distance,
        })
    }

    /// How many characters of a candidate can possibly matter.
    ///
    /// A string longer than `|term| + distance` cannot be within `distance` of the
    /// term — every extra character is one more deletion — so the walk stops there
    /// whatever follows. This is what makes the cost of examining a row
    /// independent of how long its key is.
    #[must_use]
    pub fn max_chars(&self) -> usize {
        self.term.len() + self.distance as usize + 1
    }

    #[must_use]
    pub fn distance(&self) -> u8 {
        self.distance
    }

    #[must_use]
    pub fn start(&self) -> State {
        let cap = self.distance + 1;
        let mut cells = [0u8; MAX_ROW];

        for (i, cell) in cells.iter_mut().enumerate().take(self.term.len() + 1) {
            // The empty input against the term's first `i` characters: `i`
            // deletions, capped.
            *cell = (i as u8).min(cap);
        }

        State {
            cells,
            len: (self.term.len() + 1) as u8,
        }
    }

    /// The state after consuming one more character.
    #[must_use]
    pub fn step(&self, state: &State, c: char) -> State {
        let cap = self.distance + 1;
        let mut cells = [0u8; MAX_ROW];

        cells[0] = (state.cells[0] + 1).min(cap);

        for i in 1..=self.term.len() {
            let substitute = state.cells[i - 1] + u8::from(self.term[i - 1] != c);
            let delete = state.cells[i] + 1;
            let insert = cells[i - 1] + 1;

            cells[i] = substitute.min(delete).min(insert).min(cap);
        }

        State {
            cells,
            len: state.len,
        }
    }

    /// The edit distance, if the input consumed so far **is** a match.
    #[must_use]
    pub fn accepts(&self, state: &State) -> Option<u8> {
        let final_cell = state.cells[state.len as usize - 1];
        (final_cell <= self.distance).then_some(final_cell)
    }

    /// Whether **some** extension of the input consumed so far could still match.
    ///
    /// This is the pruning question, and it is why the walk can stop early: a dead
    /// state means every key sharing this prefix is a non-answer, which is exactly
    /// the licence to seek past all of them.
    #[must_use]
    pub fn live(&self, state: &State) -> bool {
        state.cells[..state.len as usize]
            .iter()
            .any(|&cell| cell <= self.distance)
    }

    /// The **smallest character strictly greater than `after` whose transition
    /// leaves a live state** — or the smallest live character at all, for
    /// `after: None`.
    ///
    /// The whole seek target computation rests on this, and the naive version
    /// enumerates every scalar value. It does not have to: the transition depends
    /// on `c` only through whether it equals each of the term's characters, so
    /// every character outside the term produces the *same* state. The candidate
    /// set is therefore the term's own alphabet plus one representative of
    /// everything else — `O(|term|)` transitions, not `O(0x10FFFF)`.
    #[must_use]
    pub fn next_live_char(&self, state: &State, after: Option<char>) -> Option<char> {
        // The alphabet is sorted, so the first live one above `after` is the
        // smallest live one above `after`.
        let smallest_in_term = self
            .alphabet
            .iter()
            .copied()
            .filter(|&c| after.is_none_or(|a| c > a))
            .find(|&c| self.live(&self.step(state, c)));

        let Some(other) = self.first_char_outside_term(after) else {
            return smallest_in_term;
        };

        // Every character outside the term shares `other`'s transition, so if
        // `other` is live then so is every one above it — and `other` is the
        // smallest of them above `after`.
        let other_is_live =
            smallest_in_term.is_none_or(|c| other < c) && self.live(&self.step(state, other));

        match (smallest_in_term, other_is_live) {
            (Some(c), true) => Some(c.min(other)),
            (Some(c), false) => Some(c),
            (None, true) => Some(other),
            (None, false) => None,
        }
    }

    /// Whether `candidate` — its characters, from a reader that may fail — is
    /// within this automaton's distance of its term.
    ///
    /// **Pulls only as far as it must.** A dead state cannot come back to life, so
    /// a candidate is rejected at the character that killed it however much of it
    /// is left unread; against a lazy decoder that is a bound on decode work
    /// rather than only on arithmetic. No explicit length cap is needed for that:
    /// a candidate longer than `|term| + distance` has every cell of its row above
    /// the distance — an edit distance is never below the length difference — so
    /// [`live`](Self::live) has already stopped the walk.
    ///
    /// Allocates nothing: [`State`] is a fixed-size `Copy` value
    /// ([I9](../../../website/content/invariants.md#i9)).
    pub fn matches<E>(&self, candidate: impl Iterator<Item = Result<char, E>>) -> Result<bool, E> {
        let mut state = self.start();

        for c in candidate {
            state = self.step(&state, c?);

            if !self.live(&state) {
                return Ok(false);
            }
        }

        Ok(self.accepts(&state).is_some())
    }

    /// The smallest scalar value above `after` that the term does not contain.
    fn first_char_outside_term(&self, after: Option<char>) -> Option<char> {
        let mut c = match after {
            None => '\0',
            Some(a) => next_scalar(a)?,
        };

        while self.alphabet.contains(&c) {
            c = next_scalar(c)?;
        }

        Some(c)
    }
}

/// The next Unicode scalar value, stepping over the surrogate range.
///
/// The gap is not a formality here: `char::from_u32` refuses a surrogate, so
/// incrementing without the skip returns `None` in the middle of the range and
/// would silently end a seek walk that has plenty of key space left.
fn next_scalar(c: char) -> Option<char> {
    let next = match c as u32 + 1 {
        0xD800 => 0xE000,
        n => n,
    };

    char::from_u32(next)
}

/// Edit distance `<= distance`, decided directly.
///
/// The one-shot form: build, walk, throw away. A caller deciding many candidates
/// against one term wants [`Automaton::matches`] instead, which is this without
/// the per-candidate build — the executor holds its automaton for the life of the
/// level precisely so a rejected row allocates nothing
/// ([I9](../../../website/content/invariants.md#i9)).
///
/// **A term the automaton will not build for is no match**, rather than a wrong
/// answer. Both real paths refuse such a term by name long before this — the
/// front end at typecheck (`reject/fuzzy-term`, `reject/fuzzy-distance`) and the
/// executor when it opens the level ([`FuzzyTermUnsupported`]) — so this is the
/// backstop, and it is here because the arithmetic it replaced did not have one:
/// a 256-character term wrapped its own length to zero in a `u8` and reported the
/// empty string as a match.
///
/// [`FuzzyTermUnsupported`]: crate::error::FjordError::FuzzyTermUnsupported
#[must_use]
pub fn within(term: &str, candidate: &str, distance: u8) -> bool {
    Automaton::new(term, distance).is_some_and(|automaton| {
        automaton
            .matches(candidate.chars().map(Ok::<char, ()>))
            .unwrap_or(false)
    })
}

#[cfg(test)]
mod tests {
    use super::*;

    /// The oracle: a full Wagner-Fischer matrix, uncapped, written out
    /// independently of everything above it. The capped row and the early exits
    /// are the whole of what the implementation adds, so an oracle sharing them
    /// would agree by construction.
    fn edit_distance(a: &str, b: &str) -> usize {
        let a: Vec<char> = a.chars().collect();
        let b: Vec<char> = b.chars().collect();

        let mut matrix = vec![vec![0usize; b.len() + 1]; a.len() + 1];

        for (i, row) in matrix.iter_mut().enumerate() {
            row[0] = i;
        }
        for (j, cell) in matrix[0].iter_mut().enumerate() {
            *cell = j;
        }

        for i in 1..=a.len() {
            for j in 1..=b.len() {
                let substitute = matrix[i - 1][j - 1] + usize::from(a[i - 1] != b[j - 1]);
                matrix[i][j] = substitute
                    .min(matrix[i - 1][j] + 1)
                    .min(matrix[i][j - 1] + 1);
            }
        }

        matrix[a.len()][b.len()]
    }

    fn walk(automaton: &Automaton, s: &str) -> State {
        s.chars()
            .fold(automaton.start(), |state, c| automaton.step(&state, c))
    }

    const WORDS: &[&str] = &[
        "",
        "a",
        "ab",
        "parse",
        "parser",
        "parse_str",
        "pars",
        "prase",
        "arse",
        "xarse",
        "parsee",
        "PARSE",
        "encode",
        "p",
        "pa",
        "par",
        "parsx",
        "aprse",
        "zzz",
        "paarse",
    ];

    #[test]
    fn acceptance_agrees_with_an_independent_matrix() {
        let mut accepted = 0;
        let mut rejected = 0;

        for term in WORDS {
            for distance in 0..=MAX_DISTANCE {
                let automaton = Automaton::new(term, distance).expect("short term");

                for candidate in WORDS {
                    let expected = edit_distance(term, candidate) <= distance as usize;
                    let state = walk(&automaton, candidate);
                    let actual = automaton.accepts(&state).is_some();

                    assert_eq!(
                        actual, expected,
                        "{term:?} ~{distance} against {candidate:?}"
                    );

                    if expected {
                        accepted += 1;
                    } else {
                        rejected += 1;
                    }
                }
            }
        }

        // A census: a population that had drifted to all-accept or all-reject
        // would leave the assertion above green and vacuous.
        assert!(
            accepted > 100,
            "too few matches in the population: {accepted}"
        );
        assert!(rejected > 100, "too few non-matches: {rejected}");
    }

    #[test]
    fn the_reported_distance_is_the_real_one() {
        for term in WORDS {
            let automaton = Automaton::new(term, MAX_DISTANCE).expect("short term");

            for candidate in WORDS {
                let expected = edit_distance(term, candidate);
                if expected > MAX_DISTANCE as usize {
                    continue;
                }

                let state = walk(&automaton, candidate);
                assert_eq!(
                    automaton.accepts(&state),
                    Some(expected as u8),
                    "{term:?} against {candidate:?}"
                );
            }
        }
    }

    /// Liveness is the pruning claim, and getting it wrong in the *permissive*
    /// direction only costs time — in the strict direction it drops answers. So
    /// the property is stated as the thing that must never happen: a state
    /// declared dead whose extension matches.
    #[test]
    fn a_dead_state_has_no_matching_extension() {
        for term in WORDS {
            for distance in 0..=2 {
                let automaton = Automaton::new(term, distance).expect("short term");

                for candidate in WORDS {
                    let state = walk(&automaton, candidate);
                    if automaton.live(&state) {
                        continue;
                    }

                    for suffix in WORDS {
                        let extended = format!("{candidate}{suffix}");
                        assert!(
                            edit_distance(term, &extended) > distance as usize,
                            "{term:?} ~{distance}: {candidate:?} declared dead, \
                             but {extended:?} matches"
                        );
                    }
                }
            }
        }
    }

    /// The seek target's correctness condition, checked by brute force over a
    /// small alphabet: nothing live may be skipped over.
    #[test]
    fn next_live_char_skips_nothing_live() {
        let alphabet: Vec<char> = "abcpre_".chars().collect();

        for term in ["parse", "par", "ab", "a"] {
            for distance in 0..=2 {
                let automaton = Automaton::new(term, distance).expect("short term");

                for prefix in ["", "a", "p", "pa", "par", "pars", "zz"] {
                    let state = walk(&automaton, prefix);

                    for after in alphabet.iter().copied().map(Some).chain([None]) {
                        let answer = automaton.next_live_char(&state, after);

                        let brute = alphabet
                            .iter()
                            .copied()
                            .filter(|&c| after.is_none_or(|a| c > a))
                            .find(|&c| automaton.live(&automaton.step(&state, c)));

                        match (answer, brute) {
                            (Some(answer), Some(brute)) => assert!(
                                answer <= brute,
                                "{term:?} ~{distance} after {prefix:?}: answered {answer:?} \
                                 but {brute:?} is live and smaller"
                            ),
                            (None, Some(brute)) => panic!(
                                "{term:?} ~{distance} after {prefix:?}: answered nothing \
                                 but {brute:?} is live"
                            ),
                            // Answering *something* where brute force over this
                            // small alphabet found nothing is fine: the answer may
                            // be a character outside it.
                            (Some(_), None) | (None, None) => {}
                        }
                    }
                }
            }
        }
    }

    /// Whatever `next_live_char` answers must actually be live — the other half
    /// of the property above, and the one that stops a seek landing on a key that
    /// was never going to match.
    #[test]
    fn next_live_char_answers_a_live_character() {
        for term in ["parse", "encode", "a", ""] {
            for distance in 0..=2 {
                let automaton = Automaton::new(term, distance).expect("short term");

                for prefix in ["", "p", "pa", "zz", "parse"] {
                    let state = walk(&automaton, prefix);

                    if let Some(c) = automaton.next_live_char(&state, Some('a')) {
                        assert!(c > 'a');
                        assert!(
                            automaton.live(&automaton.step(&state, c)),
                            "{term:?} ~{distance} after {prefix:?}: {c:?} is not live"
                        );
                    }
                }
            }
        }
    }

    #[test]
    fn the_residual_form_agrees_with_the_walk() {
        for term in WORDS {
            for distance in 0..=MAX_DISTANCE {
                let automaton = Automaton::new(term, distance).expect("short term");

                for candidate in WORDS {
                    let walked = automaton.accepts(&walk(&automaton, candidate)).is_some();
                    assert_eq!(
                        within(term, candidate, distance),
                        walked,
                        "{term:?} ~{distance} against {candidate:?}"
                    );
                }
            }
        }
    }

    /// A residual receives the same source-language term a guide does, so the
    /// guide's fixed-state bound must not turn into integer truncation when the
    /// physical key order makes the match a filter instead. At 256 characters,
    /// casting the empty-input distance to `u8` wraps it to zero and would make
    /// the empty string look like a match at distance one.
    #[test]
    fn an_oversized_residual_term_never_wraps_into_a_match() {
        let term = "a".repeat(256);

        assert!(!within(&term, "", 1));
    }

    #[test]
    fn a_term_that_is_too_long_or_too_far_is_refused() {
        let long: String = "a".repeat(MAX_TERM_CHARS + 1);
        assert!(Automaton::new(&long, 1).is_none());
        assert!(Automaton::new("parse", MAX_DISTANCE + 1).is_none());
        assert!(Automaton::new(&"a".repeat(MAX_TERM_CHARS), 1).is_some());
    }

    /// Non-ASCII is the case where a byte-level automaton would give the wrong
    /// answer: one accented character is one edit, not two.
    #[test]
    fn a_multi_byte_character_is_one_edit() {
        let automaton = Automaton::new("café", 1).expect("short term");

        assert!(automaton.accepts(&walk(&automaton, "cafe")).is_some());
        assert!(automaton.accepts(&walk(&automaton, "café")).is_some());
        assert!(automaton.accepts(&walk(&automaton, "caf")).is_some());
        assert!(automaton.accepts(&walk(&automaton, "xyzw")).is_none());
    }

    #[test]
    fn the_surrogate_gap_is_stepped_over() {
        assert_eq!(next_scalar('\u{D7FF}'), Some('\u{E000}'));
        assert_eq!(next_scalar('a'), Some('b'));
        assert_eq!(next_scalar(char::MAX), None);
    }
}
