#define SURFACE_LEXICAL_BRANCH_TWO

// Clause 6.5.1 (general), 6.5.5 (conditional compilation directives), 6.5.6 (diagnostic
// directives) and 6.5.7 (region directives).
//
// 6.5.1's rules are about the *line*: a directive may be preceded by white space, white
// space may sit between the `#` and the directive name, and a single-line comment may
// follow the directive on the same line. All three appear below.
//
// The load-bearing shape is `LexBranch`: three declarations of one type name in one file,
// of which the pre-processor keeps exactly one. A walk over the raw text finds three; a
// walk over the parsed tree finds one.

namespace Surface.Lexical.Directives;

#if SURFACE_LEXICAL_UNREACHABLE
#error This line is a diagnostic directive that never runs, which is how 6.5.6's error
#endif

#warning A 6.5.6 diagnostic directive that only warns, so the build stays green.

#region The 6.5.7 named region that holds the conditional branches
#if SURFACE_LEXICAL_BRANCH_ONE
/// <summary>6.5.5: the first branch, which is not compiled.</summary>
public sealed class LexBranch
{
    /// <summary>Which branch the pre-processor kept.</summary>
    public int Which() => 1;
}
#elif SURFACE_LEXICAL_BRANCH_TWO
/// <summary>6.5.5: the second branch, which is the one compiled.</summary>
public sealed class LexBranch
{
    /// <summary>Which branch the pre-processor kept.</summary>
    public int Which() => 2;
}
#else
/// <summary>6.5.5: the fallback branch, which is not compiled.</summary>
public sealed class LexBranch
{
    /// <summary>Which branch the pre-processor kept.</summary>
    public int Which() => 3;
}
#endif
#endregion

#region
/// <summary>6.5.7: declared inside a region with no name.</summary>
public sealed class LexUnnamedRegion
{
    /// <summary>Something to reference.</summary>
    public const int Tag = 1;
}
#endregion

#region Outer region
#region Inner region
/// <summary>6.5.7: declared inside nested regions, which need not align with declarations.</summary>
public sealed class LexNestedRegions
{
    #region A region that opens inside a type body and closes outside it
    /// <summary>A member inside a region that the type's closing brace does not end.</summary>
    public const int Tag = 2;
}
    #endregion
#endregion
#endregion

/// <summary>
/// 6.5.5 and 6.5.1: nested conditional directives, with the 6.5.1 line-shape oddities —
/// leading white space, white space between the <c>#</c> and the name, and a trailing
/// single-line comment.
/// </summary>
public static class LexNestedConditionals
{
  #if SURFACE_LEXICAL_BRANCH_TWO // a single-line comment after the directive
    #  if !SURFACE_LEXICAL_BRANCH_ONE
    /// <summary>Selected by the inner arm of a nested pair.</summary>
    public const int Inner = 1;
    #  else
    /// <summary>Not selected.</summary>
    public const int Inner = -1;
    #  endif
  #else
    /// <summary>Not selected.</summary>
    public const int Inner = -2;
  #endif

    /// <summary>Reads the surviving branch of <c>LexBranch</c> as well as its own constant.</summary>
    public static int Total() => Inner + new LexBranch().Which() + LexUnnamedRegion.Tag
        + LexNestedRegions.Tag;
}
