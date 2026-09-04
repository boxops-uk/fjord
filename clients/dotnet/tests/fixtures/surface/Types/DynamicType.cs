// Clauses 8.2.4 and 8.7 — the dynamic type. Both clauses name it: 8.2.4 places it among the
// reference types, 8.7 states what it means. It is the one type in this project that an
// index may be unable to express: `dynamic` and `object` are the same type in metadata,
// separated only by a DynamicAttribute, and every member access below binds at run time
// against a member that does not exist until then.

namespace Surface.Types;

/// <summary>
/// 8.7 — the dynamic type in every position a type may appear in. Nothing here is
/// resolvable at compile time past the declarations themselves.
/// </summary>
public sealed class TyDynamicSurface
{
    /// <summary>8.2.4 — a field of type dynamic. In metadata this field's type is object.</summary>
    public dynamic Loose = 0;

    /// <summary>
    /// 8.7 hazard — a field of type object beside the dynamic one. The two field types
    /// erase to one identity, and only an attribute distinguishes them.
    /// </summary>
    public object Tight = 0;

    /// <summary>8.7 — dynamic as a nullable reference type (8.9.3), which the language permits.</summary>
    public dynamic? Absent;

    /// <summary>8.7 — dynamic as a parameter type and as a return type.</summary>
    public dynamic Roundtrip(dynamic input) => input;

    /// <summary>
    /// 8.7 — a member access on a dynamic receiver. <c>Anything</c> is bound by the runtime
    /// binder; there is no declaration in the corpus for it to resolve to.
    /// </summary>
    public dynamic CallUnknownMember(dynamic receiver) => receiver.Anything(Loose);

    /// <summary>8.7 — a dynamic indexer access and a dynamic operator application.</summary>
    public dynamic IndexAndAdd(dynamic receiver, dynamic addend) => receiver[0] + addend;

    /// <summary>8.7 — dynamic as a type argument, which makes the constructed type inexpressible too.</summary>
    public System.Collections.Generic.List<dynamic> Mixed = [1, "two", 3.0];

    /// <summary>8.7 — dynamic as an array element type.</summary>
    public dynamic[] LooseArray = [1, "two"];

    /// <summary>8.7 — the conversions between dynamic and object are identity conversions.</summary>
    public object Narrow(dynamic value) => value;

    /// <summary>8.7 — and back, with no cast written.</summary>
    public dynamic Widen(object value) => value;

    /// <summary>8.7 — a local of type dynamic, so 9.2.9 has a dynamic case too.</summary>
    public string DescribeLocally()
    {
        dynamic held = Loose;
        return held.ToString();
    }
}
