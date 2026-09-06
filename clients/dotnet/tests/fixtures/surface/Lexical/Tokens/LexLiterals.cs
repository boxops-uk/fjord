// Clause 6.4.5 (literals), one type per subclause. A constant's *value* is a fact an index
// holds, and each type below declares several constants that hold the same value through
// different spellings — which is the collapse each of these rows is here to provoke.

namespace Surface.Lexical.Tokens;

/// <summary>6.4.5.2: the two boolean literals.</summary>
public static class LexBooleanLiterals
{
    /// <summary>The literal <c>true</c>.</summary>
    public const bool Yes = true;

    /// <summary>The literal <c>false</c>.</summary>
    public const bool No = false;

    /// <summary>A boolean constant folded from an expression rather than a literal.</summary>
    public const bool Folded = Yes && !No;
}

/// <summary>
/// 6.4.5.3: every integer literal form. <c>DecimalTwoFiveFive</c>, <c>HexLower</c>,
/// <c>HexUpper</c>, <c>BinaryLower</c>, <c>BinaryUpper</c> and <c>SeparatedHex</c> are six
/// spellings of 255.
/// </summary>
public static class LexIntegerLiterals
{
    /// <summary>Decimal.</summary>
    public const int DecimalTwoFiveFive = 255;

    /// <summary>Hexadecimal with a lowercase prefix.</summary>
    public const int HexLower = 0xff;

    /// <summary>Hexadecimal with an uppercase prefix and uppercase digits.</summary>
    public const int HexUpper = 0XFF;

    /// <summary>Binary with a lowercase prefix.</summary>
    public const int BinaryLower = 0b11111111;

    /// <summary>Binary with an uppercase prefix and digit separators.</summary>
    public const int BinaryUpper = 0B1111_1111;

    /// <summary>Digit separators, including one straight after the <c>0x</c> prefix.</summary>
    public const int SeparatedHex = 0x_00_ff;

    /// <summary>A separator inside a decimal literal.</summary>
    public const int Separated = 1_000_000;

    /// <summary>The <c>U</c> suffix.</summary>
    public const uint SuffixUpperU = 1U;

    /// <summary>The <c>u</c> suffix.</summary>
    public const uint SuffixLowerU = 2u;

    /// <summary>The <c>L</c> suffix.</summary>
    public const long SuffixUpperL = 3L;

    /// <summary>The <c>l</c> suffix, which is CS0078 because it reads as a one.</summary>
    public const long SuffixLowerL = 4l;

    /// <summary>The <c>UL</c> suffix in each of its four cases.</summary>
    public const ulong SuffixUpperUpper = 5UL;

    /// <summary>The <c>ul</c> suffix.</summary>
    public const ulong SuffixLowerLower = 6ul;

    /// <summary>The <c>Lu</c> suffix.</summary>
    public const ulong SuffixUpperLower = 7Lu;

    /// <summary>The <c>lU</c> suffix, CS0078 again.</summary>
    public const ulong SuffixLowerUpper = 8lU;

    /// <summary>The largest <c>int</c>, which fixes the literal's type at <c>int</c>.</summary>
    public const int LargestInt = 2147483647;

    /// <summary>The smallest <c>int</c>: a literal that only exists as a negation.</summary>
    public const int SmallestInt = -2147483648;

    /// <summary>A literal too large for <c>int</c>, so its type is <c>uint</c>.</summary>
    public const uint FirstUnsigned = 4294967295;

    /// <summary>A literal too large for <c>uint</c>, so its type is <c>long</c>.</summary>
    public const long FirstLong = 9223372036854775807;

    /// <summary>A literal too large for <c>long</c>, so its type is <c>ulong</c>.</summary>
    public const ulong FirstUnsignedLong = 18446744073709551615;

    /// <summary>Zero, which is the enum-conversion special case as well as a literal.</summary>
    public const int Zero = 0;
}

/// <summary>
/// 6.4.5.4: every real literal form. <c>PositiveZero</c> and <c>NegativeZero</c> compare
/// equal and have different bits; <c>OneFloat</c>, <c>OneFloatUpper</c> and
/// <c>OneFloatExponent</c> are three spellings of the same <c>float</c>.
/// </summary>
public static class LexRealLiterals
{
    /// <summary>No suffix, so the type is <c>double</c>.</summary>
    public const double Plain = 1.0;

    /// <summary>Leading dot, with the integer part left out.</summary>
    public const double LeadingDot = .5;

    /// <summary>Trailing dot is not a real literal, so this is an exponent with no dot.</summary>
    public const double NoDot = 1e10;

    /// <summary>An uppercase exponent with an explicit sign.</summary>
    public const double SignedExponent = 1E-10;

    /// <summary>Digit separators in the integer part, the fraction and the exponent.</summary>
    public const double SeparatedEverywhere = 1_0.0_1e+1_0;

    /// <summary>The <c>f</c> suffix.</summary>
    public const float OneFloat = 1.0f;

    /// <summary>The <c>F</c> suffix.</summary>
    public const float OneFloatUpper = 1.0F;

    /// <summary>The same <c>float</c> value written with an exponent.</summary>
    public const float OneFloatExponent = 1e0f;

    /// <summary>The <c>d</c> suffix.</summary>
    public const double OneDouble = 1.0d;

    /// <summary>The <c>D</c> suffix.</summary>
    public const double OneDoubleUpper = 1.0D;

    /// <summary>The <c>m</c> suffix, which is a decimal and not a binary float.</summary>
    public const decimal OneDecimal = 1.0m;

    /// <summary>The <c>M</c> suffix, with trailing zeroes that <c>decimal</c> keeps.</summary>
    public const decimal OneDecimalUpper = 1.00M;

    /// <summary>Positive zero.</summary>
    public const double PositiveZero = 0.0;

    /// <summary>
    /// Negative zero: equal to <c>PositiveZero</c> under <c>==</c> and a different
    /// bit pattern, so a constant keyed on numeric equality loses one of the two.
    /// </summary>
    public const double NegativeZero = -0.0;
}

/// <summary>
/// 6.4.5.5: every character literal form, including all seven simple escapes and both
/// hexadecimal spellings. <c>Ascii</c>, <c>HexShort</c>, <c>HexPadded</c>,
/// <c>UnicodeShort</c> and <c>UnicodeLong</c> are five spellings of <c>'A'</c>.
/// </summary>
public static class LexCharacterLiterals
{
    /// <summary>A plain character.</summary>
    public const char Ascii = 'A';

    /// <summary>The <c>\x</c> escape with the minimum digit count.</summary>
    public const char HexShort = '\x41';

    /// <summary>The <c>\x</c> escape padded to four digits.</summary>
    public const char HexPadded = '\x0041';

    /// <summary>The four-digit Unicode escape.</summary>
    public const char UnicodeShort = '\u0041';

    /// <summary>The eight-digit Unicode escape.</summary>
    public const char UnicodeLong = '\U00000041';

    /// <summary>The escaped single quote.</summary>
    public const char Quote = '\'';

    /// <summary>The escaped double quote, which needs no escaping in a character literal.</summary>
    public const char DoubleQuote = '\"';

    /// <summary>The escaped backslash.</summary>
    public const char Backslash = '\\';

    /// <summary>Null.</summary>
    public const char Null = '\0';

    /// <summary>Alert.</summary>
    public const char Alert = '\a';

    /// <summary>Backspace.</summary>
    public const char Backspace = '\b';

    /// <summary>Form feed.</summary>
    public const char FormFeed = '\f';

    /// <summary>New line.</summary>
    public const char NewLine = '\n';

    /// <summary>Carriage return.</summary>
    public const char Return = '\r';

    /// <summary>Horizontal tab.</summary>
    public const char Tab = '\t';

    /// <summary>Vertical tab.</summary>
    public const char VerticalTab = '\v';

    /// <summary>A character outside ASCII, written literally.</summary>
    public const char Accented = 'é';

    /// <summary>An unpaired high surrogate, which is a <c>char</c> and not a rune.</summary>
    public const char LoneSurrogate = '\uD83D';
}

/// <summary>
/// 6.4.5.6: every string literal form. <c>PlainAA</c>, <c>EscapedAA</c>,
/// <c>VerbatimAA</c> and <c>MixedAA</c> are four spellings of <c>"aA"</c>.
/// </summary>
public static class LexStringLiterals
{
    /// <summary>Both characters written literally.</summary>
    public const string PlainAA = "aA";

    /// <summary>The second character as a Unicode escape.</summary>
    public const string EscapedAA = "a\u0041";

    /// <summary>The verbatim form, where a backslash would be data.</summary>
    public const string VerbatimAA = @"aA";

    /// <summary>The second character as a hexadecimal escape.</summary>
    public const string MixedAA = "a\x41";

    /// <summary>The empty string, which is not <c>null</c>.</summary>
    public const string Empty = "";

    /// <summary>The empty string in the verbatim form.</summary>
    public const string VerbatimEmpty = @"";

    /// <summary>A string containing an embedded null.</summary>
    public const string WithNull = "before\0after";

    /// <summary>A regular literal whose backslashes are escapes.</summary>
    public const string RegularPath = "C:\\temp\\file.txt";

    /// <summary>The verbatim literal of the same path, where backslashes are data.</summary>
    public const string VerbatimPath = @"C:\temp\file.txt";

    /// <summary>A doubled quote, which is how a verbatim literal escapes one.</summary>
    public const string VerbatimQuote = @"he said ""so""";

    /// <summary>The same content as a regular literal with backslash escapes.</summary>
    public const string RegularQuote = "he said \"so\"";

    /// <summary>A verbatim literal spanning two lines, so the terminator is data.</summary>
    public const string VerbatimTwoLines = @"first
second";

    /// <summary>A surrogate pair written as one eight-digit escape.</summary>
    public const string AstralEscape = "\U0001F600";

    /// <summary>The same surrogate pair written as two four-digit escapes.</summary>
    public const string AstralSurrogates = "\uD83D\uDE00";

    /// <summary>The same character written literally, as four UTF-8 bytes.</summary>
    public const string AstralLiteral = "😀";

    /// <summary>Every simple escape in one string.</summary>
    public const string AllSimpleEscapes = "\'\"\\\0\a\b\f\n\r\t\v";
}

/// <summary>6.4.5.7: the null literal, which has no type of its own.</summary>
public static class LexNullLiteral
{
    /// <summary>A constant reference initialised from the null literal.</summary>
    public const string Nothing = null;

    /// <summary>The literal reached through a default expression instead.</summary>
    public static readonly string? NothingByDefault = default;

    /// <summary>A nullable value type holding no value.</summary>
    public static readonly int? NoNumber = null;

    /// <summary>Reads both, so the null literal is compared as well as assigned.</summary>
    public static bool BothAreAbsent() => Nothing is null && NothingByDefault is null;
}
