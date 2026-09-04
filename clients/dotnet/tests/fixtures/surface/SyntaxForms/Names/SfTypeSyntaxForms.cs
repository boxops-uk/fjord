using System;
using Surface.SyntaxForms.Declarations;

namespace Surface.SyntaxForms.Names;

// The type-syntax kinds that wrap another type rather than name one: `T[]`, `T?`, `T*`,
// `delegate*<…>`, and `(…)`. None of them is a `SimpleNameSyntax`, and the symbol each
// denotes — `SymbolKind.ArrayType`, `SymbolKind.PointerType`,
// `SymbolKind.FunctionPointerType`, a constructed `ValueTuple` — is a symbol no name in
// source refers to directly. What a `SimpleNameSyntax` dispatch sees here is the *element*
// type, and only when the element is a name: `SfLedgerClass[]` yields a reference to
// `SfLedgerClass`, while `int[]` yields nothing at all.

/// <summary>Each wrapping type form, in a position where the compiler must construct it.</summary>
public sealed unsafe class SfTypeSyntaxForms
{
    /// <summary>ArrayType — one dimension, over a named element type.</summary>
    public SfLedgerClass[] Named = new SfLedgerClass[2];

    /// <summary>ArrayType — one dimension, over a predefined element type.</summary>
    public int[] Predefined = new int[2];

    /// <summary>ArrayType — rank two, and an array of arrays, so the rank rule is visible.</summary>
    public int[,][] Ragged = new int[2, 2][];

    /// <summary>NullableType over a value type, which is <c>Nullable&lt;int&gt;</c>.</summary>
    public int? MaybeCount;

    /// <summary>NullableType over a reference type, which is an annotation and not a construction.</summary>
    public string? MaybeName;

    /// <summary>PointerType — a pointer to a predefined type.</summary>
    public int* Cell;

    /// <summary>PointerType — a pointer to a pointer, and a <c>void*</c>.</summary>
    public void** Handle;

    /// <summary>FunctionPointerType — a managed function pointer with two parameters.</summary>
    public delegate*<int, int, int> Combine;

    /// <summary>FunctionPointerType with a calling convention and a named parameter type.</summary>
    public delegate* unmanaged[Cdecl]<int, void> Notify;

    /// <summary>TupleType with TupleElement names, used once so no second syntax claims the same fields.</summary>
    public (int Width, int Height) Extent;

    /// <summary>TupleType whose elements are unnamed, so the fields are only <c>Item1</c> and <c>Item2</c>.</summary>
    public (string, int) Anonymous;

    /// <summary>
    /// TupleExpression — a tuple literal, which is a construction of a
    /// <c>ValueTuple</c> and, where its elements are named, a declaration of that
    /// construction's fields.
    /// </summary>
    /// <returns>A named tuple literal.</returns>
    public (string Key, int Count) Literal()
    {
        // Argument with a NameColon: `Key:` and `Count:` each declare a field on the
        // constructed `ValueTuple<string, int>`, and the identifier in the `NameColon` is an
        // `IdentifierNameSyntax` — so a reference dispatch sees a use of a field whose
        // declaration it never wrote.
        return (Key: "extent", Count: Extent.Width * Extent.Height);
    }

    /// <summary>A second tuple literal, with a different element-name set.</summary>
    /// <returns>A pair naming its own halves.</returns>
    public (bool Ready, string Reason) Verdict()
    {
        var unnamed = (Anonymous.Item1, Anonymous.Item2);
        return (Ready: unnamed.Item2 > 0, Reason: unnamed.Item1 ?? "none");
    }

    /// <summary>Uses the pointer and function-pointer fields so none of them is dead.</summary>
    /// <returns>A rendering of what they hold.</returns>
    public string Reach()
    {
        int local = 3;
        Cell = &local;
        return $"{*Cell} {(nint)Handle} {(nint)Combine} {(nint)Notify} {MaybeCount} {MaybeName} {Named.Length} {Predefined.Length} {Ragged.Length}";
    }
}
