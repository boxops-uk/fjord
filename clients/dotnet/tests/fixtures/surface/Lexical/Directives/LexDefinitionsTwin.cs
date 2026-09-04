#define SURFACE_LEXICAL_SHARED

// Clause 6.5.2 (conditional compilation symbols): the second declaration of
// `SURFACE_LEXICAL_SHARED`. Defining a symbol twice across two files is not an error and
// not a merge of two things into one — the symbol's scope is the file it is defined in, so
// these are two *separate* symbols that happen to be spelled alike, and the same
// `#define` in a third file would be a third. Nothing in the source says which file's
// definition a given `#if` consulted.

namespace Surface.Lexical.Directives;

/// <summary>6.5.2: reads the twin definition of the shared symbol.</summary>
public static class LexSharedSymbolTwin
{
#if SURFACE_LEXICAL_SHARED
    /// <summary>Selected by this file's own definition of the shared symbol.</summary>
    public const int Selected = 1;
#else
    /// <summary>Not selected.</summary>
    public const int Selected = -1;
#endif

#if SURFACE_LEXICAL_ON
    /// <summary>Not selected — the other file's <c>#define</c> does not reach this file.</summary>
    public const int LeakedFromTheOtherFile = -2;
#else
    /// <summary>
    /// Selected, which is the fact worth a query: <c>SURFACE_LEXICAL_ON</c> is defined in
    /// LexDefinitions.cs and undefined here, so a symbol table without a file column
    /// answers this wrongly in one direction or the other.
    /// </summary>
    public const int LeakedFromTheOtherFile = 2;
#endif

    /// <summary>Reads both constants.</summary>
    public static int Total() => Selected + LeakedFromTheOtherFile;
}
