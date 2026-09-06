// Clause 23.5.7.1 (code analysis attributes, general), 23.5.7.2 (AllowNull),
// 23.5.7.3 (DisallowNull), 23.5.7.4 (DoesNotReturn) and 23.5.7.5 (DoesNotReturnIf).
//
// Every attribute in 23.5.7 lives in System.Diagnostics.CodeAnalysis and every one of them
// is applied to a *parameter*, a *return value*, or a member named by a string. None of
// them is applied to anything with a body. That is what makes the clause a block of
// hazards rather than twelve unrelated rows: the target of the application is, in eleven of
// the twelve cases, something an index either has no node for or names by the enclosing
// member.
//
// The four rows in this file are the preconditions — the ones that describe what a caller
// may pass, and what happens to control flow afterwards:
//
//   * AllowNull and DisallowNull invert the declared type of an input. `Name` is declared
//     `string` and accepts null; `Ledger` is declared `string?` and refuses it. So the
//     nullability an index reads off the type is wrong in both cases, and the attribute is
//     the only thing that says so.
//   * DoesNotReturn and DoesNotReturnIf are claims about reachability. The code after a
//     call to `Refuse` is unreachable and the compiler knows it, so an index built from a
//     control-flow graph and one built from the syntax tree disagree about whether the
//     statements below such a call exist.
//
// The hazard shared by all four: an application on a parameter is filed against a symbol
// whose name (`value`, for a setter) may not appear in the source at all, and the
// applications on `Name` and `Ledger` are on the *property*, which is a third target
// again — neither the parameter nor the backing field the value ends up in.

using System;
using System.Diagnostics.CodeAnalysis;

namespace Surface.Attributes.Reserved;

/// <summary>
/// 23.5.7.2 and 23.5.7.3: the two attributes that contradict a declared type, on one type
/// so the contradiction runs both ways.
/// </summary>
public sealed class AttrNullabilityInputs
{
    private string _name = "unnamed";

    /// <summary>
    /// 23.5.7.2: declared non-nullable and yet null is a legal argument to the setter,
    /// which the setter's own body is what makes true.
    /// </summary>
    [AllowNull]
    public string Name
    {
        get => _name;
        set => _name = value ?? "unnamed";
    }

    /// <summary>
    /// 23.5.7.3: declared nullable and yet null is not a legal argument. The getter may
    /// still return null, which is the asymmetry the attribute exists for.
    /// </summary>
    [DisallowNull]
    public string? Ledger { get; set; }

    /// <summary>23.5.7.2: the same attribute on a field rather than a property.</summary>
    [AllowNull]
    public string Note = "unnoted";

    /// <summary>
    /// 23.5.7.2: and on a parameter, which is the target the clause names first. Null is
    /// a legal argument even though the parameter's type is not nullable.
    /// </summary>
    /// <param name="candidate">May be null despite its type.</param>
    /// <returns>The candidate, or the name already held.</returns>
    public string Adopt([AllowNull] string candidate) => candidate ?? _name;

    /// <summary>
    /// 23.5.7.3: on a parameter of nullable type that still refuses null.
    /// </summary>
    /// <param name="required">Must not be null despite its type.</param>
    /// <returns>Its length.</returns>
    public int Weigh([DisallowNull] string? required) => required.Length;

    /// <summary>Uses both of the above, so neither is declared and never called.</summary>
    /// <returns>A description built from every annotated member here.</returns>
    public string Describe()
    {
        Name = null;
        Ledger = "main";
        Note = null;
        return $"{Adopt(null)}/{Weigh(Ledger)}/{Note ?? "none"}";
    }
}

/// <summary>
/// 23.5.7.4 and 23.5.7.5: the two attributes about control flow leaving a method.
/// </summary>
public static class AttrNullabilityFlow
{
    /// <summary>
    /// 23.5.7.4: never returns, so the compiler treats what follows a call as unreachable
    /// and stops complaining about state the caller cannot reach.
    /// </summary>
    /// <param name="why">What the caller did wrong.</param>
    [DoesNotReturn]
    public static void Refuse(string why) =>
        throw new Exceptions.AttrLedgerFault(why);

    /// <summary>
    /// 23.5.7.4: the same claim on a method returning a value, where the return type is a
    /// pure fiction — no value is ever produced.
    /// </summary>
    /// <param name="why">What the caller did wrong.</param>
    /// <returns>Nothing, ever.</returns>
    [DoesNotReturn]
    public static int RefuseWithAValue(string why) =>
        throw new Exceptions.AttrPostingFault(why, why.Length);

    /// <summary>
    /// 23.5.7.5: returns only when the condition is true, which is how an assertion
    /// teaches the compiler's flow analysis what a caller has just proved.
    /// </summary>
    /// <param name="condition">If false, this method does not return.</param>
    /// <param name="why">What the caller did wrong.</param>
    public static void Assert(
        [DoesNotReturnIf(false)] bool condition,
        string why)
    {
        if (!condition)
        {
            Refuse(why);
        }
    }

    /// <summary>
    /// 23.5.7.5: the other polarity. Returns only when the condition is false.
    /// </summary>
    /// <param name="failed">If true, this method does not return.</param>
    /// <param name="why">What the caller did wrong.</param>
    public static void Deny(
        [DoesNotReturnIf(true)] bool failed,
        string why)
    {
        if (failed)
        {
            Refuse(why);
        }
    }

    /// <summary>
    /// 23.5.7.4 and 23.5.7.5: the calls. After <c>Assert</c> the compiler knows
    /// <c>posting</c> is not null without a suppression, and the statement after
    /// <c>Refuse</c> is code no execution reaches.
    /// </summary>
    /// <param name="posting">A posting that may be null.</param>
    /// <returns>Its length.</returns>
    public static int Requires(string? posting)
    {
        Assert(posting is not null, "a posting is required");
        Deny(posting.Length == 0, "a posting may not be empty");

        if (posting.Length > 1024)
        {
            Refuse("a posting may not exceed 1024 characters");

            // 23.5.7.4: unreachable, and reachable in the syntax tree.
            return RefuseWithAValue("unreachable");
        }

        return posting.Length;
    }
}
