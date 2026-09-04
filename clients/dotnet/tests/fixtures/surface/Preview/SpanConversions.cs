using System;
using System.Collections.Generic;

namespace Surface.Preview;

/// <summary>Extension methods whose receiver is a span, called on things that are not spans.</summary>
public static class SpanReceiverExtensions
{
    /// <summary>An extension method on <c>ReadOnlySpan&lt;char&gt;</c>.</summary>
    public static int CountCommas(this ReadOnlySpan<char> text)
    {
        var commas = 0;

        foreach (var character in text)
        {
            if (character == ',')
            {
                commas++;
            }
        }

        return commas;
    }

    /// <summary>An extension method on <c>ReadOnlySpan&lt;T&gt;</c>, generic in the element.</summary>
    /// <typeparam name="TItem">The element type.</typeparam>
    public static int CountOf<TItem>(this ReadOnlySpan<TItem> items)
        where TItem : notnull
        => items.Length;
}

/// <summary>
/// C# 14 — More implicit conversions for <c>Span&lt;T&gt;</c> and <c>ReadOnlySpan&lt;T&gt;</c>
/// (first-class span types). The conversions are now part of the language rather than a set
/// of framework operators, which changes two things an index can see: a span-receiver
/// extension method is applicable to a <c>string</c> or an array, and a
/// <c>ReadOnlySpan&lt;Derived&gt;</c> converts to a <c>ReadOnlySpan&lt;Base&gt;</c>.
/// </summary>
public static class SpanConversions
{
    /// <summary>
    /// C# 14 — a <c>string</c> as the receiver of a <c>ReadOnlySpan&lt;char&gt;</c> extension
    /// method. Pre-C# 14 this was not applicable: an extension receiver was not a position
    /// where the string-to-span conversion applied.
    /// </summary>
    public static int CommasInString() => "a,b,c".CountCommas();

    /// <summary>C# 14 — an array as the receiver of a span extension method.</summary>
    public static int ItemsInArray()
    {
        var items = new[] { "a", "b" };

        return items.CountOf();
    }

    /// <summary>C# 14 — a <c>List&lt;T&gt;</c>'s span, for contrast: this needed a call before.</summary>
    public static int ItemsInList()
    {
        var items = new List<string> { "a" };

        return items.ToArray().CountOf();
    }

    /// <summary>
    /// C# 14 — the variance conversion: <c>ReadOnlySpan&lt;string&gt;</c> to
    /// <c>ReadOnlySpan&lt;object&gt;</c>, which no framework operator provided.
    /// </summary>
    public static int Covariant()
    {
        ReadOnlySpan<string> names = ["a", "bb"];
        ReadOnlySpan<object> objects = names;

        return objects.Length;
    }

    /// <summary>C# 14 — <c>Span&lt;T&gt;</c> to <c>ReadOnlySpan&lt;T&gt;</c>, in an argument position.</summary>
    public static int Narrowed()
    {
        Span<int> writable = [1, 2, 3];

        return Total(writable);
    }

    /// <summary>C# 14 — an array argument for a span parameter, and a string for one too.</summary>
    public static int Implicit() => Total([4, 5]) + Length("abc");

    private static int Total(ReadOnlySpan<int> values)
    {
        var total = 0;

        foreach (var value in values)
        {
            total += value;
        }

        return total;
    }

    private static int Length(ReadOnlySpan<char> text) => text.Length;
}
