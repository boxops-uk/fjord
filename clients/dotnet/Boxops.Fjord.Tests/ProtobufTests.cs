using Boxops.Fjord.Scip;

using Xunit;

namespace Boxops.Fjord.Tests;

/// <summary>
/// <para>
/// <b>Every way this reader refuses a message, provoked by the bytes that reach it.</b>
/// </para>
/// <para>
/// A <c>.scip</c> file is bytes from outside — written by an indexer this repository does
/// not own, for a language it has no compiler for — so each refusal is a contract rather
/// than an internal check. A reader that carries on past a malformed length reads the
/// next field at an offset it invented, and everything after it is plausible and wrong.
/// </para>
/// </summary>
public sealed class ProtobufTests
{
    /// <summary>A one-byte tag, for the field numbers a hand-written message uses.</summary>
    private static byte Tag(int field, int wire) => (byte)((field << 3) | wire);

    /// <summary>
    /// Read a message the way <c>ScipIndex</c> reads one: take field 1's bytes, skip
    /// every other field.
    /// </summary>
    private static void Read(byte[] message)
    {
        var reader = new Protobuf(message);

        while (reader.Next())
        {
            if (reader.Field == 1 && reader.Wire == 2)
            {
                reader.Text();
            }
            else
            {
                reader.Skip();
            }
        }
    }

    private static FormatException Refused(byte[] message) =>
        Assert.Throws<FormatException>(() => Read(message));

    /// <summary>
    /// <b>A varint with no last byte is refused rather than read as what arrived.</b>
    /// </summary>
    [Fact]
    public void A_varint_cut_off_by_the_end_of_the_message_is_refused()
    {
        var failure = Refused([0x80]);

        Assert.Equal("a varint ran off the end of the message", failure.Message);
    }

    /// <summary>
    /// <b>A varint that would not fit in 64 bits is refused, not wrapped.</b>
    /// </summary>
    /// <remarks>
    /// Ten continuation bytes: nine take the shift to 63, and the tenth carries bits that
    /// have nowhere to go. C# masks a shift count by the operand's width, so shifting
    /// anyway is <c>&lt;&lt; 6</c> rather than <c>&lt;&lt; 70</c> — the wrapped value looks
    /// like a small legal one.
    /// </remarks>
    [Fact]
    public void A_varint_longer_than_sixty_four_bits_is_refused()
    {
        var failure = Refused([0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80]);

        Assert.Equal("a varint longer than 64 bits", failure.Message);
    }

    /// <summary>
    /// <b>A length past the end of the message is refused.</b>
    /// </summary>
    [Fact]
    public void A_length_delimited_field_past_the_end_of_the_message_is_refused()
    {
        var failure = Refused([Tag(1, 2), 0x05, 0x01, 0x02]);

        Assert.Equal(
            "a length-delimited field ran off the end of the message", failure.Message);
    }

    /// <summary>
    /// <b>A length is refused for what it says, not for what it says once truncated.</b>
    /// </summary>
    /// <remarks>
    /// The length is 64 bits on the wire. Narrowing it first and testing the result splits
    /// the malformed range in two: <c>0x1_0000_0005</c> truncates to 5, which is small,
    /// positive and inside this message — so the field is taken, the reader continues five
    /// bytes along, and every field after it is read at the wrong offset.
    /// </remarks>
    [Fact]
    public void A_length_that_only_fits_once_it_is_truncated_is_refused()
    {
        var failure = Refused(
            [Tag(1, 2), 0x85, 0x80, 0x80, 0x80, 0x10, 0x61, 0x62, 0x63, 0x64, 0x65]);

        Assert.Equal(
            "a length-delimited field ran off the end of the message", failure.Message);
    }

    /// <summary>
    /// <b>A length that overflows the offset it is added to is refused, and refused as a
    /// format error.</b>
    /// </summary>
    /// <remarks>
    /// <c>int.MaxValue</c> is positive and inside no message, but the bound it is compared
    /// against is <c>_at + length</c> — which wraps negative and passes. What is thrown
    /// then comes from <c>Slice</c> and is not a <c>FormatException</c>, so it goes
    /// straight through the converter's catch filter: a stack trace out of a program whose
    /// whole job is reading somebody else's bytes.
    /// </remarks>
    [Fact]
    public void A_length_that_overflows_the_offset_it_is_added_to_is_refused()
    {
        var failure = Refused([Tag(1, 2), 0xFF, 0xFF, 0xFF, 0xFF, 0x07]);

        Assert.Equal(
            "a length-delimited field ran off the end of the message", failure.Message);
    }

    /// <summary>
    /// <b>A wire type this reader cannot measure is refused, and named.</b>
    /// </summary>
    /// <remarks>
    /// Skipping an unknown <i>field</i> is the whole reason a subset of the schema can be
    /// read safely, and it works because the wire type says how long the payload is. Wire
    /// types 3 and 4 are the deprecated group markers and 6 and 7 were never assigned, so
    /// none of them states a length — a reader that guessed would resynchronise on noise.
    /// </remarks>
    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(7)]
    public void A_wire_type_that_states_no_length_is_refused(int wire)
    {
        var failure = Refused([Tag(1, wire)]);

        Assert.Equal($"wire type {wire} is not one this reader knows", failure.Message);
    }

    /// <summary>
    /// <b>A fixed-width field the message is too short for is refused.</b>
    /// </summary>
    /// <remarks>
    /// SCIP carries no <c>double</c> or <c>fixed32</c> today, so both of these arrive only
    /// as a field this reader skips — which is exactly the case where nothing downstream
    /// would notice the reader having walked off the end.
    /// </remarks>
    [Theory]
    [InlineData(1, 7)]
    [InlineData(5, 3)]
    public void A_fixed_width_field_the_message_cannot_hold_is_refused(int wire, int payload)
    {
        var message = new byte[payload + 1];
        message[0] = Tag(1, wire);

        var failure = Refused(message);

        Assert.Equal("a field ran off the end of the message", failure.Message);
    }

    /// <summary>
    /// <b>A whole index is refused with the type the converter's catch filter names.</b>
    /// </summary>
    /// <remarks>
    /// The filter is <c>IOException or FormatException or InvalidOperationException</c>, so
    /// a reader that raises anything else turns a malformed input file into an unhandled
    /// exception — the difference between "could not convert x" and a stack trace.
    /// </remarks>
    [Fact]
    public void A_malformed_index_is_refused_as_a_format_error()
    {
        byte[] index = [Tag(1, 2), 0xFF, 0xFF, 0xFF, 0xFF, 0x07];

        Assert.Throws<FormatException>(() => ScipIndex.Read(index));
    }
}
