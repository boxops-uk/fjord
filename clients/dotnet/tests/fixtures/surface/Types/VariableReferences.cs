// Clauses 9.5 and 9.6 — variable references, and the atomicity of them. A variable
// reference is an expression classified as a variable, which is what `ref` and `out`
// arguments require and what an assignment's left operand must be. The same syntax can be
// a variable reference in one place and a value in another, and that is this file's point.

namespace Surface.Types;

/// <summary>
/// 9.5 hazard — the same member access spelling, once through a field and once through a
/// property. <c>Anchor.X</c> is a variable reference and can be assigned and passed by
/// <c>ref</c>; <c>Copy.X</c> is a value, and only the declaration of what precedes the dot
/// says which is which.
/// </summary>
public sealed class VarReferenceClassification
{
    /// <summary>9.5 — a field of struct type, so an access through it is a variable.</summary>
    public TyPoint2D Anchor;

    /// <summary>9.5 — a second one, so there is something to swap with.</summary>
    public TyPoint2D Mirror;

    /// <summary>
    /// 9.5 hazard — an automatically implemented property of the same struct type. Reading
    /// <c>Copy.X</c> is legal; assigning to it is not, because a property access is a value.
    /// </summary>
    public TyPoint2D Copy { get; set; }

    /// <summary>9.5 — assignment through a field access, which needs a variable reference.</summary>
    public void MoveAnchor(double x)
    {
        Anchor.X = x;
    }

    /// <summary>9.5 — reading through the property access, which is a value and so read-only
    /// in place. Assigning `Copy.X` here would be an error, and that is the clause.</summary>
    public double ReadCopy() => Copy.X;

    /// <summary>9.5 — the property assigned whole, which is what a value permits.</summary>
    public void ReplaceCopy(TyPoint2D point) => Copy = point;

    /// <summary>9.5 — passing fields as `ref` arguments, which only a variable reference allows.</summary>
    public void SwapAnchors() => VarReferenceParameters.Swap(ref Anchor, ref Mirror);

    /// <summary>9.5 — passing an array element and a static variable by reference, both of
    /// which are variable references too.</summary>
    public static int ReferenceEachCategory()
    {
        int local = 1;
        int fromLocal = VarReferenceParameters.Take(ref local);
        int fromElement = VarReferenceParameters.Take(ref VarArrayElements.Vector[0]);
        int fromStatic = VarReferenceParameters.Take(ref VarStaticsBase.Tally);
        return fromLocal + fromElement + fromStatic;
    }

    /// <summary>9.5 — an `out` argument, which requires a variable reference it need not
    /// have assigned yet.</summary>
    public static int OutRequiresAVariable()
    {
        int slot;
        VarOutputParameters.Fill(out slot);
        return slot;
    }

    /// <summary>9.5 — a property is a value, so it reaches an `in` parameter through an
    /// unnamed temporary rather than as a variable reference (9.7.2.7).</summary>
    public double MeasureCopy() => VarInputParameters.Measure(Copy);
}

/// <summary>
/// 9.6 — atomicity. Reads and writes of the types the clause lists are atomic; those of
/// larger types are not, and the clause offers no syntax either way. What is written here is
/// the pair of variables the guarantee separates.
/// </summary>
public static class VarAtomicity
{
    /// <summary>9.6 — a variable of a type whose reads and writes are atomic.</summary>
    public static int Atomic;

    /// <summary>9.6 — a variable of a type whose reads and writes are not: a struct wider
    /// than a native word.</summary>
    public static TyPoint2D NotAtomic;

    /// <summary>9.6 — a long, which the clause does not promise on every platform.</summary>
    public static long Wide;

    /// <summary>9.6 — the interlocked operations, which promise what the clause does not.</summary>
    public static long BumpWide() => System.Threading.Interlocked.Increment(ref Wide);

    /// <summary>9.6 — a volatile read and write, which is about ordering rather than atomicity.</summary>
    public static int ReadOrdered() => System.Threading.Volatile.Read(ref Atomic);
}
