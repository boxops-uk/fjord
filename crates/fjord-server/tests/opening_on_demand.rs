//! **A database that appears under a live root is served without a restart.**
//!
//! The open map holds what the startup scan found and what `create` has added;
//! resolution reads the disk on every bind. The two therefore disagree the moment an
//! instance is published into a serving root, and the expensive half of that
//! disagreement is a *newer* instance of a name already being served: resolution ranks
//! it first, the map has never seen it, and the name stops binding at all while the old
//! instance stays open and healthy.
//!
//! The shape these are drawn from is the deployment: CI seals `<name>/<ULID>/` and a
//! sidecar syncs it into the root a server owns, and a bind is what has to make sense
//! of whatever state that leaves behind.

use std::{sync::Arc, thread, time::Duration};

use fjord_schema::schema::{Predicate, PredicateTy, Schema};
use fjord_server::{Database, Registry, error::ServerError, registry::Schemas};
use fjord_store::fact::{Fact, ToValue};
use fjord_store_fjall::{
    catalog::{Catalog, Entry, Intent, Selector},
    identity,
    store::FjallDb,
    ulid,
};
use fjord_wire::protocol::ErrorCode;
use lasso::Rodeo;

/// One stored predicate, stated in Rust so these tests need no schema file.
fn schema() -> Schema {
    let mut rodeo = Rodeo::new();
    let file = rodeo.get_or_intern("src.File");

    Schema::new(
        rodeo.into_reader(),
        Arc::from(vec![Predicate {
            name: file,
            key: PredicateTy::Str,
            value: None,
        }]),
    )
}

/// A registry serving whatever is in `root` *now*.
fn serving(root: &std::path::Path) -> Registry {
    let catalog = Catalog::open(root).expect("a store root");
    let (registry, _listing) = Registry::open(catalog, Schemas::new("")).expect("a registry");

    registry
}

/// Publish an instance into `root` the way the sidecar does: through the catalog, with
/// nothing telling the server about it.
fn publish(root: &std::path::Path, name: &str) -> Entry {
    let catalog = Catalog::open(root).expect("a store root");

    catalog.create(name, &schema()).expect("a database")
}

/// **The outage this exists for**: a newer instance of a served name takes the name
/// over, rather than taking it away until the next restart.
///
/// The old instance is still open and still reachable by its own id, which is what
/// made the symptom confusing in the field — `halo@01M0WM` answered and `halo` did
/// not.
#[test]
fn a_newer_instance_published_into_a_live_root_takes_the_name() {
    let dir = tempfile::tempdir().expect("a scratch directory");
    let root = dir.path().join("store");

    let old = publish(&root, "halo").meta.instance;
    let registry = serving(&root);
    assert_eq!(registry.len(), 1, "the root's one instance is open");

    // Two ULIDs minted in one millisecond sort by their random half, and this test is
    // about the ranking rather than about that: separated so the timestamps differ,
    // and asserted so a clock that did not move fails here rather than below.
    thread::sleep(Duration::from_millis(2));
    let new = publish(&root, "halo").meta.instance;
    assert!(new > old, "a later create sorts after an earlier one");

    let bound = registry.bind("halo").expect("the name still binds");
    assert_eq!(bound.instance, new, "and binds to the newest instance");

    let pinned = registry.bind(&format!("halo@{old}")).expect("the old one");
    assert_eq!(pinned.instance, old, "which is open and serving as before");
    assert_eq!(registry.len(), 2, "both are open now");
}

/// A burst of binds on one cold instance opens it **once**.
///
/// Not a nicety: fjall takes a store directory exclusively, so a second open of the
/// same instance does not cost time, it fails — and a client would see a database that
/// binds or does not depending on who else was binding it at that moment.
///
/// Asserted by identity rather than by a counter: every thread holding the same `Arc`
/// is what "opened once" means to everything downstream, and one open map entry is
/// what it means on disk.
#[test]
fn concurrent_binds_of_a_cold_instance_open_it_once() {
    const BINDERS: usize = 8;

    let dir = tempfile::tempdir().expect("a scratch directory");
    let root = dir.path().join("store");

    let registry = serving(&root);
    publish(&root, "code");
    assert_eq!(registry.len(), 0, "nothing was open when it was published");

    let start = std::sync::Barrier::new(BINDERS);

    let bound: Vec<Arc<Database>> = thread::scope(|scope| {
        let handles: Vec<_> = (0..BINDERS)
            .map(|_| {
                let registry = &registry;
                let start = &start;

                scope.spawn(move || {
                    start.wait();
                    registry.bind("code").expect("every binder gets it")
                })
            })
            .collect();

        handles
            .into_iter()
            .map(|handle| handle.join().expect("a binder"))
            .collect()
    });

    for database in &bound {
        assert!(
            Arc::ptr_eq(database, &bound[0]),
            "every binder holds the same open database"
        );
    }

    assert_eq!(registry.len(), 1, "one instance, opened once");
}

/// A corrupt instance moved into a live root is **refused by name**, and its siblings
/// carry on being served.
///
/// The refusal has to say more than "no database named `bad`", which is the same
/// sentence a name that is genuinely absent gets: an operator whose publish failed
/// halfway needs the difference between a name that is not there and one that is there
/// and will not open, and which instance it was.
#[test]
fn a_corrupt_instance_is_refused_by_name_and_leaves_its_siblings_serving() {
    let dir = tempfile::tempdir().expect("a scratch directory");
    let root = dir.path().join("store");

    publish(&root, "good");
    let registry = serving(&root);

    let broken = publish(&root, "bad");
    std::fs::remove_dir_all(broken.path.join("schema")).expect("it goes");

    let Err(refused) = registry.bind("bad") else {
        panic!("a corrupt instance must not be served");
    };

    assert!(
        matches!(
            &refused,
            ServerError::Unservable { instance, .. } if instance == &broken.meta.instance
        ),
        "the refusal names the instance that would not open: {refused}"
    );
    assert!(
        refused.to_string().contains("no schema copy"),
        "and says what was wrong with it: {refused}"
    );

    let Err(absent) = registry.bind("nothing-here") else {
        panic!("a name that is not there must not bind");
    };
    assert!(
        absent.to_string().contains("no database named"),
        "which is a different answer: {absent}"
    );

    assert!(registry.bind("good").is_ok(), "its sibling is untouched");
    assert_eq!(registry.len(), 1, "and the broken one was not admitted");
}

/// A healthy instance whose store **another process is holding** is refused as
/// retryable, not as absent.
///
/// Two of the failed opens in this file end by themselves — this one when the holder
/// lets go, a half-delivered instance when its copy finishes — and one does not: a
/// schema copy the sync never delivered stays broken until somebody fixes it. "No such
/// database" is the one answer a client will never retry, so the two that end by
/// themselves answer `InUse`, which is what a held *root* already answers and what
/// [wire-protocol](../../../website/content/wire-protocol.md) calls the one code worth
/// retrying. The binding after the release is the half that makes it honest.
///
/// Finding out costs fjall's own retry — twice at 100 ms before it gives up — so a
/// client looping on a held name is an I/O and log amplifier. Recorded in `PLAN.md`
/// rather than capped here.
#[test]
fn a_locked_instance_is_refused_as_in_use_and_binds_once_the_holder_lets_go() {
    let dir = tempfile::tempdir().expect("a scratch directory");
    let root = dir.path().join("store");

    let registry = serving(&root);
    let published = publish(&root, "held");

    let held = FjallDb::open(&published.path).expect("something else has it");

    let Err(refused) = registry.bind("held") else {
        panic!("a store this server cannot take must not be served");
    };

    assert_eq!(
        refused.code(),
        ErrorCode::InUse,
        "a held store is retryable, not absent: {refused}"
    );
    assert!(
        refused.to_string().contains(&published.meta.instance),
        "and says which instance it could not take: {refused}"
    );
    assert_eq!(registry.len(), 0, "nothing was admitted");

    drop(held);

    assert!(
        registry.bind("held").is_ok(),
        "and the same bind succeeds once the holder lets go"
    );
}

/// A **pre-sidecar** instance directory — a ULID with nothing in it yet — is invisible
/// to both `list` and `bind`, and a bind leaves it as it found it.
///
/// **Not a guard on opening on demand.** This is the catalog's own rule — a directory
/// becomes a database when its sidecar lands (`ops-I7`) — and it held just as well when
/// a bind opened nothing. It covers the *first* moment of a copy and nothing after it:
/// the sidecar is one file among many and lands whenever the copy sends it, so what
/// makes opening on demand safe is
/// [`a_half_delivered_instance_is_refused_and_left_untouched`], which is about every
/// moment after this one. The assertion that carries any weight here is the last.
#[test]
fn a_sidecar_less_instance_directory_is_invisible_to_list_and_bind() {
    let dir = tempfile::tempdir().expect("a scratch directory");
    let root = dir.path().join("store");

    let registry = serving(&root);
    let arriving = root.join("arriving").join(ulid::new());
    std::fs::create_dir_all(&arriving).expect("a directory");

    let listing = registry.catalog().list().expect("a listing");
    assert!(listing.entries.is_empty(), "{:?}", listing.entries);
    assert!(listing.problems.is_empty(), "{:?}", listing.problems);

    let Err(unknown) = registry.bind("arriving") else {
        panic!("a directory with no sidecar is not a database");
    };
    assert!(
        unknown.to_string().contains("no database named"),
        "{unknown}"
    );
    assert_eq!(registry.len(), 0, "and nothing was opened");
    assert_eq!(
        std::fs::read_dir(&arriving)
            .expect("the directory is still there")
            .count(),
        0,
        "nothing was written into a directory a sync is still filling"
    );
}

// ---- a copy that has not finished ------------------------------------------

/// A sealed instance holding three facts, in a root of its own — what CI hands a sync.
fn sealed(root: &std::path::Path) -> Entry {
    let catalog = Catalog::open(root).expect("a store root");
    catalog.create("code", &schema()).expect("a database");

    let (_entry, db) = catalog
        .open_write(&Selector::of("code"))
        .expect("it opens for writing");
    for path in ["a.py", "b.py", "c.py"] {
        db.put(&schema(), &File(path)).expect("a fact");
    }
    drop(db);

    catalog
        .finish(&Selector::of("code"), false)
        .expect("it seals");
    catalog
        .resolve(&Selector::of("code"), Intent::Read)
        .expect("it is sealed and on the disk")
}

/// One fact of the one predicate — three of these are what the sidecar records.
struct File(&'static str);

impl Fact for File {
    const PREDICATE: &'static str = "src.File";
    fn key(&self) -> fjord_encoding::tuple::Value {
        self.0.to_value()
    }
}

/// Copy the top-level entries `parts` names from `from` to `to`, or everything for
/// `["*"]` — a sync that has delivered some of an instance directory and not the rest.
fn deliver(from: &std::path::Path, to: &std::path::Path, parts: &[&str]) {
    std::fs::create_dir_all(to).expect("a directory");

    for entry in std::fs::read_dir(from).expect("the built instance") {
        let entry = entry.expect("an entry");
        let name = entry.file_name();
        let wanted = parts == ["*"] || parts.iter().any(|part| *part == name.to_string_lossy());
        if !wanted {
            continue;
        }

        let target = to.join(&name);
        if entry.file_type().expect("a file type").is_dir() {
            deliver(&entry.path(), &target, &["*"]);
        } else {
            std::fs::copy(entry.path(), &target).expect("a copy");
        }
    }
}

/// Every path under `root`, relative and sorted — what a directory holds, as a value
/// two moments can be compared over.
fn contents(root: &std::path::Path) -> Vec<String> {
    fn walk(root: &std::path::Path, at: &std::path::Path, into: &mut Vec<String>) {
        for entry in std::fs::read_dir(at).expect("a directory") {
            let entry = entry.expect("an entry");
            let path = entry.path();

            into.push(
                path.strip_prefix(root)
                    .expect("under the root")
                    .to_string_lossy()
                    .into_owned(),
            );

            if entry.file_type().expect("a file type").is_dir() {
                walk(root, &path, into);
            }
        }
    }

    let mut paths = vec![];
    walk(root, root, &mut paths);
    paths.sort();
    paths
}

/// How many facts the store a handle opened actually holds — counted by the same walk
/// `finish` counts with, so the answer comes off the tables and not off the sidecar.
fn facts(bound: &Database) -> u64 {
    identity::compute(&bound.db, &bound.schema, bound.identity.schema())
        .expect("the store walks")
        .facts
}

/// **A half-delivered instance is refused, the directory is left exactly as the copy
/// left it, and the same bind serves the database once the copy finishes.**
///
/// The defect that made opening on demand unsafe. `FjallDb::open` is create-or-recover,
/// so a bind that reached it with the path of a directory a copy was still filling
/// stamped a fresh empty keyspace **into the copy's own target**, read the sidecar it
/// found beside it, and published a handle: `Complete`, answering none of the facts that
/// sidecar records, for the life of the process — while `fjord.db.List` went on
/// reporting the count, because `ops-I7` reads only the sidecar. Nothing closes an open
/// instance, so the rest of the copy landing changed nothing.
///
/// Both shapes are mid-copy states of the deployment
/// [operations](../../../website/content/operations.md) describes, and the second is the
/// one a file-level sync actually produces: the directory tree first, the files into it
/// after.
///
/// **"Left untouched" is true of these two shapes and of nothing later.** Both are
/// before fjall's own marker file lands, which is what the store-presence check reads;
/// past it the open goes ahead and its recovery deletes what the copy has not delivered.
/// That is [`a_store_one_keyspace_manifest_short_is_never_served_the_facts_it_records`],
/// which is a test of its own because the claim in this one's name is false there.
///
/// The instance is a real sealed artifact copied under the name and id it was built at,
/// rather than a sidecar written by hand, because the claim is about a database that
/// records facts it has not delivered — and a count the seal computed is the only kind
/// that cannot be arranged.
#[test]
fn a_half_delivered_instance_is_refused_and_left_untouched() {
    // What the copy has got to, in the two orders that matter: the sidecar and the
    // schema copy, and the same plus the empty tree a sync makes before it fills it.
    const IN_FLIGHT: [&[&str]; 2] = [
        &["FJORD_META", "schema"],
        &["FJORD_META", "schema", "keyspaces"],
    ];

    let built = tempfile::tempdir().expect("a scratch directory");
    let source = sealed(&built.path().join("store"));
    let recorded = source
        .meta
        .facts
        .expect("a sealed database records its facts");
    assert!(recorded > 0, "there is a count to be wrong about");

    for delivered in IN_FLIGHT {
        let dir = tempfile::tempdir().expect("a scratch directory");
        let root = dir.path().join("store");

        // Serving before any of it arrives, so the bind below is the on-demand open
        // and not something the startup scan already decided.
        let registry = serving(&root);

        // Same name, same instance id, same sidecar: a copy puts an instance where it
        // was built, and the sidecar it carries records both.
        let arriving = root.join(source.name()).join(&source.meta.instance);
        deliver(&source.path, &arriving, delivered);

        let mid_copy = contents(&arriving);
        assert!(
            mid_copy.contains(&"FJORD_META".to_owned()),
            "the sidecar has landed, or nothing below is being tested: {mid_copy:?}"
        );
        assert!(
            !mid_copy.contains(&"version".to_owned()),
            "and the store has not: {mid_copy:?}"
        );

        let Err(refused) = registry.bind(source.name()) else {
            panic!("a database whose store has not arrived must not be served: {delivered:?}");
        };

        assert!(
            matches!(
                &refused,
                ServerError::Unservable { instance, .. } if instance == &source.meta.instance
            ),
            "the refusal names the instance: {refused}"
        );
        assert_eq!(
            refused.code(),
            ErrorCode::InUse,
            "a copy in flight ends by itself, so it is retryable and not absent: {refused}"
        );
        assert!(
            refused.to_string().contains("has a sidecar but no store"),
            "and says what is missing rather than that the name is unknown: {refused}"
        );
        assert_eq!(registry.len(), 0, "nothing was admitted");
        assert_eq!(
            contents(&arriving),
            mid_copy,
            "nothing was written into the directory a copy is still filling"
        );

        // **A restart lands in the same window**, and the startup scan opens through
        // the same place a bind does — so it makes the instance a problem in the
        // listing rather than adopting the half of it that is there.
        let (restarted, listing) = Registry::open(
            Catalog::open(&root).expect("a store root"),
            Schemas::new(""),
        )
        .expect("a registry");

        assert_eq!(restarted.len(), 0, "a restart admitted nothing either");
        assert!(
            listing
                .problems
                .iter()
                .any(|problem| problem.to_string().contains("has a sidecar but no store")),
            "and said so in the listing: {:?}",
            listing.problems
        );
        assert_eq!(
            contents(&arriving),
            mid_copy,
            "with nothing written into the directory by the scan either"
        );
        drop(restarted);

        // The rest of the copy lands. Nothing cached the refusal, so the same bind
        // opens what has now arrived — the sealed database, holding the facts its
        // sidecar claimed all along.
        deliver(&source.path, &arriving, &["*"]);

        let bound = registry
            .bind(source.name())
            .expect("the finished copy binds");

        assert!(!bound.writable(), "as the sealed database it is");
        assert_eq!(
            bound.content_fingerprint(),
            source.meta.content_fingerprint,
            "with the identity the seal computed"
        );
        assert_eq!(
            facts(&bound),
            recorded,
            "and the facts its sidecar records, not none"
        );
    }
}

/// Deliver everything under `from` into `to` **except** `skip` — a copy one path short
/// of finished, where the path is nested rather than a top-level entry.
fn deliver_all_but(from: &std::path::Path, to: &std::path::Path, skip: &str) {
    for path in contents(from) {
        if path == skip {
            continue;
        }

        let (source, target) = (from.join(&path), to.join(&path));
        if source.is_dir() {
            std::fs::create_dir_all(&target).expect("a directory");
        } else {
            std::fs::create_dir_all(target.parent().expect("under the instance"))
                .expect("a parent directory");
            std::fs::copy(&source, &target).expect("a copy");
        }
    }
}

/// **The window one level below the store-presence check, recorded rather than
/// closed.**
///
/// `FJALL_VERSION_MARKER` answers "did fjall create a database at this top level", and
/// fjall's create-or-recover recurs **per keyspace** inside: a keyspace directory
/// holding no `current` manifest of its own is *deleted* by the recovery. So a copy
/// that has delivered fjall's `version` marker and is one keyspace manifest short is
/// past that check, and the open does two things a refusal would not — it destroys
/// delivered files, and it can hand back a store that is not the database the sidecar
/// beside it describes.
///
/// What this pins is the whole of what a bind can do here, asked of **every** keyspace
/// in the artifact rather than of one chosen by number: fjall assigns those, and a test
/// naming one would go red on a dependency bump that renumbered them.
///
/// 1. **No bind answers rows the sidecar does not describe.** Either it is refused —
///    `CatalogError::FactsDoNotMatch`, comparing the count the seal walked against the
///    count the recovered store holds — or it is served and no row can be read out of
///    it at all.
/// 2. **The open destroys delivered files.** So this refusal is not the harmless one
///    the shapes above it get: `a_half_delivered_instance_is_refused_and_left_untouched`
///    is true of the window before fjall's marker lands and false here, which is why
///    this is a test of its own rather than a third case inside it.
/// 3. **The copy finishing does not repair it.** Every path the copy had to send has
///    been sent, and the name still does not serve the database its sidecar records.
///
/// A count and not a re-hash, and the reason is arithmetic: measured in a release build
/// over a sealed database of 100,000 facts, the count is 96 ms against 445 ms for the
/// identity walk, on top of an open of the same database that costs 784 ms by itself.
/// The shapes a count cannot see are the ones where the deleted keyspace was an
/// `entities` tree, and those are (1)'s second arm — served, and unable to produce a
/// row.
#[test]
fn a_store_one_keyspace_manifest_short_is_never_served_the_facts_it_records() {
    let built = tempfile::tempdir().expect("a scratch directory");
    let source = sealed(&built.path().join("store"));
    let recorded = source
        .meta
        .facts
        .expect("a sealed database records its facts");

    let manifests: Vec<String> = contents(&source.path)
        .into_iter()
        .filter(|path| path.ends_with("/current"))
        .collect();
    assert!(
        manifests.len() > 1,
        "a sealed artifact holds a keyspace manifest per tree: {manifests:?}"
    );

    for in_flight in manifests {
        let dir = tempfile::tempdir().expect("a scratch directory");
        let root = dir.path().join("store");
        let registry = serving(&root);

        let arriving = root.join(source.name()).join(&source.meta.instance);
        deliver_all_but(&source.path, &arriving, &in_flight);

        let delivered = contents(&arriving);
        assert!(
            delivered.contains(&"version".to_owned()),
            "the store marker has landed, or this is the shape above it: {in_flight}"
        );

        // 1. Refused, or served and unable to answer.
        match registry.bind(source.name()) {
            Err(refused) => assert!(
                matches!(
                    &refused,
                    ServerError::Unservable { instance, .. } if instance == &source.meta.instance
                ),
                "the refusal names the instance: {refused}"
            ),
            Ok(bound) => {
                assert!(
                    identity::compute(&bound.db, &bound.schema, bound.identity.schema()).is_err(),
                    "a store one path short of {in_flight} was served and could answer \
                     rows against the {recorded} its sidecar records"
                );
            }
        }

        // 2. The open took delivered files with it.
        let lost: Vec<&String> = delivered
            .iter()
            .filter(|path| !arriving.join(path).exists())
            .collect();
        assert!(
            !lost.is_empty(),
            "the open left the directory as the copy left it, which the record for this \
             window says it does not: {in_flight}"
        );

        // 3. The copy sends the one path it had left, which is the whole of what it
        //    owes, and the name still does not serve what the sidecar records.
        let last = arriving.join(&in_flight);
        std::fs::create_dir_all(last.parent().expect("under the instance"))
            .expect("a parent directory");
        std::fs::copy(source.path.join(&in_flight), &last).expect("the last path");

        if let Ok(bound) = registry.bind(source.name()) {
            let served = identity::compute(&bound.db, &bound.schema, bound.identity.schema())
                .map(|identity| identity.facts)
                .ok();
            assert_ne!(
                served,
                Some(recorded),
                "the finished copy served its facts after all, one path short of \
                 {in_flight}"
            );
        }
    }
}
