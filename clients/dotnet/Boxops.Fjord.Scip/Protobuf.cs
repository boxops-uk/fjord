namespace Boxops.Fjord.Scip;

/// <summary>
/// Enough of the protocol-buffer wire format to read a SCIP index.
/// </summary>
/// <remarks>
/// <para>
/// <b>Four messages and eleven fields, so the format is read rather than generated.</b>
/// The wire format is a sequence of <c>(field number, wire type)</c> tags and their
/// payloads, and a reader that does not recognise a tag can always skip it — which is what
/// makes reading a subset safe rather than fragile. A protobuf package here would buy a
/// build-time code generator and a package feed in exchange for this file.
/// </para>
/// <para>
/// <b>Unknown fields are skipped, not refused.</b> SCIP has grown fields since it was
/// written and will grow more; an index carrying one this does not know about is not a
/// malformed index, and refusing it would make every consumer of this converter wait for
/// this file.
/// </para>
/// </remarks>
internal ref struct Protobuf(ReadOnlySpan<byte> bytes)
{
    private ReadOnlySpan<byte> _bytes = bytes;
    private int _at = 0;

    /// <summary>Whether there is another field in this message.</summary>
    public readonly bool More => _at < _bytes.Length;

    /// <summary>The field number of the tag just read.</summary>
    public int Field { get; private set; }

    /// <summary>The wire type of the tag just read: 0 varint, 2 length-delimited.</summary>
    public int Wire { get; private set; }

    /// <summary>Read the next tag, leaving its payload to be taken or skipped.</summary>
    public bool Next()
    {
        if (!More)
        {
            return false;
        }

        var tag = Varint();
        Field = (int)(tag >> 3);
        Wire = (int)(tag & 0x7);

        return true;
    }

    /// <summary>The current field as an unsigned varint.</summary>
    public ulong Varint()
    {
        ulong value = 0;
        var shift = 0;

        while (_at < _bytes.Length)
        {
            var b = _bytes[_at++];
            value |= (ulong)(b & 0x7F) << shift;

            if ((b & 0x80) == 0)
            {
                return value;
            }

            shift += 7;

            if (shift > 63)
            {
                throw new FormatException("a varint longer than 64 bits");
            }
        }

        throw new FormatException("a varint ran off the end of the message");
    }

    /// <summary>The current length-delimited field's bytes.</summary>
    public ReadOnlySpan<byte> Bytes()
    {
        var length = (int)Varint();

        if (length < 0 || _at + length > _bytes.Length)
        {
            throw new FormatException("a length-delimited field ran off the end of the message");
        }

        var slice = _bytes.Slice(_at, length);
        _at += length;

        return slice;
    }

    /// <summary>The current length-delimited field as UTF-8 text.</summary>
    public string Text() => System.Text.Encoding.UTF8.GetString(Bytes());

    /// <summary>A reader over the current length-delimited field, as a nested message.</summary>
    public Protobuf Message() => new(Bytes());

    /// <summary>
    /// A repeated <c>int32</c> field, packed or not.
    /// </summary>
    /// <remarks>
    /// <b>Both spellings, because both are legal and both occur.</b> proto3 packs repeated
    /// scalars by default, so <c>Occurrence.range</c> arrives as one length-delimited run
    /// of varints — but an encoder is free to write them one tag at a time, and a reader
    /// that handled only the packed form would read an empty range and silently place
    /// every occurrence at the start of its file.
    /// </remarks>
    public void Int32s(List<int> into)
    {
        if (Wire == 2)
        {
            var packed = new Protobuf(Bytes());

            while (packed.More)
            {
                into.Add((int)(long)packed.Varint());
            }

            return;
        }

        into.Add((int)(long)Varint());
    }

    /// <summary>Step over a field this reader has no use for.</summary>
    public void Skip()
    {
        switch (Wire)
        {
            case 0:
                Varint();
                break;

            case 1:
                _at += 8;
                break;

            case 2:
                Bytes();
                break;

            case 5:
                _at += 4;
                break;

            default:
                throw new FormatException($"wire type {Wire} is not one this reader knows");
        }

        if (_at > _bytes.Length)
        {
            throw new FormatException("a field ran off the end of the message");
        }
    }
}
