using System;
using System.Collections.Generic;

namespace Surface.Synthesised;

/// <summary><c>TypeKind.Class</c> — the abstract/sealed/static spread (15.2.2).</summary>
public abstract class SynClassKind
{
    /// <summary>An abstract member, so the class must be abstract.</summary>
    /// <returns>A label.</returns>
    public abstract string Label();

    /// <summary>A concrete nested class, sealed.</summary>
    public sealed class SynClassLeaf : SynClassKind
    {
        /// <inheritdoc/>
        public override string Label() => "leaf";
    }
}

/// <summary><c>TypeKind.Interface</c>, with a base interface list (18.2.4).</summary>
public interface ISynMarker
{
}

/// <summary>An interface deriving from two others.</summary>
public interface ISynDescribed : ISynMarker, ISynNamed
{
    /// <summary>A description.</summary>
    string Description { get; }
}

/// <summary><c>TypeKind.Struct</c> in each of its spellings (16.2).</summary>
/// <remarks>
/// <c>TypeKind.Structure</c> is the same value as <c>TypeKind.Struct</c> — Roslyn keeps the
/// Visual Basic spelling as an alias — so an index that stores the kind's <em>name</em>
/// stores whichever of the two <c>Enum.ToString</c> returns, and a query written against
/// the other name finds nothing. Nothing in C# source can distinguish them.
/// </remarks>
public readonly struct SynStructKind
{
    /// <summary>The one field.</summary>
    public readonly int Weight;

    /// <summary>Constructs.</summary>
    /// <param name="weight">How much.</param>
    public SynStructKind(int weight) => Weight = weight;
}

/// <summary>A <c>ref struct</c> that implements an interface (C# 13).</summary>
public ref struct SynRefStruct : ISynMarker
{
    /// <summary>A ref field (C# 11), which only a ref struct may hold.</summary>
    public ref int Slot;

    /// <summary>Constructs over a reference.</summary>
    /// <param name="slot">Where to point.</param>
    public SynRefStruct(ref int slot) => Slot = ref slot;
}

/// <summary>An inline array (C# 12), a struct whose length lives in an attribute.</summary>
[System.Runtime.CompilerServices.InlineArray(4)]
public struct SynInlineArray
{
    private int _element0;
}

/// <summary><c>TypeKind.Enum</c>, with an explicit underlying type (19.3).</summary>
public enum SynEnumKind : long
{
    /// <summary>The default.</summary>
    Unset = 0,

    /// <summary>Set.</summary>
    Set = 1L,
}

/// <summary><c>TypeKind.Delegate</c>, generic and constrained (21.2).</summary>
/// <typeparam name="TInput">What goes in.</typeparam>
/// <typeparam name="TOutput">What comes out.</typeparam>
/// <param name="input">The input.</param>
/// <returns>The output.</returns>
public delegate TOutput SynProjection<in TInput, out TOutput>(TInput input)
    where TInput : notnull;

/// <summary>
/// The type kinds that only ever appear as a <em>reference</em>: array, pointer, function
/// pointer, dynamic and type parameter (8.2, 23.3, 23.5).
/// </summary>
/// <remarks>
/// None of these has a declaration anywhere. <c>int[]</c> is a
/// <c>SymbolKind.ArrayType</c> the compiler constructs on demand, with no location and no
/// containing namespace; so are <c>int*</c> (<c>SymbolKind.PointerType</c>) and
/// <c>delegate*&lt;int, int&gt;</c> (<c>SymbolKind.FunctionPointerType</c>). Each is
/// reachable only through the type-syntax node that names it, and two syntactically
/// identical nodes in different files denote the <em>same</em> symbol — so an index keyed
/// on the symbol has one row reached from many spans, and an index keyed on the span has
/// many rows for one type.
///
/// A function pointer type also carries a synthesised method: its signature, with
/// <c>MethodKind.FunctionPointerSignature</c>, named <c>Invoke</c>, belonging to a type
/// that belongs to no assembly's declaration table.
///
/// <c>dynamic</c> is <c>SymbolKind.DynamicType</c> / <c>TypeKind.Dynamic</c>: one symbol
/// for the whole compilation, denoted by an identifier that is a contextual keyword — so
/// every <c>dynamic</c> in the corpus resolves to one row, and the identifier is not a
/// reference to any declaration.
/// </remarks>
public static unsafe class SynTypeKinds
{
    /// <summary>Arrays: single-rank, multi-rank and jagged (17.1).</summary>
    /// <returns>A digest of the lengths.</returns>
    public static string Arrays()
    {
        int[] single = [1, 2, 3];
        int[,] rectangular = new int[2, 3];
        int[,,] cubic = new int[2, 2, 2];
        int[][] jagged = [[1], [2, 3]];
        SynEntry[] ofNamedType = [new SynEntry("alpha", 1)];
        int[]?[] ofNullableArrays = new int[2][];

        rectangular[1, 2] = 4;
        cubic[1, 1, 1] = 5;

        return $"{single.Length}{rectangular.Length}{cubic.Length}{jagged.Length}" +
            $"{ofNamedType.Length}{ofNullableArrays.Length}";
    }

    /// <summary>Pointers, and the constructs that only pointers reach (23.3, 23.6, 23.8).</summary>
    /// <returns>A digest.</returns>
    public static string Pointers()
    {
        var cells = new int[4];

        fixed (int* head = cells)
        {
            int* second = head + 1;
            *second = 7;
            void* untyped = head;
            int** indirect = &second;

            var read = *(*indirect);
            var reached = second->CompareTo(0);
            var sized = sizeof(SynStructKind);

            int* stacked = stackalloc int[3];
            stacked[0] = 1;

            return $"{read}{reached}{sized}{stacked[0]}{untyped is null}";
        }
    }

    /// <summary>Function pointers, managed and unmanaged (C# 9).</summary>
    /// <returns>A digest.</returns>
    public static string FunctionPointers()
    {
        delegate*<int, int> managed = &Twice;
        delegate* managed<int, int, int> explicitly = &Add;
        delegate* unmanaged[Cdecl]<int, int> unmanaged = null;

        var doubled = managed(4);
        var summed = explicitly(1, 2);

        return $"{doubled}{summed}{unmanaged is null}";
    }

    /// <summary><c>dynamic</c> — declared, converted, invoked and constructed (8.2.4).</summary>
    /// <returns>A digest.</returns>
    public static string Dynamic()
    {
        dynamic loose = 1;
        dynamic[] ofDynamic = [1, "two"];
        List<dynamic> inGeneric = [3];

        loose = loose + 1;
        object narrowed = loose;
        dynamic widened = narrowed;

        return $"{loose}{ofDynamic.Length}{inGeneric.Count}{widened}";
    }

    /// <summary>A type parameter used as a type (<c>TypeKind.TypeParameter</c>).</summary>
    /// <typeparam name="T">Anything unmanaged, so it can also be pointed at.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>A digest.</returns>
    public static string TypeParameterAsType<T>(T value)
        where T : unmanaged
    {
        T[] ofT = [value];
        T? nullable = value;
        Func<T, T> projecting = static v => v;
        T* pointing = stackalloc T[1];
        pointing[0] = value;

        return $"{ofT.Length}{nullable}{projecting(value)}{pointing[0]}";
    }

    private static int Twice(int value) => value * 2;

    private static int Add(int left, int right) => left + right;
}

/// <summary>Nullable types, which are a constructed named type rather than a kind (8.3.12).</summary>
public class SynNullableShapes
{
    /// <summary>A nullable value type, which is <c>Nullable&lt;int&gt;</c>.</summary>
    public int? Count { get; set; }

    /// <summary>A nullable reference type, which is the same type with an annotation.</summary>
    public string? Label { get; set; }

    /// <summary>A nullable of the project's own struct.</summary>
    public SynStructKind? Weight { get; set; }
}
