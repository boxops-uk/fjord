//! The **plan** — what the query actually does, and in what order.
//!
//! This is the argument for the whole exercise. A reader who has seen the tokens
//! and the tree has seen the query restated; the plan is where the compiler's
//! decisions become visible: which predicate is scanned first, what the seek key
//! pins, which comparisons narrowed a scan and which are filtering rows that had
//! to be read anyway.
//!
//! **The text of each step is the engine's own** — [`print::steps`](fjord_engine::print::steps), the same
//! renderer `fjord query --plan` shows. That is deliberate: a second rendering
//! would decode stored bytes a second way, and the places it would differ are
//! exactly the ones worth reading (a constant's type, a union alternative's
//! name, which field a path names). What this view adds is *structure* around
//! that text — the step's kind, the register it fills, the predicates it reads —
//! so a page can address a step rather than parse a line.
//!
//! **Levels are not steps, and the distinction is load-bearing.** A cursor holds
//! one row per *level*, a derive and a test bind nothing, and `Plan::levels` is
//! not `body.len()` — so both counts are carried, named apart.

use fjord_engine::{
    levenshtein::FuzzyAnchor,
    plan::{FieldPath, Plan, ResidualOp, Source, Step, Test},
    print,
};
use fjord_schema::schema::{LocalInterner, PredicateTy, Schema};
use serde::Serialize;

/// The fuzzy matcher attached to one source, in enough structure to walk the
/// row the executor is showing through the same automaton.
#[derive(Debug, Clone, PartialEq, Eq, Serialize)]
pub struct FuzzyView {
    pub source: usize,
    pub guide: bool,
    /// Which residual this is within its source; absent for the guide itself.
    pub residual: Option<usize>,
    pub term: String,
    pub distance: u8,
    /// `"parse"~<2` rather than `"parse"~2` — the walk accepts at the first
    /// prefix within the distance instead of measuring the whole stored string.
    ///
    /// A `bool` rather than the engine's own `FuzzyAnchor`, for the reason
    /// nothing here serialises an internal: a JSON contract should not move
    /// because an enum gained a third arm.
    pub anchored: bool,
    /// JSON object keys from the decoded fact key to the candidate string. A
    /// scalar predicate has an empty path because its key is the string.
    pub path: Vec<String>,
}

/// One step of the plan's body.
#[derive(Debug, Clone, PartialEq, Eq, Serialize)]
pub struct StepView {
    /// Its position in the body, which is the order the executor runs it in.
    pub index: usize,
    /// `Level`, `Derive` or `Test` — what the machine does with it.
    pub kind: &'static str,
    /// The register it fills: `r0` for the outermost loop, `r2` for a derive.
    /// Absent for a test, which binds nothing.
    pub register: Option<String>,
    /// The level's number among *loop levels*, which is what a resume cursor
    /// pairs its entries with. Absent for a step that is not a loop.
    pub level: Option<usize>,
    /// How this step reaches its rows: `scan`, `seek`, `fetch`, `absent`,
    /// `compare`, `derive` — one entry per source, since a level may have
    /// alternatives.
    pub access: Vec<&'static str>,
    /// The predicates it reads, in source order. Empty for a derive or a
    /// comparison, which read no predicate at all.
    pub predicates: Vec<String>,
    /// How many residual filters run over rows this step produced. A residual is
    /// a row read and dropped, so this is the number worth looking at beside a
    /// seek that did not narrow.
    pub residuals: usize,
    /// Fuzzy guides and residuals this step evaluates.
    pub fuzzy: Vec<FuzzyView>,
    /// The step as the engine prints it.
    pub text: String,
}

/// A compiled plan, as a page shows it.
#[derive(Debug, Clone, PartialEq, Eq, Serialize)]
pub struct PlanView {
    /// The identity a resume cursor carries, in hex — two plans differing only
    /// in polarity must not accept each other's cursors, and this is what says
    /// so.
    pub fingerprint: String,
    /// Loop levels. A cursor holds one row per level.
    pub levels: usize,
    /// Steps in the body. Not the same number, as soon as anything is derived
    /// or tested.
    pub steps_count: usize,
    /// Registers the plan allocates.
    pub registers: usize,
    pub steps: Vec<StepView>,
    /// What the query answers, as the engine prints it.
    pub head: String,
}

/// Describe `plan`, resolving every name against the schema it was compiled for.
#[must_use]
pub fn view(plan: &Plan, schema: &Schema, interner: &LocalInterner) -> PlanView {
    let rendered = print::steps(plan, schema, interner);
    let mut level = 0;

    let steps = plan
        .body
        .iter()
        .enumerate()
        .map(|(index, step)| {
            let text = rendered.get(index).cloned().unwrap_or_default();

            let (kind, register, at_level, sources) = match step {
                Step::Level(generator) => {
                    let at = level;
                    level += 1;
                    (
                        "Level",
                        Some(format!("r{at}")),
                        Some(at),
                        Some(&generator.sources),
                    )
                }
                Step::Derive(derived) => ("Derive", Some(derived.bind.to_string()), None, None),
                Step::Test(Test::Absent(sources)) => ("Test", None, None, Some(sources)),
                Step::Test(Test::Compare { .. }) => ("Test", None, None, None),
            };

            let sources = sources.map(|sources| &sources[..]).unwrap_or_default();
            let fuzzy = sources
                .iter()
                .enumerate()
                .flat_map(|(source_index, source)| fuzzy_of(source_index, source, schema))
                .collect();

            StepView {
                index,
                kind,
                register,
                level: at_level,
                access: match step {
                    Step::Derive(_) => vec!["derive"],
                    Step::Test(Test::Compare { .. }) => vec!["compare"],
                    Step::Test(Test::Absent(_)) => sources.iter().map(|_| "absent").collect(),
                    Step::Level(_) => sources.iter().map(access_of).collect(),
                },
                predicates: sources
                    .iter()
                    .map(|source| {
                        schema
                            .get(source.predicate_id())
                            .and_then(|predicate| predicate.name())
                            .unwrap_or("?")
                            .to_owned()
                    })
                    .collect(),
                residuals: sources.iter().map(|source| source.residuals().len()).sum(),
                fuzzy,
                text,
            }
        })
        .collect();

    PlanView {
        // The raw hash, zero-padded — the engine's `Debug` prefixes it with the
        // word `plan`, which is right in a diagnostic and noise in a field
        // called `fingerprint`.
        fingerprint: format!("{:016x}", plan.fingerprint().raw()),
        levels: plan.levels(),
        steps_count: plan.body.len(),
        registers: plan.nvars,
        steps,
        head: print::head(plan, schema, interner),
    }
}

fn fuzzy_of(source_index: usize, source: &Source, schema: &Schema) -> Vec<FuzzyView> {
    let key_ty = schema
        .get(source.predicate_id())
        .map(|predicate| predicate.key().ty);
    let mut views = Vec::new();

    if let Source::Guided { guide, .. } = source {
        views.push(FuzzyView {
            source: source_index,
            guide: true,
            residual: None,
            term: guide.term.to_string(),
            distance: guide.distance,
            anchored: matches!(guide.anchor, FuzzyAnchor::Prefix),
            path: path_names(key_ty, &guide.path, schema),
        });
    }

    for (residual_index, residual) in source.residuals().iter().enumerate() {
        let ResidualOp::Fuzzy {
            term,
            distance,
            anchor,
        } = &residual.op
        else {
            continue;
        };
        views.push(FuzzyView {
            source: source_index,
            guide: false,
            residual: Some(residual_index),
            term: term.to_string(),
            distance: *distance,
            anchored: matches!(anchor, FuzzyAnchor::Prefix),
            path: path_names(key_ty, &residual.path, schema),
        });
    }

    views
}

fn path_names(key_ty: Option<&PredicateTy>, path: &FieldPath, schema: &Schema) -> Vec<String> {
    let Some(mut ty) = key_ty else {
        return Vec::new();
    };
    if !matches!(ty, PredicateTy::Record(_)) && path.field_idx() == 0 && path.steps().is_empty() {
        return Vec::new();
    }

    let mut names = Vec::new();
    for index in std::iter::once(path.field_idx()).chain(path.steps().iter().copied()) {
        match ty {
            PredicateTy::Record(fields) => {
                let Some((name, field_ty)) = fields.get(index) else {
                    return Vec::new();
                };
                names.push(schema.interner().resolve(*name).unwrap_or("?").to_owned());
                ty = field_ty;
            }
            PredicateTy::Union(alternatives) => {
                let Some(alternative) = alternatives
                    .iter()
                    .find(|alternative| u64::from(alternative.disc) == index as u64)
                else {
                    return Vec::new();
                };
                names.push(
                    schema
                        .interner()
                        .resolve(alternative.name)
                        .unwrap_or("?")
                        .to_owned(),
                );
                ty = &alternative.ty;
            }
            _ => return Vec::new(),
        }
    }
    names
}

/// How a source reaches its rows.
///
/// A seek with an empty prefix is a **scan**, and the difference is the whole
/// cost model: a scan reads every row of the predicate, a seek starts where the
/// key says. Named apart here for the same reason the printer names them apart.
fn access_of(source: &Source) -> &'static str {
    match source {
        Source::Seek { access, .. } => match &access.seek_key {
            fjord_engine::plan::SeekKey::Prefix(bytes) if bytes.is_empty() => "scan",
            _ => "seek",
        },
        Source::Fetch { .. } => "fetch",
        // Its own name, because the cost model is its own: a seek opens a range
        // and drains it, and a guided seek re-opens it wherever the automaton
        // proves the rest of a run cannot match.
        Source::Guided { .. } => "guided",
    }
}
