// Clause 21.3 — delegate members. Every delegate type has exactly the members it inherits
// from System.MulticastDelegate and System.Delegate, plus a constructor and an `Invoke`
// method whose signature is the one the declaration wrote — and, where the framework
// supports the pattern, the `BeginInvoke`/`EndInvoke` pair. None of these is written in any
// source file, so every reference below is to a declaration the compiler synthesized from
// the single `delegate` line in DelegateDeclarations.cs.

namespace Surface.EnumsDelegates;

/// <summary>
/// 21.3 — the synthesized and inherited members of a delegate type, each named explicitly at
/// one site so that a query has a use to attribute.
/// </summary>
public static class EdDelegateMembers
{
    /// <summary>21.3 — the method the delegates in this file are built over.</summary>
    public static int Add(int left, int right) => left + right;

    /// <summary>
    /// 21.3 hazard — <c>Invoke</c> named in the source. Its declaration exists only in the
    /// delegate type, so this reference and the bare <c>combine(2, 3)</c> in
    /// DelegateInvocation.cs must resolve to one member of <see cref="EdCombine"/>.
    /// </summary>
    public static int Explicitly(EdCombine combine) => combine.Invoke(2, 3);

    /// <summary>21.3 — <c>BeginInvoke</c>, whose two trailing parameters the declaration never wrote.</summary>
    public static System.IAsyncResult Begin(EdCombine combine) =>
        combine.BeginInvoke(1, 2, null, null);

    /// <summary>21.3 — <c>EndInvoke</c>, whose return type is the delegate's return type.</summary>
    public static int End(EdCombine combine, System.IAsyncResult result) =>
        combine.EndInvoke(result);

    /// <summary>21.3 — <c>Method</c>, inherited from System.Delegate.</summary>
    public static System.Reflection.MethodInfo Which(EdCombine combine) => combine.Method;

    /// <summary>21.3 — <c>Target</c>, which is null for a delegate over a static method.</summary>
    public static object? Bound(EdCombine combine) => combine.Target;

    /// <summary>21.3 — <c>GetInvocationList</c>, from System.MulticastDelegate.</summary>
    public static System.Delegate[] List(EdCombine combine) => combine.GetInvocationList();

    /// <summary>21.3 — <c>DynamicInvoke</c>, which takes the arguments as boxed objects.</summary>
    public static object? Dynamically(EdCombine combine) => combine.DynamicInvoke(4, 5);

    /// <summary>21.3 — <c>Clone</c>, inherited through System.Delegate from System.Object.</summary>
    public static object Copy(EdCombine combine) => combine.Clone();

    /// <summary>21.3 — <c>Equals</c>, which System.Delegate overrides to compare targets.</summary>
    public static bool SameTarget(EdCombine left, EdCombine right) => left.Equals(right);

    /// <summary>21.3 — the static <c>Combine</c>, which the `+` operator is defined in terms of.</summary>
    public static System.Delegate? Combined(EdCombine left, EdCombine right) =>
        System.Delegate.Combine(left, right);

    /// <summary>21.3 — the static <c>Remove</c>, which `-` is defined in terms of.</summary>
    public static System.Delegate? Removed(EdCombine left, EdCombine right) =>
        System.Delegate.Remove(left, right);

    /// <summary>
    /// 21.3 — <c>CreateDelegate</c>, which builds a delegate from a type object and a method
    /// info rather than from a method group, so no conversion is involved and the target is
    /// named by a string.
    /// </summary>
    public static System.Delegate Reflected() =>
        System.Delegate.CreateDelegate(
            typeof(EdCombine),
            typeof(EdDelegateMembers).GetMethod(nameof(Add))!);

    /// <summary>21.3 — the delegate type itself as a value, through <c>typeof</c>.</summary>
    public static System.Type Closed() => typeof(EdCombine);

    /// <summary>
    /// 21.3 hazard — the unbound generic delegate type. <c>typeof(EdMapper&lt;,&gt;)</c> names
    /// the same declaration <c>EdMapper&lt;int, string&gt;</c> constructs from, with its type
    /// arguments absent rather than inferred.
    /// </summary>
    public static System.Type Unbound() => typeof(EdMapper<,>);

    /// <summary>21.3 — the constructed form of the same declaration.</summary>
    public static System.Type Constructed() => typeof(EdMapper<int, string>);

    /// <summary>21.3 — the base classes a delegate type inherits from, named explicitly.</summary>
    public static System.MulticastDelegate AsMulticast(EdCombine combine) => combine;

    /// <summary>21.3 — and the base of that, which is where Method and Target are declared.</summary>
    public static System.Delegate AsDelegate(EdCombine combine) => combine;
}
