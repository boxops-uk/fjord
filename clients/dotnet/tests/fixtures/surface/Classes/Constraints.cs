// Clause 15.2.5 — type parameter constraints. Every form the production allows is written
// below, one class per form so that the constraint list belongs to exactly one declaration
// and a reader can walk from the census row to a single type. `where T : default` is absent:
// it is legal only on an overriding or explicitly implementing *method*, which is clause
// 15.6's row and the sibling project's to write.

namespace Surface.Classes;

/// <summary>15.2.5 — the reference type constraint.</summary>
public class ClsWhereClass<T>
    where T : class
{
    public T? Reference;
}

/// <summary>15.2.5 — the reference type constraint, annotated: `class?` admits a nullable
/// reference type where `class` does not. The two are different constraints on the same
/// keyword, which is why this project compiles in an enabled nullable context.</summary>
public class ClsWhereClassNullable<T>
    where T : class?
{
    public T? Reference;
}

/// <summary>15.2.5 — the value type constraint. `T?` under it is `System.Nullable&lt;T&gt;`
/// rather than an annotation, so the same syntax denotes a different type here.</summary>
public class ClsWhereStruct<T>
    where T : struct
{
    public T Value;

    public T? Lifted;
}

/// <summary>15.2.5 — the `notnull` constraint, which is neither `class` nor `struct` and
/// resolves to no type at all.</summary>
public class ClsWhereNotNull<T>
    where T : notnull
{
    public T? Item;
}

/// <summary>15.2.5 — the `unmanaged` constraint, which implies `struct`.</summary>
public class ClsWhereUnmanaged<T>
    where T : unmanaged
{
    public T Raw;
}

/// <summary>15.2.5 — the enumeration constraint, spelled as a base type but behaving as a
/// special constraint: `System.Enum` names a type an index can resolve.</summary>
public class ClsWhereEnum<T>
    where T : System.Enum
{
    public T? Selected;
}

/// <summary>15.2.5 — the delegate constraint, the other framework type the standard
/// singles out.</summary>
public class ClsWhereDelegate<T>
    where T : System.Delegate
{
    public T? Handler;
}

/// <summary>15.2.5 — a primary constraint that is a class type in this project.</summary>
public class ClsWhereBaseType<T>
    where T : ClsAbstractRecord
{
    public T? Record;
}

/// <summary>15.2.5 — a primary constraint that is an *annotated* class type.</summary>
public class ClsWhereBaseTypeNullable<T>
    where T : ClsPlain?
{
    public T? Loose;
}

/// <summary>15.2.5 — a secondary constraint that is an interface type.</summary>
public class ClsWhereInterface<T>
    where T : IClsTagged
{
    public T? Tagged;
}

/// <summary>15.2.5 — the constructor constraint, `new()`, which must come last.</summary>
public class ClsWhereNew<T>
    where T : new()
{
    public T? Fresh;
}

/// <summary>15.2.5 — a primary, two secondaries and the constructor constraint in the one
/// order the grammar accepts.</summary>
public class ClsWhereCombined<T>
    where T : ClsAbstractRecord, IClsTagged, IClsNamed, new()
{
    public T? Everything;
}

/// <summary>
/// 15.2.5 hazard — a type parameter constrains another type parameter. `T`'s constraint list
/// names `U`, which is a declaration in the same list rather than a type: an index that
/// records a constraint as an edge to a type has an edge here that points at a parameter,
/// and the two parameters have to stay two rows.
/// </summary>
public class ClsWhereTypeParameter<T, U>
    where T : U
    where U : notnull
{
    public T? Narrow;

    public U? Wide;
}

/// <summary>15.2.5 — a constraint whose type is a constructed type built from another
/// parameter of the same list.</summary>
public class ClsWhereConstructedInterface<TItem, TSink>
    where TItem : notnull
    where TSink : IClsSink<TItem>
{
    public TItem? Item;

    public TSink? Sink;
}

/// <summary>
/// 15.2.5 hazard — the constraint names the declaration it constrains, constructed with its
/// own parameter. The declaration is its own constituent type (15.3.7), so a walk that
/// resolves constraints eagerly meets this type while it is still being built.
/// </summary>
public class ClsSelfBound<T>
    where T : ClsSelfBound<T>
{
    public T? Self;
}

/// <summary>15.2.5 — the `allows ref struct` anti-constraint. It widens what may be
/// substituted, so `T` may be a byref-like type and therefore may not be a field's type:
/// the body is empty because the constraint forbids what would otherwise fill it.</summary>
public class ClsAllowsRefStruct<T>
    where T : allows ref struct
{
}

/// <summary>15.2.5 — `struct` beside the anti-constraint, which is the combination that
/// permits a ref struct argument to a value-type-constrained parameter.</summary>
public class ClsAllowsRefStructValue<T>
    where T : struct, allows ref struct
{
}
