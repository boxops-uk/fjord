//! What the **lifecycle** can refuse — the faults that are about a database as
//! an artifact rather than about facts.
//!
//! Separate from [`StoreError`] on purpose: a sidecar path, a held root lock, a
//! name that matches several instances and a database that is Complete are all
//! statements about how *this* backend keeps a database on a filesystem. The
//! seam cannot name them without naming an implementation, and a second backend
//! would have to either satisfy them or leave them dead.
//!
//! [`CatalogError::Store`] carries the seam's error, so a read fault raised
//! underneath a lifecycle call still bubbles through one `?`.

use fjord_store::error::StoreError;
use thiserror::Error;

/// A fault in creating, opening, listing, sealing or deleting a database.
#[derive(Debug, Error)]
#[non_exhaustive]
pub enum CatalogError {
    /// fjall itself failed.
    ///
    /// Named here, where naming it is free: this crate *is* the fjall backend.
    /// What crosses the seam is [`StoreError::Backend`], which carries the same
    /// error boxed and unnamed.
    #[error("fjall: {0}")]
    Backend(#[from] fjall::Error),

    /// A fault the seam defines — raised under a lifecycle call, and passed on
    /// rather than reworded.
    #[error("{0}")]
    Store(#[from] StoreError),

    /// A fault in a database's sidecar — missing, unreadable, malformed, or written
    /// by a sidecar format this build does not know.
    ///
    /// Carries the path because the sidecar is a file a person can go and look at,
    /// which is most of the reason it is a readable document at all.
    #[error("{path}: {detail}", path = path.display())]
    Meta {
        path: std::path::PathBuf,
        detail: String,
    },

    /// A store root already owned by another process (`ops-I1`).
    ///
    /// Not a lock to wait on: the design refuses a lock fight outright, because the
    /// alternative to failing here is two servers writing one directory.
    #[error("the store root {root} is held by another process", root = root.display())]
    RootHeld { root: std::path::PathBuf },

    /// A database the store root does not hold.
    #[error("no database named `{0}` in this store root")]
    NoSuchDatabase(String),

    /// A name that cannot be a directory, or could escape the store root.
    #[error("`{name}` is not a usable database name: {detail}")]
    BadDatabaseName { name: String, detail: &'static str },

    /// A name that holds several instances, where the caller named none and the
    /// operation must not guess.
    ///
    /// Which operations must not guess is [`Intent`](crate::catalog::Intent)'s
    /// business: a read ranks the candidates and takes the best, because reading the
    /// second-best answers oddly and is recoverable. A write or a delete refuses,
    /// because picking wrong there is neither.
    #[error(
        "`{name}` has {count} instances; name one with `{name}@<instance>` ({shown})",
        count = instances.len(),
        shown = instances.join(", "),
    )]
    AmbiguousDatabase {
        name: String,
        instances: Vec<String>,
    },

    /// An instance — or an instance prefix — that names nothing under this database.
    #[error("`{name}` has no instance matching `{instance}`")]
    NoSuchInstance { name: String, instance: String },

    /// An instance whose sidecar is there and whose **store is not**.
    ///
    /// `ops-I7` makes the sidecar what turns a directory into a database, and a copy
    /// into a store root delivers files in whatever order it likes — so "the sidecar
    /// has landed and the LSM tables have not" is the ordinary state of an instance
    /// directory for as long as the copy takes, not a corruption.
    ///
    /// **Refused rather than opened**, because
    /// [`FjallDb::open`](crate::store::FjallDb::open) is create-or-recover: it stamps
    /// a fresh empty keyspace into the copy's own target and hands back a handle that
    /// answers no rows — while the sidecar beside it goes on recording the fact count
    /// the finished database will have.
    ///
    /// Says which instance and what is missing, because the alternative sentence an
    /// operator would read is [`NoSuchDatabase`](CatalogError::NoSuchDatabase)'s, and
    /// the two want opposite actions: this one ends by itself when the copy does.
    ///
    /// The instance is named by the **path** and not again in words. A refusal reaching
    /// a client is wrapped by `ServerError::Unservable`, which names the database and
    /// the instance itself, and the two sentences read as one — so saying it here as
    /// well put the same ULID in front of an operator twice.
    #[error(
        "this instance has a sidecar but no store: nothing in {path} is a fjall \
         database yet, which is what a copy into the store root that has not finished \
         looks like",
        path = path.display(),
    )]
    NoStore {
        name: String,
        instance: String,
        path: std::path::PathBuf,
    },

    /// A `Complete` instance whose store does not hold the facts its sidecar records.
    ///
    /// **The window [`NoStore`](CatalogError::NoStore) does not reach, caught one step
    /// later.** That guard asks whether fjall created a database at the instance
    /// directory's top level; fjall's create-or-recover then recurs *per keyspace*, and
    /// a recovery deletes a keyspace directory whose own `current` manifest has not
    /// arrived. So a copy that has delivered fjall's `version` marker and one keyspace
    /// manifest short of the rest is opened, is recovered, and comes back holding fewer
    /// facts than the sidecar beside it records — served as `Complete`, answering rows
    /// the finished database will not answer, for the life of the process.
    ///
    /// **This is a refusal and not a repair, and the difference matters to whoever
    /// reads it.** The comparison can only be made once the store is open, and opening
    /// it is what deleted the delivered files — so the artifact under the store root is
    /// already destroyed by the time this is raised, and the copy finishing does not
    /// restore it: every path it had to send has been sent. What this converts is a
    /// wrong answer served silently and permanently into a refusal that names the
    /// database. It does not make the window safe;
    /// [operations](../../../website/content/operations.md#publish-by-rename-required-for-a-live-root)'s
    /// atomic publish is what does that.
    ///
    /// Counted rather than re-hashed: a fact count is a fifth of the walk that would
    /// also check the content fingerprint, and it catches every shape in which the store
    /// answers *fewer* rows than it records — the silent one. The shapes it misses are
    /// the ones where the deleted keyspace was an `entities` tree, and those fail loudly
    /// on the first row read, with `StoreError::DanglingFactId`.
    ///
    /// Read off the resolved [`Entry`](crate::catalog::Entry)'s own sidecar, which the
    /// status deliberately is not: a `Complete` sidecar is the last durable act of a
    /// seal and never changes again, so this number cannot go stale between a resolve
    /// and an open the way `status` can.
    #[error(
        "this instance records {recorded} facts and its store holds {found}: {path} is \
         not the database that sidecar describes. A copy into the store root that was \
         opened before it finished looks like this, and the open is what deleted the \
         difference — the artifact needs publishing again",
        path = path.display(),
    )]
    FactsDoNotMatch {
        name: String,
        instance: String,
        path: std::path::PathBuf,
        recorded: u64,
        found: u64,
    },

    /// A schema that cannot be written down and read back as itself.
    ///
    /// A database embeds its schema as source and is served from that copy
    /// ([I13](../../../website/content/invariants.md#i13)), so a schema that does not survive the
    /// round trip is one no database could be opened with. Refused at `create`, where
    /// nothing has been written yet — the alternative is an artifact whose predicates
    /// come back at different positions, which reads every stored row through the wrong
    /// type and reports nothing.
    #[error("the schema for `{name}` cannot be embedded: {detail}")]
    UnwritableSchema { name: String, detail: String },

    /// A schema declaring a predicate in the namespace a **server** answers.
    ///
    /// A server appends its virtual predicates to every database's own schema when it
    /// opens one, so a database that already declares one composes to two and cannot be
    /// opened at all. Refused before anything is written, for the reason the round-trip
    /// check above is: the alternative is an artifact that exists and cannot be served,
    /// which no listing can explain and nothing can repair.
    #[error(
        "the schema for `{name}` declares `{predicate}`, which is in the namespace a \
         server answers rather than stores — a database cannot hold one"
    )]
    ReservedNamespace { name: String, predicate: String },

    /// A write asked of a database that is not [`Writable`](crate::meta::Status::Writable).
    ///
    /// `ops-I2`: once Complete, immutability is structural — no writable handle
    /// exists — rather than defended per write.
    #[error("`{name}` is {status} and cannot be written to")]
    NotWritable {
        name: String,
        status: crate::meta::Status,
    },

    /// A seal asked of a database holding no facts.
    ///
    /// A silently-empty sealed artifact is the classic CI failure that looks like
    /// success — the build "succeeded" and shipped nothing — so making one takes
    /// saying so.
    #[error("`{0}` holds no facts; sealing an empty database takes --allow-zero-facts")]
    EmptyDatabase(String),
}

impl CatalogError {
    /// Whether this is a store directory **another process is holding**, rather than
    /// one that is absent or unreadable.
    ///
    /// The condition ends when the holder lets go, so the answer a caller owes is
    /// "come back", not "there is no such database" — and the two are otherwise the
    /// same failed open. Recovering it walks the source chain because the seam boxes
    /// the backend's error unnamed ([`StoreError::Backend`]), which is the cost that
    /// variant's own doc states; naming `fjall::Error::Locked` is free here, where
    /// this crate *is* the fjall backend.
    #[must_use]
    pub fn is_locked(&self) -> bool {
        let mut error: Option<&(dyn std::error::Error + 'static)> = Some(self);

        while let Some(current) = error {
            if matches!(
                current.downcast_ref::<fjall::Error>(),
                Some(fjall::Error::Locked)
            ) {
                return true;
            }

            error = current.source();
        }

        false
    }
}

#[cfg(test)]
mod tests {
    use crate::{
        catalog::{Catalog, Intent, Selector},
        store::FjallDb,
    };

    use super::CatalogError;

    /// **A held store directory is told apart from a broken one**, which is what lets
    /// a caller answer "retry" rather than "there is no such database".
    ///
    /// fjall's lock is per open file description, so a second open in *this* process
    /// is refused exactly as another process's would be — and the refusal arrives
    /// boxed and unnamed through the seam, which is the whole reason this needs
    /// asking rather than matching.
    #[test]
    fn a_store_another_handle_is_holding_reports_itself_as_locked() {
        let dir = tempfile::tempdir().expect("a scratch directory");
        let path = dir.path().join("held");

        let _held = FjallDb::open(&path).expect("a store");

        let Err(refused) = FjallDb::open(&path).map_err(CatalogError::from) else {
            panic!("a second open of one directory must be refused");
        };

        assert!(refused.is_locked(), "{refused}");
    }

    /// And a fault that is **not** a held lock says so, or every failed open would be
    /// reported as retryable.
    #[test]
    fn a_name_the_root_does_not_hold_is_not_a_held_lock() {
        let dir = tempfile::tempdir().expect("a scratch directory");
        let catalog = Catalog::open(dir.path().join("store")).expect("a store root");
        let selector = Selector::parse("nothing-here").expect("a selector");

        let Err(absent) = catalog.resolve(&selector, Intent::Read) else {
            panic!("a name the root does not hold must be refused");
        };

        assert!(!absent.is_locked(), "{absent}");
    }
}
