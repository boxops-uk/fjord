//! **What the server owns**: a store root, and every database under it.
//!
//! Until 9d the server was handed a `Vec<Arc<Database>>` opened once at startup, and
//! that was the whole reason lifecycle commands had to be refused while it ran: a
//! `create` cannot add to a list nobody holds, and a `remove` cannot delete a directory
//! this process has open. The registry is the mutable form of that list, plus the
//! [`Catalog`] the CLI's offline path already uses — which is what makes
//! [operations §5](../../../website/content/operations.md)'s "two front doors, one
//! implementation" true rather than aspirational. Everything below delegates the actual
//! work to `fjord-store`; what lives here is *when* it is safe to do it.
//!
//! # The three hazards, and where each is answered
//!
//! **A second handle on a store this process already holds.** `ops-I1` gives the server
//! every database under its root, so the offline `finish`'s first act — open the
//! directory — is exactly what the server must not do. It passes the handle it has:
//! [`Catalog::finish_held`].
//!
//! **A directory that is not a database yet.** `ops-I7` makes a sidecar what turns a
//! directory into a database, and a copy into a live store root delivers the sidecar and
//! the tables in whatever order it likes — so every open here goes through
//! [`Entry::open_store`], which refuses a directory holding no store rather than
//! creating one in it, and refuses a store that does not hold the facts its sealed
//! sidecar records. Neither is a repair: fjall's recovery deletes what a copy has not
//! delivered, and only publishing the instance directory under one rename keeps a bind
//! out of the window at all. See [`open_entry`].
//!
//! **A database pulled out from under a session.** `remove` closes the store, and a
//! query running against a closed store is a fault the client did not cause. So a
//! database is taken out of the map *first* — and, since [`Registry::bind`] opens what
//! the map does not hold, both halves run under that instance's
//! [gate](Registry::gates), which is what makes "out of the map" mean "no new session
//! can bind it". It is deleted only if this registry turns out to hold the last
//! reference; if a session still has it, the entry goes back and the request is
//! refused by name, which is what psql does and for the same reason.

use std::{
    collections::BTreeMap,
    sync::{Arc, Mutex, PoisonError, RwLock},
};

use fjord_schema::{
    fingerprint::{self, Identity},
    schema::Schema,
    syntax,
};
use fjord_store_fjall::{
    catalog::{Catalog, Entry, Finished, Intent, Listing, Selector},
    error::CatalogError,
    meta::Meta,
    schema_doc,
};

use fjord_wire::protocol::{Control, ControlOp, ControlReply};

use crate::{
    blocking,
    error::ServerError,
    session::Database,
    stats::{InterningCounters, ServerStats},
};

/// How a database's schema is arrived at.
///
/// **A schema belongs to a database, not to a server** ([I13](../../../website/content/invariants.md#i13)):
/// each one embedded its own at create, and this is what reads it back. Exactly one piece
/// is the server's rather than the database's — the **virtual** predicates,
/// `fjord.db.List` and `fjord.db.Interning`, which the server answers out of the root it
/// owns and no artifact holds.
///
/// **There is no fallback, deliberately.** Serving a database that embeds no copy with
/// any schema the server holds is a guess, and a silent one: if that schema has moved
/// since the database was written, its rows decode as something else — the loud version
/// a decode error, the quiet version a query answering zero rows. So a database with no
/// embedded copy is **listed and not served**, which is already how a copy this
/// server cannot *read* is treated, and is the same refusal
/// [I15](../../../website/content/invariants.md#i15) makes of a database carrying no format stamp.
///
/// The virtual half is carried as *source* rather than as a `Schema`, because composing
/// two schemas means composing two interners, and the language already has an operator
/// for it: concatenation. Reserved names sort last
/// ([`RESERVED_NAMESPACE`](fjord_schema::syntax::lower::RESERVED_NAMESPACE)), so
/// appending them moves no stored id.
pub struct Schemas {
    virtual_source: String,
    /// The virtual predicates alone, composed once.
    ///
    /// What a session naming **no database** is served with, and what its handshake
    /// identity is computed over. A control session therefore agrees with the server
    /// about the catalogue and about nothing else — which is all a lifecycle request can
    /// honestly agree about, since it names a database that may not exist yet.
    catalogue: Arc<Schema>,
}

impl Default for Schemas {
    /// This server's own: the catalogue, and nothing else.
    fn default() -> Schemas {
        Schemas::new(crate::catalogue::SOURCE)
    }
}

impl Schemas {
    /// `virtual_source` is appended to every database's own schema.
    ///
    /// Almost every caller wants [`Schemas::default`], which passes
    /// [`catalogue::SOURCE`](crate::catalogue::SOURCE). This form is for a battery that
    /// wants a server answering no virtual predicates at all: `""` is how a test says
    /// "no catalogue", which is a different thing from a catalogue it cannot read.
    ///
    /// # Panics
    ///
    /// If `virtual_source` does not parse and lower. On every real path it is
    /// compiled-in text, so this is a build error wearing a runtime hat — and a server
    /// that started with a catalogue it could not read would answer `fjord.db.List` with
    /// nothing and never say why.
    #[must_use]
    pub fn new(virtual_source: impl Into<String>) -> Schemas {
        let virtual_source = virtual_source.into();

        let catalogue = syntax::read("the catalogue", &virtual_source)
            .map(|schema| with_virtuals_marked(&schema))
            .unwrap_or_else(|error| panic!("the catalogue does not lower: {error}"));

        Schemas {
            virtual_source,
            catalogue: Arc::new(catalogue),
        }
    }

    /// The virtual predicates alone — what a session naming no database sees.
    #[must_use]
    pub fn catalogue(&self) -> &Arc<Schema> {
        &self.catalogue
    }

    /// The schema to serve the database at `path` with.
    ///
    /// # Errors
    ///
    /// [`CatalogError::Meta`] if the copy is **absent**, unreadable, does not lower, or is
    /// not the schema the sidecar says this database was created against. Each leaves the
    /// database **unserved** rather than served through a schema it does not hold: a
    /// schema that disagrees reads stored rows through the wrong types and reports
    /// nothing.
    pub fn of(&self, path: &std::path::Path, recorded: u64) -> Result<Arc<Schema>, CatalogError> {
        let fault = |detail: String| CatalogError::Meta {
            path: path
                .join(schema_doc::SCHEMA_DIR)
                .join(schema_doc::SCHEMA_FILE),
            detail,
        };

        // **No copy is a refusal, not a fallback.** A database that embeds no schema
        // predates one being kept; there is nothing here that can describe its rows, and
        // the only other candidate — a schema this build happens to carry — would be a
        // guess whose failure mode is a query answering nothing.
        let Some(source) = schema_doc::source(path)? else {
            return Err(fault(
                "it embeds no schema copy, so nothing here can describe its rows — \
                 re-index it against the schema it was built from"
                    .to_owned(),
            ));
        };

        let composed = format!("{source}\n{}", self.virtual_source);
        let schema = syntax::recover(schema_doc::SCHEMA_FILE, &composed).map_err(fault)?;
        let served = with_virtuals_marked(&schema);

        let embedded = fingerprint::of(&served);
        if embedded != recorded {
            return Err(fault(format!(
                "the copy is {embedded:#018x} and the sidecar records {recorded:#018x} — \
                 one of the two was edited"
            )));
        }

        Ok(Arc::new(served))
    }
}

/// Mark every predicate in the reserved namespace **virtual**.
///
/// Virtual by **namespace**, not by name: the reserved namespace is what makes "the
/// server answers this one" a property of the schema text rather than a list kept
/// somewhere else and forgotten when a second one is added.
///
/// Shared by [`Schemas::new`] and [`Schemas::of`] so a catalogue served on its own is
/// marked the same way as one appended to a database's schema. Marking it in one place
/// and not the other is how a catalogue predicate acquires keyspaces.
fn with_virtuals_marked(schema: &Schema) -> Schema {
    schema.clone().with_reserved_virtual()
}

/// The store root, and the databases open under it.
pub struct Registry {
    catalog: Catalog,
    /// How each database's schema is arrived at, and what a session bound to *no*
    /// database sees.
    schemas: Schemas,
    identity: Identity,
    /// **Keyed by instance id, not by name**, because a name holds several and a map
    /// keyed by name would silently serve one of them and drop the rest.
    ///
    /// Sorted, so a listing derived from it is stable; behind a lock, so a `create`
    /// can add to it while connections are being served.
    open: RwLock<BTreeMap<String, Arc<Database>>>,
    /// One gate per instance id: everything that opens an instance, or destroys one,
    /// holds that instance's gate while it does.
    ///
    /// Fjall takes the store directory exclusively, so a burst of binds on a cold
    /// instance would not open it N times — the losers would be *refused*, and a
    /// client would see a database that binds or does not depending on who else was
    /// binding it. And a delete that does not hold it is a delete a bind can undo
    /// half-way through, since [`bind`](Registry::bind) opens what the map does not
    /// hold.
    ///
    /// **A key is minted only for an instance [`Catalog::resolve`] returned**, which
    /// is the bound worth having: the map grows with the instances this root has held
    /// since startup and not with the names clients ask for, so no client can add to
    /// it by naming a database that is not there. Nothing removes a key — a gate
    /// outlives the instance it was minted for.
    gates: Mutex<BTreeMap<String, Arc<Mutex<()>>>>,
    /// This server's counters.
    ///
    /// Here because the registry is already *the* per-server shared value — every
    /// session is handed one, and there is exactly one per running server — so hanging
    /// the counters on it costs no new plumbing. It is not a claim that counting is a
    /// registry concern: [`ServerStats`] is its own module for that reason.
    stats: Arc<ServerStats>,
    /// Whether a write stream commits once per block rather than once per fact.
    ///
    /// **Off unless asked for, and the asking is operational rather than structural.** It
    /// is a property of *this run of the server*, not of the artifact — two databases with
    /// identical content must not differ in their metadata because of how fast somebody
    /// wanted to write them — so it lives here and not in the sidecar. What it trades is
    /// on [`Staged`](fjord_store_fjall::store::Staged): a crash during ingest may cost the
    /// index and can never cost its correctness.
    block_commits: bool,
}

impl Registry {
    /// Open every database under `catalog`'s root.
    ///
    /// A database that cannot be opened becomes a **problem in the listing** rather
    /// than a failure to start: it still appears in `list` (`ops-I7` reads its
    /// sidecar), a handshake to it is refused by name and says what could not be
    /// read, and the other nine are served. A server that refuses to start because
    /// one directory is corrupt is a server that cannot be used to find out which one.
    ///
    /// The listing records the scan; it does not settle anything.
    /// [`bind`](Registry::bind) opens what it does not find here, so an instance
    /// published after this ran — or one whose copy into the root was still in flight
    /// while it ran, and has since landed — is served when a session next asks for it,
    /// and nothing has to be restarted. A copy that is **still** in flight is refused
    /// there as it is here, by [`open_entry`], and re-tried on the next bind.
    ///
    /// A schema this server could not read is the same kind of problem as a store it
    /// could not open, and is treated the same way — the database is listed and not
    /// served, which is the only honest answer when what it holds cannot be described.
    ///
    /// # Errors
    ///
    /// [`ServerError::Catalog`] only if the root itself cannot be read.
    pub fn open(catalog: Catalog, schemas: Schemas) -> Result<(Registry, Listing), ServerError> {
        // Over the catalogue alone, which is all this server has of its own. A session
        // naming no database is handshaking about lifecycle, not about data.
        let identity = fingerprint::identity(&schemas.catalogue);

        let mut listing = catalog.list()?;
        let mut open = BTreeMap::new();

        for entry in &listing.entries {
            match open_entry(&schemas, entry) {
                Ok(database) => {
                    open.insert(entry.meta.instance.clone(), Arc::new(database));
                }
                Err(problem) => listing.problems.push(problem),
            }
        }

        Ok((
            Registry {
                block_commits: false,
                catalog,
                schemas,
                identity,
                open: RwLock::new(open),
                gates: Mutex::new(BTreeMap::new()),
                stats: Arc::new(ServerStats::default()),
            },
            listing,
        ))
    }

    /// The store root this server owns.
    ///
    /// Cheap to clone — it holds a path — and read rather than mutated: enumeration
    /// needs no ownership at all, which is the whole of `ops-I7`.
    #[must_use]
    pub fn catalog(&self) -> &Catalog {
        &self.catalog
    }

    /// This server's counters.
    ///
    /// Readable, and read by tests; not *reported* anywhere, which is
    /// [`stats`](crate::stats)'s own note to explain.
    #[must_use]
    /// Commit once per block on every write stream this registry serves.
    ///
    /// Consuming rather than a setter: it is a startup choice, and a server that changed
    /// its durability behaviour halfway through a stream would be answering two different
    /// questions in one ingest.
    pub fn with_block_commits(mut self, on: bool) -> Registry {
        self.block_commits = on;
        self
    }

    /// Whether write streams commit once per block.
    #[must_use]
    pub fn block_commits(&self) -> bool {
        self.block_commits
    }

    #[must_use]
    pub fn stats(&self) -> &Arc<ServerStats> {
        &self.stats
    }

    /// What every open database's interning path has done since this server opened it.
    ///
    /// **One row per database this server holds open**, in instance order, which is what
    /// `fjord.db.Interning` answers with. A database the root lists but this server
    /// could not open has no counters to report and no row; a sealed one is open, never
    /// interns, and reads zero — which is a true statement about it rather than a gap.
    ///
    /// Reading it takes each stripe's lock in turn, per database, so it belongs on a
    /// query about the counters and nowhere near the path that increments them. That is
    /// why nothing calls this unless a plan names the predicate.
    #[must_use]
    pub fn interning(&self) -> Vec<InterningCounters> {
        self.read()
            .values()
            .map(|database| {
                let (hits, misses) = database.db.lookup_counters();
                let (keys, entities) = database.db.intern_read_counters();

                InterningCounters {
                    name: database.name.clone(),
                    instance: database.instance.clone(),
                    hits,
                    misses,
                    keys,
                    entities,
                }
            })
            .collect()
    }

    /// The schema fingerprint a session that names no database handshakes against.
    #[must_use]
    pub fn fingerprint(&self) -> u64 {
        self.identity.schema()
    }

    /// The identity a session that names no database is checked against — the whole
    /// number and the per-predicate map alike.
    #[must_use]
    pub fn identity(&self) -> &Identity {
        &self.identity
    }

    /// The schema a session that names **no database** sees: the catalogue, which is all
    /// this server holds that is not some database's. A session bound to a database sees
    /// that database's own.
    #[must_use]
    pub fn schema(&self) -> &Arc<Schema> {
        &self.schemas.catalogue
    }

    /// The database `address` names — `name`, or `name@instance`.
    ///
    /// **Resolved through the catalog rather than over the open map**, which costs a
    /// walk of the root's sidecars per bind and buys two things. The rule for what an
    /// unqualified name means lives in exactly one place ([`Intent`]) instead of being
    /// restated here over a different element type. And the answer comes from the
    /// authoritative state on disk, so a database created a moment ago by the offline
    /// path is bindable without this server having noticed it.
    ///
    /// **A miss in the open map is not an absence**, so what resolution chose is
    /// opened here rather than refused. The map holds what the startup scan found and
    /// what `create` has added; the root holds whatever has been published into it
    /// since. Refusing on a miss leaves an instance synced into a live root listed and
    /// unbindable — and, when it is a *newer* instance of a name already being served,
    /// takes that name out entirely, because resolution ranks the new one first and
    /// the map has never seen it.
    ///
    /// **Blocking**: on a miss this opens a store, which replays its journals. Every
    /// caller reaches it through [`blocking::run`](crate::blocking::run).
    ///
    /// # Errors
    ///
    /// Whatever resolution reports — an unknown name, an unknown instance, or an
    /// ambiguity the caller must settle — and [`ServerError::Unservable`] for one the
    /// root holds and this server cannot open.
    pub fn bind(&self, address: &str) -> Result<Arc<Database>, ServerError> {
        let selector = Selector::parse(address)?;
        let entry = self.catalog.resolve(&selector, Intent::Read)?;

        if let Some(database) = self.by_instance(&entry.meta.instance) {
            return Ok(database);
        }

        self.attach(address, &entry)
    }

    /// Open the instance resolution chose, and serve it from here on.
    ///
    /// Takes the resolved [`Entry`] rather than the address, so that the instance
    /// opened is the one that was ranked: re-resolving under the gate could land on
    /// a newer one that appeared while this bind waited, and publish a handle for a
    /// database the caller was never told about.
    ///
    /// # Errors
    ///
    /// [`ServerError::Unservable`] for an instance this server cannot open, and
    /// [`ServerError::Catalog`] carrying [`CatalogError::NoSuchInstance`] for one that
    /// has been deleted since it was resolved.
    fn attach(&self, address: &str, entry: &Entry) -> Result<Arc<Database>, ServerError> {
        let gate = self.gate(&entry.meta.instance);
        let _opening = gate.lock().unwrap_or_else(PoisonError::into_inner);

        // **Read again under the gate.** Whoever held it may have been opening this
        // very instance, and fjall holds a store directory exclusively — so the loser
        // of that race must take the handle the winner published rather than be
        // refused by a lock this server itself is holding.
        if let Some(database) = self.by_instance(&entry.meta.instance) {
            return Ok(database);
        }

        // **An instance `remove` deleted is absent, not half-delivered.** Both
        // conditions reach [`open_entry`] as a directory with no store in it, and
        // there the answer is [`CatalogError::NoStore`] — "a copy has not finished",
        // which is the wrong sentence for a database the operator asked to be rid of
        // and would tell them to wait for something that is never coming. Asking here
        // is enough because `remove` holds this gate across the delete: nothing can be
        // part-way through one.
        if !entry.path.is_dir() {
            return Err(CatalogError::NoSuchInstance {
                name: entry.name().to_owned(),
                instance: entry.meta.instance.clone(),
            }
            .into());
        }

        match open_entry(&self.schemas, entry) {
            Ok(database) => {
                let database = Arc::new(database);

                self.write()
                    .insert(entry.meta.instance.clone(), Arc::clone(&database));

                Ok(database)
            }

            // **The startup scan's treatment, where there is no listing to put a
            // problem in.** The request is refused by name and every other database is
            // untouched — but a refusal by name alone is the same sentence a name that
            // is genuinely absent gets, so the reason is written to the server's log as
            // well as returned. One line per failed open: nothing here caches a
            // failure, so a corrupt instance is re-opened, and re-reported, every time
            // a session asks for it.
            Err(problem) => {
                eprintln!(
                    "cannot open `{address}` (instance {}): {problem}",
                    entry.meta.instance
                );

                Err(ServerError::Unservable {
                    address: address.to_owned(),
                    instance: entry.meta.instance.clone(),
                    source: Box::new(problem),
                })
            }
        }
    }

    /// The gate for one instance id, minted if this is the first caller to reach it.
    ///
    /// Held only long enough to clone the `Arc` out: the open — or the delete — happens
    /// under the inner lock, so one cold instance never blocks a bind of another.
    fn gate(&self, instance: &str) -> Arc<Mutex<()>> {
        let mut gates = self.gates.lock().unwrap_or_else(PoisonError::into_inner);

        Arc::clone(gates.entry(instance.to_owned()).or_default())
    }

    /// The database with this exact instance id, if this server opened it.
    #[must_use]
    pub fn by_instance(&self, instance: &str) -> Option<Arc<Database>> {
        self.read().get(instance).map(Arc::clone)
    }

    /// How many databases are being served — what `serve` prints, and what a test
    /// checks a `create` changed.
    #[must_use]
    pub fn len(&self) -> usize {
        self.read().len()
    }

    #[must_use]
    pub fn is_empty(&self) -> bool {
        self.len() == 0
    }

    /// Carry out a lifecycle request.
    ///
    /// **Takes the `Arc` rather than a reference to it**, because each of the three
    /// hands the registry itself to the blocking pool: the store one opens or deletes is
    /// opened or deleted under that instance's [gate](Registry::gates), and a gate held
    /// across an `.await` would be a lock held across a suspend.
    ///
    /// # Errors
    ///
    /// Whatever the catalog reports, or [`ServerError::InUse`] for a database a
    /// session still holds.
    pub async fn execute(
        self: Arc<Registry>,
        request: &Control,
    ) -> Result<ControlReply, ServerError> {
        match request.op {
            ControlOp::Create => self.create(&request.database, &request.schema).await,
            ControlOp::Finish => {
                self.finish(&request.database, request.allow_zero_facts)
                    .await
            }
            ControlOp::Remove => self.remove(&request.database).await,
        }
    }

    /// Create a database and start serving it.
    ///
    /// Built and opened before it is published, so a name appears in the registry only
    /// once it names something a session could actually bind — which is the same
    /// all-or-nothing rule [`Catalog::create`] follows on the disk, one level up.
    ///
    /// `source` is the schema to create it against, already resolved by the caller, and
    /// it is **required**. It is also **lowered here rather than trusted**: the text
    /// arrived over a socket, and a database created from a schema nothing read is a
    /// database nothing can serve.
    ///
    /// An empty `source` must not mean "this server's own" — that is the write half
    /// of the guess [`Schemas`] refuses to read: a database whose embedded schema is
    /// whatever binary happened to be listening, so the same command against two
    /// builds produces two different artifacts. `create` requires a schema
    /// ([operations](../../../website/content/operations.md)).
    ///
    /// **What that must not become is a rule about the request field.** Refusing an
    /// empty `source` here refuses a legitimately empty schema *file* with it, and only
    /// over the wire — the same `create` goes through against the directory when
    /// nothing is listening. An empty source lowers to an empty schema, and
    /// [`Catalog::create`] refuses that at both doors, by name.
    ///
    /// # Errors
    ///
    /// [`ServerError::Protocol`] if `source` does not lower.
    async fn create(
        self: Arc<Registry>,
        name: &str,
        source: &str,
    ) -> Result<ControlReply, ServerError> {
        let schema = Arc::new(
            syntax::read("the schema this client sent", source).map_err(ServerError::Protocol)?,
        );

        let wanted = name.to_owned();

        blocking::run(move || {
            let entry = self.catalog.create(&wanted, &schema)?;

            // **Opened through the gate, not here.** [`Catalog::create`] publishes the
            // instance directory with one rename, so a bind can resolve it before this
            // line — and fjall holds a store directory exclusively, so a second open
            // is refused rather than slow: an operator would be told `Locked` about a
            // `create` that in fact succeeded and is being served. Through the gate,
            // whichever of the two gets there first publishes the handle and the other
            // takes it.
            //
            // It also means a fresh database is served from its own embedded schema
            // copy rather than from the text this client sent — the same thing on the
            // happy path, and the difference is a database served, once, through a copy
            // nothing ever read back.
            //
            // **What it must not also mean is a bind's answer.** `attach` refuses an
            // instance it cannot open the way a session naming a missing database is
            // refused, and code 2 in reply to a `create` says the database this server
            // has just made and left under the root does not exist.
            self.attach(&wanted, &entry)
                .map_err(|failed| created_not_opened(&wanted, failed))?;

            Ok(ControlReply::Created {
                instance: entry.meta.instance,
            })
        })
        .await
    }

    /// Seal a database, and stop taking writes for it.
    ///
    /// **One path, through the handle this server holds.** A database the root lists
    /// and this server has not opened is opened here first rather than sealed offline,
    /// because the offline path opens the store itself: a bind reaching the same
    /// instance beside it is refused by fjall's lock, and one that reaches it in the
    /// gap between that seal releasing the directory and its sidecar landing can still
    /// read the pre-seal status and publish a `Writable` handle on a database that is
    /// now `Complete` — which every `ops-I2` gate then reads. Sealing through the
    /// handle this process has is what `ops-I1` asks of it anyway, and leaves no second
    /// open to race.
    async fn finish(
        self: Arc<Registry>,
        address: &str,
        allow_zero_facts: bool,
    ) -> Result<ControlReply, ServerError> {
        // **Resolved once, and everything below addresses the instance it chose.**
        // Re-resolving would let a `create` arriving in between move the answer, and a
        // seal is the one operation where landing on the wrong instance is unrecoverable.
        // `Intent::Write` prefers the writable one and falls back to the sole one, which
        // is what keeps a re-run after a crash able to report "already sealed".
        let selector = Selector::parse(address)?;
        let entry = self.catalog.resolve(&selector, Intent::Write)?;
        let exact = entry.selector();

        let database = match self.by_instance(&entry.meta.instance) {
            Some(database) => database,
            None => {
                let registry = Arc::clone(&self);
                let opening = entry.clone();
                let named = address.to_owned();

                blocking::run(move || registry.attach(&named, &opening)).await?
            }
        };

        // **The seal takes the barrier exclusively**, and that is what makes `ops-I2`
        // exact rather than nearly. A block whose session established while the database
        // was still Writable either takes the barrier before the seal — and the seal
        // waits behind it — or takes it after, and finds the database no longer
        // writable. There is no third order. Since 12e writers hold that barrier
        // *shared*, so "waits behind it" means waiting for every in-flight block rather
        // than for the one that happened to be running.
        let _sealing = database.sealing.write().await;

        let catalog = self.catalog.clone();
        let schema = Arc::clone(&database.schema);
        let held = Arc::clone(&database);

        let sealed = blocking::run(move || {
            Ok(catalog.finish_held(&exact, held.db.as_ref(), &schema, allow_zero_facts)?)
        })
        .await?;

        // Recorded before the seal, in program order — see `mark_complete`'s own
        // doc comment for why that ordering is a best effort and not a promise.
        database.mark_complete(sealed.fingerprint);
        database.seal();

        Ok(finished(&sealed))
    }

    /// Stop serving a database, then delete it.
    ///
    /// The order is the whole of it, and it is the same shape as
    /// [`Catalog::remove`]'s rename-then-delete one level down: make it unreachable
    /// first, destroy it second. Unreachable means what it says only while nothing can
    /// reach it again, and [`bind`](Registry::bind) opens what the map does not hold —
    /// so both halves run under the instance's [gate](Registry::gates).
    async fn remove(self: Arc<Registry>, address: &str) -> Result<ControlReply, ServerError> {
        // `Intent::Sole` rather than `Read`: a delete must not rank and commit, so
        // `rm code` where `code` holds three instances is a question, not a guess.
        let selector = Selector::parse(address)?;
        let entry = self.catalog.resolve(&selector, Intent::Sole)?;
        let instance = entry.meta.instance.clone();
        let exact = entry.selector();
        let named = address.to_owned();

        blocking::run(move || {
            // **Held across both halves.** Without it a bind that resolved this
            // instance a moment ago opens it while `catalog.remove` is still
            // re-resolving, and leaves a handle in the map on a directory that is about
            // to be gone — a session bound to a deleted store. With it, a bind either
            // gets here first and publishes a handle the removal below then finds, or
            // gets here after and finds no directory to open.
            let gate = self.gate(&instance);
            let _closing = gate.lock().unwrap_or_else(PoisonError::into_inner);

            {
                let mut open = self.write();

                if let Some(database) = open.remove(&instance) {
                    match Arc::try_unwrap(database) {
                        // The last reference, so the fjall handle closes right here —
                        // before anything deletes the directory it is holding.
                        Ok(database) => drop(database),

                        // A session still has it. Put it back: a query that is running
                        // is not a reason to hand a client a half-deleted database, and
                        // the caller can ask again once the session has gone. Reported
                        // by the address the caller used, since that is what they can
                        // act on.
                        Err(shared) => {
                            open.insert(instance, shared);
                            return Err(ServerError::InUse(named));
                        }
                    }
                }
            }

            self.catalog.remove(&exact)?;

            Ok(ControlReply::Removed)
        })
        .await
    }

    /// A poisoned lock is recovered from rather than propagated.
    ///
    /// The map is a `BTreeMap` of `Arc`s and nothing here can leave it half-updated,
    /// so the invariant a poison flag protects does not exist — and a server that
    /// answered every later request with a panic because one task died holding this
    /// would be strictly worse than one that carries on.
    fn read(&self) -> std::sync::RwLockReadGuard<'_, BTreeMap<String, Arc<Database>>> {
        self.open.read().unwrap_or_else(PoisonError::into_inner)
    }

    fn write(&self) -> std::sync::RwLockWriteGuard<'_, BTreeMap<String, Arc<Database>>> {
        self.open.write().unwrap_or_else(PoisonError::into_inner)
    }
}

/// Open one instance directory and read the schema and the status it will be served
/// with.
///
/// The **one** place this server opens a store: the startup scan and
/// [`Registry::attach`] both come through here, so a database that appears under a live
/// root is admitted on exactly the terms the scan would have admitted it on — and
/// refused on the same ones.
///
/// **The open is an open-*existing*, and that is what makes opening on demand safe.**
/// [`Entry::open_store`] refuses a directory that holds no store instead of creating
/// one in it, so an instance whose sidecar a copy has delivered and whose tables it
/// has not is refused by name rather than served. The alternative — which is what
/// [`FjallDb::open`](fjord_store_fjall::store::FjallDb::open) does, being also the
/// create path — is a fresh empty keyspace stamped into the directory the copy is
/// still writing, answering zero rows as `Complete` while the sidecar beside it
/// records the count the finished database will hold. Resolving an [`Entry`] rather
/// than a path buys nothing against that: the entry's `path` *is* the copy's target.
///
/// Nothing caches the refusal, so the next bind after the copy lands opens it.
///
/// **One level of that window is past the presence check, and what catches it there is
/// the sidecar's own fact count.** fjall's create-or-recover recurs per keyspace, and a
/// recovery *deletes* a keyspace directory whose `current` manifest a copy has not
/// delivered — so a store can be present, open, and hold fewer facts than the sealed
/// sidecar beside it records. [`Entry::open_store`] counts and refuses
/// ([`CatalogError::FactsDoNotMatch`]), which converts a wrong answer served for the
/// life of the process into a refusal and **repairs nothing**: the delete happened
/// inside the open, before there was anything to compare. Only publishing the instance
/// directory under one rename removes that window —
/// [operations](../../../website/content/operations.md#publish-by-rename-required-for-a-live-root)
/// requires it for a live root, and `Catalog::create` is what already does it.
///
/// **Status and content identity come off the sidecar once the store is open**, never
/// from the [`Entry`] a caller resolved earlier. [`Database::writable`] is stamped once
/// from this status and nothing re-reads it, so a seal landing between the resolve and
/// this open would publish a `Writable` handle on a `Complete` database — and every
/// `ops-I2` gate reads that stamp for the life of the handle, which the open map makes
/// the life of the process.
///
/// The read is authoritative where it is because fjall holds the directory exclusively
/// from the line above, and the only seal that can reach an instance under a root this
/// server owns is [`Catalog::finish_held`] — which seals through the handle this server
/// already has, and so holds that directory across the sidecar write. `ops-I1`'s root
/// lock is what keeps another process from being the one sealing it. Re-reading one
/// *instance's* own sidecar cannot drift onto another the way re-resolving a **name**
/// can: the path pins it.
fn open_entry(schemas: &Schemas, entry: &Entry) -> Result<Database, CatalogError> {
    let db = entry.open_store()?;
    let meta = Meta::read(&entry.path)?;
    let schema = schemas.of(&entry.path, meta.schema_fingerprint)?;

    Ok(Database::new(
        &meta.name,
        &meta.instance,
        db,
        schema,
        meta.status,
        meta.content_fingerprint,
    ))
}

/// Restate a failed open as a **failed `create`**, and leave everything else alone.
///
/// Neither refusal the open can produce is an honest answer to a `create`, which has
/// just published the instance the open then refused.
/// [`CatalogError::NoSuchInstance`] answers
/// [`ErrorCode::UnknownDatabase`](fjord_wire::protocol::ErrorCode::UnknownDatabase),
/// because an instance that names nothing is nothing to bind.
/// [`ServerError::Unservable`] answers that code for a fault that will not clear and
/// [`ErrorCode::InUse`](fjord_wire::protocol::ErrorCode::InUse) for one that will — a
/// held store, a copy still delivering — and *both* of those describe a database
/// somebody else is publishing rather than one this server just did. The
/// catch-all arm is not a third case — it is there because the open's error type is
/// the whole of [`ServerError`], and rewording anything that is not one of those two
/// would be this function guessing.
fn created_not_opened(database: &str, failed: ServerError) -> ServerError {
    match failed {
        ServerError::Unservable { .. }
        | ServerError::Catalog(CatalogError::NoSuchInstance { .. }) => {
            ServerError::CreatedNotOpened {
                database: database.to_owned(),
                detail: failed.to_string(),
            }
        }
        other => other,
    }
}

fn finished(sealed: &Finished) -> ControlReply {
    ControlReply::Finished {
        fingerprint: sealed.fingerprint,
        facts: sealed.facts,
        bytes: sealed.bytes,
        already_complete: sealed.already_complete,
    }
}

#[cfg(test)]
mod tests {
    use fjord_schema::schema::{Predicate, PredicateId, PredicateTy};
    use fjord_store_fjall::meta::Status;
    use lasso::Rodeo;

    use super::*;

    /// One stored predicate, stated in Rust so this file does not depend on a schema
    /// file to have a database to compose against.
    fn stored() -> Schema {
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

    /// A database created against [`stored`], and the schema a server serves it with.
    fn served() -> (tempfile::TempDir, Schema, Arc<Schema>) {
        let dir = tempfile::tempdir().expect("a scratch directory");
        let catalog = Catalog::open(dir.path().join("store")).expect("a store root");
        let entry = catalog.create("code", &stored()).expect("a database");

        let schemas = Schemas::default();
        let served = schemas
            .of(&entry.path, entry.meta.schema_fingerprint)
            .expect("it is servable");

        (dir, stored(), served)
    }

    /// **The property the whole arrangement rests on**: appending a virtual predicate
    /// does not change what a client has to agree with.
    ///
    /// If this ever fails, every .NET client stops connecting until it declares a
    /// predicate it can never write to — which is the outcome the virtual/stored split
    /// exists to avoid, and the reason identity skips virtuals rather than the server
    /// keeping two schemas and hoping they stay in step.
    #[test]
    fn the_catalogue_does_not_change_the_handshake() {
        let (_dir, stored, served) = served();

        assert_eq!(
            fingerprint::of(&stored),
            fingerprint::of(&served),
            "a virtual predicate must be invisible to the handshake"
        );
    }

    /// Restating the stored schema must not move an id, because an id is a position and
    /// is the tag in every `FactId` already written.
    #[test]
    fn appending_the_catalogue_moves_no_stored_id() {
        let (_dir, stored, served) = served();

        let appended = syntax::read("the catalogue", crate::catalogue::SOURCE)
            .expect("the catalogue lowers")
            .len();

        assert_eq!(
            served.len(),
            stored.len() + appended,
            "the catalogue's predicates appended, nothing else"
        );

        for index in 0..stored.len() {
            let id = PredicateId(index as u32);
            assert_eq!(
                served.get(id).and_then(|p| p.name()),
                stored.get(id).and_then(|p| p.name()),
                "predicate {index} moved"
            );
        }
    }

    /// **Exactly the catalogue's predicates are virtual, and nothing else is.**
    ///
    /// Stated as a set rather than as one id: the failure this guards is a predicate
    /// added to `catalogue.sigla` and left *stored*, which gives it keyspaces at `create`,
    /// puts it in `ops-I4`'s identity, and moves the fingerprint every client agrees
    /// with. That is what happened the first time one was added.
    #[test]
    fn exactly_the_catalogues_predicates_are_virtual() {
        let (_dir, stored, served) = served();

        let declared =
            syntax::read("the catalogue", crate::catalogue::SOURCE).expect("the catalogue lowers");

        let mut expected: Vec<PredicateId> = (0..declared.len())
            .filter_map(|index| declared.get(PredicateId(index as u32))?.name())
            .filter_map(|name| served.find_position(name).map(|(id, _)| id))
            .collect();
        expected.sort_unstable();

        assert!(!expected.is_empty(), "the catalogue declares something");
        assert_eq!(served.virtuals(), expected.as_slice());
        assert!(
            served
                .find_position(crate::catalogue::PREDICATE)
                .is_some_and(|(id, _)| served.is_virtual(id)),
            "the listing is one of them"
        );
        assert!(stored.virtuals().is_empty(), "the stored schema has none");
    }

    /// A registry serving whatever is in `root` **now**, and a catalog over the same
    /// root for putting things there behind its back — which is how an instance ends up
    /// cold: published after the scan, so an open of it is an open on demand.
    fn serving(root: &std::path::Path) -> (Registry, Catalog) {
        let (registry, _listing) = Registry::open(
            Catalog::open(root).expect("a store root"),
            Schemas::default(),
        )
        .expect("a registry");

        (registry, Catalog::open(root).expect("a store root"))
    }

    /// **An on-demand open stamps the status on the disk it opened, not the status it
    /// resolved** ([`ops-I2`](../../../website/content/invariants.md)).
    ///
    /// The window is a whole `finish` — a scan of the database and an fsync — and what
    /// falls into it is not transient: the handle goes into the open map, so a
    /// `Writable` stamp on a `Complete` database is what every later session for that
    /// name takes, and all three `ops-I2` gates read that one flag. A ReadWrite session
    /// would be admitted to a sealed database, and its blocks accepted, after the
    /// `ops-I4` identity had been computed and reported to a client.
    ///
    /// Stated by hand as resolve → seal → open, because that is the interleaving: a
    /// burst of binds racing a `finish` produces it about once in 150 rounds, which is
    /// not a test.
    #[test]
    fn an_on_demand_open_stamps_the_status_on_disk_and_not_the_one_it_resolved() {
        let dir = tempfile::tempdir().expect("a scratch directory");
        let (registry, catalog) = serving(&dir.path().join("store"));

        let published = catalog.create("sealme", &stored()).expect("a database");
        assert_eq!(registry.len(), 0, "nothing was open when it was published");

        // What a bind holds between resolving a name and opening what it chose.
        let selector = Selector::parse("sealme").expect("a selector");
        let resolved = catalog
            .resolve(&selector, Intent::Read)
            .expect("it resolves");
        assert!(
            resolved.status().is_writable(),
            "resolved while it was still writable"
        );

        let sealed = catalog
            .finish(&resolved.selector(), true)
            .expect("it seals");
        assert_eq!(
            Meta::read(&published.path).expect("a sidecar").status,
            Status::Complete,
            "the sidecar records the seal"
        );

        let bound = registry.attach("sealme", &resolved).expect("it opens");

        assert!(
            !bound.writable(),
            "a handle published on a sealed database must take no writes"
        );
        assert_eq!(
            bound.content_fingerprint(),
            Some(sealed.fingerprint),
            "and reports the identity the seal computed, not none"
        );
    }

    /// **A bind that resolved an instance a `remove` then deleted finds it gone**, and
    /// is told so in those words.
    ///
    /// The directory is not there, so the open refuses it either way — but a deleted
    /// instance and one whose copy into the root has not finished are the same shape
    /// from inside [`open_entry`], and [`CatalogError::NoStore`]'s "a copy has not
    /// finished" would tell an operator to wait for a database they asked to be rid
    /// of. Which of the two it is is knowable only here, under the gate `remove`
    /// holds.
    #[tokio::test]
    async fn a_bind_that_resolved_a_removed_instance_finds_it_gone() {
        let dir = tempfile::tempdir().expect("a scratch directory");
        let (registry, catalog) = serving(&dir.path().join("store"));
        let registry = Arc::new(registry);

        catalog.create("gone", &stored()).expect("a database");

        let selector = Selector::parse("gone").expect("a selector");
        let resolved = catalog
            .resolve(&selector, Intent::Read)
            .expect("it resolves");

        let removed = Arc::clone(&registry)
            .execute(&Control {
                op: ControlOp::Remove,
                database: "gone".to_owned(),
                schema: String::new(),
                allow_zero_facts: false,
            })
            .await
            .expect("it is removed");
        assert_eq!(removed, ControlReply::Removed);

        let Err(refused) = registry.attach("gone", &resolved) else {
            panic!("an instance that has been deleted must not open");
        };

        assert!(
            matches!(
                &refused,
                ServerError::Catalog(CatalogError::NoSuchInstance { instance, .. })
                    if instance == &resolved.meta.instance
            ),
            "it is refused as absent, naming the instance: {refused}"
        );
        assert!(
            !resolved.path.exists(),
            "and nothing was resurrected at {}",
            resolved.path.display()
        );
        assert!(registry.is_empty(), "with no handle left behind");
    }

    /// **`remove` does not begin while an open of the same instance is under way.**
    ///
    /// The other half of the claim above, and the one the module charter makes: taking
    /// an instance out of the map is only "unreachable" while nothing can put it back.
    /// A bind that got to the gate first must be finished — its handle in the map,
    /// where the removal below finds it and refuses or closes it — before anything
    /// renames the directory it is holding.
    ///
    /// Stated by holding the gate a bind mid-open holds, because that is the only point
    /// where the two meet: what a bind is doing in there is a store open, and there is
    /// no stopping one half-way. The wait is an observation window rather than a
    /// deadline — `remove` past the gate is a rename and an unlink.
    #[tokio::test]
    async fn a_remove_does_not_begin_while_an_open_of_the_same_instance_is_under_way() {
        let dir = tempfile::tempdir().expect("a scratch directory");
        let root = dir.path().join("store");

        // Warm rather than cold, so the map removal has something to take out.
        let published = Catalog::open(&root)
            .expect("a store root")
            .create("busy", &stored())
            .expect("a database");
        let (registry, _catalog) = serving(&root);
        let registry = Arc::new(registry);
        assert_eq!(registry.len(), 1, "the scan opened it");

        // The stand-in bind runs on a thread of its own, which is also where every real
        // holder of this gate is: it is a `std::sync::Mutex`, and the open it guards is
        // on the blocking pool.
        let (taken, held) = std::sync::mpsc::channel();
        let (release, wait) = std::sync::mpsc::channel::<()>();
        let opening = {
            let gate = registry.gate(&published.meta.instance);

            std::thread::spawn(move || {
                let _opening = gate.lock().expect("a fresh gate");
                taken.send(()).expect("the test is waiting for it");
                wait.recv().expect("the test releases it");
            })
        };
        held.recv().expect("the gate is held");

        let removing = tokio::spawn({
            let registry = Arc::clone(&registry);
            async move { registry.remove("busy").await }
        });

        tokio::time::sleep(std::time::Duration::from_millis(250)).await;

        assert!(
            registry.by_instance(&published.meta.instance).is_some(),
            "the instance is still served: `remove` has not passed the gate"
        );
        assert!(
            published.path.is_dir(),
            "and its directory is untouched at {}",
            published.path.display()
        );

        release.send(()).expect("the holder is waiting");
        opening.join().expect("the holder");

        assert_eq!(
            removing
                .await
                .expect("the remove task")
                .expect("it removes"),
            ControlReply::Removed,
            "and it runs to completion once the gate is free"
        );
        assert!(!published.path.exists(), "the directory is gone");
        assert!(registry.is_empty(), "and so is the handle");
    }

    /// A server's own schema is the catalogue and nothing else — which is what a session
    /// naming no database sees, and what its handshake identity is over.
    #[test]
    fn a_servers_own_schema_is_the_catalogue_alone() {
        let schemas = Schemas::default();
        let catalogue = schemas.catalogue();

        assert!(!catalogue.is_empty(), "it declares the virtual predicates");
        assert_eq!(
            catalogue.virtuals().len(),
            catalogue.len(),
            "and every one of them is virtual: a server stores nothing of its own"
        );
    }
}
