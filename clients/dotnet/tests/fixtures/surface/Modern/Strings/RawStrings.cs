namespace Surface.Modern.Strings;

/// <summary>C# 11 and C# 13 — the literal forms the language grew after C# 9.</summary>
public static class RawStrings
{
    /// <summary>C# 11 — Raw string literals: the single-line form.</summary>
    public const string SingleLine = """a "quoted" word""";

    /// <summary>
    /// C# 11 — Raw string literals: the multi-line form, whose indentation is stripped to
    /// the closing delimiter's column. The value contains no leading spaces even though the
    /// source does, which is a fact about the constant that only the lexer knows.
    /// </summary>
    public const string MultiLine = """
        {
          "key": "value"
        }
        """;

    /// <summary>C# 11 — Raw string literals: five quotes, so three can be content.</summary>
    public const string Nested = """""
        a """ literal inside a literal
        """"";

    /// <summary>
    /// C# 11 — Raw string literals: the interpolated form with a doubled brace count, so a
    /// single brace is content and a doubled one is a hole.
    /// </summary>
    public static string Template(int id) => $$"""
        { "id": {{id}}, "label": "{{ConstantInterpolation.Label}}" }
        """;

    /// <summary>
    /// C# 11 — Newlines in string interpolation expressions: the hole below spans four lines.
    /// Before C# 11 a hole in a non-verbatim interpolated string had to fit on one line.
    /// </summary>
    public static string Spanning(int left, int right) => $"sum: {
        left
        +
        right
    }";

    /// <summary>
    /// C# 11 — UTF-8 string literals: the <c>u8</c> suffix, whose type is
    /// <c>ReadOnlySpan&lt;byte&gt;</c> rather than <c>string</c> — a literal whose constant
    /// is bytes and whose type is a ref struct.
    /// </summary>
    public static ReadOnlySpan<byte> Magic => "FJORD"u8;

    /// <summary>C# 11 — a UTF-8 literal in a local, and its length, so the span is read.</summary>
    public static int MagicLength()
    {
        ReadOnlySpan<byte> magic = "FJORD\n"u8;

        return magic.Length;
    }

    /// <summary>C# 13 — New escape sequence <c>\e</c>: ESCAPE, U+001B.</summary>
    public const string Reset = "\e[0m";

    /// <summary>
    /// C# 13 — <c>\e</c> holds the same code point as the two older spellings, which is how
    /// the corpus can state what the new escape means without depending on a console.
    /// </summary>
    public static bool EscapesAgree() => Reset[0] == '\u001b' && Reset[0] == '\x1b';
}
