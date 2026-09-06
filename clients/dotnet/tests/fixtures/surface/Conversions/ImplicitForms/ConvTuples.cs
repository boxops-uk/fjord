namespace Surface.Conversions.ImplicitForms;

/// <summary>
/// Clauses 10.2.13 and 10.3.6 — the tuple conversions, which are element-wise: a tuple
/// converts when every element does, implicitly or explicitly, and the element names are not
/// part of the type at all.
///
/// The deliberate hazard is that last part. The three fields at the bottom declare one type
/// three ways.
/// </summary>
public static class ConvTuples
{
    /// <summary>Clause 10.2.13 — element-wise implicit numeric widening.</summary>
    public static (long, double) WidenElements((int, float) pair) => pair;

    /// <summary>Clause 10.2.13 — the same tuple type, gaining element names.</summary>
    public static (int Left, int Right) NameElements((int, int) pair) => pair;

    /// <summary>Clause 10.2.13 — element-wise boxing and reference conversion.</summary>
    public static (object, object) BoxElements((int, string) pair) => pair;

    /// <summary>Clause 10.2.13 — the tuple conversion under a nullable conversion.</summary>
    public static (long, double)? ToNullableTuple((int, float) pair) => pair;

    /// <summary>Clause 10.2.13 — a nested tuple, converted at both levels at once.</summary>
    public static ((long, double) Inner, object Outer) NestWiden(((int, float) Inner, string Outer) value) => value;

    /// <summary>Clause 10.2.13 — an element-wise reference conversion up the hierarchy.</summary>
    public static (ConvNode, IConvLabelled) WidenReferences((ConvLeaf, ConvLeaf) pair) => pair;

    /// <summary>Clause 10.2.13 — a three-element tuple, so arity is not always two.</summary>
    public static (long, long, long) WidenTriple((int, short, byte) triple) => triple;

    /// <summary>Clause 10.3.6 — element-wise explicit numeric narrowing.</summary>
    public static (int, float) NarrowElements((long, double) pair) => ((int, float))pair;

    /// <summary>Clause 10.3.6 — narrowing, then picking up names on the way out.</summary>
    public static (int First, int Second) NarrowAndName((long, long) pair) => ((int, int))pair;

    /// <summary>Clause 10.3.6 — an element-wise reference downcast.</summary>
    public static (ConvLeaf, int) NarrowReferences((ConvNode, long) pair) => ((ConvLeaf, int))pair;

    /// <summary>Clause 10.3.6 — an element-wise unboxing conversion.</summary>
    public static (int, ConvStroke) UnboxElements((object, object) pair) => ((int, ConvStroke))pair;

    /// <summary>Clause 10.3.6 — the explicit form under a nullable conversion.</summary>
    public static (int, int)? NarrowNullableTuple((long, long)? pair) => ((int, int)?)pair;

    // Clauses 10.2.13 and 10.3.6, deliberate hazard: one type, three spellings.

    /// <summary>A pair with names.</summary>
    public static readonly (int Left, int Right) NamedPair = (1, 2);

    /// <summary>The same value under different names — an identity conversion, not a tuple one.</summary>
    public static readonly (int First, int Second) RenamedPair = NamedPair;

    /// <summary>And with no names at all.</summary>
    public static readonly (int, int) UnnamedPair = NamedPair;
}
