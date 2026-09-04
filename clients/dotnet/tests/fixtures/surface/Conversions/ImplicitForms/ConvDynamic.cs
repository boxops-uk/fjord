using System.Collections.Generic;

namespace Surface.Conversions.ImplicitForms;

/// <summary>
/// Clause 10.2.10 — the implicit dynamic conversions. Everything converts to <c>dynamic</c>
/// and <c>dynamic</c> converts to everything, with the check deferred to run time.
///
/// The deliberate hazard here is that <c>dynamic</c> is not a type in metadata: it is
/// <c>object</c> plus an attribute. Any two declarations below that differ only by
/// <c>dynamic</c> versus <c>object</c> are one signature once that attribute is dropped.
/// </summary>
public static class ConvDynamic
{
    /// <summary>Clause 10.2.10 — a value type to <c>dynamic</c>, which also boxes.</summary>
    public static dynamic FromInt(int value) => value;

    /// <summary>Clause 10.2.10 — a reference type to <c>dynamic</c>.</summary>
    public static dynamic FromString(string text) => text;

    /// <summary>Clause 10.2.10 — a class declared in this project to <c>dynamic</c>.</summary>
    public static dynamic FromLeaf(ConvLeaf leaf) => leaf;

    /// <summary>Clause 10.2.10 — <c>dynamic</c> to a value type, checked at run time.</summary>
    public static int ToInt(dynamic value) => value;

    /// <summary>Clause 10.2.10 — <c>dynamic</c> to a reference type.</summary>
    public static ConvLeaf ToLeaf(dynamic value) => value;

    /// <summary>Clause 10.2.10 — an operator whose operands are both <c>dynamic</c>.</summary>
    public static dynamic Combine(dynamic left, dynamic right) => left + right;

    /// <summary>Clause 10.2.10 — a member access bound at run time.</summary>
    public static string LabelOf(dynamic value) => value.Label;

    // Clause 10.2.10, deliberate hazard: the pairs below.

    /// <summary>A list of <c>dynamic</c>.</summary>
    public static readonly List<dynamic> DynamicItems = [];

    /// <summary>The same list, declared as a list of <c>object</c>.</summary>
    public static readonly List<object> ObjectItems = DynamicItems;

    /// <summary>Takes an <c>object</c>.</summary>
    public static void TakeObject(object value)
    {
    }

    /// <summary>Takes a <c>dynamic</c> — one erased parameter list away from the above.</summary>
    public static void TakeDynamic(dynamic value)
    {
    }
}
