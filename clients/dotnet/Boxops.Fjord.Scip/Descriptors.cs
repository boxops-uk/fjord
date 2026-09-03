namespace Boxops.Fjord.Scip;

/// <summary>
/// Reading a display name back out of a SCIP symbol, when the index did not give one.
/// </summary>
/// <remarks>
/// <para>
/// <b>The last descriptor, and only the last.</b> A SCIP symbol ends in a run of
/// descriptors — <c>Namespace/Type#method().</c> — each with a suffix character saying
/// what it is. What a search index wants is the short name somebody types, which is the
/// final descriptor with its suffix and its disambiguator removed.
/// </para>
/// <para>
/// <b>`display_name` is preferred wherever the index carries it</b>, because a producer
/// knows its own language's spelling and this does not: a C++ operator, a Scala given, a
/// Rust impl block all have names their own indexer can state and no amount of
/// descriptor-splitting can recover.
/// </para>
/// <para>
/// <b>Backticks are the escape, and they are unwrapped.</b> A descriptor whose name is
/// not an identifier is wrapped in backticks with internal ones doubled, so a name that
/// arrives wrapped is unwrapped rather than shown with its quoting.
/// </para>
/// </remarks>
internal static class Descriptors
{
    /// <summary>The short name a SCIP symbol's last descriptor gives.</summary>
    public static string NameOf(string symbol)
    {
        // A local is `local <n>`, which has no descriptors at all.
        if (symbol.StartsWith("local ", StringComparison.Ordinal))
        {
            return symbol["local ".Length..];
        }

        // Five space-separated fields: scheme, manager, package name, version, and then
        // the descriptors, which are the rest of the string.
        var descriptors = Rest(symbol, 4);

        if (descriptors.Length == 0)
        {
            return symbol;
        }

        // **A parameter names itself inside its brackets**, and its own suffix is the
        // closing one — `greet().(who)` is the parameter `who` of the method `greet`, so
        // stripping the method's parentheses first would answer `greet`.
        if (Bracketed(descriptors, '(', ')') is { } parameter)
        {
            return Unquote(parameter);
        }

        if (Bracketed(descriptors, '[', ']') is { } typeParameter)
        {
            return Unquote(typeParameter);
        }

        // The suffix character: `#` a type, `.` a term, `/` a namespace, `:` a meta,
        // `!` a macro.
        var body = descriptors.TrimEnd('#', '.', '/', ':', '!');

        // A method's disambiguator, which is not part of its name: `name(+1).`
        if (body.EndsWith(')') && body.LastIndexOf('(') is var open && open >= 0)
        {
            body = body[..open];
        }

        return Unquote(body[(LastSeparator(body) + 1)..]);
    }

    /// <summary>
    /// The last descriptor's separator, ignoring the ones inside backticks.
    /// </summary>
    /// <remarks>
    /// <b>The backticks are why this is a scan and not an <c>IndexOfAny</c>.</b> A
    /// descriptor whose name is not an identifier is wrapped in them — a file descriptor
    /// is <c>`greet.ts`</c> — and the dot inside is part of the name. Searching for the
    /// last separator without tracking them answers <c>ts`</c>.
    /// </remarks>
    private static int LastSeparator(string descriptors)
    {
        var separator = -1;
        var quoted = false;

        for (var at = 0; at < descriptors.Length; at++)
        {
            var character = descriptors[at];

            if (character == '`')
            {
                // A backtick inside a quoted name is doubled, so a pair is one character
                // of the name rather than the end of the quoting.
                if (quoted && at + 1 < descriptors.Length && descriptors[at + 1] == '`')
                {
                    at++;
                    continue;
                }

                quoted = !quoted;
                continue;
            }

            if (!quoted && character is '/' or '#' or '.')
            {
                separator = at;
            }
        }

        return separator;
    }

    /// <summary>
    /// The text inside a trailing bracket pair, or nothing if it does not end in one.
    /// </summary>
    private static string? Bracketed(string descriptors, char open, char close)
    {
        if (descriptors.Length < 2 || descriptors[^1] != close)
        {
            return null;
        }

        var at = descriptors.LastIndexOf(open);

        return at < 0 ? null : descriptors[(at + 1)..^1];
    }

    /// <summary>A backtick-quoted name, unwrapped and undoubled.</summary>
    private static string Unquote(string name) =>
        name.Length >= 2 && name[0] == '`' && name[^1] == '`'
            ? name[1..^1].Replace("``", "`", StringComparison.Ordinal)
            : name;

    /// <summary>Everything after the <paramref name="skip"/>th space.</summary>
    private static string Rest(string symbol, int skip)
    {
        var at = 0;

        for (var n = 0; n < skip; n++)
        {
            var space = symbol.IndexOf(' ', at);

            if (space < 0)
            {
                return string.Empty;
            }

            at = space + 1;
        }

        return symbol[at..];
    }
}
