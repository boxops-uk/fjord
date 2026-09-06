#define SURFACE_LEXICAL_ON
#define SURFACE_LEXICAL_TEMPORARY
#define SURFACE_LEXICAL_SHARED
#undef SURFACE_LEXICAL_TEMPORARY

// Clause 6.5.2 (conditional compilation symbols), 6.5.3 (pre-processing expressions) and
// 6.5.4 (definition directives). The definition directives above have to precede every
// token in the file, so they sit before this comment's own clause list only because a
// comment is not a token. `SURFACE_LEXICAL_SHARED` is defined here *and* in
// LexDefinitionsTwin.cs: a conditional compilation symbol lives in no namespace and has no
// declaring type, so those are two declarations of one name with nothing to tell them apart.

namespace Surface.Lexical.Directives;

/// <summary>
/// 6.5.3: pre-processing expressions, which admit only symbols, <c>true</c>, <c>false</c>,
/// <c>!</c>, <c>&amp;&amp;</c>, <c>||</c>, <c>==</c>, <c>!=</c> and parentheses. Each
/// constant below records which expression selected it.
/// </summary>
public static class LexPreprocessingExpressions
{
#if SURFACE_LEXICAL_ON
    /// <summary>Selected by a bare symbol reference.</summary>
    public const int BareSymbol = 1;
#else
    /// <summary>Not selected: the bare symbol is defined.</summary>
    public const int BareSymbol = -1;
#endif

#if !SURFACE_LEXICAL_TEMPORARY
    /// <summary>Selected by <c>!</c> over a symbol that <c>#undef</c> removed.</summary>
    public const int Negated = 2;
#else
    /// <summary>Not selected: the symbol was undefined before this line.</summary>
    public const int Negated = -2;
#endif

#if SURFACE_LEXICAL_ON && !SURFACE_LEXICAL_TEMPORARY
    /// <summary>Selected by a conjunction.</summary>
    public const int Conjunction = 3;
#else
    /// <summary>Not selected.</summary>
    public const int Conjunction = -3;
#endif

#if SURFACE_LEXICAL_TEMPORARY || SURFACE_LEXICAL_SHARED
    /// <summary>Selected by a disjunction whose second operand is the defined one.</summary>
    public const int Disjunction = 4;
#else
    /// <summary>Not selected.</summary>
    public const int Disjunction = -4;
#endif

#if SURFACE_LEXICAL_ON == true
    /// <summary>Selected by equality against the <c>true</c> literal.</summary>
    public const int EqualityAgainstTrue = 5;
#else
    /// <summary>Not selected.</summary>
    public const int EqualityAgainstTrue = -5;
#endif

#if SURFACE_LEXICAL_TEMPORARY != true
    /// <summary>Selected by inequality against the <c>true</c> literal.</summary>
    public const int InequalityAgainstTrue = 6;
#else
    /// <summary>Not selected.</summary>
    public const int InequalityAgainstTrue = -6;
#endif

#if (SURFACE_LEXICAL_ON || SURFACE_LEXICAL_TEMPORARY) && !(false)
    /// <summary>Selected by a parenthesised expression over the literal <c>false</c>.</summary>
    public const int Parenthesised = 7;
#else
    /// <summary>Not selected.</summary>
    public const int Parenthesised = -7;
#endif

#if SURFACE_LEXICAL_UNDEFINED
    /// <summary>Not selected: the symbol was never defined anywhere.</summary>
    public const int NeverDefined = -8;
#else
    /// <summary>Selected because an undefined symbol evaluates to <c>false</c>.</summary>
    public const int NeverDefined = 8;
#endif

    /// <summary>Sums the selected constants, which is 36 when every branch went the way
    /// its summary says it did.</summary>
    public static int Total() =>
        BareSymbol + Negated + Conjunction + Disjunction + EqualityAgainstTrue
        + InequalityAgainstTrue + Parenthesised + NeverDefined;
}
