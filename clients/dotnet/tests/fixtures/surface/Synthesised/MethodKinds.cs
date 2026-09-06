using System;
using System.Collections.Generic;
using System.Linq;

namespace Surface.Synthesised;

/// <summary>An interface, so that explicit implementation has something to implement.</summary>
public interface ISynNamed
{
    /// <summary>The name.</summary>
    string Name { get; }

    /// <summary>Renames.</summary>
    /// <param name="name">The new name.</param>
    void Rename(string name);

    /// <summary>Fires when the name changes.</summary>
    event EventHandler Renamed;

    /// <summary>A default interface member (C# 8), which is an ordinary method with a body.</summary>
    /// <returns>The name, upper-cased.</returns>
    string Shout() => Name.ToUpperInvariant();
}

/// <summary><c>MethodKind.Ordinary</c>, in the shape that makes it a hazard (15.6).</summary>
/// <remarks>
/// Six declarations named <c>Measure</c> in one type. They differ by parameter count, by
/// parameter type, by generic arity and by <c>ref</c>-ness of a parameter — every axis a
/// C# signature has, and none of them is the name. An index that keys a method by
/// (containing type, name) mints one identity six times here; an index that keys by
/// (containing type, name, ordinal) is sensitive to the order they appear in, so the
/// declarations are deliberately not in a natural order.
/// </remarks>
public class SynOverloads
{
    /// <summary>No parameters.</summary>
    /// <returns>Zero.</returns>
    public int Measure() => 0;

    /// <summary>Two parameters.</summary>
    /// <param name="left">The left.</param>
    /// <param name="right">The right.</param>
    /// <returns>Their sum.</returns>
    public int Measure(int left, int right) => left + right;

    /// <summary>One generic parameter.</summary>
    /// <typeparam name="T">Anything.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>One.</returns>
    public int Measure<T>(T value) => value is null ? 0 : 1;

    /// <summary>One parameter.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The value.</returns>
    public int Measure(int value) => value;

    /// <summary>Two generic parameters — a different arity, not a different name.</summary>
    /// <typeparam name="TFirst">The first.</typeparam>
    /// <typeparam name="TSecond">The second.</typeparam>
    /// <param name="first">The first value.</param>
    /// <param name="second">The second value.</param>
    /// <returns>Two.</returns>
    public int Measure<TFirst, TSecond>(TFirst first, TSecond second) => 2;

    /// <summary>A <c>ref</c> parameter, which differs from <c>Measure(int)</c> by ref-ness alone.</summary>
    /// <param name="value">Doubled in place.</param>
    /// <returns>The doubled value.</returns>
    public int Measure(ref int value)
    {
        value *= 2;
        return value;
    }

    /// <summary>A <c>params</c> collection (C# 13), which is a span, not an array.</summary>
    /// <param name="values">However many.</param>
    /// <returns>Their count.</returns>
    public int Total(params ReadOnlySpan<int> values) => values.Length;

    /// <summary>Optional and named arguments (C# 4), which change no signature.</summary>
    /// <param name="scale">How much.</param>
    /// <param name="offset">From where.</param>
    /// <returns>The scaled offset.</returns>
    public int Scaled(int scale = 1, int offset = 0) => (scale * 10) + offset;
}

/// <summary><c>MethodKind.UserDefinedOperator</c> and friends (15.10).</summary>
/// <remarks>
/// The declared operators here carry <c>MethodKind.UserDefinedOperator</c>; the metadata
/// names (<c>op_Addition</c>, <c>op_UnaryNegation</c>, <c>op_CheckedAddition</c>,
/// <c>op_UnsignedRightShift</c>) appear nowhere in source, so declared name and indexed
/// name differ for every one of them. The <c>checked</c> operator (C# 11) is a second
/// declaration of <c>operator +</c> in the same type, distinguished by a keyword rather
/// than by a signature — the pair mints two metadata names from one spelling.
/// </remarks>
public readonly struct SynOperators
{
    /// <summary>How much.</summary>
    public int Weight { get; init; }

    /// <summary>Addition.</summary>
    /// <param name="left">The left.</param>
    /// <param name="right">The right.</param>
    /// <returns>The sum.</returns>
    public static SynOperators operator +(SynOperators left, SynOperators right) =>
        new() { Weight = left.Weight + right.Weight };

    /// <summary>Checked addition (C# 11): a second <c>operator +</c> in one type.</summary>
    /// <param name="left">The left.</param>
    /// <param name="right">The right.</param>
    /// <returns>The sum, checked.</returns>
    public static SynOperators operator checked +(SynOperators left, SynOperators right) =>
        new() { Weight = checked(left.Weight + right.Weight) };

    /// <summary>Negation, a unary operator.</summary>
    /// <param name="value">The value.</param>
    /// <returns>Its negation.</returns>
    public static SynOperators operator -(SynOperators value) =>
        new() { Weight = -value.Weight };

    /// <summary>Unsigned right shift (C# 11).</summary>
    /// <param name="value">The value.</param>
    /// <param name="places">How far.</param>
    /// <returns>The shifted value.</returns>
    public static SynOperators operator >>>(SynOperators value, int places) =>
        new() { Weight = (int)((uint)value.Weight >> places) };

    /// <summary>Equality, which the compiler pairs with <c>Equals</c> and <c>GetHashCode</c>.</summary>
    /// <param name="left">The left.</param>
    /// <param name="right">The right.</param>
    /// <returns>Whether the weights agree.</returns>
    public static bool operator ==(SynOperators left, SynOperators right) =>
        left.Weight == right.Weight;

    /// <summary>Inequality, which C# requires beside equality.</summary>
    /// <param name="left">The left.</param>
    /// <param name="right">The right.</param>
    /// <returns>Whether the weights differ.</returns>
    public static bool operator !=(SynOperators left, SynOperators right) =>
        !(left == right);

    /// <summary><c>true</c>, which C# requires beside <c>false</c>.</summary>
    /// <param name="value">The value.</param>
    /// <returns>Whether it carries weight.</returns>
    public static bool operator true(SynOperators value) => value.Weight != 0;

    /// <summary><c>false</c>.</summary>
    /// <param name="value">The value.</param>
    /// <returns>Whether it carries no weight.</returns>
    public static bool operator false(SynOperators value) => value.Weight == 0;

    /// <inheritdoc/>
    public override bool Equals(object? other) =>
        other is SynOperators op && op.Weight == Weight;

    /// <inheritdoc/>
    public override int GetHashCode() => Weight;
}

/// <summary><c>MethodKind.Conversion</c> (15.10.4).</summary>
/// <remarks>
/// Four conversion operators, whose metadata names are only <c>op_Implicit</c>,
/// <c>op_Explicit</c> and <c>op_CheckedExplicit</c> — so two of these declarations share a
/// metadata name and differ only in their return type, which is not part of a C# signature.
/// An index that keys a method by (type, metadata name, parameter types) has two
/// declarations wanting one identity, and the collision is invisible in source.
/// </remarks>
public readonly struct SynConverted
{
    /// <summary>The carried count.</summary>
    public int Count { get; init; }

    /// <summary>Implicit, from <c>int</c>.</summary>
    /// <param name="count">The count.</param>
    public static implicit operator SynConverted(int count) => new() { Count = count };

    /// <summary>Implicit, to <c>long</c> — <c>op_Implicit</c> a second time.</summary>
    /// <param name="value">The value.</param>
    public static implicit operator long(SynConverted value) => value.Count;

    /// <summary>Explicit, to <c>short</c>.</summary>
    /// <param name="value">The value.</param>
    public static explicit operator short(SynConverted value) => (short)value.Count;

    /// <summary>Explicit and checked (C# 11), to <c>short</c> again.</summary>
    /// <param name="value">The value.</param>
    public static explicit operator checked short(SynConverted value) => checked((short)value.Count);
}

/// <summary><c>MethodKind.ExplicitInterfaceImplementation</c> (18.6.2).</summary>
/// <remarks>
/// Each explicitly implemented member's name in metadata is qualified —
/// <c>Surface.Synthesised.ISynNamed.Rename</c> — while its declared name is <c>Rename</c>
/// and the interface's is <c>Rename</c> too. Three strings, one member. The type also
/// declares a public <c>Name</c> beside the explicit <c>ISynNamed.Name</c>, so two
/// properties in one type share a simple name legally; and the explicit event supplies
/// <c>MethodKind.EventAdd</c> and <c>MethodKind.EventRemove</c> with real accessor
/// declarations, to sit beside the synthesised pair in <see cref="SynFieldEvent"/>.
/// </remarks>
public sealed class SynExplicitImpl : ISynNamed
{
    private string _name = "unnamed";
    private EventHandler? _renamed;

    /// <summary>The public property, unrelated to the interface's.</summary>
    public string Name => $"public:{_name}";

    /// <summary>The interface's property, explicitly implemented.</summary>
    string ISynNamed.Name => _name;

    /// <summary>The interface's method, explicitly implemented.</summary>
    /// <param name="name">The new name.</param>
    void ISynNamed.Rename(string name)
    {
        _name = name;
        _renamed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The interface's event, explicitly implemented.</summary>
    event EventHandler ISynNamed.Renamed
    {
        add => _renamed += value;
        remove => _renamed -= value;
    }

    /// <summary>An implicit implementation of nothing, to contrast with the above.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => Name;
}

/// <summary><c>MethodKind.EventAdd</c> and <c>MethodKind.EventRemove</c>, declared (15.8.2).</summary>
public class SynEventPair
{
    private SynCallback? _handlers;

    /// <summary>An event over the project's own delegate type.</summary>
    public event SynCallback? Observed
    {
        add
        {
            _handlers += value;
        }

        remove
        {
            _handlers -= value;
        }
    }

    /// <summary>Invokes the delegate, which binds <c>MethodKind.DelegateInvoke</c>.</summary>
    /// <param name="entry">What changed.</param>
    /// <returns>Whether anything said to keep going.</returns>
    public bool Notify(SynEntry entry) => _handlers?.Invoke(entry, 0) ?? false;
}

/// <summary>Extension methods, which have two forms in the symbol table (15.6.10).</summary>
/// <remarks>
/// A declared extension method is <c>MethodKind.Ordinary</c> with an extra parameter. The
/// <em>same</em> method seen at an instance-form call site is a different symbol:
/// <c>MethodKind.ReducedExtension</c>, whose parameter list is one shorter and whose
/// <c>ReducedFrom</c> points back at the declaration. So one declaration is reached by two
/// symbols with the same name, the same containing type and the same span, differing only
/// in arity — the exact shape an index keyed on (type, name, arity) treats as two members
/// and an index keyed on (type, name) treats as one.
/// </remarks>
public static class SynReduced
{
    /// <summary>Sums the weights, declared form.</summary>
    /// <param name="entries">The entries.</param>
    /// <returns>The total weight.</returns>
    public static int SynTotalWeight(this IEnumerable<SynEntry> entries)
    {
        var total = 0;

        foreach (var entry in entries)
        {
            total += entry.Weight;
        }

        return total;
    }

    /// <summary>A generic extension method with a constraint.</summary>
    /// <typeparam name="T">Anything that is not null.</typeparam>
    /// <param name="value">The receiver.</param>
    /// <param name="label">What to call it.</param>
    /// <returns>A label.</returns>
    public static string SynLabel<T>(this T value, string label)
        where T : notnull => $"{label}={value}";
}

/// <summary><c>MethodKind.BuiltinOperator</c> and the reduced-extension call sites.</summary>
/// <remarks>
/// <c>left + right</c> on two <c>int</c>s binds to a method the compiler owns and no
/// assembly declares: <c>MethodKind.BuiltinOperator</c>, with an empty
/// <c>DeclaringSyntaxReferences</c>, no containing source and a metadata name
/// (<c>op_Addition</c>) that is shared with <see cref="SynOperators"/>'s declared one. An
/// index that resolves every operator token to a method finds a target here that lives in
/// no file, and if it keys that target by name alone it collides with the user-defined
/// operator of the same name.
/// </remarks>
public static class SynBuiltin
{
    /// <summary>Exercises the predefined operators over the predefined types (12.4.3).</summary>
    /// <param name="left">The left.</param>
    /// <param name="right">The right.</param>
    /// <returns>A digest of every result.</returns>
    public static string Predefined(int left, int right)
    {
        var sum = left + right;
        var difference = left - right;
        var product = left * right;
        var quotient = right == 0 ? 0 : left / right;
        var remainder = right == 0 ? 0 : left % right;
        var shifted = left << 2;
        var unshifted = left >> 2;
        var unsigned = left >>> 2;
        var anded = left & right;
        var ored = left | right;
        var xored = left ^ right;
        var negated = -left;
        var complemented = ~left;
        var ordered = left < right && right > left && left <= right && right >= left;
        var equal = left == right;
        var unequal = left != right;
        var lifted = (int?)left + (int?)right;
        var concatenated = "left" + left;
        var logical = ordered || equal;

        return $"{sum}{difference}{product}{quotient}{remainder}{shifted}{unshifted}" +
            $"{unsigned}{anded}{ored}{xored}{negated}{complemented}{equal}{unequal}" +
            $"{lifted}{concatenated}{logical}";
    }

    /// <summary>Calls the extension methods in reduced form and in static form.</summary>
    /// <returns>A digest.</returns>
    public static string Reduced()
    {
        SynEntry[] entries = [new SynEntry("alpha", 1), new SynEntry("beta", 2)];

        var reducedForm = entries.SynTotalWeight();
        var staticForm = SynReduced.SynTotalWeight(entries);
        var labelled = 7.SynLabel("seven");
        var queried = entries.Where(e => e.Weight > 1).Select(e => e.Key).Count();

        return $"{reducedForm}{staticForm}{labelled}{queried}";
    }
}

/// <summary>
/// The nearest C# has to <c>MethodKind.DeclareMethod</c> — an <c>extern</c> method whose
/// implementation is elsewhere (23.9).
/// </summary>
/// <remarks>
/// <c>MethodKind.DeclareMethod</c> is a Visual Basic <c>Declare Sub</c>/<c>Declare
/// Function</c>, and no C# source produces it: the C# equivalent is this, an <c>extern</c>
/// method carrying <c>DllImport</c>, and Roslyn reports it as <c>MethodKind.Ordinary</c>
/// with <c>IsExtern</c> set. It is written here so that the arm's C# neighbour is in the
/// corpus even though the arm itself is dead — an <c>extern</c> declaration is a method
/// declaration with no body and no block to walk into, which is a shape worth an index
/// row of its own.
/// </remarks>
public static class SynExtern
{
    /// <summary>Declared here, implemented in a library that need not exist.</summary>
    /// <param name="value">Anything.</param>
    /// <returns>Anything.</returns>
    [System.Runtime.InteropServices.DllImport("surface-not-a-real-library")]
    public static extern int SynDeclared(int value);

    /// <summary>An <c>extern</c> method with the source-generated marshalling form.</summary>
    /// <param name="value">Anything.</param>
    /// <returns>Anything.</returns>
    [System.Runtime.InteropServices.DllImport("surface-not-a-real-library", EntryPoint = "syn_widened")]
    public static extern long SynWidened(int value);
}

/// <summary>Calls the operators and conversions, so each declaration is also referenced.</summary>
public static class SynOperatorUse
{
    /// <summary>Binds every declared operator in <see cref="SynOperators"/>.</summary>
    /// <returns>A digest.</returns>
    public static string Exercise()
    {
        var left = new SynOperators { Weight = 3 };
        var right = new SynOperators { Weight = 4 };

        var sum = left + right;
        var checkedSum = checked(left + right);
        var negated = -left;
        var shifted = left >>> 1;
        var equal = left == right;
        var truthy = left ? 1 : 0;

        SynConverted converted = 5;
        long widened = converted;
        var narrowed = (short)converted;
        var checkedNarrowed = checked((short)converted);

        return $"{sum.Weight}{checkedSum.Weight}{negated.Weight}{shifted.Weight}{equal}" +
            $"{truthy}{widened}{narrowed}{checkedNarrowed}";
    }
}
