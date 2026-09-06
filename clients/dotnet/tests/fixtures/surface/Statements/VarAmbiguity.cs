// Clause 13.6.2.1 — the `var` ambiguity, which needs a namespace to itself.
//
// `var` is not a keyword. 13.6.2.1 resolves the ambiguity by rule: if a type named `var` is
// in scope, `var x = e;` is an *explicitly* typed declaration of that type, and implicit
// typing is unavailable for the rest of that scope. So the specimen cannot share a namespace
// with the rest of this project — declaring `var` in `Surface.Statements` would change the
// meaning of every implicitly typed local in every other file, and most of them would stop
// compiling.
//
// It therefore lives in `Surface.Statements.VarAmbiguity`, which no other file opens. A name
// in an enclosing namespace is in scope in the nested ones and not the other way around, so
// this file can see the whole project and the project cannot see this type.

namespace Surface.Statements.VarAmbiguity;

/// <summary>
/// A type actually named <c>var</c>. CS8981 warns that a lowercase type name may become a
/// keyword; the warning is the point, because it is the compiler saying that this declaration
/// changes what other declarations mean.
/// </summary>
public sealed class var
{
    /// <summary>How deep the box is.</summary>
    public int Depth { get; set; }

    /// <summary>Reads the depth back.</summary>
    /// <returns>The depth.</returns>
    public int Measure() => Depth;
}

/// <summary>Clause 13.6.2.1 — what <c>var</c> means when <c>var</c> is a type.</summary>
public static class StmtVarAmbiguity
{
    /// <summary>
    /// A declaration statement that looks implicitly typed and is not.
    /// </summary>
    /// <remarks>
    /// 13.6.2.1. `var probe = new var();` declares a local of type
    /// <see cref="VarAmbiguity.var"/>. The identifier `var` in the type position is a
    /// reference to the class above — a reference edge that an index reading `var` as a
    /// keyword will not record, and the only place in this project where the token has a
    /// declaration to point at.
    /// </remarks>
    /// <param name="depth">The depth to box.</param>
    /// <returns>The depth, read back through the box.</returns>
    public static int Boxed(int depth)
    {
        var probe = new var();
        probe.Depth = depth;

        // 13.6.2.1 again, with the type written twice, which is what the statement above
        // means. The two declarations are the same declaration form spelled two ways.
        var explicitProbe = new var { Depth = depth + 1 };

        return probe.Measure() + explicitProbe.Measure();
    }

    /// <summary>
    /// The consequence, recorded because it cannot be written: with <c>var</c> in scope as a
    /// type, <c>var count = 1;</c> is CS0029 — cannot convert <c>int</c> to <c>var</c> — and
    /// implicit typing is simply gone from this namespace. Every local here is therefore
    /// explicitly typed.
    /// </summary>
    /// <param name="depth">A number to count to.</param>
    /// <returns>The count.</returns>
    public static int NoImplicitTypingHere(int depth)
    {
        int count = 0;

        for (int i = 0; i < depth; i++)
        {
            count += i;
        }

        return count;
    }
}
