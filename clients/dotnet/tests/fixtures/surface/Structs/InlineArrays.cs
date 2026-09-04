// Post-standard — inline arrays, which C# 12 added. A struct with exactly one instance
// field and an `[InlineArray(n)]` attribute is a fixed-size buffer of n of that field's
// type, usable from safe code. The struct gets element access, a length, and implicit
// conversions to Span<T> and ReadOnlySpan<T>, and it declares none of them.
//
// This is the only shape in C# where the number of a type's instance fields is written as
// an attribute argument. It is also a type with element access and no indexer: it declares
// no `this[...]`, so it does not run into the one-indexer rule, and an index expression
// over it resolves to a member that exists in no source file in the corpus.

using System;
using System.Runtime.CompilerServices;

namespace Surface.Structs;

/// <summary>
/// Post-standard — an inline array of four integers. One field is declared; the type is
/// sixteen bytes wide, and the other three elements have no declaration at all.
/// </summary>
[InlineArray(4)]
public struct StInlineQuad
{
    /// <summary>
    /// The single field the attribute repeats. Its name is by convention only — the
    /// compiler cares that there is exactly one instance field, not what it is called —
    /// and no correct code ever reads it by name.
    /// </summary>
    private int _element0;
}

/// <summary>
/// Post-standard — an inline array whose element type is another struct, so the buffer
/// holds eight copies of a value type declared elsewhere in this project.
/// </summary>
[InlineArray(8)]
public struct StInlineOctet
{
    private StValuePair _element0;
}

/// <summary>
/// Post-standard — an inline array field inside an ordinary struct, which is how one is
/// meant to be used: the buffer is part of the containing value, with no heap allocation
/// and no pointer syntax.
/// </summary>
public struct StBufferedGauge
{
    /// <summary>Clause 16.3.1 — a field whose type is an inline array.</summary>
    public StInlineQuad Recent;

    /// <summary>Clause 16.3.1 — how many of the four slots are in use.</summary>
    public int Used;

    /// <summary>Clause 16.4.9 — the constructor, which fills the buffer.</summary>
    public StBufferedGauge(int seed)
    {
        Used = 4;
        for (int i = 0; i < 4; i++)
        {
            Recent[i] = seed + i;
        }
    }

    /// <summary>Clause 16.4.12 — a method that reads the buffer through the implicit span conversion.</summary>
    public int Sum()
    {
        int total = 0;
        foreach (int value in Recent)
        {
            total += value;
        }

        return total;
    }
}

/// <summary>
/// Post-standard — the uses. Element access, `foreach`, slicing and the span conversions
/// all reach synthesized members; the only names written here are the buffer fields.
/// </summary>
public static class StInlineArrayUse
{
    /// <summary>Post-standard — element access on an inline array, with no indexer to resolve to.</summary>
    public static int WriteThenRead()
    {
        StInlineQuad quad = default;
        quad[0] = 1;
        quad[3] = 4;
        return quad[0] + quad[3];
    }

    /// <summary>Post-standard — the implicit conversion to Span, which no declaration states.</summary>
    public static int SumAsSpan()
    {
        StInlineQuad quad = default;
        quad[2] = 7;
        Span<int> span = quad;
        int total = 0;
        foreach (int value in span)
        {
            total += value;
        }

        return total;
    }

    /// <summary>Post-standard — the implicit conversion to ReadOnlySpan, and a slice of it.</summary>
    public static int SliceLength()
    {
        StInlineQuad quad = default;
        ReadOnlySpan<int> values = quad;
        return values[1..3].Length;
    }

    /// <summary>Post-standard — `foreach` directly over the buffer, which uses the span conversion.</summary>
    public static int SumDirect()
    {
        StInlineQuad quad = default;
        quad[1] = 5;
        int total = 0;
        foreach (int value in quad)
        {
            total += value;
        }

        return total;
    }

    /// <summary>Post-standard — an inline array of structs, whose elements are at their default values.</summary>
    public static int FirstOfOctet()
    {
        StInlineOctet octet = default;
        octet[0] = new StValuePair(1, 2);
        return octet[0].First;
    }

    /// <summary>Post-standard — the inline array as a field of a struct, used as intended.</summary>
    public static int Buffered() => new StBufferedGauge(10).Sum();
}
