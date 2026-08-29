//! The Unix socket listener.
//!
//! **Unix socket only, and that is `ops-I10` rather than a shortcut.** The design is
//! default-closed: TCP is an explicit opt-in the operator puts behind an
//! authenticated gateway, and a server that binds a network interface because nobody
//! said not to is the failure that rule exists to prevent. Adding the opt-in is a
//! second listener and a flag; leaving it out is the safe default, not an omission to
//! be tidied up later.
//!
//! # Binding is synchronous, accepting is not
//!
//! [`Listener::bind`] takes the socket with `std` and only converts to tokio's inside
//! [`run`](Listener::run). That is not fussiness: `tokio::net::UnixListener::bind`
//! requires a runtime context, so binding there would mean a caller could not find out
//! whether the socket was available until it had already started a runtime — and the
//! *readiness file* has to be written after a successful bind and before anything
//! connects, which is much easier to get right when binding is an ordinary fallible
//! call.
//!
//! One task per connection, spawned onto the runtime. The blocking half of a
//! connection's work — every fjall read and write — goes to
//! [`spawn_blocking`](tokio::task::spawn_blocking) from inside
//! [`session`](crate::session), so a task here is doing framing and nothing else.
//!
//! # The loop does not end, and that is the whole of its error handling
//!
//! An `accept` that fails is answered by [`admission`](crate::admission) and the loop
//! goes round again. Propagating it instead is the failure this arrangement exists to
//! prevent: `EMFILE` is a statement about the process, so a loop that returned it
//! ended the server and dropped every live connection in order to refuse one new one.
//! What is above the loop — [`serve_on`]'s `select!` — therefore only ever hears about
//! a panic, which is why an accept loop's return type says it has no other way out.

use std::{
    convert::Infallible,
    fs,
    future::Future,
    io::Write,
    os::unix::net::UnixListener as StdUnixListener,
    path::{Path, PathBuf},
    sync::Arc,
};

use tokio::{
    io::{AsyncRead, AsyncWrite, AsyncWriteExt},
    net::{TcpListener, UnixListener, UnixStream},
};

use fjord_wire::{FrameKind, StreamId, encode_frame, protocol};

use crate::{
    admission::{ACCEPT_BACKOFF, AcceptOutcome, Admission, after_accept_error},
    error::ServerError,
    registry::Registry,
    stats::ServerStats,
};

/// A bound listener, and the socket path it owns.
pub struct Listener {
    listener: StdUnixListener,
    path: PathBuf,
    examined_ceiling: u64,
    /// Shared rather than owned, because the cap is on the **process**: descriptors
    /// are not per-listener, so a socket and an opted-in TCP port draw on one pool.
    admission: Arc<Admission>,
}

impl Listener {
    /// Bind the socket at `path`.
    ///
    /// A stale socket file from a killed server is removed first. That is safe only
    /// because `ops-I1` gives one process ownership of the store root — the lock on
    /// the data directory is what says nobody is serving it, and the socket file is a
    /// consequence rather than the lock itself.
    ///
    /// # Errors
    ///
    /// [`ServerError::Io`] if the socket cannot be bound.
    pub fn bind(path: impl AsRef<Path>) -> Result<Listener, ServerError> {
        let path = path.as_ref().to_path_buf();

        if path.exists() {
            fs::remove_file(&path)?;
        }

        let listener = StdUnixListener::bind(&path)?;
        listener.set_nonblocking(true)?;

        Ok(Listener {
            listener,
            path,
            examined_ceiling: crate::session::EXAMINED_CEILING,
            admission: Arc::new(Admission::from_fd_limit()),
        })
    }

    /// Set the deployment ceiling applied independently to each executor chunk.
    ///
    /// The default is intentionally generous; an embedding may tighten it for its
    /// workload. It is policy rather than query semantics and never enters a plan or
    /// cursor fingerprint.
    #[must_use]
    pub fn with_examined_ceiling(mut self, ceiling: u64) -> Self {
        self.examined_ceiling = ceiling;
        self
    }

    /// Cap concurrent connections at `max`, replacing the derived default.
    ///
    /// The default is a share of the descriptor limit
    /// ([`Admission::from_fd_limit`]); this is for a deployment that knows its own
    /// number — and for a test that wants a cap it can reach.
    #[must_use]
    pub fn with_max_connections(mut self, max: usize) -> Self {
        self.admission = Arc::new(Admission::with_max(max));
        self
    }

    /// The cap this listener admits under, so a second listener can share it.
    #[must_use]
    pub fn admission(&self) -> &Arc<Admission> {
        &self.admission
    }

    #[must_use]
    pub fn path(&self) -> &Path {
        &self.path
    }

    /// Write the readiness file, **after** the listener is accepting.
    ///
    /// Glean's `--write-port`, and the ordering is the whole of it: a signal that
    /// appears before the listener does is a race dressed as a signal, and a test that
    /// waits on it would connect to nothing and blame the server. Because
    /// [`bind`](Self::bind) has already taken the socket by the time this is called,
    /// a client that sees the file can connect.
    ///
    /// # Errors
    ///
    /// [`ServerError::Io`] if the file cannot be written.
    pub fn announce(&self, at: impl AsRef<Path>) -> Result<(), ServerError> {
        let mut file = fs::File::create(at)?;
        file.write_all(self.path.as_os_str().as_encoded_bytes())?;
        file.sync_all()?;
        Ok(())
    }

    /// Accept forever, serving each connection on its own task.
    ///
    /// **Only the setup can fail.** Once the loop is running, an `accept` that fails
    /// is refused or backed off and never returned — one client, or one exhausted
    /// resource, is not a reason to stop serving the others.
    ///
    /// # Errors
    ///
    /// [`ServerError::Io`] if the bound socket cannot be handed to the runtime.
    pub async fn run(self, registry: Arc<Registry>) -> Result<(), ServerError> {
        let listener = UnixListener::from_std(self.listener.try_clone()?)?;

        match accept_loop(
            listener,
            registry,
            Arc::clone(&self.admission),
            self.examined_ceiling,
        )
        .await {}
    }

    /// [`run`](Self::run) on a runtime of its own, for a caller that has none.
    ///
    /// # Errors
    ///
    /// [`ServerError::Io`] if the runtime cannot be built, or whatever `run` reports.
    pub fn run_blocking(self, registry: Arc<Registry>) -> Result<(), ServerError> {
        let runtime = tokio::runtime::Builder::new_multi_thread()
            .enable_all()
            .build()?;

        runtime.block_on(self.run(registry))
    }
}

impl Drop for Listener {
    /// Take the socket file with it. A leftover file is what the next `bind` has to
    /// clean up, and leaving one behind makes "is a server running?" ambiguous —
    /// which §2 says the socket is supposed to answer.
    fn drop(&mut self) {
        let _ = fs::remove_file(&self.path);
    }
}

/// What an accept loop needs of a listener.
///
/// Two transports, one loop: the admission rule, the backoff and the refusal are the
/// same decisions on a Unix socket and on an opted-in TCP port, and a second copy of
/// them is a second copy that can be fixed only once.
trait Accepting {
    /// The read half of an accepted connection.
    ///
    /// **Owned halves, not [`tokio::io::split`]**: splitting behind a lock would put
    /// one on every frame read and every frame written, which is the hot path §18 of
    /// `bench/FINDINGS.md` measures. Each transport hands over its own pair instead,
    /// so the generic loop costs nothing the transport did not already cost.
    type Reader: AsyncRead + Unpin + Send + 'static;
    type Writer: AsyncWrite + Unpin + Send + 'static;

    fn accept(&self) -> impl Future<Output = std::io::Result<(Self::Reader, Self::Writer)>> + Send;
}

impl Accepting for UnixListener {
    type Reader = tokio::net::unix::OwnedReadHalf;
    type Writer = tokio::net::unix::OwnedWriteHalf;

    async fn accept(&self) -> std::io::Result<(Self::Reader, Self::Writer)> {
        UnixListener::accept(self)
            .await
            .map(|(stream, _peer)| stream.into_split())
    }
}

impl Accepting for TcpListener {
    type Reader = tokio::net::tcp::OwnedReadHalf;
    type Writer = tokio::net::tcp::OwnedWriteHalf;

    async fn accept(&self) -> std::io::Result<(Self::Reader, Self::Writer)> {
        TcpListener::accept(self)
            .await
            .map(|(stream, _peer)| stream.into_split())
    }
}

/// Accept forever: admit, refuse, or wait — never stop.
///
/// The return type is the claim. [`Infallible`] says there is no path out of here
/// short of a panic or the task being dropped, which is what makes a caller's
/// "whichever stops first stops the server" a statement about panics rather than
/// about the weather.
async fn accept_loop<L>(
    listener: L,
    registry: Arc<Registry>,
    admission: Arc<Admission>,
    examined_ceiling: u64,
) -> Infallible
where
    L: Accepting,
{
    loop {
        let (reader, writer) = match listener.accept().await {
            Ok(halves) => halves,
            Err(error) => {
                on_accept_error(&error, registry.stats()).await;
                continue;
            }
        };

        // **Taken before the task is spawned, and moved into it.** Acquiring inside the
        // task would let unboundedly many connections exist while they queued for a
        // permit, which is the descriptor consumption the cap is here to stop.
        let Some(admitted) = admission.try_admit() else {
            // **Politeness has a budget of its own.** With none left the connection is
            // dropped where it stands rather than queued behind a refusal, because a
            // refusal waiting its turn is a descriptor held for exactly as long as the
            // connection it was refusing would have held one.
            let Some(refusing) = admission.try_refuse() else {
                registry.stats().connection_dropped();
                continue;
            };

            let stats = Arc::clone(registry.stats());
            let max = admission.max();

            tokio::spawn(async move {
                refuse(reader, writer, max, &stats).await;
                drop(refusing);
            });
            continue;
        };

        let registry = Arc::clone(&registry);

        tokio::spawn(async move {
            let _admitted = admitted;

            if let Err(error) =
                crate::session::serve(reader, writer, &registry, examined_ceiling).await
            {
                eprintln!("connection ended: {error}");
            }
        });
    }
}

/// Answer a failed `accept` and let the loop go round again.
async fn on_accept_error(error: &std::io::Error, stats: &Arc<ServerStats>) {
    stats.accept_failed();

    match after_accept_error(error) {
        AcceptOutcome::Backoff => {
            // Logged every time, and that is affordable *because* of the sleep below:
            // the backoff rate-limits this line to twenty a second however hard the
            // flood pushes. It is also the only signal an operator gets that the
            // process is at its descriptor limit rather than idle.
            eprintln!("accept: {error} — pausing {ACCEPT_BACKOFF:?} for descriptors to free");
            tokio::time::sleep(ACCEPT_BACKOFF).await;
        }
        // Counted and not printed: a peer that goes away between the SYN and the
        // accept is ordinary, arrives at whatever rate the peer chooses, and a log
        // line per occurrence is a second denial of service written by us.
        AcceptOutcome::Retry => {}
    }
}

/// Tell a connection the server is full, and close it.
///
/// A frame rather than a silent close, because a client that is told can back off,
/// and one that is not has to guess between "full", "crashed" and "wrong path".
///
/// **The close is the delicate part.** Closing a TCP socket with the peer's bytes
/// still unread sends an RST, and an RST can discard a refusal the peer has received
/// but not yet read — so the write half is shut down first and what the peer sent is
/// drained, bounded by [`REFUSAL_LINGER`] so a client that never closes cannot hold a
/// descriptor the cap has already promised to somebody else.
async fn refuse<R, W>(mut reader: R, mut writer: W, max: usize, stats: &Arc<ServerStats>)
where
    R: AsyncRead + Unpin,
    W: AsyncWrite + Unpin,
{
    stats.connection_refused();

    let error = ServerError::AtCapacity { max };
    let payload = protocol::encode_error(error.code(), &error.to_string());

    let mut frame = Vec::new();

    // Encoding cannot fail for a payload this size, and a refusal that failed to be
    // written is still a refusal: the connection is dropped either way.
    if encode_frame(&mut frame, FrameKind::ERROR, StreamId(0), &payload).is_err() {
        return;
    }

    if writer.write_all(&frame).await.is_err() {
        return;
    }

    let _ = writer.flush().await;
    let _ = writer.shutdown().await;
    let _ = tokio::time::timeout(REFUSAL_LINGER, drain(&mut reader)).await;
}

/// How long a refused connection is given to close after being told.
///
/// The frame is already in the peer's receive buffer by then, and a peer that is
/// waiting to read it closes in microseconds — this is only ever spent on one that
/// does not. That makes it a **budget rather than a courtesy**: every millisecond here
/// is a descriptor held, and a place in [`Admission::try_refuse`]'s small budget not
/// available to the next connection to be turned away.
const REFUSAL_LINGER: std::time::Duration = std::time::Duration::from_millis(10);

/// Read until the peer closes, keeping nothing.
async fn drain<S: AsyncRead + Unpin>(stream: &mut S) {
    use tokio::io::AsyncReadExt;

    let mut scratch = [0u8; 512];

    while let Ok(read) = stream.read(&mut scratch).await {
        if read == 0 {
            break;
        }
    }
}

/// Serve one accepted connection.
///
/// # Errors
///
/// Whatever [`session::serve`](crate::session::serve) reports as fatal.
pub async fn serve_stream(stream: UnixStream, registry: &Arc<Registry>) -> Result<(), ServerError> {
    serve_stream_with_ceiling(stream, registry, crate::session::EXAMINED_CEILING).await
}

async fn serve_stream_with_ceiling(
    stream: UnixStream,
    registry: &Arc<Registry>,
    examined_ceiling: u64,
) -> Result<(), ServerError> {
    // Split rather than cloned: the session holds a buffered reader and a buffered
    // writer at once, and `into_split` is what gives it two independently-owned halves
    // of one socket.
    let (reader, writer) = stream.into_split();
    crate::session::serve(reader, writer, registry, examined_ceiling).await
}

/// Bind, announce, and serve — the whole of what a `serve` command does, on a runtime
/// of its own.
///
/// # Errors
///
/// [`ServerError::Io`] if the socket cannot be bound or the readiness file written.
pub fn serve_unix(
    socket: impl AsRef<Path>,
    ready_file: Option<&Path>,
    registry: Arc<Registry>,
) -> Result<(), ServerError> {
    let listener = Listener::bind(socket)?;

    if let Some(at) = ready_file {
        listener.announce(at)?;
    }

    listener.run_blocking(registry)
}

/// Bind, announce, and serve — over the socket, and over TCP as well when asked.
///
/// # `ops-I10`, and what an address argument does and does not buy
///
/// Binding a network interface is **default-closed**, and stays that way: `listen` is
/// `None` unless somebody passed an address, and there is no configuration file entry,
/// no environment variable and no "listen on localhost by default" that could turn it on
/// while nobody was looking. That is the whole of the invariant's mechanism, and it is
/// deliberately this crude.
///
/// What it is *not* is access control. The handshake accepts anonymous and has a
/// reserved credential slot nothing fills, so an opted-in TCP port is reachable by
/// anyone who can route to it. The design's answer is that access control belongs to the
/// transport — a Unix socket has file permissions, and a TCP port has whatever gateway
/// the operator puts in front of it — and the honest statement of that is here rather
/// than in a comment nobody reads: **an operator who passes this flag is taking that
/// responsibility on.**
///
/// # Errors
///
/// [`ServerError::Io`] if either listener cannot be bound, or the readiness file cannot
/// be written.
pub fn serve_on(
    socket: impl AsRef<Path>,
    listen: Option<&str>,
    ready_file: Option<&Path>,
    max_connections: Option<usize>,
    registry: Arc<Registry>,
) -> Result<(), ServerError> {
    let mut listener = Listener::bind(socket)?;

    if let Some(max) = max_connections {
        listener = listener.with_max_connections(max);
    }

    if let Some(at) = ready_file {
        listener.announce(at)?;
    }

    let Some(address) = listen else {
        return listener.run_blocking(registry);
    };

    let address = address.to_owned();
    let examined_ceiling = listener.examined_ceiling;
    // **One cap over both doors.** Descriptors are the process's, so two listeners
    // admitting `max` each would reserve nothing at all.
    let admission = Arc::clone(listener.admission());
    let runtime = tokio::runtime::Builder::new_multi_thread()
        .enable_all()
        .build()?;

    runtime.block_on(async move {
        // Bound before either is served, so a bad address fails the command rather than
        // leaving a half-open server that answers on one door and not the other.
        let tcp = TcpListener::bind(&address).await?;

        let unix = {
            let registry = Arc::clone(&registry);
            tokio::spawn(async move { listener.run(registry).await })
        };

        let tcp = tokio::spawn(accept_loop(tcp, registry, admission, examined_ceiling));

        // Whichever stops first stops the server, and after the accept loops stopped
        // being able to end that means a panic: a server still answering on one door
        // while the other has fallen over is a worse state than one that has stopped.
        tokio::select! {
            result = unix => result.map_err(|error| ServerError::Io(std::io::Error::other(error)))?,
            result = tcp => match result {
                Ok(never) => match never {},
                Err(error) => Err(ServerError::Io(std::io::Error::other(error))),
            },
        }
    })
}
