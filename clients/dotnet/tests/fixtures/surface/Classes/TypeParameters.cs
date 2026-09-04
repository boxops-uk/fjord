// Clause 15.2.3 — type parameters. A class's `type_parameter_list` declares names that are
// in scope over the whole declaration, including its nested types (15.3.9.7). Variance is
// *not* among the things a class may declare: `out`/`in` are legal only on an interface's or
// a delegate's type parameters, so the variant declarations here are the interfaces the
// classes in BaseSpecification.cs implement, and clause 18 owns their rules.

namespace Surface.Classes;

/// <summary>15.2.3 — one type parameter, used as a field type and as the argument to the
/// enclosing declaration's own instance type (15.3.2).</summary>
/// <typeparam name="T">The single parameter, named as the standard's examples name it.</typeparam>
public class ClsBox<T>
{
    /// <summary>15.5.1 — a field whose type is the type parameter.</summary>
    public T? Item;

    /// <summary>15.3.2 — a field of the instance type of the declaration it is declared in.</summary>
    public ClsBox<T>? Next;

    /// <summary>
    /// 15.2.3 hazard — the nested declaration declares its own `T`, which shadows the outer
    /// one (CS0693). Two type parameters, one name, and the inner one is a different
    /// declaration in a different scope: a query has to see two, and an `Item` typed by the
    /// inner `T` has nothing to do with the outer type's `Item`.
    /// </summary>
    public sealed class ClsBoxSlot<T>
    {
        public T? Held;
    }
}

/// <summary>15.2.3 — two type parameters, with a constraint on one of them only.</summary>
public class ClsMapEntry<TKey, TValue>
    where TKey : notnull
{
    public TKey? Key;

    public TValue? Value;

    /// <summary>15.3.2 — the instance type with its arguments in the declared order.</summary>
    public ClsMapEntry<TKey, TValue>? Chain;

    /// <summary>15.2.3 hazard — the instance type's arguments reversed. One declaration, a
    /// second constructed type, and the arity is the same both ways.</summary>
    public ClsMapEntry<TValue, TKey>? Flipped;
}

/// <summary>15.2.3 — three type parameters, which is where a positional identity has to
/// carry an ordinal as well as a name.</summary>
public class ClsTriple<TFirst, TSecond, TThird>
    where TSecond : struct
{
    public TFirst? First;

    public TSecond Second;

    public TThird? Third;
}

/// <summary>
/// 15.2.3 hazard — a type parameter whose name is the name of a type that is in scope.
/// Inside this declaration `ClsPlain` means the parameter and never the class, so the field
/// below has the parameter's type. Two declarations want the name `ClsPlain`, one of them is
/// not a type declaration at all, and an index that keys a name binding on the spelling
/// alone resolves the field's type to the wrong one of the two.
/// </summary>
public class ClsParamNameShadow<ClsPlain>
{
    /// <summary>15.2.3 — typed by the type parameter, not by <c>Surface.Classes.ClsPlain</c>.</summary>
    public ClsPlain? Held;
}

/// <summary>
/// 15.2.3 — covariance, which a class may not declare. Declared here because
/// <see cref="ClsProducerOfInt"/> in BaseSpecification.cs implements it, and a class's
/// interface list is the only place a variant type argument reaches a class declaration.
/// </summary>
/// <typeparam name="TOut">Covariant, so `IClsProducer&lt;string&gt;` converts to
/// `IClsProducer&lt;object&gt;`.</typeparam>
public interface IClsProducer<out TOut>
{
}

/// <summary>15.2.3 — contravariance, the other annotation a class may not declare.</summary>
/// <typeparam name="TIn">Contravariant.</typeparam>
public interface IClsConsumer<in TIn>
{
}

/// <summary>15.2.3 — both annotations on one declaration, which is the shape a class's
/// interface list can name but a class's own parameter list cannot.</summary>
public interface IClsTransform<in TIn, out TOut>
{
}
