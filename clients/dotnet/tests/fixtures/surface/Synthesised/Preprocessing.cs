#define SYN_LOCAL
#undef SYN_RETIRED

using System;

namespace Surface.Synthesised;

/// <summary><c>SymbolKind.Preprocessing</c> — conditional compilation symbols (6.5).</summary>
/// <remarks>
/// A preprocessing symbol is not a member of anything: it has no containing type, no
/// containing namespace, no type of its own, and it exists only in the file that defines
/// it and the expressions that test it. <c>SYN_LOCAL</c> is defined at the top of this
/// file, so it is in scope for the whole file and for no other file — while a symbol from
/// <c>&lt;DefineConstants&gt;</c> would be in scope for every file and defined in none.
/// Both are <c>SymbolKind.Preprocessing</c>, and only one of them has a declaration an
/// index can point at.
///
/// The disabled branches below are real text the compiler tokenises and discards. The
/// types they declare are not declared, so a reader can check whether an index invented
/// them: <c>SynRetiredShape</c> must appear in no query's answer.
/// </remarks>
public class SynPreprocessed
{
#if SYN_LOCAL
    /// <summary>Compiled, because <c>SYN_LOCAL</c> is defined in this file.</summary>
    public string Which => "local";
#else
    /// <summary>Never compiled.</summary>
    public string Which => "absent";
#endif

#if SYN_RETIRED
    /// <summary>Never compiled — the symbol was undefined above.</summary>
    public sealed class SynRetiredShape
    {
    }
#endif

#if SYN_LOCAL && !SYN_RETIRED
    /// <summary>A compound condition over both symbols.</summary>
    public bool Both => true;
#elif SYN_RETIRED
    /// <summary>Never compiled.</summary>
    public bool Both => false;
#endif

    /// <summary>A conditional method call, which the compiler may erase (23.5.3).</summary>
    /// <param name="message">What to trace.</param>
    [System.Diagnostics.Conditional("SYN_LOCAL")]
    public void Trace(string message) => GC.KeepAlive(message);

    /// <summary>
    /// A call to a conditional method whose symbol <em>is</em> defined here, so the call
    /// stands. A conditional method's calls are erased per call site, from the file's
    /// point of view, not per declaration.
    /// </summary>
    public void Exercise() => Trace("kept");
}
