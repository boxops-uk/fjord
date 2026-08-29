//! **A burst of connections, and the question of what the server does with the last
//! descriptor.**
//!
//! `soak` asks what happens to a cheap query while expensive ones are running.
//! This asks the cruder question underneath it: what happens when there are more
//! connections than the process has file descriptors. That failure needs no valid
//! handshake, no query and no data — a socket, opened and left alone, is enough — which
//! is why it is worth a tool of its own rather than a flag on the load generator.
//!
//! ```text
//! ulimit -n 1024
//! cargo run --release --example flood -- --data-dir /tmp/fj-bench --connections 1500
//! ```
//!
//! # What it reports, and how to read it
//!
//! Each connection is classified by what the server said, which is the whole point:
//!
//! - **held** — nothing was said to it. Either the server admitted it and is waiting
//!   for a handshake, or the kernel is still holding it in the backlog with no
//!   descriptor free to accept it with. From a client those two are indistinguishable,
//!   which is itself the finding when the cap sits above the descriptor limit.
//! - **refused (busy)** — accepted, told `Busy` by name, and closed. This is the
//!   admission cap working: the connection cost a descriptor for as long as the
//!   refusal took to write, and the server kept the rest.
//! - **closed with no frame** — accepted and dropped without a word, or the kernel's
//!   backlog overflowed. Not a refusal a client can act on; if this is large, the
//!   flood is outrunning the accept loop rather than the cap.
//! - **connect failed** — this *program* ran out of descriptors first, which happens
//!   whenever the flood and the server share a limit. It is a fact about the tool, not
//!   about the server, and it is printed separately for that reason.
//!
//! **The probe is the finding**, and it has three outcomes rather than two. A fresh
//! connection asks the catalogue a question while the flood is held:
//!
//! - **answered** — the flood is inside the cap and the server has room. Run with
//!   `--connections` below `--max-connections` to see this.
//! - **refused (busy)** — the flood has taken every place under the cap. The probe is
//!   turned away *by name*, immediately, which is what a saturated server should look
//!   like from outside. The cap reserves descriptors for the store and for the
//!   connections already being served; it does not hold a place open for a caller who
//!   has not arrived yet, and this line is what says so.
//! - **no answer** — the kernel took the connection into the backlog and the server
//!   has no descriptor to accept it with, so nothing replies and nothing refuses.
//!   That is the state a cap **above** the descriptor limit leaves: the server is
//!   alive, its accept loop is cycling on `EMFILE`, and a new client simply waits.
//!   The cure is a cap below the limit, which is the default.
//!
//! What must never happen, in any of the three, is the process exiting: an `accept`
//! that fails is one connection's problem. If the probe *after* the flood has let go
//! cannot connect either, the server is not saturated, it is dead — which is the bug
//! this tool was written for.

use std::{
    io::{ErrorKind, Read},
    os::unix::net::UnixStream,
    path::PathBuf,
    sync::{Arc, mpsc},
    thread,
    time::{Duration, Instant},
};

use fjord_cli::sample_schema;
use fjord_client::{Connection, ErrorCode, Mode};
use fjord_wire::{FrameKind, frame, protocol};

struct Options {
    socket: PathBuf,
    database: String,
    connections: usize,
    settle_ms: u64,
    hold_seconds: u64,
}

const USAGE: &str = "\
usage: flood [options]

  --socket PATH       where the server is listening
  --data-dir PATH     derives the socket path, as the CLI does
  --database NAME     the probe's database (default `code`)
  --connections N     raw connections to open and hold (default 1500)
  --settle-ms MS      how long to let the server answer before classifying (default 250)
  --hold-seconds S    keep the flood open this long after reporting, to look at the
                      server while it is under one (default 0)

The connections carry no handshake and send nothing. That is deliberate: an idle
socket is the cheapest thing a client can hold and the most expensive thing a server
can be asked to hold, and no authentication stands between the two.";

/// What one flooded connection turned out to be.
#[derive(Default)]
struct Tally {
    held: usize,
    refused: usize,
    closed: usize,
    failed: usize,
    /// Connections this program could not open because *it* ran out of descriptors.
    exhausted: usize,
    /// The server's own wording for the refusal, kept once, because a code without a
    /// message is not what an operator reads.
    refusal: Option<String>,
}

fn main() {
    let options = match parse() {
        Ok(options) => options,
        Err(message) => {
            eprintln!("flood: {message}\n\n{USAGE}");
            std::process::exit(2);
        }
    };

    println!(
        "flood — {} connections at {}\n",
        options.connections,
        options.socket.display()
    );

    let mut sockets = Vec::with_capacity(options.connections);
    let mut tally = Tally::default();

    let opened = Instant::now();
    for _ in 0..options.connections {
        match UnixStream::connect(&options.socket) {
            Ok(stream) => sockets.push(stream),
            Err(error) => {
                tally.failed += 1;
                // `EMFILE` here is this process hitting the same limit the server is
                // under, which is expected when they share one.
                if error.raw_os_error() == Some(24) {
                    tally.exhausted += 1;
                }
            }
        }
    }
    let opening = opened.elapsed();

    // The refusal is written by a task the server spawned, so it arrives after the
    // `connect` returned. Without this pause every connection classifies as held.
    std::thread::sleep(Duration::from_millis(options.settle_ms));

    for stream in &sockets {
        classify(stream, &mut tally);
    }

    println!("  opened in           {opening:?}");
    println!("  held                {}", tally.held);
    println!("  refused (busy)      {}", tally.refused);
    if let Some(message) = &tally.refusal {
        println!("                      \"{message}\"");
    }
    println!("  closed with no frame {}", tally.closed);
    println!(
        "  connect failed      {}  ({} because this program ran out of descriptors)",
        tally.failed, tally.exhausted
    );

    println!("\n  probe, while the flood is held");
    probe(&options);

    if options.hold_seconds > 0 {
        println!("\n  holding for {}s", options.hold_seconds);
        std::thread::sleep(Duration::from_secs(options.hold_seconds));
    }

    drop(sockets);

    // The descriptors are back. A server that survived answers exactly as it did
    // before; one that died refuses the connection, which is the failure this whole
    // tool exists to be able to state.
    println!("\n  probe, after the flood has let go");
    probe(&options);
}

/// Read what the server said to one flooded connection, without waiting on it.
fn classify(stream: &UnixStream, tally: &mut Tally) {
    let mut stream = stream;
    if stream.set_nonblocking(true).is_err() {
        tally.closed += 1;
        return;
    }

    let mut head = [0u8; frame::HEADER_LEN];

    match stream.read(&mut head) {
        // Nothing to say yet: the server has taken it and is waiting for a handshake
        // that is never coming, which is exactly what a held connection looks like.
        Err(error) if error.kind() == ErrorKind::WouldBlock => tally.held += 1,
        Ok(0) | Err(_) => tally.closed += 1,
        Ok(read) if read < head.len() => tally.closed += 1,
        Ok(_) => {
            let Ok(header) = frame::decode_header(&head) else {
                tally.closed += 1;
                return;
            };

            let mut payload = vec![0u8; header.length as usize];
            if stream.read_exact(&mut payload).is_err() || header.kind != FrameKind::ERROR {
                tally.closed += 1;
                return;
            }

            match protocol::decode_error(&payload) {
                Ok((ErrorCode::Busy, message)) => {
                    tally.refused += 1;
                    tally.refusal.get_or_insert(message);
                }
                // Any other error frame is the server saying something else entirely,
                // and lumping it in with a refusal would hide it.
                _ => tally.closed += 1,
            }
        }
    }
}

/// How long the probe waits before reporting that nothing answered.
///
/// **Not a client setting.** A connection with no descriptor to be accepted with sits
/// in the kernel's backlog indefinitely and the client waits on a handshake that
/// cannot come, so a tool whose whole job is to describe a saturated server must put
/// its own bound on that or hang reporting nothing.
const PROBE_PATIENCE: Duration = Duration::from_secs(5);

/// Ask the catalogue a question on a fresh connection, and say what happened.
///
/// `fjord.db.List` rather than anything in the data schema, so this says the same
/// thing about any server.
fn probe(options: &Options) {
    let at = Instant::now();
    let (answer, answered) = mpsc::channel();

    let socket = options.socket.clone();
    let database = options.database.clone();

    // The thread is abandoned rather than joined when it does not answer: it is parked
    // on a socket nobody will accept, and waiting for it is the thing being avoided.
    thread::spawn(move || {
        let counted = Connection::connect(
            &socket,
            &database,
            Arc::new(sample_schema::schema()),
            Mode::ReadOnly,
            false,
        )
        .and_then(|mut connection| connection.count("N where fjord.db.List {name = N}"));

        let _ = answer.send(counted.map_err(|error| (error.code(), error.to_string())));
    });

    match answered.recv_timeout(PROBE_PATIENCE) {
        Ok(Ok(databases)) => println!(
            "    ok — {databases} databases listed in {:?}",
            at.elapsed()
        ),
        Ok(Err((Some(ErrorCode::Busy), message))) => {
            println!("    refused, by name, in {:?} — {message}", at.elapsed());
        }
        Ok(Err((_, message))) => println!("    FAILED — {message}"),
        Err(_) => println!(
            "    no answer in {PROBE_PATIENCE:?} — accepted by the kernel, and the \
             server has no descriptor to take it with"
        ),
    }
}

fn parse() -> Result<Options, String> {
    let argv: Vec<String> = std::env::args().skip(1).collect();

    let mut data_dir: Option<PathBuf> = None;
    let mut socket: Option<PathBuf> = None;
    let mut database = "code".to_owned();
    let mut connections = 1500;
    let mut settle_ms = 250;
    let mut hold_seconds = 0;

    let mut at = 0;
    while at < argv.len() {
        let flag = argv[at].as_str();
        let mut value = || -> Result<String, String> {
            at += 1;
            argv.get(at)
                .cloned()
                .ok_or_else(|| format!("{flag} needs a value"))
        };

        match flag {
            "--socket" => socket = Some(PathBuf::from(value()?)),
            "--data-dir" => data_dir = Some(PathBuf::from(value()?)),
            "--database" => database = value()?,
            "--connections" => {
                connections = value()?
                    .parse()
                    .map_err(|_| "--connections takes a number")?;
            }
            "--settle-ms" => {
                settle_ms = value()?.parse().map_err(|_| "--settle-ms takes a number")?;
            }
            "--hold-seconds" => {
                hold_seconds = value()?
                    .parse()
                    .map_err(|_| "--hold-seconds takes a number")?;
            }
            "--help" | "-h" => {
                println!("{USAGE}");
                std::process::exit(0);
            }
            other => return Err(format!("unknown flag `{other}`")),
        }

        at += 1;
    }

    let socket = socket
        .or_else(|| data_dir.map(|dir| dir.join("fjord.sock")))
        .ok_or("one of --socket or --data-dir is needed")?;

    Ok(Options {
        socket,
        database,
        connections: connections.max(1),
        settle_ms,
        hold_seconds,
    })
}
