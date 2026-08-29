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

/// **Which question the automaton is asked** — the whole stored string, or a
/// prefix of it.
///
/// One type carried from the source text through to the plan rather than two
/// pattern kinds, because everything between the two spellings treats them
/// identically: both are string patterns, both end a seek prefix, both may guide
/// or filter. Only three places care which — the acceptance predicate
/// ([`Automaton::matches_anchored`]), the order two of them are applied in, and
/// the plan fingerprint.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum FuzzyAnchor {
    /// `"parse"~2` — within the distance of the **whole** candidate.
    Whole,
    /// `"parse"~<2` — within the distance of **some prefix** of the candidate.
    Prefix,
}

impl FuzzyAnchor {
    /// The byte a plan fingerprint folds in.
    ///
    /// Not decoration: a cursor is accepted on a plan fingerprint, so two plans
    /// differing only in which question they ask must not accept each other's —
    /// the same reason `Prefix` and `NotPrefix` carry distinct tags.
    #[must_use]
    pub const fn tag(self) -> u8 {
        match self {
            FuzzyAnchor::Whole => 0,
            FuzzyAnchor::Prefix => 1,
        }
    }
}

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
    /// **The same number for both questions**, and for the same arithmetic. A
    /// prefix longer than `|term| + distance` has every cell of its row above the
    /// distance — an edit distance is never below the length difference — so it
    /// can neither be a match nor be extended into one, and the walk stops there
    /// whatever follows. Whole-string reaches that bound by the candidate simply
    /// being too long; anchored reaches it by every *later* prefix being too long,
    /// an earlier one having already accepted or nothing having. Either way this
    /// is what makes the cost of examining a row independent of how long its key
    /// is.
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

    /// Whether **some prefix** of `candidate` is within this automaton's distance
    /// of its term — the anchored question, `"parse"~<1`.
    ///
    /// The difference from [`matches`](Self::matches) is one of *where* acceptance
    /// is asked, and everything else about the walk is the same machine. Three
    /// facts make that cheap, and each is load-bearing rather than incidental:
    ///
    /// - **The liveness exit cannot cut a match short.** If a prefix accepts then
    ///   every prefix of *it* is live — it is its own witnessing extension — so
    ///   the walk always reaches the first accepting one.
    /// - **The length bound is unchanged.** A prefix longer than
    ///   `|term| + distance` has every cell of its row above the distance, so
    ///   [`live`](Self::live) has already stopped the walk;
    ///   [`max_chars`](Self::max_chars) is the same number for both questions.
    /// - **A match is upward-closed.** Once a prefix accepts, every extension of
    ///   the candidate matches too, which is what licenses the guide to stop
    ///   reading rather than seek.
    ///
    /// A term no longer than `distance` accepts the **empty** prefix and so matches
    /// every stored string. That is the definition rather than an oversight, and
    /// the language documents it rather than refusing it —
    /// `a_term_no_longer_than_its_distance_matches_everything` is the test that
    /// stops it drifting into a silent refusal.
    ///
    /// Allocates nothing: [`State`] is a fixed-size `Copy` value
    /// ([I9](../../../website/content/invariants.md#i9)).
    pub fn matches_prefix<E>(
        &self,
        candidate: impl Iterator<Item = Result<char, E>>,
    ) -> Result<bool, E> {
        let mut state = self.start();

        if self.accepts(&state).is_some() {
            return Ok(true);
        }

        for c in candidate {
            state = self.step(&state, c?);

            if self.accepts(&state).is_some() {
                return Ok(true);
            }

            if !self.live(&state) {
                return Ok(false);
            }
        }

        Ok(false)
    }

    /// The answer to whichever question `anchor` names.
    ///
    /// The one place a caller chooses, so no path can drift into asking the other
    /// one: the executor's residual and the compiler's constant fold both come
    /// through here.
    pub fn matches_anchored<E>(
        &self,
        anchor: FuzzyAnchor,
        candidate: impl Iterator<Item = Result<char, E>>,
    ) -> Result<bool, E> {
        match anchor {
            FuzzyAnchor::Whole => self.matches(candidate),
            FuzzyAnchor::Prefix => self.matches_prefix(candidate),
        }
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

/// Some prefix within `distance` edits, decided directly.
///
/// The one-shot form of [`Automaton::matches_prefix`], and [`within`]'s anchored
/// sibling; the same backstop applies, for the same reason — **a term the
/// automaton will not build for is no match** rather than a wrong answer.
#[must_use]
pub fn within_prefix(term: &str, candidate: &str, distance: u8) -> bool {
    Automaton::new(term, distance).is_some_and(|automaton| {
        automaton
            .matches_prefix(candidate.chars().map(Ok::<char, ()>))
            .unwrap_or(false)
    })
}

/// Canonical strategies for the matcher's two domains — a term and a candidate.
///
/// Drawn **together** rather than independently, which is the whole of why this
/// module exists: two unrelated strings over any useful alphabet are almost never
/// within three edits of one another, so an independent pair leaves every
/// acceptance property green and saying nothing. A candidate is grown from the
/// term by a few edits and then given a suffix — the one shape that separates an
/// anchored match from a whole-string one.
/// [`within`] or [`within_prefix`], as `anchor` says.
///
/// What the compiler folds a fuzzy match against a constant with, and what the
/// differential model decides a row by.
#[must_use]
pub fn within_anchored(term: &str, candidate: &str, distance: u8, anchor: FuzzyAnchor) -> bool {
    match anchor {
        FuzzyAnchor::Whole => within(term, candidate, distance),
        FuzzyAnchor::Prefix => within_prefix(term, candidate, distance),
    }
}

#[cfg(any(test, feature = "proptest"))]
pub mod proptest {
    use super::MAX_DISTANCE;
    use ::proptest::prelude::*;

    /// The alphabet every generated string is drawn from.
    ///
    /// Deliberately small and deliberately repetitive: the interesting cases are
    /// near-misses, and a wide alphabet draws strings that mismatch at every
    /// character. `é` is in it because a byte-level automaton would count one
    /// accented character as two edits, and no property here would notice.
    const ALPHABET: [char; 8] = ['a', 'b', 'c', 'e', 'p', 'r', 's', 'é'];

    /// The longest term drawn. Far below [`MAX_TERM_CHARS`](super::MAX_TERM_CHARS),
    /// which is the refusal boundary and has its own test — a term that long has
    /// no near-misses in a candidate this generator would ever draw.
    const MAX_DRAWN_TERM: usize = 8;

    fn letter() -> impl Strategy<Value = char> {
        ::proptest::sample::select(ALPHABET.as_slice())
    }

    fn text(len: std::ops::Range<usize>) -> impl Strategy<Value = String> {
        ::proptest::collection::vec(letter(), len).prop_map(|cs| cs.into_iter().collect())
    }

    /// An edit distance an automaton is built for.
    pub fn distance() -> impl Strategy<Value = u8> {
        1u8..=MAX_DISTANCE
    }

    /// A term an automaton is built for: never empty, never past the bound.
    pub fn term() -> impl Strategy<Value = String> {
        text(1..MAX_DRAWN_TERM)
    }

    /// What one of the term's characters becomes while a candidate is grown from
    /// it.
    #[derive(Debug, Clone, Copy)]
    enum Edit {
        Keep,
        Drop,
        Replace(char),
        Insert(char),
    }

    fn edit() -> impl Strategy<Value = Edit> {
        prop_oneof![
            6 => Just(Edit::Keep),
            1 => Just(Edit::Drop),
            1 => letter().prop_map(Edit::Replace),
            1 => letter().prop_map(Edit::Insert),
        ]
    }

    /// A candidate grown from the term, plus a suffix of its own.
    ///
    /// The suffix is what makes the population able to tell the two questions
    /// apart: it costs a whole-string match one deletion per character and an
    /// anchored one nothing at all.
    fn grown() -> impl Strategy<Value = (String, String)> {
        term()
            .prop_flat_map(|term| {
                let edits = term.chars().count();
                (
                    Just(term),
                    ::proptest::collection::vec(edit(), edits),
                    text(0..10),
                )
            })
            .prop_map(|(term, edits, suffix)| {
                let mut candidate = String::new();

                for (c, edit) in term.chars().zip(edits) {
                    match edit {
                        Edit::Keep => candidate.push(c),
                        Edit::Drop => {}
                        Edit::Replace(other) => candidate.push(other),
                        Edit::Insert(other) => {
                            candidate.push(other);
                            candidate.push(c);
                        }
                    }
                }

                candidate.push_str(&suffix);
                (term, candidate)
            })
    }

    /// A term and a candidate to decide it against.
    ///
    /// Mostly grown, but not only: an unrelated pair is what keeps the rejecting
    /// half of every property populated, and the census asserts both halves are.
    pub fn term_and_candidate() -> impl Strategy<Value = (String, String)> {
        prop_oneof![
            4 => grown(),
            1 => (term(), text(0..12)),
        ]
    }
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

    // ---- the anchored question ---------------------------------------------

    /// The anchored oracle: the whole matrix against **every** prefix, taking the
    /// best. Written over [`edit_distance`] above — which is itself independent of
    /// the capped row — so nothing the implementation does is assumed here.
    fn prefix_distance(term: &str, candidate: &str) -> usize {
        let chars: Vec<char> = candidate.chars().collect();

        (0..=chars.len())
            .map(|k| edit_distance(term, &chars[..k].iter().collect::<String>()))
            .min()
            .expect("the empty prefix is always one of them")
    }

    #[test]
    fn prefix_acceptance_agrees_with_an_independent_matrix() {
        let mut accepted = 0;
        let mut rejected = 0;

        for term in WORDS {
            for distance in 0..=MAX_DISTANCE {
                for candidate in WORDS {
                    let expected = prefix_distance(term, candidate) <= distance as usize;
                    let actual = within_prefix(term, candidate, distance);

                    assert_eq!(
                        actual, expected,
                        "{term:?} ~<{distance} against {candidate:?}"
                    );

                    if expected {
                        accepted += 1;
                    } else {
                        rejected += 1;
                    }
                }
            }
        }

        // A census, as for the whole-string form: anchoring accepts strictly more,
        // so the population that could drift to vacuity here is the rejecting one.
        assert!(
            accepted > 100,
            "too few matches in the population: {accepted}"
        );
        assert!(rejected > 100, "too few non-matches: {rejected}");
    }

    /// **The property the two questions differ by.** A whole-string match is
    /// destroyed by a long enough suffix; an anchored one survives every suffix,
    /// and that is what licenses a guide to stop reading a row rather than compute
    /// a seek target from it.
    #[test]
    fn a_matching_prefix_survives_every_suffix() {
        let mut witnessed = 0;

        for term in WORDS {
            for distance in 1..=MAX_DISTANCE {
                for candidate in WORDS {
                    if !within_prefix(term, candidate, distance) {
                        continue;
                    }

                    for suffix in WORDS {
                        let extended = format!("{candidate}{suffix}");
                        assert!(
                            within_prefix(term, &extended, distance),
                            "{term:?} ~<{distance}: {candidate:?} matches, \
                             but {extended:?} does not"
                        );

                        witnessed += usize::from(!suffix.is_empty());
                    }
                }
            }
        }

        assert!(witnessed > 100, "too few extensions witnessed: {witnessed}");
    }

    /// Anchoring only ever accepts more, because a candidate is one of its own
    /// prefixes. Stated as an implication rather than an equality precisely
    /// because the converse is the feature.
    #[test]
    fn a_whole_string_match_is_a_prefix_match() {
        let mut strictly_more = 0;

        for term in WORDS {
            for distance in 0..=MAX_DISTANCE {
                for candidate in WORDS {
                    let whole = within(term, candidate, distance);
                    let prefix = within_prefix(term, candidate, distance);

                    assert!(
                        !whole || prefix,
                        "{term:?} ~{distance} matches {candidate:?} but ~<{distance} does not"
                    );

                    strictly_more += usize::from(prefix && !whole);
                }
            }
        }

        // Without this the implication above is satisfied by a matcher that simply
        // answers the whole-string question.
        assert!(
            strictly_more > 20,
            "anchoring never reached further than the whole-string form: {strictly_more}"
        );
    }

    /// A term no longer than the distance is within it of the **empty** prefix, so
    /// it matches every stored string. That is the definition and not an oversight
    /// — pinned here so it cannot drift into a silent refusal, and so the book's
    /// paragraph about it has an owner.
    #[test]
    fn a_term_no_longer_than_its_distance_matches_everything() {
        for term in ["a", "ab", "abc"] {
            let distance = term.chars().count() as u8;

            for candidate in WORDS {
                assert!(
                    within_prefix(term, candidate, distance),
                    "{term:?} ~<{distance} did not match {candidate:?}"
                );
            }
        }
    }

    /// The backstop [`an_oversized_residual_term_never_wraps_into_a_match`] guards
    /// for the whole-string form: at 256 characters, casting the empty-input
    /// distance to `u8` wraps it to zero — and the anchored form asks about the
    /// empty prefix *first*, so it would answer `true` for every candidate.
    #[test]
    fn an_oversized_prefix_term_never_wraps_into_a_match() {
        let term = "a".repeat(256);

        assert!(!within_prefix(&term, "", 1));
        assert!(!within_prefix(&term, "zzz", 1));
    }

    /// The four cases [issue #22] was filed with, pinned as examples rather than
    /// left to the properties: the third is the one a person doubts, and the
    /// fourth is the whole difference between anchored and substring search.
    ///
    /// [issue #22]: https://github.com/boxops-uk/fjord/issues/22
    #[test]
    fn a_misspelt_term_reaches_the_identifier_it_prefixes() {
        assert!(within_prefix("parse", "parse_node", 1));
        assert!(within_prefix("parsr", "parser_function", 1));
        assert!(within_prefix("prser", "parser_function", 1));

        // Anchored, not substring: the term has to reach the *start* of the key.
        assert!(!within_prefix("parsr", "my_parser_function", 1));

        // And none of them is a whole-string match, which is why `~` alone left
        // search-as-you-type with nothing to answer.
        for candidate in ["parse_node", "parser_function", "my_parser_function"] {
            assert!(!within("parse", candidate, 1));
            assert!(!within("parsr", candidate, 1));
        }
    }

    /// A candidate reader that counts what the walk pulled out of it.
    ///
    /// The bound is on *decode work*, not on arithmetic: against the lazy
    /// `StrChars` the executor hands it, a pull is a character actually decoded.
    struct Counting<'a> {
        chars: std::str::Chars<'a>,
        pulled: &'a std::cell::Cell<usize>,
    }

    impl Iterator for Counting<'_> {
        type Item = Result<char, ()>;

        fn next(&mut self) -> Option<Self::Item> {
            let c = self.chars.next()?;
            self.pulled.set(self.pulled.get() + 1);
            Some(Ok(c))
        }
    }

    /// **The bound a long key costs nothing beyond**, made mechanical rather than
    /// argued. Rejection stops at the character that killed the row; acceptance
    /// stops at the accepting prefix. Neither reads the rest, however much there
    /// is of it.
    #[test]
    fn a_prefix_walk_pulls_no_more_than_the_term_can_reach() {
        for term in ["parse", "a", "café"] {
            for distance in 1..=MAX_DISTANCE {
                let automaton = Automaton::new(term, distance).expect("short term");

                for head in ["parse", "zzzz", "", "parsx", "café"] {
                    let candidate = format!("{head}{}", "x".repeat(500));
                    let pulled = std::cell::Cell::new(0);

                    let walked = automaton
                        .matches_prefix(Counting {
                            chars: candidate.chars(),
                            pulled: &pulled,
                        })
                        .expect("the reader cannot fail");

                    assert_eq!(
                        walked,
                        within_prefix(term, &candidate, distance),
                        "{term:?} ~<{distance} against {candidate:?}"
                    );

                    assert!(
                        pulled.get() <= automaton.max_chars(),
                        "{term:?} ~<{distance} pulled {} characters of {candidate:?}, \
                         past the bound of {}",
                        pulled.get(),
                        automaton.max_chars()
                    );
                }
            }
        }
    }

    mod generated {
        use super::*;
        use crate::levenshtein::proptest::{distance, term_and_candidate};
        use ::proptest::prelude::*;

        proptest! {
            /// The streaming walk against the offline definition. The walk exits at
            /// the first dead state, so this is what says that exit can never cut a
            /// match short — a claim the exhaustive corpus above can only sample.
            #[test]
            fn the_streaming_walk_agrees_with_the_offline_answer(
                (term, candidate) in term_and_candidate(),
                distance in distance(),
            ) {
                prop_assert_eq!(
                    within_prefix(&term, &candidate, distance),
                    prefix_distance(&term, &candidate) <= distance as usize,
                    "{:?} ~<{} against {:?}", term, distance, candidate
                );
            }

            /// Upward closure again, over generated suffixes rather than a fixed
            /// word list — the guide's licence to stop reading is only as good as
            /// the population this was checked over.
            #[test]
            fn a_generated_matching_prefix_survives_its_suffix(
                (term, candidate) in term_and_candidate(),
                distance in distance(),
                suffix in "[a-z]{0,12}",
            ) {
                prop_assume!(within_prefix(&term, &candidate, distance));

                prop_assert!(
                    within_prefix(&term, &format!("{candidate}{suffix}"), distance),
                    "{:?} ~<{} lost {:?} to the suffix {:?}", term, distance, candidate, suffix
                );
            }
        }

        /// **The census.** Every property above is satisfied by a generator that
        /// draws nothing but rejections, and the one class that matters — a
        /// candidate the anchored question accepts and the whole-string one does
        /// not — is the one an unlucky alphabet would lose first.
        #[test]
        fn the_population_reaches_all_three_classes() {
            use ::proptest::strategy::{Strategy, ValueTree};
            use ::proptest::test_runner::TestRunner;

            const RUNS: usize = 512;

            let mut runner = TestRunner::deterministic();
            let (mut whole, mut anchored_only, mut neither) = (0usize, 0usize, 0usize);

            for _ in 0..RUNS {
                let ((term, candidate), distance) = (term_and_candidate(), distance())
                    .new_tree(&mut runner)
                    .expect("a strategy with no assumptions")
                    .current();

                match (
                    within(&term, &candidate, distance),
                    within_prefix(&term, &candidate, distance),
                ) {
                    (true, _) => whole += 1,
                    (false, true) => anchored_only += 1,
                    (false, false) => neither += 1,
                }
            }

            let mut missing = vec![];
            if whole == 0 {
                missing.push("a whole-string match");
            }
            if anchored_only < RUNS / 20 {
                missing.push("enough anchored-only matches");
            }
            if neither == 0 {
                missing.push("a non-match");
            }

            assert!(
                missing.is_empty(),
                "{RUNS} draws never reached {} \
                 (whole {whole}, anchored-only {anchored_only}, neither {neither})",
                missing.join(" or ")
            );
        }
    }
}
