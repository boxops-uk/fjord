using System.Collections.Concurrent;
using System.Diagnostics;

namespace Boxops.Fjord.Client;

/// <summary>
/// Where facts go: batched by predicate, encoded as blocks, and written.
/// </summary>
/// <remarks>
/// <para>
/// <b>A block carries one predicate</b>, so the batching is per predicate rather than
/// one queue in emission order — an indexer produces a declaration, its search entry
/// and a dozen references interleaved, and a single queue would mean a block per fact.
/// </para>
/// <para>
/// <b>A full block is handed to a writer thread, not written where it filled.</b>
/// <see cref="Add"/> holds that predicate's own lock across the flush, so a write done
/// inline would hold it across a network round trip and a server intern — measured at
/// 368 ms per block — and every other producer of that predicate would queue behind it.
/// A flush detaches the full list and hands it over; nothing but that happens under the
/// lock.
/// </para>
/// <para>
/// <b>Several writers, each with its own target.</b> The server excludes writers per
/// <em>key</em> rather than per database, so a database takes as many as there are
/// streams. One connection cannot carry them: <see cref="FjordConnection"/> issues
/// streams sequentially and shares one socket, so concurrency here means one connection
/// per writer thread and nothing shared between them but the queue. What a writer holds
/// is an <see cref="IBlockTarget"/> rather than a connection, because a block is the same
/// thing whichever database it is going into — see that interface for why.
/// </para>
/// <para>
/// <b>The queue is bounded, and that bound is the backpressure.</b> An unbounded queue
/// would convert a stall into memory rather than into throughput. The bound scales with
/// the writer count, because the thing it has to keep fed is now several drains rather
/// than one. When the walk outruns them, producers block in <see cref="Queueing"/> —
/// which is what says whether the write path is still the ceiling.
/// </para>
/// <para>
/// <b>Nothing here holds an id.</b> Every reference is the target fact nested inline,
/// which is why this sink can flush a partial index at any moment and in any order: no
/// fact it has queued depends on one it has already sent.
/// </para>
/// </remarks>
public sealed class FactSink : IDisposable
{
    /// <summary>Blocks that may wait <em>per writer</em> before producers block.</summary>
    /// <remarks>
    /// Small on purpose, and multiplied by the writer count rather than fixed. One block
    /// is up to <c>--batch</c> facts, so a deep queue costs real memory and buys nothing
    /// once the writers are keeping up — it only hides the stall from
    /// <see cref="Queueing"/>, which is the number we want to see.
    /// </remarks>
    private const int QueueDepthPerWriter = 4;

    private readonly FjordSchema _schema;
    private readonly int _batch;

    /// <summary>Whether to encode a block even when no target wants the bytes.</summary>
    /// <remarks>
    /// A connected run hands its facts to the client, which encodes them once on the way
    /// out, so encoding here as well would be measuring this sink twice. A run with no
    /// target at all is measuring exactly that encoding, and a run writing a file needs
    /// the bytes anyway.
    /// </remarks>
    private readonly bool _encode;

    private readonly FileStream? _emit;
    private readonly List<FjordFact>[] _pending;

    /// <summary>One lock per predicate, and it outlives the batch it guards.</summary>
    /// <remarks>
    /// <b>Not the list itself.</b> A flush hands the batch to a writer and puts a fresh
    /// list in its place, so a thread that had locked the old one would be excluding
    /// nobody — and could append to a list a writer was already encoding. The lock has to
    /// be the thing that does not move.
    /// </remarks>
    private readonly object[] _locks;
    private readonly BlockingCollection<(uint Predicate, List<FjordFact> Facts)> _queue;
    private readonly Thread[] _writers;

    /// <summary>Set if a writer thread died; rethrown at the next flush and from <see cref="Drain"/>.</summary>
    /// <remarks>
    /// <para>
    /// A writer that fails silently is a partial index that looks complete, so the failure
    /// is latched and the queue is closed — which unblocks any producer parked on a full
    /// queue and makes the next <see cref="Enqueue"/> throw rather than hang.
    /// </para>
    /// <para>
    /// <b>Throwing there rather than only at the end is the whole point, and it was
    /// learned the expensive way.</b> A <c>dotnet/runtime</c> re-index lost its server 23
    /// minutes in; the queue closed, every subsequent flush was swallowed as
    /// "the writer has stopped consuming", and the walk went on producing progress lines
    /// for another 39 minutes while writing nothing at all. The counters kept climbing
    /// because they count what was <em>queued</em>. Only <see cref="Drain"/> at the very
    /// end said so.
    /// </para>
    /// </remarks>
    private Exception? _failure;

    /// <summary>The predicate the first failing block belonged to, or -1.</summary>
    private long _failedOn = -1;

    private long _queueingTicks;

    /// <summary>Time producers spent waiting for a predicate's batch, summed over threads.</summary>
    /// <remarks>
    /// The successor to the walk's single gate, and the reason it can be retired with a
    /// number rather than a hope: if this is a real share of the run, striping by
    /// predicate was the wrong axis.
    /// </remarks>
    private long _contendedTicks;

    private long _contentions;

    // Written by every writer thread, so every one of them is interlocked and the
    // properties below are projections rather than fields.
    private long _blocks;
    private long _bytes;
    private long _created;
    private long _deduped;
    private long _writingTicks;

    private bool _drained;

    /// <summary>A sink writing through <paramref name="targets"/>, one writer thread each.</summary>
    /// <remarks>
    /// <para>
    /// An empty list is a run that writes to nothing, which still wants one thread so the
    /// encoding it measures happens off the producer's own.
    /// </para>
    /// <para>
    /// <b>A schema and not a producer.</b> Everything here — the batching, the bounded
    /// queue, the writer threads, the latched failure — is generic write support: it needs
    /// to know how many predicates there are and how to encode one, and nothing else. A
    /// sink that reached into a particular producer's constants would be usable by that
    /// producer alone, which is the opposite of a seam.
    /// </para>
    /// </remarks>
    /// <param name="schema">What the facts are shaped like, and how many predicates there are.</param>
    /// <param name="targets">Where blocks go; empty writes to nothing.</param>
    /// <param name="batch">Facts per block before it is flushed.</param>
    /// <param name="emit">A file to write every block to as well, or nothing.</param>
    public FactSink(
        FjordSchema schema,
        IReadOnlyList<IBlockTarget> targets,
        int batch = 4096,
        string? emit = null)
    {
        _schema = schema;
        _batch = Math.Max(1, batch);
        _encode = emit is not null || targets.Count == 0;
        _emit = emit is null ? null : File.Create(emit);

        var count = schema.Predicates.Count;
        _pending = new List<FjordFact>[count];
        _locks = new object[count];

        for (var predicate = 0; predicate < count; predicate++)
        {
            _pending[predicate] = new List<FjordFact>(_batch);
            _locks[predicate] = new object();
        }

        Facts = new long[count];

        var writers = Math.Max(1, targets.Count);
        _queue = new BlockingCollection<(uint, List<FjordFact>)>(QueueDepthPerWriter * writers);

        _writers = new Thread[writers];
        for (var n = 0; n < writers; n++)
        {
            // Each thread owns exactly one target, so nothing about a socket, a stream
            // number or an output file is shared and none of it needs a lock.
            var target = n < targets.Count ? targets[n] : null;
            _writers[n] = new Thread(() => WriteLoop(target))
            {
                IsBackground = false,
                Name = $"fjord-writer-{n}",
            };
            _writers[n].Start();
        }
    }

    /// <summary>How many writer threads are draining the queue.</summary>
    public int Writers => _writers.Length;

    /// <summary>Facts queued per predicate, whether or not they turned out to be new.</summary>
    public long[] Facts { get; }

    public long Blocks => Interlocked.Read(ref _blocks);

    /// <summary>Bytes written: what a target reports, plus what this sink encoded for itself.</summary>
    /// <remarks>
    /// Two measurements share one counter because only one of them is ever non-zero. A
    /// connected Fjord run encodes inside the client and is told no size, so this is
    /// the block bytes the sink encoded for <c>--emit</c> or <c>--dry-run</c>; a run
    /// writing Glean batch files takes neither path, and this is the JSON it wrote.
    /// </remarks>
    public long Bytes => Interlocked.Read(ref _bytes);

    public ulong Created => (ulong)Interlocked.Read(ref _created);

    public ulong Deduped => (ulong)Interlocked.Read(ref _deduped);

    /// <summary>Time <em>summed over the writers</em> inside <see cref="IBlockTarget.Write"/>.</summary>
    /// <remarks>
    /// Not time the walk pays, and — with more than one writer — not wall clock either:
    /// it is a sum across threads that overlap each other as well as the walk, so it can
    /// exceed the run's elapsed time and says nothing on its own. Divide by
    /// <see cref="Writers"/> for a rough per-writer figure, and read
    /// <see cref="Queueing"/> for the question that actually matters — whether the walk
    /// ever had to wait for them.
    /// </remarks>
    public TimeSpan Writing => Stopwatch.GetElapsedTime(0, Interlocked.Read(ref _writingTicks));

    /// <summary>Time producers spent blocked on a full queue, i.e. waiting for the writer.</summary>
    public TimeSpan Queueing => Stopwatch.GetElapsedTime(0, Interlocked.Read(ref _queueingTicks));

    /// <summary>Time producers spent waiting for a predicate's batch.</summary>
    public TimeSpan Contended => Stopwatch.GetElapsedTime(0, Interlocked.Read(ref _contendedTicks));

    /// <summary>How many <see cref="Add"/> calls found a batch already held.</summary>
    public long Contentions => Interlocked.Read(ref _contentions);

    public long Total
    {
        get
        {
            var total = 0L;
            foreach (var count in Facts)
            {
                total += count;
            }

            return total;
        }
    }

    /// <summary>
    /// Queue one fact, from any thread.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One lock per predicate, and the batch is its own lock.</b> The walk's threads
    /// almost never want the same predicate at the same instant — a declaration is
    /// touching <c>csharp.Method</c> while a reference is touching
    /// <c>codemarkup.FileXRef</c> — so striping by predicate turns what was one gate
    /// around the whole of fact production into sixty-five that are each held for a
    /// list append.
    /// </para>
    /// <para>
    /// <b>Not per-thread buffers</b>, which would look cheaper and are not: every thread
    /// would build its own <c>src.File</c> and <c>csharp.Name</c> facts, multiplying the
    /// duplicates by the thread count and destroying the dedup ratio that is the whole
    /// measurement this producer exists to take.
    /// </para>
    /// </remarks>
    public void Add(uint predicate, FjordFact fact)
    {
        var gate = _locks[predicate];
        var contended = false;

        if (!Monitor.TryEnter(gate))
        {
            // Measured rather than assumed: the gate this replaced reported what it cost,
            // and a replacement that reports nothing cannot be compared with it.
            var before = Stopwatch.GetTimestamp();
            Monitor.Enter(gate);
            Interlocked.Add(ref _contendedTicks, Stopwatch.GetTimestamp() - before);
            contended = true;
        }

        List<FjordFact>? full;

        try
        {
            var batch = _pending[predicate];
            batch.Add(fact);
            Interlocked.Increment(ref Facts[predicate]);

            full = batch.Count >= _batch ? Detach(predicate) : null;
        }
        finally
        {
            Monitor.Exit(gate);
        }

        if (contended)
        {
            Interlocked.Increment(ref _contentions);
        }

        // **Handed over with the lock released**, which is the whole of why the two steps
        // are separate. The queue is bounded, so this blocks whenever the writers are
        // behind — and blocking here while holding the predicate's lock made one walker's
        // wait every walker's: each of the others producing that predicate queued up
        // behind a thread that was itself waiting for a writer. Measured over 5.8M facts,
        // `queueing` was 12.8% of a walker's time and `contended` 48%, and the second is
        // mostly the first seen from the other side.
        //
        // **Two threads may hand over the same predicate's batches out of order**, and
        // that is sound: `writer_count_and_write_order_do_not_change_the_database` is
        // asserted, and `ops-I4` is order-independent by construction. The one consumer
        // that needs a deterministic run of blocks is `--emit`, which forces one walker
        // and one writer and so never reaches this.
        if (full is not null)
        {
            Enqueue(predicate, full);
        }
    }

    public void FlushAll()
    {
        for (var predicate = 0u; predicate < _pending.Length; predicate++)
        {
            List<FjordFact>? full;

            lock (_locks[predicate])
            {
                full = Detach(predicate);
            }

            if (full is not null)
            {
                Enqueue(predicate, full);
            }
        }
    }

    /// <summary>
    /// Take the pending block, leaving a fresh one behind. Hands nothing to a writer.
    /// </summary>
    /// <remarks>
    /// <b>Called with this predicate's lock held</b>, and it is the only part that needs
    /// to be: a swap racing an append is a fact written into a batch a writer has already
    /// taken. A fresh list rather than <c>Clear()</c>, because the writer owns the one
    /// handed over until it has encoded and sent it.
    /// </remarks>
    private List<FjordFact>? Detach(uint predicate)
    {
        var batch = _pending[predicate];

        if (batch.Count == 0)
        {
            return null;
        }

        _pending[predicate] = new List<FjordFact>(_batch);

        return batch;
    }

    /// <summary>Hand a detached block to the writers, waiting if they are behind.</summary>
    /// <remarks>
    /// <b>Called with no lock held.</b> The bound on the queue is the backpressure, so
    /// this is where a walk waits when the writers cannot keep up — and it must be the
    /// only thing waiting, not a lock every producer of that predicate needs.
    /// </remarks>
    private void Enqueue(uint predicate, List<FjordFact> batch)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            _queue.Add((predicate, batch));
        }
        catch (InvalidOperationException)
        {
            // The writer latched a failure and closed the queue. Stop the run here: a walk
            // that keeps filling blocks nobody is draining is an hour of work that reads as
            // progress and writes nothing. See `_failure`.
            throw Failed();
        }

        Interlocked.Add(ref _queueingTicks, Stopwatch.GetTimestamp() - started);
    }

    /// <summary>One writer: encodes, emits and writes to its own target.</summary>
    /// <remarks>
    /// Every counter it touches is interlocked, because several of these run at once and
    /// the totals are read from the walk's thread at the end.
    /// </remarks>
    private void WriteLoop(IBlockTarget? target)
    {
        // Which block was in the writer's hands when it failed. A database rejects a
        // *fact*, and it can name the predicate and nothing else — it has never heard of
        // the declaration this producer was describing or the file it came out of. Saying
        // which predicate is the last thing this side can add before the answer is "one of
        // eighteen million facts was refused".
        var writing = -1L;

        try
        {
            foreach (var (predicate, facts) in _queue.GetConsumingEnumerable())
            {
                Volatile.Write(ref writing, predicate);

                // Encoded here only when the bytes are wanted for themselves: a connected
                // run hands the facts to the client, which encodes them once on the way out.
                if (_encode)
                {
                    var block = Block.Encode(_schema, predicate, facts);
                    Interlocked.Add(ref _bytes, block.Length);

                    // Only ever one writer when emitting — see `Program.Connect` — so the
                    // file is a deterministic run of blocks rather than an interleaving.
                    _emit?.Write(block);
                }

                if (target is not null)
                {
                    var started = Stopwatch.GetTimestamp();
                    var written = target.Write(predicate, facts);
                    Interlocked.Add(ref _writingTicks, Stopwatch.GetTimestamp() - started);

                    Interlocked.Add(ref _created, (long)written.Created);
                    Interlocked.Add(ref _deduped, (long)written.Deduped);
                    Interlocked.Add(ref _bytes, written.Bytes);
                }

                Interlocked.Increment(ref _blocks);
            }
        }
        catch (Exception error)
        {
            // First failure wins; the rest are consequences of the queue closing.
            if (Interlocked.CompareExchange(ref _failure, error, null) is null)
            {
                Volatile.Write(ref _failedOn, Volatile.Read(ref writing));
            }

            _queue.CompleteAdding();

            // Drain, so a producer parked on a full queue is released rather than left
            // waiting on writers that have stopped consuming.
            foreach (var _ in _queue.GetConsumingEnumerable())
            {
            }
        }
    }

    /// <summary>Queue everything left, then wait for every writer to finish.</summary>
    /// <remarks>
    /// <b>Call this before reading any count.</b> <see cref="FlushAll"/> only hands the
    /// remaining blocks over; until they have all drained, <see cref="Blocks"/>,
    /// <see cref="Created"/>, <see cref="Deduped"/> and <see cref="Writing"/> are still
    /// moving. A report taken between the two would be short, and the elapsed time it
    /// divided by would stop before the last block was written.
    /// </remarks>
    public void Drain()
    {
        if (_drained)
        {
            return;
        }

        _drained = true;
        FlushAll();
        _queue.CompleteAdding();
        foreach (var writer in _writers)
        {
            writer.Join();
        }

        if (_failure is not null)
        {
            throw Failed();
        }
    }

    /// <summary>The latched writer failure, as the exception a caller should see.</summary>
    /// <remarks>
    /// A queue closes only because a writer latched something first, so this reads the
    /// latch rather than describing the queue — the cause a person needs is the socket
    /// that closed or the disk that filled, not the collection that was completed.
    /// </remarks>
    private InvalidOperationException Failed()
    {
        var predicate = Volatile.Read(ref _failedOn);
        var where = predicate >= 0 && predicate < _schema.Predicates.Count
            ? $" while writing {_schema.NameOf((uint)predicate)}"
            : string.Empty;

        return new InvalidOperationException(
            $"the fact writer failed{where}", Volatile.Read(ref _failure));
    }

    public void Dispose()
    {
        Drain();
        _emit?.Dispose();
        _queue.Dispose();
    }
}
