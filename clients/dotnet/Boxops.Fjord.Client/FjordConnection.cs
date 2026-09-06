using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;

namespace Boxops.Fjord.Client;

/// <summary>Which way a session may go, declared once at startup.</summary>
public enum SessionMode : byte
{
    ReadOnly = 0,
    ReadWrite = 1,
}

/// <summary>What the server said when the session opened.</summary>
public sealed record ServerHello(uint Version, ulong SchemaFingerprint, ulong Predicates);

/// <summary>What a write stream did.</summary>
/// <remarks>
/// <paramref name="Created"/> counts <b>every</b> fact written, nested targets
/// included, and <paramref name="Deduped"/> those already there. A producer sending a
/// thousand declarations that all name one file sees a thousand and one created and
/// nine hundred and ninety-nine deduped — which is how it can tell interning is
/// working without querying anything.
/// </remarks>
public sealed record WriteSummary(ulong Created, ulong Deduped)
{
    public ulong Seen => Created + Deduped;
}

/// <summary>A query's rows, and the shape they came in.</summary>
public sealed record QueryResult(FjordType Shape, IReadOnlyList<FjordValue> Rows);

/// <summary>One page of a result, and where to carry on from.</summary>
/// <remarks>
/// <paramref name="Resume"/> is <see langword="null"/> when this page reached the end of
/// the result. It is the terminator: an empty token is not one, and a caller that pages
/// until the rows run out rather than until the token does will ask one more time and be
/// told nothing.
/// </remarks>
public sealed record QueryPage(FjordType Shape, IReadOnlyList<FjordValue> Rows, byte[]? Resume);

/// <summary>
/// A connection to a Fjord server.
/// </summary>
/// <remarks>
/// <para>
/// One connection carries several streams: a write is a stream and a query is a
/// stream, each identified by a number the caller chooses. This client issues them
/// sequentially — it sends a stream's frames and reads its replies before starting the
/// next — which is all the current server does anyway. The stream ids are real
/// nonetheless, and the server tags every reply with the stream it belongs to.
/// </para>
/// <para>
/// <b>The schema is the client's.</b> Nothing in the protocol describes it: the value
/// codec sends no names and no types because both ends already have them. The
/// handshake asserts they agree, by fingerprint, before a byte of data flows.
/// </para>
/// </remarks>
public sealed class FjordConnection : IDisposable
{
    /// <summary>The protocol version this client speaks.</summary>
    /// <remarks>
    /// 2 is Phase 8's: a startup frame's schema fingerprint carries chapter 6's schema
    /// identity, where 1 carried a provisional hash each end computed for itself. Every
    /// number changed, so a client pinned to the old one is told it speaks a different
    /// protocol rather than left to fail a comparison it cannot interpret.
    /// </remarks>
    public const uint ProtocolVersion = 4;

    private readonly Socket _socket;
    private readonly NetworkStream _stream;
    private readonly FjordSchema _schema;
    private uint _nextStream = 1;
    private bool _streaming;

    /// <summary>Rows per round trip when a caller does not choose.</summary>
    /// <remarks>
    /// Big enough that a page is not a round trip per handful of rows, small enough that
    /// abandoning one after the first row cannot cost much: the drain that hands the
    /// connection back is bounded by this and by nothing else.
    /// </remarks>
    public const int DefaultPageSize = 1024;

    private FjordConnection(Socket socket, FjordSchema schema, ServerHello hello)
    {
        _socket = socket;
        _stream = new NetworkStream(socket, ownsSocket: false);
        _schema = schema;
        Hello = hello;
    }

    public ServerHello Hello { get; }

    /// <summary>
    /// Connect over a Unix socket and complete the handshake.
    /// </summary>
    /// <param name="socketPath">Where the server is listening.</param>
    /// <param name="database">The database to open.</param>
    /// <param name="schema">The schema this client writes against.</param>
    /// <param name="mode">Read-only or read-write, resolved once here.</param>
    /// <param name="assertSchema">
    /// Whether to send the schema fingerprint as a claim. <c>true</c> is the right
    /// default for a producer: a disagreement is then refused at the handshake instead
    /// of by writing facts nobody can read back. <c>false</c> sends <c>0</c>, which
    /// means "do not check" and is what a reader wants.
    /// </param>
    public static FjordConnection Connect(
        string socketPath,
        string database,
        FjordSchema schema,
        SessionMode mode = SessionMode.ReadWrite,
        bool assertSchema = true)
    {
        return Connect(
            FjordAddress.ForSocket(socketPath, database),
            database,
            schema,
            mode,
            assertSchema);
    }

    /// <summary>
    /// Connect wherever <paramref name="address"/> says, and complete the handshake.
    /// </summary>
    /// <remarks>
    /// The database comes from the address, so the two halves of "where and what" cannot
    /// be passed separately and disagree. An address that names no target is a
    /// programming error here rather than a default: this client has no configuration
    /// layer to fall back to, so the caller resolves it with
    /// <see cref="FjordAddress.OrSocket"/> before arriving.
    /// </remarks>
    public static FjordConnection Connect(
        FjordAddress address,
        FjordSchema schema,
        SessionMode mode = SessionMode.ReadWrite,
        bool assertSchema = true)
        => Connect(address, address.Database, schema, mode, assertSchema);

    /// <summary>
    /// Connect where <paramref name="address"/> points, bound to no database.
    /// </summary>
    /// <remarks>
    /// <b>For the lifecycle requests, which name their database in the frame.</b> Creating
    /// one cannot bind to it first — it is not there — and binding to some other database
    /// to ask would make the caller's choice of a bystander part of whether the create
    /// works. A session with no database is what <c>CONTROL</c> is shaped for, and it
    /// asserts no schema because there is none to assert.
    /// </remarks>
    public static FjordConnection ConnectUnbound(
        FjordAddress address,
        FjordSchema schema,
        SessionMode mode = SessionMode.ReadWrite)
        => Connect(address, string.Empty, schema, mode, assertSchema: false);

    private static FjordConnection Connect(
        FjordAddress address,
        string database,
        FjordSchema schema,
        SessionMode mode,
        bool assertSchema)
    {
        Socket socket;

        if (address.SocketPath is { } path)
        {
            socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            socket.Connect(new UnixDomainSocketEndPoint(path));
        }
        else if (address.Host is { } host)
        {
            socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            socket.Connect(host, address.Port);

            // Small frames, answered one at a time: Nagle would hold a handshake back
            // waiting for company that is not coming. The Rust client says the same.
            socket.NoDelay = true;
        }
        else
        {
            throw new ArgumentException(
                $"`{address}` names no server to connect to", nameof(address));
        }

        var stream = new NetworkStream(socket, ownsSocket: false);

        var startup = new ByteBuffer();
        Varint.Write(startup, ProtocolVersion);
        WriteString(startup, database);
        startup.WriteByte((byte)mode);
        Varint.Write(startup, assertSchema ? schema.Fingerprint : 0);

        // **No per-predicate claims**, which is what a client carrying a constant has
        // to send. The field is how a producer writing *part* of a database's schema
        // says which part — it costs a fingerprint per predicate, and computing those
        // is exactly what a client does not do (see FjordSchema). Zero here means
        // "judge me by the number above".
        Varint.Write(startup, 0);

        FrameIo.Write(stream, FrameKind.Startup, 0, startup.Span);

        var reply = FrameIo.Read(stream);
        ThrowIfError(reply);

        if (reply.Kind != FrameKind.Ready)
        {
            throw new FjordProtocolException(
                $"expected a ready frame, got `{(char)reply.Kind}`");
        }

        var at = 0;
        var hello = new ServerHello(
            (uint)Varint.Read(reply.Payload, ref at),
            Varint.Read(reply.Payload, ref at),
            Varint.Read(reply.Payload, ref at));

        if (hello.Version != ProtocolVersion)
        {
            throw new FjordProtocolException(
                $"this client speaks protocol {ProtocolVersion}, the server speaks {hello.Version}");
        }

        return new FjordConnection(socket, schema, hello);
    }

    /// <summary>
    /// Write facts, all of one predicate, as one block on one write stream.
    /// </summary>
    /// <remarks>
    /// References inside the facts may be nested — the whole target fact rather than an
    /// id — and the server interns them. That is what lets a producer keep no book of
    /// what it has already sent.
    /// </remarks>
    public WriteSummary Write(uint predicate, IReadOnlyList<FjordFact> facts) =>
        Write([(predicate, facts)]);

    /// <summary>Write several blocks on one write stream.</summary>
    public WriteSummary Write(IReadOnlyList<(uint Predicate, IReadOnlyList<FjordFact> Facts)> blocks)
    {
        var stream = _nextStream++;

        FrameIo.Write(_stream, FrameKind.OpenWrite, stream, []);
        var opened = FrameIo.Read(_stream);
        ThrowIfError(opened);

        if (opened.Kind != FrameKind.CopyInResponse)
        {
            throw new FjordProtocolException(
                $"expected a copy-in response, got `{(char)opened.Kind}`");
        }

        foreach (var (predicate, facts) in blocks)
        {
            var block = Block.Encode(_schema, predicate, facts);
            FrameIo.Write(_stream, FrameKind.CopyData, stream, block);
        }

        FrameIo.Write(_stream, FrameKind.CopyDone, stream, []);

        var complete = FrameIo.Read(_stream);
        ThrowIfError(complete);

        if (complete.Kind != FrameKind.Complete)
        {
            throw new FjordProtocolException(
                $"expected a complete frame, got `{(char)complete.Kind}`");
        }

        var at = 0;
        return new WriteSummary(
            Varint.Read(complete.Payload, ref at),
            Varint.Read(complete.Payload, ref at));
    }

    /// <summary>Run a sigla query and collect its rows.</summary>
    /// <remarks>
    /// The server sends a <b>row descriptor</b> first, because a query's shape comes
    /// from its head rather than from any predicate — <c>{a = X, b = Y}</c> is a record
    /// no predicate declares. Rows then follow positionally against it, decoded by the
    /// same codec that encodes facts.
    /// </remarks>
    public QueryResult Query(string sigla)
    {
        var stream = _nextStream++;
        FrameIo.Write(_stream, FrameKind.Query, stream, Encoding.UTF8.GetBytes(sigla));

        var described = FrameIo.Read(_stream);
        ThrowIfError(described);

        if (described.Kind != FrameKind.RowDescription)
        {
            throw new FjordProtocolException(
                $"expected a row description, got `{(char)described.Kind}`");
        }

        var at = 0;
        var shape = RowDescriptor.Read(described.Payload, ref at);
        var rows = new List<FjordValue>();

        while (true)
        {
            var frame = FrameIo.Read(_stream);
            ThrowIfError(frame);

            if (frame.Kind == FrameKind.Complete)
            {
                return new QueryResult(shape, rows);
            }

            if (frame.Kind != FrameKind.DataRow)
            {
                throw new FjordProtocolException(
                    $"expected a data row, got `{(char)frame.Kind}`");
            }

            var rowAt = 0;
            rows.Add(ValueCodec.ReadValue(frame.Payload, _schema, shape, ref rowAt));
        }
    }

    /// <summary>
    /// This database's schema, as sigla source.
    /// </summary>
    /// <remarks>
    /// <b>A real question rather than a formality.</b> A database carries the schema it was
    /// created against, so a store root holds artifacts of different shapes and a client's
    /// built-in idea of one is nobody's answer but its own. It is also the only way this
    /// client can obtain schema *source*: it states its schema as predicates and has no
    /// sigla parser, so it cannot compose one from files the way the CLI does.
    /// </remarks>
    public string SchemaSource()
    {
        var stream = _nextStream++;
        FrameIo.Write(_stream, FrameKind.Schema, stream, []);

        var reply = FrameIo.Read(_stream);
        ThrowIfError(reply);

        if (reply.Kind != FrameKind.SchemaReply)
        {
            throw new FjordProtocolException(
                $"expected a schema, got `{(char)reply.Kind}`");
        }

        return Encoding.UTF8.GetString(reply.Payload);
    }

    /// <summary>
    /// Create a database from sigla <paramref name="schemaSource"/>, and answer the instance
    /// it was published as.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The source must be import-free.</b> The server lowers exactly what it is sent and
    /// cannot resolve an <c>import</c> against paths on this machine — the CLI composes
    /// first and sends the printed union. <see cref="SchemaSource"/> answers in that form,
    /// which is what makes "create a sibling like this one" expressible here.
    /// </para>
    /// <para>
    /// <b>Requires a read-write session</b>, the same rule a write stream follows: creating
    /// a database is a change to the store root, not a question about one.
    /// </para>
    /// </remarks>
    public string CreateDatabase(string name, string schemaSource)
    {
        var payload = new ByteBuffer();
        payload.WriteByte(ControlOp.Create);
        WriteString(payload, name);

        // `allowZeroFacts` is `finish`'s question, not `create`'s; the field is in every
        // control frame because one shape carries all three ops.
        payload.WriteByte(0);
        WriteString(payload, schemaSource);

        var stream = _nextStream++;
        FrameIo.Write(_stream, FrameKind.Control, stream, payload.Span);

        var reply = FrameIo.Read(_stream);
        ThrowIfError(reply);

        if (reply.Kind != FrameKind.ControlReply)
        {
            throw new FjordProtocolException(
                $"expected a control reply, got `{(char)reply.Kind}`");
        }

        if (reply.Payload.Length < 1 || reply.Payload[0] != ControlOp.Create)
        {
            throw new FjordProtocolException(
                "a control reply that is not the create this asked for");
        }

        var at = 1;
        var length = Varint.Read(reply.Payload, ref at);

        return Encoding.UTF8.GetString(reply.Payload, at, (int)length);
    }

    /// <summary>How many rows a query has, without encoding one of them.</summary>
    /// <remarks>
    /// The plan and the executor are the same as a query's; what differs is that the
    /// accumulator keeps a number instead of a row. `bench/FINDINGS.md` §9 puts row
    /// encoding at 1.5× the executor and the wire above it at another 3.6×, all of which
    /// a caller that only wants the total throws away — so asking this rather than
    /// counting <see cref="Query"/>'s rows is the difference between a number and a
    /// result set held in memory.
    /// </remarks>
    public long CountRows(string sigla)
    {
        var stream = _nextStream++;
        FrameIo.Write(_stream, FrameKind.QueryCount, stream, Encoding.UTF8.GetBytes(sigla));

        var counted = FrameIo.Read(_stream);
        ThrowIfError(counted);

        if (counted.Kind != FrameKind.Count)
        {
            throw new FjordProtocolException(
                $"expected a count, got `{(char)counted.Kind}`");
        }

        if (counted.Payload.Length < 8)
        {
            throw new FjordProtocolException(
                $"a count frame of {counted.Payload.Length} bytes, where 8 are needed");
        }

        var total = BinaryPrimitives.ReadUInt64LittleEndian(counted.Payload);

        var complete = FrameIo.Read(_stream);
        ThrowIfError(complete);

        if (complete.Kind != FrameKind.Complete)
        {
            throw new FjordProtocolException(
                $"expected the stream to complete, got `{(char)complete.Kind}`");
        }

        return (long)total;
    }

    /// <summary>One page of a query's rows, and the token to carry on from.</summary>
    /// <remarks>
    /// <b>Paging is stateless, which is the whole point of the token.</b> The server keeps
    /// nothing between pages: each request carries the query and where to resume, so a
    /// caller may take page two on a different connection, or an hour later, or never. A
    /// result that lived in the session would have to be held by whoever asked for it, and
    /// "everything after key K" is not expressible in the language.
    /// </remarks>
    /// <param name="sigla">The query.</param>
    /// <param name="limit">The most rows this page may carry; 0 asks for all of them.</param>
    /// <param name="cursor">A previous page's <see cref="QueryPage.Resume"/>, or null to start.</param>
    public QueryPage Page(string sigla, ulong limit, byte[]? cursor = null)
    {
        var stream = _nextStream++;
        FrameIo.Write(_stream, FrameKind.QueryPage, stream, EncodePage(limit, cursor, sigla));

        var shape = ReadRowDescription();
        var rows = new List<FjordValue>();
        byte[]? resume = null;

        while (true)
        {
            var frame = FrameIo.Read(_stream);
            ThrowIfError(frame);

            if (frame.Kind == FrameKind.Complete)
            {
                return new QueryPage(shape, rows, resume);
            }

            if (frame.Kind == FrameKind.Resume)
            {
                resume = frame.Payload;
                continue;
            }

            if (frame.Kind != FrameKind.DataRow)
            {
                throw new FjordProtocolException(
                    $"expected a data row, got `{(char)frame.Kind}`");
            }

            var at = 0;
            rows.Add(ValueCodec.ReadValue(frame.Payload, _schema, shape, ref at));
        }
    }

    /// <summary>
    /// A query's rows, pulled a page at a time and yielded one by one — so
    /// <c>Take(n)</c> costs one page rather than the whole result.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Abandoning this is safe, and that is what the cancel is for.</b> Stopping early
    /// leaves the current page's remaining rows unread on a socket that carries every
    /// stream, so the next query would read this one's tail. Disposing the enumerator —
    /// which <c>foreach</c>, <c>Take</c> and LINQ all do — sends a cancel on the open
    /// stream and then reads to its <c>Complete</c>, so the connection is handed back
    /// clean whether the caller finished or not. What that drain costs is bounded by
    /// <paramref name="pageSize"/> and never by the size of the result.
    /// </para>
    /// <para>
    /// <b>One at a time per connection.</b> This client reads frames in arrival order
    /// without demultiplexing on the stream id, so two open results would each decode the
    /// other's rows. A second enumeration while one is open is refused here rather than
    /// left to produce garbage.
    /// </para>
    /// </remarks>
    /// <param name="sigla">The query.</param>
    /// <param name="pageSize">Rows per round trip. Larger trades memory for round trips.</param>
    public IEnumerable<FjordValue> Rows(string sigla, int pageSize = DefaultPageSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);

        return Paging(sigla, pageSize);
    }

    private IEnumerable<FjordValue> Paging(string sigla, int pageSize)
    {
        if (_streaming)
        {
            throw new InvalidOperationException(
                "a streaming result is already open on this connection; finish or dispose "
                + "it before starting another, because rows are read in arrival order");
        }

        _streaming = true;

        // The stream a page is on while its rows are being handed out, and null between
        // pages. A caller that stops mid-page leaves this set, which is what the cleanup
        // below reads to know there is something to cancel.
        uint? open = null;

        try
        {
            byte[]? cursor = null;

            while (true)
            {
                var stream = _nextStream++;
                FrameIo.Write(
                    _stream, FrameKind.QueryPage, stream, EncodePage((ulong)pageSize, cursor, sigla));

                var shape = ReadRowDescription();
                byte[]? resume = null;
                open = stream;

                while (true)
                {
                    var frame = FrameIo.Read(_stream);
                    ThrowIfError(frame);

                    if (frame.Kind == FrameKind.Complete)
                    {
                        break;
                    }

                    if (frame.Kind == FrameKind.Resume)
                    {
                        resume = frame.Payload;
                        continue;
                    }

                    if (frame.Kind != FrameKind.DataRow)
                    {
                        throw new FjordProtocolException(
                            $"expected a data row, got `{(char)frame.Kind}`");
                    }

                    var at = 0;
                    yield return ValueCodec.ReadValue(frame.Payload, _schema, shape, ref at);
                }

                open = null;

                // The token is the terminator, not the row count: a full page that happened
                // to land on the last row still sends none, and a caller that paged until a
                // page came back short would ask once more and be told nothing.
                if (resume is null)
                {
                    yield break;
                }

                cursor = resume;
            }
        }
        finally
        {
            if (open is { } stream)
            {
                Abandon(stream);
            }

            _streaming = false;
        }
    }

    /// <summary>Stop an open stream and read to its end, so the socket is usable again.</summary>
    /// <remarks>
    /// <b>Failures here are swallowed on purpose.</b> This runs from a <c>finally</c>,
    /// including one unwinding an exception, and a throw would replace the fault the caller
    /// is being told about with one about the tidying. A connection this could not drain is
    /// broken anyway, and the next use of it says so.
    /// </remarks>
    private void Abandon(uint stream)
    {
        try
        {
            FrameIo.Write(_stream, FrameKind.Cancel, stream, []);

            while (FrameIo.Read(_stream).Kind != FrameKind.Complete)
            {
                // A cancel is an early end rather than a failure, so the stream still
                // completes — after however many rows were already on their way.
            }
        }
        catch (FjordProtocolException)
        {
        }
        catch (IOException)
        {
        }
        catch (SocketException)
        {
        }
    }

    private FjordType ReadRowDescription()
    {
        var described = FrameIo.Read(_stream);
        ThrowIfError(described);

        if (described.Kind != FrameKind.RowDescription)
        {
            throw new FjordProtocolException(
                $"expected a row description, got `{(char)described.Kind}`");
        }

        var at = 0;
        return RowDescriptor.Read(described.Payload, ref at);
    }

    /// <summary>
    /// A page request: <c>[limit u64][cursor length u32][cursor][query utf8]</c>, all
    /// little-endian, the query running to the end of the frame.
    /// </summary>
    private static byte[] EncodePage(ulong limit, byte[]? cursor, string sigla)
    {
        var query = Encoding.UTF8.GetBytes(sigla);
        var token = cursor ?? [];
        var payload = new byte[8 + 4 + token.Length + query.Length];

        BinaryPrimitives.WriteUInt64LittleEndian(payload, limit);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(8), (uint)token.Length);
        token.CopyTo(payload, 12);
        query.CopyTo(payload, 12 + token.Length);

        return payload;
    }

    private static void ThrowIfError(Frame frame)
    {
        if (frame.Kind != FrameKind.Error)
        {
            return;
        }

        if (frame.Payload.Length < 1)
        {
            throw new FjordProtocolException("an error frame with no code");
        }

        var code = (FjordErrorCode)frame.Payload[0];
        var at = 1;
        var length = Varint.Read(frame.Payload, ref at);
        var message = Encoding.UTF8.GetString(frame.Payload, at, (int)length);

        throw new FjordServerException(code, message);
    }

    private static void WriteString(IBufferSink sink, string text)
    {
        var utf8 = Encoding.UTF8.GetBytes(text);
        Varint.Write(sink, (ulong)utf8.Length);
        sink.Write(utf8);
    }

    public void Dispose()
    {
        _stream.Dispose();
        _socket.Dispose();
    }
}

/// <summary>
/// The row descriptor: the outbound direction's type source.
/// </summary>
/// <remarks>
/// This is the <b>one</b> place the format carries type tags, and it carries them once
/// per stream rather than once per field per row — which is exactly the trade that
/// makes tagging affordable here and not in a fact.
/// </remarks>
public static class RowDescriptor
{
    public static FjordType Read(ReadOnlySpan<byte> bytes, ref int at)
    {
        var tag = Varint.Read(bytes, ref at);

        switch (tag)
        {
            case 0:
                return FjordType.Integer;

            case 1:
                return FjordType.String;

            // Appended after the union's tag 4, on the same argument: a client built
            // before this type meets tag 5, has no case for it, and refuses the stream
            // rather than reading the field as a string.
            case 5:
                return FjordType.Blob;

            case 2:
                return FjordType.Reference((uint)Varint.Read(bytes, ref at));

            case 3:
            {
                var count = Varint.Read(bytes, ref at);
                if (count > (ulong)bytes.Length)
                {
                    throw new FjordProtocolException("a descriptor declares more fields than could fit");
                }

                var fields = new List<(string, FjordType)>((int)count);

                for (ulong index = 0; index < count; index++)
                {
                    var length = Varint.Read(bytes, ref at);
                    if (length > (ulong)(bytes.Length - at))
                    {
                        throw new FjordProtocolException("a field name runs past the descriptor");
                    }

                    var name = Encoding.UTF8.GetString(bytes.Slice(at, (int)length));
                    at += (int)length;

                    fields.Add((name, Read(bytes, ref at)));
                }

                return new FjordType.Record(fields);
            }

            // A union: each alternative's **name and tag**, since a row carries only the
            // tag. Tag 4, appended after the record's — which is what lets an older
            // client meet it and say so rather than mis-read what follows.
            case 4:
            {
                var count = Varint.Read(bytes, ref at);
                if (count > (ulong)bytes.Length)
                {
                    throw new FjordProtocolException(
                        "a descriptor declares more alternatives than could fit");
                }

                var alternatives = new List<(string, uint, FjordType)>((int)count);

                for (ulong index = 0; index < count; index++)
                {
                    var length = Varint.Read(bytes, ref at);
                    if (length > (ulong)(bytes.Length - at))
                    {
                        throw new FjordProtocolException(
                            "an alternative name runs past the descriptor");
                    }

                    var name = Encoding.UTF8.GetString(bytes.Slice(at, (int)length));
                    at += (int)length;

                    var disc = Varint.Read(bytes, ref at);
                    alternatives.Add((name, (uint)disc, Read(bytes, ref at)));
                }

                return new FjordType.Union(alternatives);
            }

            default:
                throw new FjordProtocolException($"unknown descriptor tag {tag}");
        }
    }
}
