// Clause 21.2 — delegate declarations. A `delegate-declaration` is attributes, modifiers,
// the `delegate` keyword, a return type, an identifier, an optional variant type parameter
// list, a parameter list, optional constraint clauses, and a semicolon. The identifier names
// a type; the parameter list and return type are the *signature* of a method that the
// declaration does not declare. Every form the grammar allows appears below once.

namespace Surface.EnumsDelegates;

/// <summary>21.2 — parameters and a return type, the ordinary case.</summary>
public delegate int EdCombine(int left, int right);

/// <summary>21.2 — a return type that is a class type rather than a primitive.</summary>
public delegate object EdMakeObject();

/// <summary>
/// 21.2 — a generic delegate with two type parameters, one used as a parameter type and one
/// as the return type.
/// </summary>
public delegate TOut EdMapper<TIn, TOut>(TIn value);

/// <summary>
/// 21.2 — a generic delegate with a constraint clause. The `new()` constraint sits after the
/// parameter list, which is the one place a delegate declaration has a clause at all.
/// </summary>
public delegate T EdFactory<T>()
    where T : new();

/// <summary>21.2 — a generic delegate with two constraints on one type parameter.</summary>
public delegate void EdConstrained<T>(T item)
    where T : class, System.IComparable<T>;

/// <summary>
/// 21.2 hazard — a contravariant type parameter. `in` is a modifier on the type parameter,
/// and it is the only thing that distinguishes this declaration from an invariant one; an
/// identity that drops it makes <c>EdSink&lt;T&gt;</c> indistinguishable from a delegate
/// that has none of its conversions.
/// </summary>
public delegate void EdSink<in T>(T item);

/// <summary>21.2 hazard — a covariant type parameter, the mirror of the one above.</summary>
public delegate T EdSource<out T>();

/// <summary>
/// 21.2 — one declaration with a variance annotation of each kind, which is what makes a
/// conversion between two of its constructed forms possible in both positions at once.
/// </summary>
public delegate TOut EdVariantMapper<in TIn, out TOut>(TIn value);

/// <summary>21.2 — a variadic parameter list, whose last parameter is a parameter array.</summary>
public delegate void EdLogger(string format, params object?[] args);

/// <summary>21.2 — an output parameter, which a compatible method must also declare `out`.</summary>
public delegate bool EdTryParse(string text, out int value);

/// <summary>21.2 — a by-reference parameter.</summary>
public delegate void EdMutate(ref int slot);

/// <summary>21.2 — a read-only by-reference parameter.</summary>
public delegate double EdScale(in double factor);

/// <summary>21.2 — a by-reference *return*, so an invocation of it is a variable reference.</summary>
public delegate ref int EdPick(int[] slots);

/// <summary>21.2 — a parameter with a default argument, which the invocation may omit.</summary>
public delegate void EdDefaulted(int retries = 3);

/// <summary>21.2 — a delegate whose return type is another delegate type.</summary>
public delegate EdCombine EdChooser(bool preferSum);

/// <summary>21.2 — the delegate type the events in DelegateEvents.cs are declared with.</summary>
public delegate void EdChanged(object source, string field);

/// <summary>
/// 21.2 hazard — the left of two delegate types with identical signatures. Nothing about
/// <c>void(string)</c> distinguishes this declaration from <see cref="EdNotifyRight"/>, so an
/// identity keyed on the signature merges two unrelated types — and the language keeps them
/// apart hard enough that no implicit conversion exists between them.
/// </summary>
public delegate void EdNotifyLeft(string message);

/// <summary>21.2 hazard — the right of that pair, signature for signature.</summary>
public delegate void EdNotifyRight(string message);

/// <summary>
/// 21.2 — attributes on a delegate declaration, on a parameter, and on the return type.
/// Referenced by nothing, so the obsoletion warns at no site.
/// </summary>
[System.Obsolete("kept for the census, called by nobody")]
[return: System.Diagnostics.CodeAnalysis.MaybeNull]
public delegate string EdRetired(
    [System.Diagnostics.CodeAnalysis.AllowNull] string input);

/// <summary>21.2 — no modifier, so this delegate type is `internal` by rule.</summary>
delegate void EdDeclImplicit();

/// <summary>21.2 — a delegate nested in a class, so its container is a type.</summary>
public class EdDelegateBase
{
    /// <summary>21.2 — the nested delegate declaration.</summary>
    public delegate void EdNestedClassDelegate(int value);

    /// <summary>21.2 — a private nested delegate, and a member typed by it.</summary>
    private delegate void EdNestedPrivate();

    /// <summary>21.2 — a protected nested delegate.</summary>
    protected delegate void EdNestedProtected();

    /// <summary>21.2 — the delegate the derived class hides with `new`.</summary>
    public delegate void EdHiddenDelegate();

    private EdNestedPrivate? _hidden;

    /// <summary>21.2 — a use of the private nested delegate type.</summary>
    public void Arm(System.Action action) => _hidden = new EdNestedPrivate(action.Invoke);

    /// <summary>21.2 — and a read of it, so the field is not merely assigned.</summary>
    public bool Armed => _hidden is not null;
}

/// <summary>
/// 21.2 hazard — `new` on a nested delegate declaration, hiding
/// <see cref="EdDelegateBase.EdHiddenDelegate"/>. Two delegate types carry one simple name in
/// two types related by inheritance, and their signatures differ.
/// </summary>
public class EdDelegateDerived : EdDelegateBase
{
    /// <summary>21.2 — the hiding declaration, whose signature is not the hidden one's.</summary>
    public new delegate void EdHiddenDelegate(int value);

    /// <summary>21.2 — the hidden type, still reachable through the base type's name.</summary>
    public static EdDelegateBase.EdHiddenDelegate Base(System.Action action) =>
        new EdDelegateBase.EdHiddenDelegate(action.Invoke);

    /// <summary>21.2 — the hiding type, reached by the simple name inside the derived class.</summary>
    public static EdHiddenDelegate Derived() => static value => { };
}

/// <summary>21.2 — a delegate nested in a struct.</summary>
public readonly struct EdDelegateStructHost
{
    /// <summary>21.2 — the nested delegate declaration, inside a value type.</summary>
    public delegate void EdNestedStructDelegate(in double factor);
}

/// <summary>21.2 — a delegate nested in an interface, which the language permits.</summary>
public interface IEdDelegateHost
{
    /// <summary>21.2 — the nested delegate declaration, inside an interface.</summary>
    delegate void EdNestedInterfaceDelegate(string message);

    /// <summary>21.2 — an interface member typed by the delegate nested beside it.</summary>
    EdNestedInterfaceDelegate Listener { get; }
}
