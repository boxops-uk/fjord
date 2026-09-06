// Clause 6.4.2 (Unicode character escape sequences) and 6.4.3 (identifiers). Two
// identifiers are the same when they are identical after the `@` prefix is dropped, every
// unicode-escape-sequence is replaced by the character it denotes, and every formatting
// character is removed. Each of those three transformations gets a type here, because each
// one is a pair of spellings that must reach one symbol.

namespace Surface.Lexical.Tokens;

/// <summary>
/// 6.4.2: one declaration reached through three spellings — plain, the four-digit escape
/// and the eight-digit escape. The declaration and every reference are one identifier.
/// </summary>
public sealed class LexEscapedSpellings
{
    /// <summary>Declared with no escape at all.</summary>
    public const int AlphaCount = 1;

    /// <summary>Declared with the escape, so the plain reference below is the odd spelling.</summary>
    public const int \u0042etaCount = 2;

    /// <summary>An escape inside an identifier rather than at its start: <c>Gam\u006Da</c>.</summary>
    public const int Gam\u006Da = 3;

    /// <summary>Reads <c>AlphaCount</c> through <c>\u0041lphaCount</c>.</summary>
    public int ThroughShortEscape() => \u0041lphaCount;

    /// <summary>Reads <c>AlphaCount</c> through <c>\U00000041lphaCount</c>.</summary>
    public int ThroughLongEscape() => \U00000041lphaCount;

    /// <summary>Reads the escape-declared member with no escape.</summary>
    public int PlainAfterEscapedDeclaration() => BetaCount;

    /// <summary>Reads the interior-escape member with no escape.</summary>
    public int PlainGamma() => Gamma;
}

/// <summary>
/// 6.4.3: verbatim identifiers. The <c>@</c> is not part of the identifier, so a member
/// declared <c>@Ordinary</c> and read as <c>Ordinary</c> is one member — and a member
/// named for a keyword has no other spelling.
/// </summary>
public sealed class LexVerbatimIdentifiers
{
    /// <summary>A member whose identifier is the keyword <c>class</c>.</summary>
    public const int @class = 1;

    /// <summary>A member whose identifier is the keyword <c>int</c>.</summary>
    public const int @int = 2;

    /// <summary>A member whose identifier is the keyword <c>if</c>.</summary>
    public const int @if = 3;

    /// <summary>A member whose identifier is the keyword <c>this</c>.</summary>
    public const int @this = 4;

    /// <summary>A member whose identifier is the keyword <c>base</c>.</summary>
    public const int @base = 5;

    /// <summary>A member whose identifier is the keyword <c>namespace</c>.</summary>
    public const int @namespace = 6;

    /// <summary>A member whose identifier is the keyword <c>operator</c>.</summary>
    public const int @operator = 7;

    /// <summary>A member whose identifier is the boolean literal <c>true</c>.</summary>
    public const int @true = 8;

    /// <summary>A member whose identifier is the null literal.</summary>
    public const int @null = 9;

    /// <summary>Declared verbatim although the identifier needs no escaping.</summary>
    public const int @Ordinary = 10;

    /// <summary>Reads the keyword-named members, which only the verbatim form can name.</summary>
    public int ReadKeywordNames() =>
        @class + @int + @if + @this + @base + @namespace + @operator + @true + @null;

    /// <summary>Reads <c>@Ordinary</c> without the <c>@</c>: one identifier, two spellings.</summary>
    public int WithoutTheAt() => Ordinary;
}

/// <summary>
/// 6.4.3: formatting characters (Unicode class Cf) are removed before two identifiers are
/// compared. Each member below is declared with a Cf character inside it and read without
/// one, so the two spellings differ in bytes and name one symbol. Declaring *both*
/// spellings in one type is CS0102, which is why only one spelling of each is declared.
/// </summary>
public sealed class LexFormattingCharacters
{
    /// <summary>Declared as <c>zero</c> + U+200D ZERO WIDTH JOINER + <c>width</c>.</summary>
    public const int zero‍width = 1;

    /// <summary>Declared as <c>non</c> + U+200C ZERO WIDTH NON-JOINER + <c>joiner</c>.</summary>
    public const int non‌joiner = 2;

    /// <summary>Declared as <c>soft</c> + U+00AD SOFT HYPHEN + <c>hyphen</c>.</summary>
    public const int soft­hyphen = 3;

    /// <summary>Declared as <c>word</c> + U+2060 WORD JOINER + <c>joiner</c>.</summary>
    public const int word⁠joiner = 4;

    /// <summary>Declared as <c>byte</c> + U+FEFF, which is a formatting character here
    /// rather than a byte order mark, + <c>order</c>.</summary>
    public const int byteOrder﻿Mark = 5;

    /// <summary>Reads every one of them with the formatting characters left out.</summary>
    public int WithoutTheFormatCharacters() =>
        zerowidth + nonjoiner + softhyphen + wordjoiner + byteOrderMark;
}

/// <summary>
/// 6.4.3: two identifiers that are canonically equivalent under Unicode normalisation and
/// are *not* the same identifier, because 6.4.3 compares code points and normalises
/// nothing. Both members are named <c>café</c> as a reader sees it.
/// </summary>
public sealed class LexCanonicalPair
{
    /// <summary>Spelled <c>caf</c> + U+00E9 LATIN SMALL LETTER E WITH ACUTE.</summary>
    public const int café = 1;

    /// <summary>Spelled <c>cafe</c> + U+0301 COMBINING ACUTE ACCENT.</summary>
    public const int café = 2;

    /// <summary>Reads the precomposed member.</summary>
    public int ReadPrecomposed() => café;

    /// <summary>Reads the decomposed member.</summary>
    public int ReadDecomposed() => café;
}

/// <summary>
/// 6.4.3: identifier characters drawn from outside ASCII — every class the production
/// admits, plus the three folding hazards: a fullwidth letter, a dotted capital I and
/// a compatibility ligature. A supplementary-plane letter is *not* here: U+1D504 is Lu
/// and 6.4.3 admits it, but Roslyn lexes identifiers over UTF-16 units and rejects it
/// as CS1056 in both its literal and its escaped spelling.
/// </summary>
public sealed class LexNonAsciiIdentifiers
{
    /// <summary>Greek letters (Lu and Ll).</summary>
    public const int Σύνολο = 1;

    /// <summary>Cyrillic letters, with a precomposed U+0451.</summary>
    public const int Счёт = 2;

    /// <summary>CJK ideographs (Lo), which have no case.</summary>
    public const int 合計 = 3;

    /// <summary>An underscore start and an underscore part (Pc).</summary>
    public const int _under_score = 4;

    /// <summary>A single underscore, which is an identifier as well as a discard.</summary>
    public const int _ = 5;

    /// <summary>A letter-number (Nl) start: U+216B ROMAN NUMERAL TWELVE.</summary>
    public const int Ⅻth = 6;

    /// <summary>A connector punctuation (Pc) part: U+203F UNDERTIE.</summary>
    public const int tie‿d = 7;

    /// <summary>A combining mark (Mn) part: <c>mark</c> + U+0301, which may not start one.</summary>
    public const int marḱ = 8;

    /// <summary>U+FF21 FULLWIDTH LATIN CAPITAL LETTER A (Lu): one UTF-16 unit, three
    /// UTF-8 bytes, so a column counted in bytes is not the column 6.3.4 gives.</summary>
    public const int Ａlpha = 9;

    /// <summary>Reaches the fullwidth identifier through its four-digit escape.</summary>
    public int ThroughFullwidthEscape() => \uFF21lpha;

    /// <summary>U+0130 LATIN CAPITAL LETTER I WITH DOT ABOVE, whose Turkish lowercase is
    /// two code points — so a case-insensitive lookup can merge it with the next member.</summary>
    public const int İnvariant = 10;

    /// <summary>Plain ASCII <c>Invariant</c>, a different identifier from the one above.</summary>
    public const int Invariant = 11;

    /// <summary>U+FB01 LATIN SMALL LIGATURE FI (Ll), which NFKC folds to <c>fi</c> — so a
    /// compatibility-normalising index merges it with the next member.</summary>
    public const int ﬁle = 12;

    /// <summary>Plain ASCII <c>file</c>, which is a contextual keyword and a legal
    /// member name, and a different identifier from the ligature spelling.</summary>
    public const int file = 13;

    /// <summary>Reads the rest, so every identifier above has a reference.</summary>
    public int Total() =>
        Σύνολο + Счёт + 合計 + _under_score + _ + Ⅻth + tie‿d + marḱ
        + İnvariant + Invariant + ﬁle + file;
}
