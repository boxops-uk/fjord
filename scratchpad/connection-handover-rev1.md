# Connection handover — an authz proxy that leaves the data path

| | |
|---|---|
| **Issue** | not filed yet; this document is the plan under review |
| **Area** | `fjord-server` (`server`, `admission`, `session`, `stats`), `operations.mdx`. No client change, no protocol change, no format change |
| **Invariants** | **`ops-I10`** (the transport is the trust boundary) — strengthened rather than bent. `ops-I1` untouched: the proxy never opens the store. `ops-I6` is where the grant is checked |
| **Format** | unchanged |
| **Protocol** | **unchanged**, and that is the headline: a client cannot tell |
| **New dependencies** | none — `libc` is already here for `getrlimit` and the accept-time errnos |

## The problem

`ops-I10` says authentication is the transport's job, and operations says TCP is
"reachability, not access control — put a gateway in front". So a deployment that wants
per-database authorisation runs a proxy: terminate TCP, authorise, relay to Fjord's Unix
socket.

The proxy then sits in the data path **for the life of every connection**. It costs a hop
and a copy on every frame, in both directions, for a decision it made once at connect. A
query answering ten thousand rows pays the proxy ten thousand times for one authorisation.

## The mechanism

`SCM_RIGHTS`. The proxy already holds a Unix socket to Fjord; instead of relaying bytes
over it forever, it **passes the client's socket file descriptor** over it once:

```
client ──TCP──▶ proxy          proxy ──sendmsg(SCM_RIGHTS: fd, grant)──▶ fjord
                                         (proxy closes its fd and forgets)
client ◀────────────── TCP ──────────────────────────────────────────▶ fjord
```

The kernel shares the socket object rather than copying it, so nothing about the TCP
connection changes from the client's side — same sequence numbers, same window, no
reconnect. The proxy is not a faster relay; it is **not a relay**. No extra hop, no second
copy, no per-connection memory, and the proxy can be restarted without dropping live
sessions.

## Why this server takes it cheaply

`Accepting` is already the seam:

```rust
trait Accepting {
    type Reader: AsyncRead + Unpin + Send + 'static;
    type Writer: AsyncWrite + Unpin + Send + 'static;
    fn accept(&self) -> impl Future<Output = io::Result<(Self::Reader, Self::Writer)>> + Send;
}
```

`UnixListener` and `TcpListener` implement it, and `accept_loop` is generic over it —
admission, refusal, accept-error backoff and task spawning are all transport-agnostic
already. **A handover listener is a third implementation** whose `accept` is a `recvmsg`
that returns a received descriptor instead of a real accept. A received TCP fd becomes
`tokio::net::TcpStream::from_std`, which splits into the same `Owned{Read,Write}Half`
pair `TcpListener` already yields, so everything downstream is untouched.

The client needs **nothing**: `Transport` is already `Unix | Tcp`, the client is holding
an ordinary TCP socket, and it never learns the other end changed process.

tokio does not expose ancillary data, so the control socket is a `std::os::unix::net::
UnixStream` driven through `tokio::io::unix::AsyncFd` with `libc::recvmsg` — which is why
this adds no dependency.

## The one thing that is not simple: who has read what

A socket cannot be handed over mid-protocol without also handing over **the bytes the
proxy has already consumed**. That is the trap in every fd-handover design, and it decides
the shape.

### Shape A — hand over before any Fjord byte is read (recommended)

The proxy authorises on transport facts alone — peer address, a pre-protocol preamble of
its own design, or a token exchange it defines — and hands the fd over **before** the
client's `Startup` frame arrives. It carries a **grant** alongside: which databases this
connection may open, and in which modes.

Fjord then reads `Startup { version, database, mode, fingerprint }` itself, exactly as
today, and checks it against the grant at the point `ops-I6` already resolves a session's
mode. Nothing has been consumed; the handshake code does not move.

### Shape B — hand over after the startup frame

The proxy reads `Startup` to learn the database and mode, authorises *that*, and hands
over the fd **plus the frame bytes it consumed**. Fjord must then resume mid-stream.

Shape B needs no preamble the client must know about, which is its whole appeal — but it
puts a "here are the bytes I already read" path into the session, and a bug there is a
protocol desynchronisation rather than a clean failure. **Build A; keep B in mind only if
a deployment genuinely cannot add a preamble.**

### What kills handover entirely

**A proxy that transforms the byte stream cannot hand over.** If it terminates TLS, it is
in the crypto path for the life of the connection and no descriptor passing can remove it.
Handover suits a proxy that authorises and forwards *verbatim*: plaintext on a trusted
network, or TLS terminated at an ingress ahead of it. This is worth stating in the book
before someone tries it behind an mTLS terminator and finds the latency unchanged.

## The grant is now the trust boundary, and it is a message

Everything security-critical moves into one small structure on one socket. Three
consequences, none optional:

- **The control socket's peer must be exactly the proxy.** `SO_PEERCRED` on accept, plus
  filesystem permissions on the socket path. Fjord checks neither today, and with
  handover a process that can connect to the control socket can assert *any* grant.
- **The grant must be explicit and closed.** "May open database X in mode Y", never "is
  authenticated" — the point is that Fjord makes no policy decision, it enforces one it
  was handed. That keeps `ops-I10` true: the transport still decides, it just stops
  carrying the bytes.
- **A grant that does not cover the requested database is a refusal by name**, on the
  existing refusal path, not a dropped connection.

## Admission, which is where this quietly goes wrong

Connections handed over arrive **by a different door**. `Admission::try_admit` is called
in `accept_loop`, so a handover listener going through the same loop inherits the cap for
free — but two things still need deciding:

- **A received fd is installed by `recvmsg` whether or not there is a permit.** So the
  refusal path must own it and close it. That composes with `refuse()` as it stands, and
  it must, or the cap leaks descriptors precisely under the load it exists for.
- **The proxy's own descriptors and Fjord's are now two pools.** §F8 in `bench/FINDINGS.md`
  is about descriptor exhaustion under flood; handover moves where the ceiling binds, and
  the numbers there were measured on the other arrangement.

Stats should distinguish a handed-over connection from an accepted one, or an operator
cannot tell which door a load arrived through.

## The one signature that has to change

`Accepting::accept` returns `(Reader, Writer)`, and a grant is a third thing. Either the
trait gains an associated `Context` (`()` for the two existing listeners) or `accept`
returns a triple. That is the only intrusive edit in this work item, and it is worth doing
as an associated type: a listener that carries no context should not be made to say so at
every call site.

## Acceptance criteria

1. **A handed-over connection is indistinguishable to a client.** The .NET client, which
   shares no constants with the Rust one, completes a handshake and a query over a
   connection it opened to a proxy that handed it over — asserted end to end, because
   "the protocol does not change" is the claim and a client that was never told is the
   only thing that can prove it.
2. **The grant is enforced at `ops-I6`'s resolution point**, with a test per outcome: a
   database inside the grant opens, one outside it is refused **by name**, and a mode
   escalation (`read-write` against a read-only grant) is refused by name.
3. **The control socket refuses a peer that is not the proxy** — `SO_PEERCRED` checked,
   provoked by a test that connects as another uid.
4. **The cap holds on the handover path**, and a refused handover **closes the received
   descriptor** — asserted by descriptor count across a flood, not by reading the code.
   This is the one that would otherwise be found in production.
5. **A proxy restart does not drop live sessions**, which is the property that is not just
   latency: kill the proxy mid-query and the rows still arrive.
6. **The latency claim is measured, not asserted.** `examples/loadgen` through a relaying
   proxy against the same proxy handing over, same corpus and mix, reported as a
   distribution rather than a mean — the win should be a constant per frame, so it should
   show up hardest on the small-row mixes where §11's code-search workload lives.
7. **The book gains the deployment**, `operations.mdx`, including the TLS-termination
   exclusion above, which is the thing that will otherwise be discovered the expensive way.

## Traps

- **Do not let the grant become policy.** The moment Fjord decides *who* may do something
  rather than enforcing what it was handed, `ops-I10` is gone and there is an auth system
  in the database.
- **Do not hand over a descriptor the proxy has written to.** Shape A's whole safety is
  that the proxy has neither read nor written a Fjord byte.
- **Do not assume `sendmsg` sends the fd promptly under load.** In-flight descriptors are
  a bounded kernel resource; a proxy that queues thousands of handovers against a stalled
  server is holding both ends' descriptors. The control socket wants backpressure and a
  bound of its own.
- **`ops-I1` stays the store's.** The proxy authorises; it never opens a database, and
  nothing here gives it a reason to.

## Not in scope

- **Authentication itself.** What the proxy does before it decides is the proxy's, and
  deliberately outside this document and outside Fjord.
- **Handing a connection *back*.** A session ends at the server; there is no path from
  Fjord to the proxy and no reason to build one.
- **Multi-server routing.** One control socket, one server. A proxy in front of several
  is the same mechanism repeated and needs nothing new here.
