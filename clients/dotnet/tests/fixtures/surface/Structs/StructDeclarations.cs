// Clause 16.2 — Struct declarations — and 16.2.1, General. The struct_declaration
// production is: attributes, struct_modifiers, `ref`, `partial`, `struct`, identifier, an
// optional type_parameter_list, optional struct_interfaces, optional
// type_parameter_constraints_clauses, then the struct_body and an optional semicolon.
//
// Every optional part of that production appears at least once in this project. This file
// carries the attributes, the type parameter list and the constraints clauses; the
// modifiers are in `StructModifiers.cs`, `ref` in `RefModifier.cs`, `partial` in
// `PartialModifier.cs` and its second part, and the interfaces in `StructInterfaces.cs`.

using System;

namespace Surface.Structs;

/// <summary>
/// Clause 16.2.1 — an attribute class, declared so that a struct declaration can carry
/// the attributes part of its production.
/// </summary>
// `AllowMultiple` is set so that the two parts of the partial struct in
// `PartialModifier.cs` and `PartialModifierPart2.cs` can each carry one: clause 16.2.4
// unions the attributes of every part, and without this the union is a compile error.
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Field, AllowMultiple = true)]
public sealed class StSurveyedAttribute : Attribute
{
    /// <summary>The census row this declaration answers.</summary>
    public StSurveyedAttribute(string clause) => Clause = clause;

    /// <summary>The clause, as a string so no type reference is implied.</summary>
    public string Clause { get; }
}

/// <summary>
/// Clause 16.2.1 — the attributes part of a struct declaration, and a trailing semicolon
/// after the struct body, which the grammar allows and almost nothing writes.
/// </summary>
[StSurveyed("16.2.1")]
public struct StAttributedPair
{
    /// <summary>An attribute on a member of an attributed struct.</summary>
    [StSurveyed("16.3.1")]
    public int Left;

    /// <summary>The second half of the pair.</summary>
    public int Right;
};

/// <summary>
/// Clause 16.2.1 — the type_parameter_list part. Declared at exactly one arity: there is
/// deliberately no non-generic <c>StBox</c> anywhere in the corpus, because one name at
/// two arities is the shape that refuses a write.
/// </summary>
/// <typeparam name="T">The boxed value type.</typeparam>
public struct StBox<T>
    where T : struct
{
    /// <summary>The single value.</summary>
    public T Item;

    /// <summary>Clause 16.2.1 — a constructor on a generic struct.</summary>
    public StBox(T item) => Item = item;

    /// <summary>Clause 16.4.12 — a method whose return type is the type parameter.</summary>
    public T Unwrap() => Item;
}

/// <summary>
/// Clause 16.2.1 — two type parameters and two constraints clauses, one naming a
/// constraint keyword and an interface, the other naming a class constraint.
/// </summary>
/// <typeparam name="TKey">The key, which must be non-null and orderable.</typeparam>
/// <typeparam name="TValue">The value, which must be a reference type.</typeparam>
public struct StConstrainedEntry<TKey, TValue>
    where TKey : notnull, IComparable<TKey>
    where TValue : class
{
    /// <summary>The key.</summary>
    public TKey Key;

    /// <summary>The value, which the class constraint allows to be null.</summary>
    public TValue? Value;

    /// <summary>Clause 16.4.12 — a method that uses the interface constraint.</summary>
    public int CompareKeyTo(TKey other) => Key.CompareTo(other);
}

/// <summary>
/// Clause 16.2 / 16.2.1 hazard — the generic struct above closed over two different type
/// arguments. <c>StBox&lt;int&gt;</c> and <c>StBox&lt;double&gt;</c> are two constructed
/// types over one declaration, and every member of each is a distinct constructed member
/// whose name is the same.
/// </summary>
public static class StDeclarationUse
{
    /// <summary>The first construction.</summary>
    public static StBox<int> Counted = new StBox<int>(1);

    /// <summary>The second construction, of the same declaration.</summary>
    public static StBox<double> Measured = new StBox<double>(1.5);

    /// <summary>A construction nested inside another construction.</summary>
    public static StBox<StBox<int>>? Doubled = null;

    /// <summary>Clause 16.2.1 — the constrained struct, closed.</summary>
    public static StConstrainedEntry<string, StSurveyedAttribute> Entry = default;

    /// <summary>Two calls to the same declared method through two constructed types.</summary>
    public static string BothUnwraps() => $"{Counted.Unwrap()}/{Measured.Unwrap()}";
}
