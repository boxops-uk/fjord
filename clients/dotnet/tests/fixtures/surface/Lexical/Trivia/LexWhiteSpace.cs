// Clause 6.3.4 (white space): any character of Unicode class Zs, plus U+0009, U+000B
// and U+000C. Every separator below is one of those rather than a space, so a member's
// column is not the column a reader gets by counting spaces.

namespace Surface.Lexical.Trivia;

/// <summary>Declarations separated by the exotic white space of 6.3.4.</summary>
public sealed class LexWhiteSpace
{
    /// <summary>Separated by U+0009 CHARACTER TABULATION.</summary>
	public	const	int	Tabulated	=	1;
    /// <summary>Separated by U+000B LINE TABULATION, which is white space and not a terminator.</summary>
    publicconstintVerticallyTabulated=2;
    /// <summary>Separated by U+000C FORM FEED, likewise white space and not a terminator.</summary>
    publicconstintFormFed=3;
    /// <summary>Separated by U+00A0 NO-BREAK SPACE (Zs).</summary>
    public const int NoBreak = 4;
    /// <summary>Separated by U+1680 OGHAM SPACE MARK (Zs), which is not blank when rendered.</summary>
    public const int Ogham = 5;
    /// <summary>Separated by U+2000 EN QUAD (Zs).</summary>
    public const int EnQuad = 6;
    /// <summary>Separated by U+200A HAIR SPACE (Zs).</summary>
    public const int Hair = 7;
    /// <summary>Separated by U+202F NARROW NO-BREAK SPACE (Zs).</summary>
    public const int NarrowNoBreak = 8;
    /// <summary>Separated by U+205F MEDIUM MATHEMATICAL SPACE (Zs).</summary>
    public const int MediumMathematical = 9;
    /// <summary>Separated by U+3000 IDEOGRAPHIC SPACE (Zs), which is two columns wide.</summary>
    public　const　int　Ideographic　=　10;

    /// <summary>Reads every constant, so none of them is unreferenced.</summary>
    public int Total() =>
        Tabulated + VerticallyTabulated + FormFed + NoBreak + Ogham
        + EnQuad + Hair + NarrowNoBreak + MediumMathematical + Ideographic;
}
