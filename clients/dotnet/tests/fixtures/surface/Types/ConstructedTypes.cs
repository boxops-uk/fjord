// Clauses 8.4 and 8.5 — constructed types and type parameters. 8.4.1 states what a
// constructed type is, 8.4.2 what its type arguments are, 8.4.3 the open/closed division,
// 8.4.4 the bound/unbound one, and 8.4.5 the constraints a type argument must satisfy.
// Every generic declaration in this project appears at exactly one arity: the pair
// `TyPair` beside `TyPair<T>` is the arity collision, and it lives in a quarantine project.

namespace Surface.Types;

/// <summary>
/// 8.4.1 — a generic class declaration. Every reference to it below is a constructed type,
/// and they all point back to this one declaration.
/// </summary>
/// <typeparam name="TFirst">8.5 — the first type parameter.</typeparam>
/// <typeparam name="TSecond">8.5 — the second.</typeparam>
public class TyPair<TFirst, TSecond>
{
    /// <summary>8.5 — a field whose type is a type parameter.</summary>
    public TFirst? First;

    public TSecond? Second;

    /// <summary>
    /// 8.4.3 — an open type: this field's type is the enclosing declaration written with its
    /// own type parameters as arguments, which is the same spelling as the declaration itself.
    /// </summary>
    public TyPair<TFirst, TSecond>? Next;

    /// <summary>8.4.3 — a closed type inside the same open declaration.</summary>
    public TyPair<int, int>? ClosedNeighbour;

    /// <summary>
    /// 8.4.1 hazard — a constructed type built from this declaration with the arguments in
    /// the other order. One declaration, two constructed types, and the reverse of this
    /// method's own type is its own return type.
    /// </summary>
    public TyPair<TSecond, TFirst> Flip() => new() { First = Second, Second = First };

    /// <summary>8.4.2 — a method type parameter used as a type argument to the enclosing type.</summary>
    public TyPair<TFirst, TThird> Retype<TThird>(TThird third) =>
        new() { First = First, Second = third };

    /// <summary>8.4.1 — a non-generic type nested in a generic one. Its identity carries the
    /// outer type's arguments even though it declares none of its own.</summary>
    public sealed class TyPairCursor
    {
        public TyPair<TFirst, TSecond>? Current;
    }

    /// <summary>8.4.2 — a generic type nested in a generic one, so two argument lists meet.</summary>
    public sealed class TyPairTagged<TTag>
    {
        public TTag? Tag;

        public TyPair<TFirst, TSecond>? Tagged;
    }
}

/// <summary>8.4.1 — a generic struct, so constructed value types exist too.</summary>
public struct TyCell<TValue>
{
    public TValue? Content;

    public TyCell(TValue content) => Content = content;
}

/// <summary>
/// 8.4.1 hazard — the same declaration constructed several ways. Every field's type is a
/// distinct type; every field's type reference is to one declaration.
/// </summary>
public static class TyConstructedUse
{
    /// <summary>8.4.1 — a closed constructed type.</summary>
    public static TyPair<int, string>? IntThenString;

    /// <summary>8.4.1 hazard — the same declaration with the arguments swapped.</summary>
    public static TyPair<string, int>? StringThenInt;

    /// <summary>8.4.2 hazard — a type argument that is itself a constructed type.</summary>
    public static TyPair<TyPair<int, int>, string>? Nested;

    /// <summary>8.4.2 — a type argument that is an array type, and one that is a tuple type.</summary>
    public static TyPair<int[], (int Row, int Column)>? Shaped;

    /// <summary>8.4.2 — a type argument that is a nullable value type, and one that is nullable
    /// reference (8.9.3).</summary>
    public static TyPair<int?, string?>? Optional;

    /// <summary>8.4.1 — the nested types of a constructed type, spelled through it.</summary>
    public static TyPair<int, string>.TyPairCursor? Cursor;

    public static TyPair<int, string>.TyPairTagged<bool>? Tagged;

    /// <summary>8.4.1 — a constructed value type.</summary>
    public static TyCell<double> Measure = new(1.5);

    /// <summary>
    /// 8.4.4 hazard — the unbound generic type. <c>TyPair&lt;,&gt;</c> names the declaration
    /// with no arguments at all, and it is legal only inside <c>typeof</c>.
    /// </summary>
    public static readonly System.Type Unbound = typeof(TyPair<,>);

    /// <summary>8.4.4 hazard — and the bound form of the same declaration, beside it.</summary>
    public static readonly System.Type Bound = typeof(TyPair<int, string>);

    /// <summary>8.4.4 — the unbound form of a nested generic type needs both arities.</summary>
    public static readonly System.Type UnboundNested = typeof(TyPair<,>.TyPairTagged<>);

    /// <summary>8.4.4 — `nameof` over a generic type, which yields the name without arity.</summary>
    public static readonly string BareName = nameof(TyPair<int, string>);

    /// <summary>8.4.3 — a closed type's members can be called; an open type's cannot be
    /// instantiated except through a type parameter.</summary>
    public static TyPair<string, int> FlipOne() =>
        new TyPair<int, string> { First = 1, Second = "one" }.Flip();
}

/// <summary>
/// 8.4.5 — every constraint kind the language has, one type parameter at a time. A
/// constraint is not always a type: <c>class</c>, <c>struct</c>, <c>notnull</c>,
/// <c>unmanaged</c>, <c>new()</c> and <c>allows ref struct</c> are keywords with nothing to
/// resolve to, while a base class or interface constraint is a genuine type reference.
/// </summary>
public static class TyConstraints
{
    /// <summary>8.4.5 — the reference type constraint.</summary>
    public static TValue? AsReference<TValue>(TValue? value)
        where TValue : class => value;

    /// <summary>8.4.5 — the nullable reference type constraint (8.9.3).</summary>
    public static TValue? AsNullableReference<TValue>(TValue? value)
        where TValue : class? => value;

    /// <summary>8.4.5 — the value type constraint, which also admits enums.</summary>
    public static TValue AsValue<TValue>(TValue value)
        where TValue : struct => value;

    /// <summary>8.4.5 — the notnull constraint, which is the one with no type behind it.</summary>
    public static string Render<TValue>(TValue value)
        where TValue : notnull => value.ToString() ?? string.Empty;

    /// <summary>8.8 / 8.4.5 — the unmanaged constraint.</summary>
    public static bool IsBlittable<TValue>()
        where TValue : unmanaged => true;

    /// <summary>8.4.5 — the constructor constraint, which permits `new TValue()`.</summary>
    public static TValue Fresh<TValue>()
        where TValue : new() => new();

    /// <summary>8.4.5 — a base class constraint, which is a reference to a class declaration.</summary>
    public static string DescribeAccount<TAccount>(TAccount account)
        where TAccount : TyAccount => account.Describe();

    /// <summary>8.4.5 — an interface constraint, which is a reference to an interface declaration.</summary>
    public static string NameOf<TNamed>(TNamed named)
        where TNamed : ITyNamed => named.Name();

    /// <summary>8.4.5 — a self-referential interface constraint over the type parameter itself.</summary>
    public static int Compare<TValue>(TValue left, TValue right)
        where TValue : System.IComparable<TValue> => left.CompareTo(right);

    /// <summary>8.4.5 — a type parameter constrained by another type parameter.</summary>
    public static TBase Widen<TBase, TDerived>(TDerived derived)
        where TDerived : TBase => derived;

    /// <summary>8.4.5 — the System.Enum constraint, which the standard singles out.</summary>
    public static string EnumName<TEnum>(TEnum value)
        where TEnum : struct, System.Enum => value.ToString();

    /// <summary>8.4.5 — the System.Delegate constraint.</summary>
    public static object? InvokeAny<TDelegate>(TDelegate handler)
        where TDelegate : System.Delegate => handler.DynamicInvoke();

    /// <summary>8.4.5 — several constraints on one type parameter, in the order the grammar requires.</summary>
    public static TValue Combined<TValue>()
        where TValue : TyAccount, ITyNamed, new() => new();

    /// <summary>
    /// 8.4.5 — the `allows ref struct` anti-constraint (post-standard), which widens the
    /// type argument set rather than narrowing it: a ref struct may be substituted here.
    /// </summary>
    public static int CountOf<TSpanLike>(TSpanLike value)
        where TSpanLike : allows ref struct => value is null ? 0 : 1;

    /// <summary>8.4.5 — a call that satisfies constraints with a ref struct type argument.</summary>
    public static int CountWindow(TyStackWindow window) => CountOf(window);

    /// <summary>8.4.5 — calls that satisfy each of the constraints above.</summary>
    public static string SatisfyAll()
    {
        string reference = AsReference("text") ?? string.Empty;
        int value = AsValue(1);
        string rendered = Render(2);
        bool blittable = IsBlittable<TyPoint2D>();
        TyPoint2D fresh = Fresh<TyPoint2D>();
        string name = NameOf(new TyDoubleNamed());
        int order = Compare(1, 2);
        object widened = Widen<object, string>("wide");
        string enumName = EnumName(TyStroke.Thin);
        return $"{reference}{value}{rendered}{blittable}{fresh.X}{name}{order}{widened}{enumName}";
    }
}

/// <summary>
/// 8.4.5 — a class satisfying the combined constraint above, so the constraint's type
/// arguments have something to bind to.
/// </summary>
public sealed class TyNamedAccount : TyAccount, ITyNamed
{
    /// <summary>8.4.5 — the parameterless constructor the `new()` constraint requires.</summary>
    public TyNamedAccount()
        : base("unnamed")
    {
    }

    public string Name() => OwnerName;
}

/// <summary>
/// 8.5 hazard — a method type parameter with the same name as its containing type's. Both
/// are declarations named <c>T</c>, one nested inside the other's scope, and the inner one
/// hides the outer.
/// </summary>
public sealed class TyShadowHost<T>
{
    /// <summary>8.5 — the outer type parameter, in a field's type.</summary>
    public T? Outer;

    /// <summary>
    /// 8.5 hazard — the method's own <c>T</c>, unrelated to the class's. The compiler warns
    /// and compiles.
    /// </summary>
    public T? Shadow<T>(T? candidate) => candidate;

    /// <summary>8.5 — a method type parameter that does not shadow, for contrast.</summary>
    public TOther? Distinct<TOther>(TOther? candidate) => candidate;
}

/// <summary>
/// 8.5 — a type parameter's default value and its nullable form. For an unconstrained type
/// parameter <c>T?</c> is neither a nullable value type nor a nullable reference type until
/// the argument is known.
/// </summary>
public abstract class TyDefaulting
{
    /// <summary>8.5 — an abstract generic method whose parameter type is `TValue?`.</summary>
    public abstract TValue? OrNothing<TValue>(TValue? candidate);

    /// <summary>9.3 — `default(T)` for an unknown T, which is the only way to name its default.</summary>
    public static TValue Nothing<TValue>() => default!;
}

/// <summary>
/// 8.4.5 — the <c>default</c> constraint, which exists only on an override to say that the
/// overridden type parameter carries neither the class nor the struct constraint.
/// </summary>
public sealed class TyDefaultConstrained : TyDefaulting
{
    public override TValue? OrNothing<TValue>(TValue? candidate)
        where TValue : default => candidate;
}
