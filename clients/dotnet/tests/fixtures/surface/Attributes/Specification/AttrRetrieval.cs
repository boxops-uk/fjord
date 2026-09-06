// Clause 23.4.3 (run-time retrieval of an attribute instance).
//
// The clause is a statement about what System.Reflection does at run time, so an index
// holds no fact about it that it does not already hold about clause 12's invocations — the
// census records it `not-applicable` for that reason. The code is here anyway, because it
// is what makes every application in this project checkable by a person: an application
// that reached metadata comes back from `GetCustomAttributes` and one that did not comes
// back from nothing.
//
// It is also the only place in this project where an attribute *class* is referenced the
// way an ordinary class is — `typeof(AttrTracedAttribute)`, a cast, a property read. Every
// other reference to these classes is an application, and an application does not name the
// class at all: it names a constructor. So a query for references to AttrTracedAttribute
// should find the two here and however many the applications contribute, and the
// difference between those two counts is the whole subject of 23.4.2.

using System;
using System.Linq;
using System.Reflection;
using Surface.Attributes.Reserved;

namespace Surface.Attributes.Specification;

/// <summary>23.4.3: reading applications back, which is what they are for.</summary>
public static class AttrRetrieval
{
    /// <summary>
    /// 23.4.3 and 23.5.3.4: the conditional pair, retrieved. The kept application is
    /// there and the dropped one is not, and no source difference between them says so.
    /// </summary>
    /// <returns>What survived to metadata, in source order.</returns>
    public static string[] ConditionalApplications() =>
        typeof(AttrConditionalApplications)
            .GetCustomAttributes(inherit: false)
            .OfType<AttrTracedAttribute>()
            .Select(static traced => traced.What)
            .ToArray();

    /// <summary>
    /// 23.4.3: the typed overload, which returns an instance whose named parameters were
    /// assigned after its constructor ran.
    /// </summary>
    /// <returns>The note the type-level application carried, or nothing.</returns>
    public static string? TypeLevelNote() =>
        typeof(Classes.AttrUsageDemonstration)
            .GetCustomAttribute<Classes.AttrMarkAttribute>(inherit: false)
            ?.Note;

    /// <summary>
    /// 23.4.3 and 23.2.2: <c>Inherited</c> decided by a run-time argument. The derived
    /// type declares no application of its own, so the two calls differ only in the flag.
    /// </summary>
    /// <returns>How many marks the derived type reports with and without inheritance.</returns>
    public static (int Inherited, int Declared) InheritedMarks() =>
        (typeof(Classes.AttrInheritsMarks).GetCustomAttributes(
                typeof(Classes.AttrMarkAttribute),
                inherit: true).Length,
            typeof(Classes.AttrInheritsMarks).GetCustomAttributes(
                typeof(Classes.AttrMarkAttribute),
                inherit: false).Length);

    /// <summary>
    /// 23.4.3 and 23.3: the targets that have no declaration in the source — a return
    /// value and a generated backing field — reached through the members that own them.
    /// </summary>
    /// <returns>How many applications each unnamed target carries.</returns>
    public static (int OnTheReturnValue, int OnTheBackingField) UnnamedTargets()
    {
        MethodInfo describe = typeof(AttrEveryTarget<string>)
            .GetMethod(nameof(AttrEveryTarget<string>.Describe))!;

        FieldInfo? backing = typeof(AttrEveryTarget<string>).GetField(
            "<Name>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);

        return (describe.ReturnTypeCustomAttributes.GetCustomAttributes(inherit: false).Length,
            backing?.GetCustomAttributes(inherit: false).Length ?? 0);
    }

    /// <summary>
    /// 23.4.3 and 23.2.3: the parameter target, whose applications hang off a
    /// <c>ParameterInfo</c> rather than off anything with a name of its own.
    /// </summary>
    /// <returns>The notes on the first parameter of <c>Describe</c>.</returns>
    public static string[] ParameterNotes() =>
        typeof(AttrEveryTarget<string>)
            .GetMethod(nameof(AttrEveryTarget<string>.Describe))!
            .GetParameters()[0]
            .GetCustomAttributes(typeof(Classes.AttrDetailAttribute), inherit: false)
            .Cast<Classes.AttrDetailAttribute>()
            .Select(static detail => detail.Note)
            .ToArray();

    /// <summary>Runs every retrieval above, so none of them is dead code.</summary>
    /// <returns>A one-line summary a reader can compare against the source.</returns>
    public static string Report()
    {
        (int inherited, int declared) = InheritedMarks();
        (int onReturn, int onBacking) = UnnamedTargets();

        return string.Join(
            ' ',
            $"conditional={ConditionalApplications().Length}",
            $"typeNote={TypeLevelNote() ?? "none"}",
            $"inherited={inherited}/{declared}",
            $"unnamed={onReturn}/{onBacking}",
            $"params={ParameterNotes().Length}");
    }
}
