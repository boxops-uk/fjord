// Clause 23.5.3.2 (conditional methods), 23.5.3.3 (conditional local functions) and
// 23.5.3.4 (conditional attribute classes).
//
// `Conditional` is the one reserved attribute whose effect is to make source and metadata
// disagree, and the disagreement is the fact worth indexing. The project defines
// SURFACE_TRACE and never defines SURFACE_AUDIT, so every pair below has a kept half and a
// dropped half, written the same way:
//
//   * A call to a method marked `[Conditional("SURFACE_AUDIT")]` is omitted from the
//     emitted IL *together with its arguments*, so an index built from metadata finds no
//     call and no evaluation of the argument expression, while an index built from the
//     syntax tree finds both. `DropsItsArgument` makes that measurable: the argument is a
//     call to a method that would otherwise be referenced from nowhere else.
//   * An application of a conditional *attribute class* is omitted the same way, so
//     `AttrAuditedAttribute` is declared here, applied twice below, and present in no
//     assembly this project produces — confirmed by reflecting over the built output,
//     where `AttrConditionalApplications` and its `Counted` property each carry exactly
//     one attribute and it is the traced one. Its constructor is a declaration nothing
//     calls, and the class itself is still emitted.
//
// 23.5.3.3's hazard is the local function itself rather than the condition. A local
// function's name is scoped to a block, so `Trace` is declared three times in this file —
// twice in one method, in sibling blocks, and once in another. An identity string minted
// from (enclosing method, name) merges the first two silently; one minted from (type,
// name) merges all three.
//
// The assembly does separate them, and how it does so is the fact to check an index
// against: all three are emitted as private methods of the enclosing type, named
// `<Logs>g__Trace|1_0`, `<Logs>g__Trace|1_1` and `<LogsAgain>g__Trace|2_0`. The mangled
// name carries the enclosing method, an ordinal for the method and an ordinal for the
// local function — so metadata has three distinct names for what the source spells once,
// and the ordinals depend on declaration order. Note also that the *dropped* local
// function is still emitted: `[Conditional]` removes calls, never declarations, and
// `<Logs>g__Trace|1_1` is in the assembly with no caller anywhere.

using System;
using System.Diagnostics;

namespace Surface.Attributes.Reserved;

/// <summary>
/// 23.5.3.2: conditional methods, one whose symbol is defined and one whose is not.
/// </summary>
public static class AttrConditionalMethods
{
    private static int _traced;

    /// <summary>23.5.3.2: SURFACE_TRACE is defined, so calls to this are emitted.</summary>
    /// <param name="what">What happened.</param>
    [Conditional("SURFACE_TRACE")]
    public static void Trace(string what) => _traced += what.Length;

    /// <summary>23.5.3.2: SURFACE_AUDIT is never defined, so calls to this are not.</summary>
    /// <param name="what">What happened.</param>
    [Conditional("SURFACE_AUDIT")]
    public static void Audit(string what) => _traced += what.Length;

    /// <summary>
    /// 23.5.3.2: two conditions on one method. The clause's rule is a disjunction, so one
    /// defined symbol is enough and calls to this one are emitted.
    /// </summary>
    /// <param name="what">What happened.</param>
    [Conditional("SURFACE_AUDIT")]
    [Conditional("SURFACE_TRACE")]
    public static void Either(string what) => _traced += what.Length;

    /// <summary>
    /// 23.5.3.2: neither condition holds, so this one is unreachable from any emitted call
    /// even though two call sites name it.
    /// </summary>
    /// <param name="what">What happened.</param>
    [Conditional("SURFACE_AUDIT")]
    [Conditional("SURFACE_UNSET")]
    public static void Neither(string what) => _traced += what.Length;

    /// <summary>How many characters were traced, so the counter is read.</summary>
    public static int Traced => _traced;

    /// <summary>The argument of an omitted call, and of nothing else.</summary>
    private static string Expensive() => new('x', _traced % 8);

    /// <summary>
    /// 23.5.3.2: five call sites. Three survive to metadata, two do not, and the argument
    /// expression of the fourth — a call to <c>Expensive</c> — goes with it.
    /// </summary>
    public static void Calls()
    {
        Trace("kept");
        Either("kept, by the defined half of a disjunction");
        Audit("dropped");
        Neither("dropped, by both halves");
        DropsItsArgument();
    }

    /// <summary>23.5.3.2: the argument is evaluated only if the call is emitted.</summary>
    public static void DropsItsArgument() => Audit(Expensive());
}

/// <summary>23.5.3.3: conditional local functions, plus a name declared three times.</summary>
public static class AttrConditionalLocals
{
    private static int _logged;

    /// <summary>
    /// 23.5.3.3: two local functions named <c>Trace</c> in sibling blocks of one method,
    /// one kept and one dropped, and a call to each.
    /// </summary>
    /// <param name="mode">Which block runs.</param>
    public static int Logs(int mode)
    {
        if (mode > 0)
        {
            [Conditional("SURFACE_TRACE")]
            static void Trace(string what) => AttrConditionalMethods.Trace(what);

            Trace("the kept local function");
        }
        else
        {
            [Conditional("SURFACE_AUDIT")]
            static void Trace(string what) => AttrConditionalMethods.Trace(what);

            Trace("the dropped local function");
        }

        return _logged;
    }

    /// <summary>
    /// 23.5.3.3: a third <c>Trace</c>, in a different method, and a local function with an
    /// ordinary attribute rather than a conditional one — so the two kinds of application
    /// on a local function sit side by side.
    /// </summary>
    public static int LogsAgain()
    {
        [Conditional("SURFACE_TRACE")]
        [Obsolete("kept, and obsolete")]
        static void Trace(string what) => AttrConditionalMethods.Trace(what);

        [return: Classes.AttrDetail("a local function's return value")]
        static int Weigh([Classes.AttrDetail("a local function's parameter")] string what) =>
            what.Length;

#pragma warning disable CS0618 // 23.5.4: the call to an obsolete local function is the point.
        Trace("the third declaration of this name");
#pragma warning restore CS0618

        _logged += Weigh("counted");
        return _logged;
    }
}

/// <summary>
/// 23.5.3.4: a conditional attribute class whose symbol is defined, so its applications
/// survive into metadata.
/// </summary>
[Conditional("SURFACE_TRACE")]
[AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
public sealed class AttrTracedAttribute : Attribute
{
    /// <summary>What is being traced.</summary>
    /// <param name="what">A note.</param>
    public AttrTracedAttribute(string what) => What = what;

    /// <summary>The note.</summary>
    public string What { get; }
}

/// <summary>
/// 23.5.3.4: a conditional attribute class whose symbol is never defined. It is declared,
/// it is applied twice, and no application of it reaches any assembly.
/// </summary>
[Conditional("SURFACE_AUDIT")]
[AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
public sealed class AttrAuditedAttribute : Attribute
{
    /// <summary>What is being audited.</summary>
    /// <param name="what">A note.</param>
    public AttrAuditedAttribute(string what) => What = what;

    /// <summary>The note. Assigned by no constructor call that is ever emitted.</summary>
    public string What { get; }
}

/// <summary>
/// 23.5.3.4: four applications in the source and two in the assembly, on targets that are
/// otherwise identical.
/// </summary>
[AttrTraced("on the type, kept")]
[AttrAudited("on the type, dropped")]
public sealed class AttrConditionalApplications
{
    /// <summary>One kept application and one dropped one on the same member.</summary>
    [AttrTraced("on the member, kept")]
    [AttrAudited("on the member, dropped")]
    public int Counted { get; set; }

    /// <summary>Runs the calls above, so nothing here is declared and never reached.</summary>
    public int Run()
    {
        AttrConditionalMethods.Calls();
        return AttrConditionalMethods.Traced
            + AttrConditionalLocals.Logs(Counted)
            + AttrConditionalLocals.LogsAgain();
    }
}
