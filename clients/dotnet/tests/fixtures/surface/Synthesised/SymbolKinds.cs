using System;
using System.Collections.Generic;
using System.Linq;

namespace Surface.Synthesised;

/// <summary><c>SymbolKind.Local</c> — locals whose names repeat within one method (13.6.2).</summary>
/// <remarks>
/// A local's identity has to include its scope, because C# lets the same name be declared
/// twice in one method body as long as the declaration spaces are disjoint. Every method
/// here does that deliberately: an index keyed on (containing method, name) mints one
/// identity for two locals, and the two have different types, so whichever write lands
/// second either loses or is refused.
/// </remarks>
public class SynLocals
{
    /// <summary>Two locals named <c>item</c>, in sibling blocks, with different types.</summary>
    /// <returns>A digest.</returns>
    public string Shadowed()
    {
        var digest = string.Empty;

        {
            var item = 1;
            digest += item;
        }

        {
            var item = "one";
            digest += item;
        }

        for (var index = 0; index < 2; index++)
        {
            var item = index * 2;
            digest += item;
        }

        foreach (var item in new[] { 3, 4 })
        {
            digest += item;
        }

        return digest;
    }

    /// <summary>Locals introduced by constructs other than a declaration statement.</summary>
    /// <param name="text">Something to parse.</param>
    /// <returns>A digest.</returns>
    public string Implicit(string text)
    {
        // 13.6.2 — an out variable, a pattern designation, a deconstruction, a `using`
        // declaration, a `fixed` statement and a `catch` clause each declare a local with
        // no local-declaration statement of its own.
        if (!int.TryParse(text, out var parsed))
        {
            parsed = -1;
        }

        object boxed = parsed;

        if (boxed is int unboxed and > -2)
        {
            parsed = unboxed;
        }

        var (row, column) = (parsed, parsed + 1);

        using var reader = new System.IO.StringReader(text);
        var line = reader.ReadLine() ?? string.Empty;

        var switched = boxed switch
        {
            int narrow when narrow > 0 => narrow,
            string wide => wide.Length,
            _ => 0,
        };

        try
        {
            _ = int.Parse(line);
        }
        catch (FormatException caught) when (caught.Message.Length > 0)
        {
            switched = -1;
        }

        return $"{parsed}{row}{column}{switched}";
    }

    /// <summary>A <c>ref</c> local and a <c>ref readonly</c> local (C# 7.0 and 7.2).</summary>
    /// <param name="cells">Where to point.</param>
    /// <returns>The first cell, after writing through the alias.</returns>
    public int Aliased(int[] cells)
    {
        ref var head = ref cells[0];
        head = 7;
        head = ref cells[1];
        ref readonly var tail = ref cells[^1];

        return head + tail;
    }
}

/// <summary><c>SymbolKind.Label</c> — <c>goto</c> and switch labels (13.4, 13.10).</summary>
/// <remarks>
/// Both methods declare a label named <c>Retry</c>, so (containing type, name) is not an
/// identity; and the switch labels are named by their constant pattern rather than by an
/// identifier, so <c>case 1:</c> in two different sections of two different switches shares
/// whatever name the index derives from the constant. <c>default:</c> is a label with no
/// name at all.
/// </remarks>
public class SynLabels
{
    /// <summary>A backwards <c>goto</c> to a declared label.</summary>
    /// <param name="attempts">How many times to loop.</param>
    /// <returns>The count reached.</returns>
    public int Loop(int attempts)
    {
        var seen = 0;

    Retry:
        seen++;

        if (seen < attempts)
        {
            goto Retry;
        }

        return seen;
    }

    /// <summary>A second label named <c>Retry</c>, in the same type.</summary>
    /// <param name="value">What to classify.</param>
    /// <returns>A classification.</returns>
    public string Classify(object value)
    {
        var pass = 0;

    Retry:
        switch (value)
        {
            case 1:
            case 2:
                return "small";

            case int large when large > 100:
                return "large";

            case string text:
                value = text.Length;
                pass++;

                if (pass < 2)
                {
                    goto Retry;
                }

                return "text";

            case null:
                return "none";

            default:
                return "other";
        }
    }

    /// <summary><c>goto case</c> and <c>goto default</c>, which reference switch labels.</summary>
    /// <param name="kind">Which arm.</param>
    /// <returns>A digest.</returns>
    public string Chained(int kind)
    {
        switch (kind)
        {
            case 0:
                goto case 1;

            case 1:
                goto default;

            default:
                return "fell through";
        }
    }
}

/// <summary><c>SymbolKind.Discard</c> — the <c>_</c> symbol (12.21).</summary>
/// <remarks>
/// Six discards in one method, all spelled <c>_</c>, all in the same declaration space.
/// They are distinct symbols with distinct types and no declaration between them, which is
/// the one identifier C# guarantees will repeat. A discard designation in a pattern is a
/// seventh shape, and a <c>_</c> that <em>is</em> a declared local (the last one here) is an
/// eighth: it is a <c>SymbolKind.Local</c>, not a discard, and only the binding tells them
/// apart.
/// </remarks>
public class SynDiscards
{
    /// <summary>Discards in every position C# allows one.</summary>
    /// <param name="text">Something to parse.</param>
    /// <returns>A digest.</returns>
    public string Everywhere(string text)
    {
        _ = int.TryParse(text, out _);
        (_, var kept) = (1, 2);
        _ = kept;

        var matched = text switch
        {
            null => 0,
            _ => text.Length,
        };

        if (text is { Length: _ })
        {
            matched++;
        }

        if (text is not null and var _)
        {
            matched++;
        }

        Action<int, int> ignoring = static (_, _) => { };
        ignoring(1, 2);

        return $"{kept}{matched}";
    }

    /// <summary>A local actually named <c>_</c>, which suppresses the discard meaning.</summary>
    /// <returns>The local's value.</returns>
    public int Named()
    {
        var _ = 9;

        return _;
    }
}

/// <summary><c>SymbolKind.RangeVariable</c> — query range variables (12.20).</summary>
/// <remarks>
/// A range variable is not a local: it has its own <c>SymbolKind</c>, it is declared by a
/// clause rather than by a declarator, and the compiler rewrites it into a lambda parameter
/// or an anonymous-type member. Two queries in <see cref="Twice"/> each declare
/// <c>entry</c>, so the name repeats in one method; <c>into</c> declares a continuation
/// range variable that shares the identifier of the one it replaces.
/// </remarks>
public class SynRangeVariables
{
    /// <summary>Every clause that declares a range variable.</summary>
    /// <param name="entries">What to query.</param>
    /// <returns>The keys.</returns>
    public IEnumerable<string> Clauses(IEnumerable<SynEntry> entries)
    {
        return from entry in entries
               let scaled = entry.Weight * 2
               join other in entries on entry.Key equals other.Key into matched
               from match in matched
               where scaled > 0
               orderby scaled descending, match.Key ascending
               group match by match.Key into grouped
               select grouped.Key;
    }

    /// <summary>Two queries in one method, each with a range variable named <c>entry</c>.</summary>
    /// <param name="entries">What to query.</param>
    /// <returns>A digest.</returns>
    public string Twice(IEnumerable<SynEntry> entries)
    {
        var first = from entry in entries
                    select entry.Weight;

        var second = from entry in entries
                     select entry.Key;

        return $"{first.Sum()}{string.Concat(second)}";
    }

    /// <summary>A continuation, whose <c>into</c> variable reuses the identifier.</summary>
    /// <param name="entries">What to query.</param>
    /// <returns>The heaviest key per group.</returns>
    public IEnumerable<string> Continued(IEnumerable<SynEntry> entries)
    {
        return from entry in entries
               group entry by entry.Weight into entry
               select entry.First().Key;
    }
}

/// <summary><c>SymbolKind.Parameter</c>, including the ones no source declares (15.6.2).</summary>
/// <remarks>
/// A setter's <c>value</c> is a parameter with no parameter declaration — its location is
/// the accessor, and there is no <c>ParameterSyntax</c> to walk to. <see cref="Set"/>
/// declares a real parameter named <c>value</c> in the same type, so the two are
/// distinguishable only by their containing method. Beyond that: every parameter modifier
/// (<c>ref</c>, <c>out</c>, <c>in</c>, <c>ref readonly</c>, <c>params</c>, <c>this</c>,
/// <c>scoped</c>) and a default value.
/// </remarks>
public class SynParameters
{
    private int _threshold;
    private readonly int[] _cells = new int[4];

    /// <summary>A property whose setter's <c>value</c> parameter is synthesised.</summary>
    public int Threshold
    {
        get => _threshold;
        set => _threshold = value;
    }

    /// <summary>An indexer, whose setter has both a declared and a synthesised parameter.</summary>
    /// <param name="index">Declared.</param>
    public int this[int index]
    {
        get => _cells[index];
        set => _cells[index] = value;
    }

    /// <summary>A declared parameter named <c>value</c>, to sit beside the synthesised ones.</summary>
    /// <param name="value">The threshold.</param>
    public void Set(int value) => _threshold = value;

    /// <summary>Every parameter modifier the language has.</summary>
    /// <param name="byRef">Read and written.</param>
    /// <param name="byOut">Written only.</param>
    /// <param name="byIn">Read only, by reference.</param>
    /// <param name="byRefReadonly">Read only, by reference, C# 12 spelling.</param>
    /// <param name="withDefault">Optional.</param>
    /// <param name="rest">However many.</param>
    /// <returns>A digest.</returns>
    public int Modifiers(
        ref int byRef,
        out int byOut,
        in int byIn,
        ref readonly int byRefReadonly,
        int withDefault = 4,
        params int[] rest)
    {
        byRef += byIn + byRefReadonly;
        byOut = byRef + withDefault;

        return byOut + rest.Length;
    }

    /// <summary>A <c>scoped</c> ref-struct parameter (C# 11).</summary>
    /// <param name="window">The span.</param>
    /// <returns>Its length.</returns>
    public static int Scoped(scoped ReadOnlySpan<char> window) => window.Length;

    /// <summary>A lambda with a default parameter value (C# 12).</summary>
    /// <returns>The lambda.</returns>
    public static Func<int, int> Defaulted() => (int step = 2) => step * 3;
}

/// <summary><c>SymbolKind.Field</c> in the shapes that make a field's identity hard (15.5).</summary>
/// <remarks>
/// One field declaration with three declarators produces three fields at one node; a
/// <c>const</c> is a field whose value the index must hold rather than its storage; a
/// <c>readonly ref</c> field (C# 11) is a field whose type is not a type an ordinary field
/// may have; and <c>[field:]</c> targets the backing field of an auto-property, so an
/// attribute application lands on a field that has no declaration.
/// </remarks>
public class SynFields
{
    /// <summary>Three fields, one declaration node, one type, one doc comment.</summary>
    public int First, Second, Third;

    /// <summary>A constant, which is a field with no storage.</summary>
    public const int Ceiling = 128;

    /// <summary>A static readonly field with an initializer.</summary>
    public static readonly string Marker = "synthesised";

    /// <summary>A volatile field, whose modifier changes only the emitted access.</summary>
    public volatile int Pending;

    /// <summary>An attribute applied to a field that has no declaration (C# 7.3).</summary>
    [field: NonSerialized]
    public int Transient { get; set; }
}

/// <summary><c>SymbolKind.TypeParameter</c> and every constraint kind (15.2.3, 15.2.5).</summary>
/// <typeparam name="TClass">Constrained to a reference type.</typeparam>
/// <typeparam name="TStruct">Constrained to a value type.</typeparam>
/// <typeparam name="TDerived">Constrained to another type parameter.</typeparam>
/// <remarks>
/// A type parameter is declared by the type or method that owns it and is a distinct symbol
/// for each — so <c>TClass</c> on the type and <c>TClass</c> on <see cref="Nested{TClass}"/>
/// are two symbols with one name in one lexical region, which the compiler warns about
/// (CS0693) and still accepts.
/// </remarks>
public class SynConstrained<TClass, TStruct, TDerived>
    where TClass : class, ISynNamed, new()
    where TStruct : struct, IComparable<TStruct>
    where TDerived : TClass
{
    /// <summary>A method whose type parameter shadows the type's.</summary>
    /// <typeparam name="TClass">A second type parameter of that name.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>Its text.</returns>
    public string Nested<TClass>(TClass value) => $"{value}";

    /// <summary>An <c>unmanaged</c> constraint (C# 7.3).</summary>
    /// <typeparam name="TBlittable">Unmanaged.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>Its size.</returns>
    public static unsafe int Unmanaged<TBlittable>(TBlittable value)
        where TBlittable : unmanaged => sizeof(TBlittable);

    /// <summary>A <c>notnull</c> constraint and an <c>allows ref struct</c> one (C# 13).</summary>
    /// <typeparam name="TAny">Not null, and allowed to be a ref struct.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>Whether it was default.</returns>
    public static bool Permissive<TAny>(TAny value)
        where TAny : allows ref struct => value is null;

    /// <summary>An annotated constraint, which only exists in a nullable context.</summary>
    /// <typeparam name="TMaybe">A possibly-null reference type.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>The value or a default.</returns>
    public static TMaybe? OrDefault<TMaybe>(TMaybe? value)
        where TMaybe : class? => value;
}

/// <summary>A static abstract interface member (C# 11), which needs a type parameter to call.</summary>
/// <typeparam name="TSelf">The implementing type.</typeparam>
public interface ISynSeeded<TSelf>
    where TSelf : ISynSeeded<TSelf>
{
    /// <summary>The seed, as a static abstract property.</summary>
    static abstract TSelf Seed { get; }

    /// <summary>Combines two, as a static abstract operator.</summary>
    /// <param name="left">The left.</param>
    /// <param name="right">The right.</param>
    /// <returns>The combination.</returns>
    static abstract TSelf operator +(TSelf left, TSelf right);
}

/// <summary>Implements the static abstract members, which are ordinary statics here.</summary>
public readonly struct SynSeed : ISynSeeded<SynSeed>
{
    /// <summary>How much.</summary>
    public int Weight { get; init; }

    /// <inheritdoc/>
    public static SynSeed Seed => new() { Weight = 1 };

    /// <inheritdoc/>
    public static SynSeed operator +(SynSeed left, SynSeed right) =>
        new() { Weight = left.Weight + right.Weight };
}
