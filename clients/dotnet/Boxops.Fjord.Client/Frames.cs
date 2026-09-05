using System.Buffers.Binary;
using System.Net.Sockets;

namespace Boxops.Fjord.Client;

/// <summary>What a frame carries. A byte, not a closed enum — see <see cref="FrameIo"/>.</summary>
public static class FrameKind
{
    public const byte Startup = (byte)'S';
    public const byte Ready = (byte)'R';
    public const byte OpenWrite = (byte)'W';
    public const byte CopyInResponse = (byte)'G';
    public const byte CopyData = (byte)'d';
    public const byte CopyDone = (byte)'c';
    public const byte Query = (byte)'Q';
    public const byte RowDescription = (byte)'T';
    public const byte DataRow = (byte)'D';
    public const byte Complete = (byte)'C';
    public const byte Error = (byte)'E';

    /// <summary>Client → server: run a query, stop after N rows, hand back a token.</summary>
    /// <remarks>
    /// <b>The same byte as <see cref="CopyInResponse"/>, and they do not collide: a frame
    /// kind is read in a direction.</b> <c>G</c> server → client opens a copy-in; <c>G</c>
    /// client → server asks for a page. Nothing on this connection ever has to decide which
    /// one a byte is, because each is only ever written by one end and only ever read by
    /// the other.
    /// </remarks>
    public const byte QueryPage = (byte)'G';

    /// <summary>Client → server: run a query and report only how many rows it has.</summary>
    public const byte QueryCount = (byte)'N';

    /// <summary>Server → client: how many rows the query has, as <c>u64</c> little-endian.</summary>
    public const byte Count = (byte)'n';

    /// <summary>
    /// Server → client: the token to carry on from, sent once just before
    /// <see cref="Complete"/>.
    /// </summary>
    /// <remarks>
    /// <b>Only when the page was cut short and there is more.</b> A page that reached the
    /// end of the result sends none, which is how a caller knows it has seen everything
    /// without asking again to be told nothing — so "no resume frame" is the terminator
    /// and an empty token is not one.
    /// </remarks>
    public const byte Resume = (byte)'r';

    /// <summary>Client → server: stop this stream.</summary>
    /// <remarks>
    /// In band, on the stream it cancels, so it does not disturb the other streams sharing
    /// the socket. The stream still ends with a <see cref="Complete"/>: a cancel is an early
    /// end and not a failure, so a caller that sends one still has to read to that frame
    /// before the connection is its own again.
    /// </remarks>
    public const byte Cancel = (byte)'X';

    /// <summary>Client → server: a lifecycle request — create, finish, remove.</summary>
    /// <remarks>
    /// <b>The database is named in the frame rather than taken from the session</b>, so one
    /// connection can create a database it is not bound to — which is what lets a producer
    /// make the databases it is about to write to instead of being handed them.
    /// </remarks>
    public const byte Control = (byte)'L';

    /// <summary>Server → client: what the lifecycle request came to.</summary>
    public const byte ControlReply = (byte)'M';

    /// <summary>Client → server: what can I ask you? No payload.</summary>
    public const byte Schema = (byte)'H';

    /// <summary>Server → client: this database's schema, as source. The whole payload.</summary>
    public const byte SchemaReply = (byte)'h';
}

/// <summary>A lifecycle operation, by the byte the wire assigns it.</summary>
/// <remarks>
/// <b>The discriminants are a wire contract: append only, never renumber.</b> A reply
/// carries the same byte, so an answer is decoded without remembering what was asked.
/// </remarks>
public static class ControlOp
{
    public const byte Create = 1;
    public const byte Finish = 2;
    public const byte Remove = 3;
}

/// <summary>A frame as it arrived.</summary>
public sealed record Frame(byte Kind, uint Stream, byte[] Payload);

/// <summary>
/// The frame layer: <c>[kind u8][stream u32][length u32][payload]</c>, little-endian.
/// </summary>
/// <remarks>
/// The <c>stream</c> field is what departs from PostgreSQL, and it is the reason for
/// departing: PG's model is strictly serial, so a long query blocks a short one behind
/// it. Here a query is a stream and a write is a stream, and a frame says which.
/// <para>
/// An unrecognised kind is <b>returned, not rejected</b>. A framing layer delimits; it
/// does not interpret. Refusing here would leave a client unable to skip a frame it did
/// not understand, which is the one thing the length is for.
/// </para>
/// </remarks>
public static class FrameIo
{
    public const int HeaderLength = 1 + 4 + 4;
    public const uint MaxPayload = Block.MaxPayload;

    public static void Write(Stream stream, byte kind, uint streamId, ReadOnlySpan<byte> payload)
    {
        if (payload.Length > MaxPayload)
        {
            throw new FjordProtocolException(
                $"{payload.Length} payload bytes exceeds the maximum of {MaxPayload}");
        }

        Span<byte> header = stackalloc byte[HeaderLength];
        header[0] = kind;
        BinaryPrimitives.WriteUInt32LittleEndian(header[1..], streamId);
        BinaryPrimitives.WriteUInt32LittleEndian(header[5..], (uint)payload.Length);

        stream.Write(header);
        stream.Write(payload);
        stream.Flush();
    }

    public static Frame Read(Stream stream)
    {
        var header = ReadExactly(stream, HeaderLength);

        var kind = header[0];
        var streamId = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(1));
        var length = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(5));

        // A length sizes an allocation and came from the peer.
        if (length > MaxPayload)
        {
            throw new FjordProtocolException(
                $"frame declares {length} payload bytes, past the maximum of {MaxPayload}");
        }

        return new Frame(kind, streamId, ReadExactly(stream, (int)length));
    }

    private static byte[] ReadExactly(Stream stream, int count)
    {
        var buffer = new byte[count];
        var filled = 0;

        while (filled < count)
        {
            var read = stream.Read(buffer, filled, count - filled);
            if (read == 0)
            {
                throw new FjordProtocolException(
                    $"the connection closed {filled} bytes into a {count}-byte read");
            }
            filled += read;
        }

        return buffer;
    }
}
