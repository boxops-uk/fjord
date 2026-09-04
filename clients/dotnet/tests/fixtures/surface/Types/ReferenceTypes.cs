// Clause 8.2 — reference types. Every reference-type category the standard names lives
// here: class types (8.2.2), the object type (8.2.3), the string type (8.2.5), interface
// types (8.2.6), array types (8.2.7) and delegate types (8.2.8). The dynamic type (8.2.4,
// 8.7) has its own file, because what an index can say about it is a separate question.

namespace Surface.Types;

/// <summary>
/// 8.1 — the roster of type categories, reachable from one declaration. <typeparamref name="TItem"/>
/// is the third category (a type parameter, 8.5); it is instantiated below at both a value
/// type and a reference type, so one declaration is referred to from both sides of 8.1's
/// division. Pointer types are the fourth category and belong to clause 23.
/// </summary>
public sealed class TyCategoryRoster<TItem>
{
    /// <summary>8.1 — a field whose type is the type parameter itself.</summary>
    public TItem? Held;

    /// <summary>8.1 — a value type (8.3).</summary>
    public int ValueCategory;

    /// <summary>8.1 — a reference type (8.2).</summary>
    public string ReferenceCategory = string.Empty;

    /// <summary>8.1 — an instantiation of this same declaration at a value type.</summary>
    public static TyCategoryRoster<int> OverValue { get; } = new();

    /// <summary>8.1 — and at a reference type. Two references, one declaration.</summary>
    public static TyCategoryRoster<string> OverReference { get; } = new();
}

/// <summary>
/// 8.2.2 — a class type, and the root of a small heritage chain. Its own base type is
/// <see cref="object"/> (8.2.3), which appears in no token of this declaration.
/// </summary>
public class TyAccount
{
    /// <summary>9.2.3.2 — an instance variable in a class.</summary>
    protected readonly string OwnerName;

    /// <summary>8.3.3 — a declared instance constructor, so this class has no default one.</summary>
    public TyAccount(string ownerName) => OwnerName = ownerName;

    /// <summary>8.2.2 — a virtual member, so derivation has something to bind to.</summary>
    public virtual string Describe() => $"account of {OwnerName}";
}

/// <summary>8.2.2 — a sealed derived class.</summary>
public sealed class TySavingsAccount : TyAccount
{
    /// <summary>8.3.8 — the Decimal type.</summary>
    private readonly decimal _annualRate;

    public TySavingsAccount(string ownerName, decimal annualRate)
        : base(ownerName) => _annualRate = annualRate;

    public decimal AnnualRate => _annualRate;

    public override string Describe() => $"savings of {OwnerName} at {_annualRate}";
}

/// <summary>8.2.2 — an abstract class type.</summary>
public abstract class TyStatement
{
    public abstract int LineCount { get; }
}

/// <summary>8.2.2 — a static class type: uninstantiable, so 8.3.3 gives it no constructor.</summary>
public static class TyReferenceSpellings
{
    /// <summary>8.2.3 — the object type, written as the keyword.</summary>
    public static object BoxedByKeyword = 0;

    /// <summary>
    /// 8.2.3 hazard — the same type written as its framework name. Two field declarations
    /// whose type references must resolve to one identity.
    /// </summary>
    public static System.Object BoxedByFrameworkName = "text";

    /// <summary>8.2.5 — the string type, written as the keyword.</summary>
    public static string TextByKeyword = "one";

    /// <summary>8.2.5 hazard — and by framework name. Again one type, two spellings.</summary>
    public static System.String TextByFrameworkName = "two";

    /// <summary>8.2.3 — `object` as a return type, with a boxing conversion (8.3.13) in the body.</summary>
    public static object BoxAnInteger(int value) => value;

    /// <summary>8.3.13 — the matching unboxing conversion, which is a cast and so a type reference.</summary>
    public static int UnboxAnInteger(object boxed) => (int)boxed;

    /// <summary>8.2.5 — the string type has a literal form, and null is assignable to it (8.2.1).</summary>
    public static string? NullableText = null;
}

/// <summary>8.2.2 — a record class (post-standard), whose primary constructor parameters
/// become properties of the same names.</summary>
public record TyLedgerEntry(string Account, decimal Amount);

/// <summary>8.2.2 — the same shape written with the explicit `record class` keyword pair.</summary>
public record class TyPostingEntry(string Account, decimal Amount)
{
    /// <summary>8.2.2 — a record may add members beside its primary constructor's.</summary>
    public string Summary => $"{Account}: {Amount}";
}

/// <summary>8.2.6 — an interface type: a method, a property, an event, and a default
/// implementation (post-standard) that gives the interface a body to index.</summary>
public interface ITyDescribable
{
    /// <summary>8.2.6 — an interface method, with no implementation here.</summary>
    string Describe();

    /// <summary>8.2.6 — an interface property.</summary>
    int Weight { get; }

    /// <summary>8.2.6 — an interface event.</summary>
    event TyNotifier? Changed;

    /// <summary>8.2.6 — a default implementation, so the interface declares a body.</summary>
    string Label => $"described({Weight})";
}

/// <summary>8.2.6 — an interface with a base interface list and a static abstract member.</summary>
public interface ITyMeasurable : ITyDescribable
{
    /// <summary>8.2.6 — a static abstract member, which only a constrained type parameter can call.</summary>
    static abstract string UnitName { get; }

    /// <summary>8.2.6 — one indexer, which is the most a type may declare here.</summary>
    double this[int dimension] { get; }
}

/// <summary>8.2.6 — a generic interface with both variance annotations.</summary>
public interface ITyConverter<in TSource, out TResult>
{
    TResult Convert(TSource source);
}

/// <summary>8.2.6 — an implementation of the interface chain above.</summary>
public sealed class TyRuler : ITyMeasurable
{
    public event TyNotifier? Changed;

    public int Weight => 1;

    public static string UnitName => "mm";

    public double this[int dimension] => dimension * 0.5;

    public string Describe() => "a ruler";

    /// <summary>8.2.6 — raising the event, so the event has a reference as well as a declaration.</summary>
    public void Nudge() => Changed?.Invoke("nudged");
}

/// <summary>8.2.6 — an interface whose member is implemented twice over.</summary>
public interface ITyNamed
{
    string Name();
}

/// <summary>
/// 8.2.6 hazard — an ordinary public method named <c>Name</c> beside an explicit
/// implementation of <see cref="ITyNamed.Name"/>. Both are members of this type and both
/// have the simple name <c>Name</c>; only the qualifier separates them.
/// </summary>
public sealed class TyDoubleNamed : ITyNamed
{
    /// <summary>The implicit implementation, and an ordinary member of this class.</summary>
    public string Name() => "ordinary";

    /// <summary>The explicit implementation, which is what interface dispatch reaches.</summary>
    string ITyNamed.Name() => "explicit";
}

/// <summary>
/// 8.2.7 — array types. The overloads differ only in the shape of their array parameter:
/// rank, then jaggedness. An identity that drops rank merges all four.
/// </summary>
public static class TyArrayShapes
{
    /// <summary>8.2.7 — a single-dimensional array type.</summary>
    public static int Total(int[] vector)
    {
        int running = 0;
        foreach (int element in vector)
        {
            running += element;
        }

        return running;
    }

    /// <summary>8.2.7 hazard — rank two, same element type, same method name.</summary>
    public static int Total(int[,] grid) => grid.Length;

    /// <summary>8.2.7 hazard — rank three.</summary>
    public static int Total(int[,,] cube) => cube.Length;

    /// <summary>8.2.7 hazard — an array of arrays, which is one dimension of a different thing.</summary>
    public static int Total(int[][] jagged) => jagged.Length;

    /// <summary>8.2.7 — array creation for each shape.</summary>
    public static int[] NewVector() => new int[3];

    public static int[,] NewGrid() => new int[2, 3];

    public static int[][] NewJagged() => [new int[1], new int[2]];

    /// <summary>8.2.7 — an initialized array, and a collection expression (post-standard).</summary>
    public static readonly string[] Labels = { "alpha", "beta" };

    public static readonly int[] Counts = [1, 2, 3];

    /// <summary>8.2.7 — array covariance: every array type converts to its element base's array.</summary>
    public static readonly object[] Covariant = new string[1];

    /// <summary>8.2.7 — System.Array is the base type of every array type.</summary>
    public static System.Array AsBase => Counts;
}

/// <summary>8.2.8 — a delegate type.</summary>
public delegate int TyAdder(int left, int right);

/// <summary>
/// 8.2.8 hazard — a second delegate type with an identical parameter list and return type.
/// Nothing but the name separates it from <see cref="TyAdder"/>.
/// </summary>
public delegate int TyCombiner(int left, int right);

/// <summary>8.2.8 — a generic delegate type, with variance on both parameters.</summary>
public delegate TResult TyProjector<in TSource, out TResult>(TSource source);

/// <summary>8.2.8 — a void-returning delegate, used as the type of the events above.</summary>
public delegate void TyNotifier(string message);

/// <summary>8.2.8 — delegate types in use: construction, invocation, and combination.</summary>
public static class TyDelegateUse
{
    /// <summary>8.2.8 — a delegate-typed field initialized from a lambda (9.4.4.31).</summary>
    public static readonly TyAdder Add = (left, right) => left + right;

    /// <summary>8.2.8 — the identically-shaped delegate, initialized from a method group.</summary>
    public static readonly TyCombiner Combine = Multiply;

    /// <summary>8.2.8 — a constructed generic delegate type.</summary>
    public static readonly TyProjector<int, string> Render = value => value.ToString();

    private static int Multiply(int left, int right) => left * right;

    /// <summary>8.2.8 — invoking a delegate, both implicitly and through Invoke.</summary>
    public static int Apply(TyAdder adder, int seed) => adder(seed, adder.Invoke(seed, seed));

    /// <summary>8.2.8 — delegate combination, which is an operator on the delegate type.</summary>
    public static TyNotifier Chain(TyNotifier first, TyNotifier second) => first + second;
}
