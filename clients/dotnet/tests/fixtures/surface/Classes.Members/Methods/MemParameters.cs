// Clause 15.6.2 (method parameters). 15.6.2.1 general: a parameter list is a sequence of
// fixed parameters optionally followed by a parameter array. 15.6.2.2 value parameters — no
// modifier, the argument is copied. 15.6.2.3.1 by-reference parameters in general, then
// 15.6.2.3.2 `in`, 15.6.2.3.3 `ref` and 15.6.2.3.4 `out`. 15.6.2.4 parameter arrays: `params`.
//
// The hazard runs through all of these rows at once. A parameter modifier is part of the
// signature (7.6), but *which* modifier is not enough on its own: C# refuses two overloads
// that differ only in `ref` versus `out` versus `in` (CS0663, demonstrated in the README),
// so the only pair one type may hold is value-versus-by-reference. `MemParameterModifiers`
// holds that legal pair; the illegal one is written across an inheritance boundary in
// `MemOverloads.cs`, where it does compile.

using System;

namespace Surface.Classes.Members.Methods;

/// <summary>15.6.2.1: one method per parameter-list shape, from empty to a parameter array.</summary>
public static class MemParameters
{
    /// <summary>15.6.2.1: the empty parameter list.</summary>
    public static int None() => 0;

    /// <summary>15.6.2.2: a value parameter. The argument is copied in and the copy is
    /// assignable, which is why `total` may be used as scratch space here.</summary>
    public static int Value(int total)
    {
        total += 1;
        return total;
    }

    /// <summary>15.6.2.2: a value parameter of a reference type — the reference is copied,
    /// the object is not.</summary>
    public static int ValueOfReferenceType(string text) => text.Length;

    /// <summary>15.6.2.2: a value parameter with a default, and one whose default is a
    /// constant expression rather than a literal.</summary>
    public static int Defaulted(int width = 1, int height = 2 * 3, string? unit = null)
        => width * height * (unit?.Length ?? 1);

    /// <summary>15.6.2.3.2: an input parameter — passed by reference, readable and not
    /// assignable.</summary>
    public static int Input(in int total) => total + 1;

    /// <summary>15.6.2.3.2: `ref readonly`, which is the `in` semantics with the callee's
    /// spelling of the modifier and a required `ref` or `in` at the call site.</summary>
    public static int RefReadonlyInput(ref readonly int total) => total + 1;

    /// <summary>15.6.2.3.3: a reference parameter — the argument variable itself is passed,
    /// must be initialised by the caller, and writes are seen by the caller.</summary>
    public static void Reference(ref int total) => total += 1;

    /// <summary>15.6.2.3.4: an output parameter — passed by reference, need not be initialised
    /// by the caller, and must be assigned before the method returns normally.</summary>
    public static bool Output(int seed, out int doubled, out string label)
    {
        doubled = seed * 2;
        label = doubled.ToString();
        return doubled > 0;
    }

    /// <summary>15.6.2.3.1: `scoped` restricts the lifetime of a by-reference parameter, which
    /// is a property of the parameter and not of the type it names.</summary>
    public static int Scoped(scoped ref int total) => total;

    /// <summary>15.6.2.3.1: a by-reference parameter of a generic type, so the modifier and the
    /// type parameter appear together.</summary>
    public static void ReferenceOfT<TValue>(ref TValue slot, TValue value) => slot = value;

    /// <summary>15.6.2.4: a parameter array. One declaration, invoked below both in expanded
    /// form and with a single array argument.</summary>
    public static int SumArray(params int[] values)
    {
        var total = 0;

        foreach (var value in values)
        {
            total += value;
        }

        return total;
    }

    /// <summary>15.6.2.4: a `params` span — a parameter collection, which is the same clause
    /// with a type that is not an array.</summary>
    public static int SumSpan(params ReadOnlySpan<int> values)
    {
        var total = 0;

        foreach (var value in values)
        {
            total += value;
        }

        return total;
    }

    /// <summary>15.6.2.4: a `params` list, so three spellings of the clause exist side by side
    /// with three different parameter types.</summary>
    public static int SumList(params System.Collections.Generic.List<int> values) => values.Count;

    /// <summary>15.6.2.4: fixed parameters ahead of the parameter array, which is the only place
    /// the array may appear.</summary>
    public static string Join(string separator, params string[] parts) => string.Join(separator, parts);

    /// <summary>15.6.2.1: every parameter form above, called. Each by-reference call site carries
    /// the modifier too, so a reference from an argument is distinguishable from a value one.</summary>
    public static string UseAll()
    {
        var total = 1;
        Reference(ref total);
        _ = Input(in total);
        _ = RefReadonlyInput(in total);
        _ = Scoped(ref total);
        ReferenceOfT(ref total, 9);
        var ok = Output(total, out var doubled, out var label);
        return $"{None()} {Value(total)} {ValueOfReferenceType(label)} {Defaulted()} {ok} {doubled} " +
               $"{SumArray(1, 2, 3)} {SumArray([4, 5])} {SumSpan(6, 7)} {SumList([8])} {Join("-", "a", "b")}";
    }
}

/// <summary>15.6.2.3.1: the one pair of parameter-modifier overloads a single type may declare —
/// by value beside by reference. Adding `Load(out int)` or `Load(in int)` here is CS0663.</summary>
public class MemParameterModifiers
{
    /// <summary>15.6.2.2: the by-value half.</summary>
    public int Load(int slot) => slot;

    /// <summary>15.6.2.3.3: the by-reference half. Same name, same parameter type, same arity —
    /// the modifier is the whole of the difference.</summary>
    public int Load(ref int slot) => slot += 1;

    /// <summary>15.6.2.3.1: both halves, selected by the call site's modifier.</summary>
    public int Both()
    {
        var slot = 1;
        return Load(slot) + Load(ref slot);
    }
}
