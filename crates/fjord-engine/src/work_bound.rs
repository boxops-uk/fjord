//! A deterministic harness for history-sensitive relation read work.
//!
//! It compares identical contents built in one batch and over N rounds, using the
//! batch-built relation as the read-cost oracle.

/// What one read cost, in units an implementation counts for itself.
///
/// Comparisons rather than wall clock, because the failure this measures is structural —
/// a read walking N segments instead of one — and a timing-based version of it is a test
/// that passes on a fast machine.
pub type Work = u64;

/// A relation representation, as much of one as the work bound needs to see.
///
/// The reads are an empty-range seek, a narrow seek, a point lookup and a full scan.
/// An empty range is included because it
/// is the one read that should touch nothing at all, and a segmented relation still pays
/// per segment to discover that.
pub trait MeasuredRelation {
    /// Build from every tuple at once.
    fn batch(tuples: &[Vec<u8>]) -> Self;

    /// Build by adding one tuple, as a round of a fixpoint does.
    fn round(self, tuple: Vec<u8>) -> Self;

    fn empty_range_seek(&self) -> Work;
    fn narrow_seek(&self, key: &[u8]) -> Work;
    fn point(&self, key: &[u8]) -> Work;
    fn full_scan(&self) -> Work;
}

/// A read whose N-round cost is not within the bound.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Exceeded {
    pub read: &'static str,
    pub batch: Work,
    pub rounds: Work,
    /// The same read at 2N, when the failure is growth rather than a constant factor.
    pub doubled: Option<Work>,
}

/// Measure one relation representation against the bound.
///
/// `factor` is how much more an N-round relation may cost than the batch-built oracle.
///
/// **Growth is measured as a ratio to the oracle, not as an absolute.** A bigger relation
/// legitimately costs more to read — a binary search over 2N is one comparison more than
/// over N — so an absolute comparison across sizes fails every correct representation.
/// What must not grow is the N-round relation's cost *relative to the batch-built one*,
/// which is exactly the quadratic this exists to catch and is invisible to any single
/// size.
///
/// # Errors
///
/// [`Exceeded`] for the first read that breaks the bound, naming both costs.
pub fn measure<R: MeasuredRelation>(n: usize, factor: Work) -> Result<(), Exceeded> {
    let doubled_n = n.checked_mul(2).ok_or(Exceeded {
        read: "relation size",
        batch: 0,
        rounds: n as Work,
        doubled: None,
    })?;

    let at = |rounds: usize| -> [(&'static str, Work, Work); 4] {
        let tuples: Vec<Vec<u8>> = (0..rounds)
            .map(|i| (i as u64).to_be_bytes().to_vec())
            .collect();

        let batch = R::batch(&tuples);
        // Identical final contents, arrived at one round at a time.
        let stepped = tuples.iter().fold(R::batch(&[]), |relation, tuple| {
            relation.round(tuple.clone())
        });

        let probe = tuples.last().cloned().unwrap_or_default();

        [
            (
                "empty-range seek",
                batch.empty_range_seek(),
                stepped.empty_range_seek(),
            ),
            (
                "narrow seek",
                batch.narrow_seek(&probe),
                stepped.narrow_seek(&probe),
            ),
            ("point", batch.point(&probe), stepped.point(&probe)),
            ("full scan", batch.full_scan(), stepped.full_scan()),
        ]
    };

    let single = at(n);
    let doubled = at(doubled_n);

    for (index, (read, batch, rounds)) in single.into_iter().enumerate() {
        if u128::from(rounds) > u128::from(batch) * u128::from(factor) {
            return Err(Exceeded {
                read,
                batch,
                rounds,
                doubled: None,
            });
        }

        // **Growth is the real finding.** A representation can sit inside any constant
        // factor at one size and still be quadratic; only comparing two sizes says
        // which. Cross-multiplied rather than divided, so the ratio is exact and a
        // zero-cost oracle does not divide.
        let (_, batch_doubled, grown) = doubled[index];
        if u128::from(grown) * u128::from(batch) > u128::from(rounds) * u128::from(batch_doubled) {
            return Err(Exceeded {
                read,
                batch,
                rounds,
                doubled: Some(grown),
            });
        }
    }

    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    /// **The obvious one that should pass.** However it was built, the contents end up
    /// in one sorted run, so a read cannot tell the two histories apart.
    struct SortedSnapshot(Vec<Vec<u8>>);

    impl MeasuredRelation for SortedSnapshot {
        fn batch(tuples: &[Vec<u8>]) -> Self {
            let mut held = tuples.to_vec();
            held.sort();
            SortedSnapshot(held)
        }

        fn round(mut self, tuple: Vec<u8>) -> Self {
            let at = self.0.partition_point(|held| held < &tuple);
            self.0.insert(at, tuple);
            self
        }

        fn empty_range_seek(&self) -> Work {
            self.0.len().ilog2() as Work + 1
        }

        fn narrow_seek(&self, _key: &[u8]) -> Work {
            self.0.len().ilog2() as Work + 1
        }

        fn point(&self, _key: &[u8]) -> Work {
            self.0.len().ilog2() as Work + 1
        }

        fn full_scan(&self) -> Work {
            self.0.len() as Work
        }
    }

    /// **The obvious one that should fail.** A round is a segment, so every read pays
    /// per segment to find out where anything is — the cost a per-open copy guard cannot
    /// see, because this copies nothing.
    struct SegmentedPerRound(Vec<Vec<Vec<u8>>>);

    impl MeasuredRelation for SegmentedPerRound {
        fn batch(tuples: &[Vec<u8>]) -> Self {
            if tuples.is_empty() {
                return SegmentedPerRound(vec![]);
            }
            let mut one = tuples.to_vec();
            one.sort();
            SegmentedPerRound(vec![one])
        }

        fn round(mut self, tuple: Vec<u8>) -> Self {
            self.0.push(vec![tuple]);
            self
        }

        fn empty_range_seek(&self) -> Work {
            self.0.len() as Work
        }

        fn narrow_seek(&self, _key: &[u8]) -> Work {
            self.0.len() as Work
        }

        fn point(&self, _key: &[u8]) -> Work {
            self.0.len() as Work
        }

        fn full_scan(&self) -> Work {
            self.0.iter().map(|segment| segment.len() as Work).sum()
        }
    }

    #[test]
    fn a_sorted_snapshot_is_within_the_bound() {
        assert_eq!(measure::<SortedSnapshot>(64, 4), Ok(()));
    }

    /// The harness's positive control. Without a representation it *rejects*, "the bound
    /// holds" would be a sentence about nothing.
    #[test]
    fn a_per_round_segmented_relation_is_refused() {
        let refused = measure::<SegmentedPerRound>(64, 4).expect_err("segments must fail");

        assert_eq!(refused.read, "empty-range seek");
        assert!(
            refused.rounds > refused.batch,
            "the segmented relation read no more than the batch-built one: {refused:?}"
        );
    }

    /// **Growth is caught even inside a generous factor.** A factor large enough to
    /// admit the segmented relation at one size must still fail it for growing.
    #[test]
    fn growth_is_caught_even_within_the_factor() {
        let refused =
            measure::<SegmentedPerRound>(64, Work::MAX).expect_err("growth must still fail");

        assert!(
            refused.doubled.is_some_and(|grown| grown > refused.rounds),
            "the failure was a factor rather than growth: {refused:?}"
        );
    }

    /// Cross-products used for the ratio must not saturate to the same value and
    /// turn real growth into equality.
    #[test]
    fn growth_is_caught_when_u64_cross_products_would_saturate() {
        struct LargeCounters {
            len: usize,
            stepped: bool,
        }

        impl MeasuredRelation for LargeCounters {
            fn batch(tuples: &[Vec<u8>]) -> Self {
                LargeCounters {
                    len: tuples.len(),
                    stepped: false,
                }
            }

            fn round(mut self, _tuple: Vec<u8>) -> Self {
                self.len += 1;
                self.stepped = true;
                self
            }

            fn empty_range_seek(&self) -> Work {
                if self.stepped && self.len >= 128 {
                    Work::MAX
                } else {
                    1 << 63
                }
            }

            fn narrow_seek(&self, _key: &[u8]) -> Work {
                self.empty_range_seek()
            }

            fn point(&self, _key: &[u8]) -> Work {
                self.empty_range_seek()
            }

            fn full_scan(&self) -> Work {
                self.empty_range_seek()
            }
        }

        let refused = measure::<LargeCounters>(64, 2).expect_err("the ratio doubles");
        assert_eq!(refused.read, "empty-range seek");
        assert_eq!(refused.doubled, Some(Work::MAX));
    }

    /// **The factor is the other half of the bound, and nothing else here exercises it.**
    /// Every rejection above is growth: disable the factor comparison and this module
    /// stays green, so the arithmetic Movement 1 will lean on to say "a bounded factor
    /// more" would be unmeasured. This relation costs a fixed multiple of the oracle at
    /// every size — constant ratio, so growth has nothing to report — and must be
    /// refused for the factor alone.
    #[test]
    fn a_constant_overhead_outside_the_factor_is_refused() {
        struct ConstantOverhead {
            len: usize,
            stepped: bool,
        }

        impl ConstantOverhead {
            const OVERHEAD: Work = 8;

            fn cost(&self, base: Work) -> Work {
                base * if self.stepped { Self::OVERHEAD } else { 1 }
            }

            fn depth(&self) -> Work {
                self.len.max(1).ilog2() as Work + 1
            }
        }

        impl MeasuredRelation for ConstantOverhead {
            fn batch(tuples: &[Vec<u8>]) -> Self {
                ConstantOverhead {
                    len: tuples.len(),
                    stepped: false,
                }
            }

            fn round(self, _tuple: Vec<u8>) -> Self {
                ConstantOverhead {
                    len: self.len + 1,
                    stepped: true,
                }
            }

            fn empty_range_seek(&self) -> Work {
                self.cost(self.depth())
            }

            fn narrow_seek(&self, _key: &[u8]) -> Work {
                self.cost(self.depth())
            }

            fn point(&self, _key: &[u8]) -> Work {
                self.cost(self.depth())
            }

            fn full_scan(&self) -> Work {
                self.cost(self.len as Work)
            }
        }

        let refused = measure::<ConstantOverhead>(64, 4).expect_err("8x exceeds a factor of 4");

        assert_eq!(refused.read, "empty-range seek");
        assert_eq!(
            refused.doubled, None,
            "the factor must be what rejected it, not growth: {refused:?}"
        );
        assert_eq!(refused.rounds, refused.batch * ConstantOverhead::OVERHEAD);

        // And the same relation inside a factor that admits it passes, so the
        // rejection above is the factor doing its job rather than a blanket refusal.
        assert_eq!(measure::<ConstantOverhead>(64, 8), Ok(()));
    }

    #[test]
    fn a_size_that_cannot_be_doubled_is_refused_before_building() {
        let refused = measure::<SortedSnapshot>(usize::MAX, 4).expect_err("2N overflows");
        assert_eq!(refused.read, "relation size");
    }
}
