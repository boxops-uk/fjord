// Clause 22.3 (the System.Exception class).
//
// The clause's shape is a *derived* type: an exception class inherits Message,
// InnerException, StackTrace, Data, HelpLink and Source rather than declaring them, and
// the conventional three constructors each chain to a different base constructor. That is
// the hazard. All three are `.ctor` in metadata and they differ only in their parameter
// lists, so an identity string minted from the enclosing type plus the member name is one
// string for three declarations — and the two that chain to `base(message)` and
// `base(message, inner)` mint the same string a *second* time for their base references.

using System;
using System.Collections.Generic;

namespace Surface.Attributes.Exceptions;

/// <summary>
/// 22.3: an exception class with the three conventional constructors, each chaining to a
/// different constructor of <see cref="Exception"/>.
/// </summary>
public class AttrLedgerFault : Exception
{
    /// <summary>The parameterless form, which leaves the runtime's default message.</summary>
    public AttrLedgerFault()
    {
    }

    /// <summary>The message form.</summary>
    public AttrLedgerFault(string message)
        : base(message)
    {
    }

    /// <summary>The wrapping form, which is what <c>InnerException</c> reads back.</summary>
    public AttrLedgerFault(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// 22.3: a property an index must not confuse with the inherited <c>Source</c> — this
    /// one is declared here and shadows nothing.
    /// </summary>
    public string Ledger { get; init; } = "unnamed";

    /// <summary>Reads the inherited members the clause names, so each is referenced.</summary>
    public IReadOnlyList<string> Describe() =>
    [
        Message,
        InnerException?.Message ?? "none",
        StackTrace ?? "unthrown",
        HelpLink ?? "undocumented",
        Source ?? "unsourced",
        Data.Count.ToString(),
        GetBaseException().GetType().Name,
        ToString(),
    ];
}

/// <summary>
/// 22.3: a second level of derivation, so the base reference is to a declaration in this
/// corpus rather than to the framework.
/// </summary>
public sealed class AttrPostingFault : AttrLedgerFault
{
    /// <summary>Chains to the derived message constructor, not to <c>Exception</c>'s.</summary>
    public AttrPostingFault(string message, int line)
        : base(message)
    {
        Line = line;
    }

    /// <summary>Which line of the posting file was refused.</summary>
    public int Line { get; }

    /// <summary>22.3: <c>ToString</c> is virtual, and this override is the reference.</summary>
    public override string ToString() => $"{base.ToString()} at line {Line}";
}
