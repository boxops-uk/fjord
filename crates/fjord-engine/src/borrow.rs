//! A sealed borrowed [`FactStore`] for the program driver.
//!
//! The constructor is private to this module and its descendants. Keeping the
//! driver beneath that boundary prevents any sibling engine path from weakening
//! [I8](../../../website/content/invariants.md#i8) by constructing a non-owning
//! executor whose snapshot owner can survive suspension.

use fjord_schema::id::FactId;
use fjord_store::{
    error::StoreError,
    fact_store::{Entity, FactStore},
};

/// A [`FactStore`] borrowed for the life of one rule's execution.
///
/// Delegates both seam methods and adds nothing: `Scan` is the inner store's own
/// iterator type, so a borrowed run and an owning one execute the same code.
///
/// The private constructor is the seal; outside code cannot build one:
///
/// ```compile_fail
/// let store = fjord_store_mem::MemStore::new();
/// let _ = fjord_engine::borrow::StoreRef::new(&store);
/// ```
///
/// A `compile_fail` case passes when the snippet fails to compile for **any**
/// reason, so a typo in it would assert nothing. Rustdoc's error-code form is no
/// help — the code is not checked on stable, and a deliberately impossible one
/// still passes. So the discriminating half is this positive control: the same
/// snippet with the sealed call removed, which compiles, leaving `new` as the
/// only difference between the two.
///
/// ```
/// let store = fjord_store_mem::MemStore::new();
/// let _ = &store;
/// ```
#[derive(Debug)]
pub struct StoreRef<'a, S: FactStore> {
    inner: &'a S,
}

impl<'a, S: FactStore> StoreRef<'a, S> {
    /// Borrow `inner` for one execution.
    ///
    /// Private so only this module's driver descendant can construct a borrowing
    /// executor. Sibling engine modules cannot opt out of I8's owning seam.
    #[allow(dead_code)]
    fn new(inner: &'a S) -> Self {
        Self { inner }
    }
}

impl<S: FactStore> FactStore for StoreRef<'_, S> {
    type Scan = S::Scan;

    fn scan(&self, lo: &[u8], hi: Option<&[u8]>) -> Result<Self::Scan, StoreError> {
        self.inner.scan(lo, hi)
    }

    fn point(&self, id: FactId) -> Result<Option<Entity>, StoreError> {
        self.inner.point(id)
    }
}

#[cfg(test)]
mod tests {
    use std::sync::atomic::Ordering;

    use fjord_schema::{id::FactId, schema::PredicateId};
    use fjord_store::{
        fact_store::FactStore,
        fixtures::{
            DropProbe, assert_scan_stays_in_predicate, assert_short_bound_is_rejected, compose,
            i64_field,
        },
    };
    use fjord_store_fjall::store::FjallDb;
    use fjord_store_mem::MemStore;
    use tempfile::TempDir;
    use tokio_util::sync::CancellationToken;

    use super::StoreRef;
    use crate::{
        iter::{Executor, Iteratee, Stream},
        plan::{Access, Address, Level, Plan, Project, SeekKey, Step},
    };

    fn seeded() -> MemStore {
        let mut store = MemStore::new();
        for p in [0u32, 1] {
            for i in 1..=8u64 {
                store.insert(PredicateId(p), i64_field(i as i64), i);
            }
        }
        store
    }

    fn seed_fjall(db: &FjallDb) -> FactId {
        let mut first = None;
        for p in [0u32, 1] {
            for i in 1..=8u64 {
                let id = db
                    .put_fact(PredicateId(p), &i64_field(i as i64), &[])
                    .expect("seed fjall");
                if p == 0 && i == 1 {
                    first = Some(id);
                }
            }
        }
        first.expect("the fixture inserts predicate zero's first fact")
    }

    fn scan_all(predicate_id: PredicateId) -> Plan {
        Plan {
            nvars: 1,
            body: Step::levels([Level::seek(
                Access {
                    predicate_id,
                    seek_key: SeekKey::Prefix(Box::new([])),
                },
                Box::new([Address::new(0)]),
                Box::new([]),
            )]),
            head: Project::FactRef(Address::new(0)),
        }
    }

    fn drain<S: fjord_store::fact_store::FactStore>(store: S, plan: Plan) -> usize {
        let cancel = CancellationToken::new();
        let out = Executor::new(store, plan)
            .enumerate(0usize, |n, _row| Ok(Stream::Continue(n + 1)), &cancel)
            .expect("drain");
        match out {
            Iteratee::Done(n) => n,
            Iteratee::Suspended(_, _) => panic!("a drain that never suspends did not complete"),
        }
    }

    fn assert_borrowed_seam<S: FactStore>(store: &S, present: FactId) {
        let borrowed = StoreRef::new(store);

        let lo = compose(&[&0u32.to_be_bytes()[..], &i64_field(1)]);
        assert_scan_stays_in_predicate(&borrowed, &lo, None).expect("borrowed scan");

        let neighbour = 1u32.to_be_bytes().to_vec();
        assert_scan_stays_in_predicate(&borrowed, &neighbour, None).expect("borrowed scan");

        assert_short_bound_is_rejected(&borrowed, &[0u8; 1]);

        let rows = borrowed
            .scan(&0u32.to_be_bytes(), None)
            .expect("borrowed scan")
            .collect::<Result<Vec<_>, _>>()
            .expect("borrowed rows");
        assert_eq!(rows.len(), 8, "the scan witness must not be empty");

        let entity = borrowed
            .point(present)
            .expect("borrowed point")
            .expect("present fact");
        assert_eq!(entity.key.to_vec(), i64_field(1));
        assert!(entity.value.is_empty());

        let absent = FactId::new(PredicateId(0), 9).expect("absent fixture id");
        assert!(borrowed.point(absent).expect("borrowed point").is_none());
    }

    /// The seal is module privacy, not crate privacy. Widening it would let a
    /// sibling engine path construct a borrowing executor outside the driver.
    #[test]
    fn the_constructor_is_module_private() {
        let source = include_str!("borrow.rs");
        let signature = "new(inner: &'a S) -> Self {";
        let private = ["    fn ", signature].concat();
        assert_eq!(
            source.matches(&private).count(),
            1,
            "the constructor is absent, duplicated, or no longer module-private"
        );

        for visibility in ["pub ", "pub(crate) ", "pub(super) ", "pub(in crate) "] {
            let widened = ["    ", visibility, "fn ", signature].concat();
            assert!(
                !source.contains(&widened),
                "the constructor was widened to `{visibility}`"
            );
        }
    }

    /// The wrapper keeps the complete seam contract over the in-memory backend.
    #[test]
    fn a_borrowed_mem_store_keeps_the_seam_contract() {
        let store = seeded();
        let present = FactId::new(PredicateId(0), 1).expect("fixture id");
        assert_borrowed_seam(&store, present);
    }

    /// The same contract is asserted independently over the durable backend.
    #[test]
    fn a_borrowed_fjall_store_keeps_the_seam_contract() {
        let dir = TempDir::new().expect("tempdir");
        let db = FjallDb::open(dir.path()).expect("open");
        let present = seed_fjall(&db);
        assert_borrowed_seam(&db.reader(), present);
    }

    /// A borrowed run answers exactly what an owning run answers — the wrapper
    /// delegates and adds nothing, including at the `Scan` associated type, which
    /// is the inner store's own.
    #[test]
    fn a_borrowed_run_answers_what_an_owning_run_answers() {
        let owned = drain(seeded(), scan_all(PredicateId(0)));

        let store = seeded();
        let borrowed = drain(StoreRef::new(&store), scan_all(PredicateId(0)));

        assert_eq!(owned, 8);
        assert_eq!(owned, borrowed);
    }

    /// Reference ownership introduces no hidden allocation or snapshot clone.
    #[test]
    fn borrowing_a_store_allocates_nothing() {
        // The counting allocator is linked only as a dev-dependency; if that
        // wiring breaks, `measure` reports zeroes and the assertion below holds
        // vacuously. Prove the probe sees a known allocation first.
        let control = allocation_counter::measure(|| {
            std::hint::black_box(Vec::<u8>::with_capacity(4096));
        });
        assert!(
            control.count_total > 0,
            "counting allocator is not installed; this guard would pass vacuously: {control:?}"
        );

        let store = seeded();
        let info = allocation_counter::measure(|| {
            std::hint::black_box(StoreRef::new(std::hint::black_box(&store)));
        });

        assert_eq!(
            info.count_total, 0,
            "borrowing a store allocated {} times; the wrapper is not a borrow",
            info.count_total
        );
    }

    /// Dropping a borrowing executor releases every scan but not its snapshot
    /// owner. The program driver therefore owns I8's every-exit-path release.
    #[test]
    fn dropping_a_borrowing_executor_leaves_the_owner_alive() {
        let dir = TempDir::new().expect("tempdir");
        let db = FjallDb::open(dir.path()).expect("open");
        seed_fjall(&db);
        let (probe, live) = DropProbe::new(db.reader());

        // Both witnesses start at one: the store handle owns one real snapshot.
        assert_eq!(live.load(Ordering::SeqCst), 1);
        assert_eq!(db.open_snapshots(), 1);

        let rows = drain(StoreRef::new(&probe), scan_all(PredicateId(0)));
        assert_eq!(rows, 8);

        // Every scan the run opened is released — that half is still the
        // executor's, and it still holds.
        assert_eq!(
            live.load(Ordering::SeqCst),
            1,
            "a borrowing run leaked an open scan"
        );
        assert_eq!(
            db.open_snapshots(),
            1,
            "a borrowing run cloned or leaked the base snapshot"
        );

        drop(probe);
        assert_eq!(
            live.load(Ordering::SeqCst),
            0,
            "the owner is what releases the snapshot now"
        );
        assert_eq!(db.open_snapshots(), 0);
    }
}
