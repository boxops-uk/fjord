using System.Runtime.CompilerServices;

namespace Surface.Modern.Structs;

/// <summary>
/// C# 12 — Inline arrays: a struct with <see cref="InlineArrayAttribute"/> and exactly one
/// instance field. The element access, the length and the span conversion are all synthesized
/// — the type declares no indexer, no <c>Length</c> and no <c>AsSpan</c>, and every one of
/// them is usable below. An index that only records declared members holds none of them.
/// </summary>
[InlineArray(Length)]
public struct QuadBuffer
{
    /// <summary>How many elements the buffer holds. The attribute argument must be a constant.</summary>
    public const int Length = 4;

    private int _element0;
}

/// <summary>C# 12 — an inline array over a struct element type, and a longer one.</summary>
[InlineArray(8)]
public struct BearingOctet
{
    private double _element0;
}

/// <summary>Reads both inline arrays through the members the compiler synthesizes.</summary>
public static class InlineBuffers
{
    /// <summary>Fills a <see cref="QuadBuffer"/> through the synthesized element access.</summary>
    public static int SumQuad()
    {
        var buffer = default(QuadBuffer);

        for (var index = 0; index < QuadBuffer.Length; index++)
        {
            buffer[index] = index * index;
        }

        var total = 0;

        foreach (var element in buffer)
        {
            total += element;
        }

        return total;
    }

    /// <summary>Takes a span of the octet, which is a conversion nothing declares.</summary>
    public static double FirstOfOctet()
    {
        var octet = default(BearingOctet);
        octet[7] = 1.5;

        Span<double> span = octet;

        return span[7];
    }
}
