// Clause 23.5.6.1 (caller-info attributes, general), 23.5.6.2 (CallerLineNumber),
// 23.5.6.3 (CallerFilePath) and 23.5.6.4 (CallerMemberName).
//
// These three are the only attributes in the clause that change what an *argument list*
// means. Each marks an optional parameter, and at a call site that omits that argument the
// compiler substitutes a literal — a line number, this file's path as the compiler saw it,
// or the name of the enclosing member. So a caller-info argument is an argument with no
// expression: there is no token at the call site to attribute it to, and there is no
// reference from the call site to anything.
//
// That is the hazard, and it has two halves worth asserting separately.
//
//   * The *substituted* half. `Record()` below is called nine times with no arguments, and
//     each call passes three values that appear nowhere in the source. An index that
//     records an argument count per call site reads 0 at every one of them, while the
//     assembly's call sites all pass 3. `RecordsExplicitly` is the control: one call that
//     writes all three arguments out, where the count agrees.
//
//   * The *member name* half. CallerMemberName is the name of the enclosing *member*, not
//     of the enclosing method, and the two differ wherever a member has accessors: a call
//     inside `Weight`'s getter reports "Weight" and not "get_Weight". Inside a constructor
//     it reports ".ctor", inside the static constructor ".cctor", and inside a lambda or a
//     local function it reports the member that encloses it — a name that belongs to a
//     declaration two levels up from the call.
//
// The line-number substitutions are the reason nothing may be inserted above a call site in
// this file without moving a value the index holds.

using System;
using System.Runtime.CompilerServices;

namespace Surface.Attributes.Reserved;

/// <summary>
/// 23.5.6.1: the sink. Three optional parameters, one per caller-info attribute, in the
/// order the clause presents them.
/// </summary>
public static class AttrCallerLog
{
    private static readonly System.Collections.Generic.List<string> Entries = [];

    /// <summary>
    /// 23.5.6.2, 23.5.6.3, 23.5.6.4: the three attributes on three optional parameters.
    /// </summary>
    /// <param name="what">The one argument a caller actually writes.</param>
    /// <param name="line">Substituted with the call site's line number.</param>
    /// <param name="path">Substituted with the call site's file path.</param>
    /// <param name="member">Substituted with the calling member's name.</param>
    /// <returns>How many entries the log holds.</returns>
    public static int Record(
        string what,
        [CallerLineNumber] int line = 0,
        [CallerFilePath] string path = "",
        [CallerMemberName] string member = "")
    {
        Entries.Add($"{member}@{System.IO.Path.GetFileName(path)}:{line} {what}");
        return Entries.Count;
    }

    /// <summary>
    /// 23.5.6.2: the line number alone, so a query can find a parameter carrying exactly
    /// one caller-info attribute.
    /// </summary>
    /// <param name="line">Substituted with the call site's line number.</param>
    /// <returns>The line the caller sits on.</returns>
    public static int Line([CallerLineNumber] int line = 0) => line;

    /// <summary>
    /// 23.5.6.3: the file path alone. Its default is the empty string and its value never
    /// is, which is the only way to tell a substituted argument from a defaulted one.
    /// </summary>
    /// <param name="path">Substituted with the call site's file path.</param>
    /// <returns>The file the caller sits in.</returns>
    public static string Path([CallerFilePath] string path = "") => path;

    /// <summary>
    /// 23.5.6.4: the member name alone.
    /// </summary>
    /// <param name="member">Substituted with the calling member's name.</param>
    /// <returns>The member the caller sits in.</returns>
    public static string Member([CallerMemberName] string member = "") => member;

    /// <summary>
    /// 23.5.6.1: a caller-info parameter on a *constructor*, which is where the clause's
    /// "member name" is a name no source identifier spells.
    /// </summary>
    public sealed class Entry
    {
        /// <summary>Records where it was constructed.</summary>
        /// <param name="member">Substituted with the constructing member's name.</param>
        /// <param name="line">Substituted with the construction's line number.</param>
        public Entry([CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
        {
            Member = member;
            Line = line;
        }

        /// <summary>Who constructed it.</summary>
        public string Member { get; }

        /// <summary>Where.</summary>
        public int Line { get; }
    }
}

/// <summary>
/// 23.5.6.4: one call per kind of enclosing member, so every shape of substituted name is
/// present in the assembly and absent from the source.
/// </summary>
public sealed class AttrCallerSites
{
    private int _weight;

    /// <summary>Substitutes ".cctor", which is not a legal identifier.</summary>
    static AttrCallerSites() => AttrCallerLog.Record("from the static constructor");

    /// <summary>Substitutes ".ctor", likewise.</summary>
    public AttrCallerSites()
    {
        _weight = AttrCallerLog.Record("from the instance constructor");
        Constructed = new AttrCallerLog.Entry();
    }

    /// <summary>What the constructor recorded about itself.</summary>
    public AttrCallerLog.Entry Constructed { get; }

    /// <summary>
    /// 23.5.6.4: inside an accessor, the substituted name is the *property's*, so the
    /// value is "Weight" and the enclosing method is `get_Weight`.
    /// </summary>
    public int Weight
    {
        get
        {
            AttrCallerLog.Record("from a getter");
            return _weight;
        }

        set
        {
            AttrCallerLog.Record("from a setter");
            _weight = value;
        }
    }

    /// <summary>
    /// 23.5.6.4: inside an event accessor, the substituted name is the event's. This is
    /// the one event in this type, with explicit accessors so there is a body to call from.
    /// </summary>
    public event EventHandler Changed
    {
        add => AttrCallerLog.Record($"from an add accessor for {value.Method.Name}");
        remove => AttrCallerLog.Record($"from a remove accessor for {value.Method.Name}");
    }

    /// <summary>23.5.6.4: inside an indexer, the substituted name is "Item".</summary>
    /// <param name="slot">Which slot.</param>
    /// <returns>The slot's weight.</returns>
    public int this[int slot]
    {
        get
        {
            AttrCallerLog.Record("from an indexer");
            return _weight + slot;
        }
    }

    /// <summary>
    /// 23.5.6.4: inside a lambda and inside a local function, the substituted name is the
    /// enclosing member's — a declaration two levels above the call.
    /// </summary>
    /// <returns>Both substituted names, which are the same string.</returns>
    public string FromNestedBodies()
    {
        Func<string> fromLambda = () => AttrCallerLog.Member();

        static string FromLocal() => AttrCallerLog.Member();

        return $"{fromLambda()}/{FromLocal()}";
    }

    /// <summary>
    /// 23.5.6.1: the control. Every argument is written out, so the compiler substitutes
    /// nothing and the call site's argument count in the source is the one in the assembly.
    /// </summary>
    /// <returns>How many entries the log holds.</returns>
    public int RecordsExplicitly() =>
        AttrCallerLog.Record("written out in full", 0, "not-a-real-path.cs", "not-a-real-member");

    /// <summary>
    /// 23.5.6.2 and 23.5.6.3: two calls to the single-parameter forms, whose whole result
    /// is a value this file's own layout decides.
    /// </summary>
    /// <returns>The line and the file of the two calls below.</returns>
    public string WhereAmI() => $"{AttrCallerLog.Line()} in {AttrCallerLog.Path()}";

    /// <summary>Exercises the event and the indexer, so both accessors are reached.</summary>
    /// <returns>The weight after everything has run.</returns>
    public int Run()
    {
        Changed += OnChanged;
        Changed -= OnChanged;
        Weight = this[1];
        FromNestedBodies();
        RecordsExplicitly();
        WhereAmI();
        return Weight;
    }

    private void OnChanged(object? sender, EventArgs args) => _weight++;
}
