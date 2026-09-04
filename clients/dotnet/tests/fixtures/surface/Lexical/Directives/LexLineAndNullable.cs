// Clause 6.5.8 (line directives), 6.5.9 (the nullable directive) and 6.5.10 (pragma
// directives).
//
// 6.5.8 is the row that can move a declaration into a file that does not exist. The two
// mapped classes below claim to live in `Phantom/Virtual.cs` at lines 200 and 300, and the
// third claims a *span* rather than a line. Their mapped lines are deliberately far apart:
// two declarations mapped to one line in one phantom file would be one position, and an
// index that keys an occurrence by its mapped position rather than its real one would then
// have two facts wanting one key.
//
// 6.5.9 is only observable because Lexical.csproj sets `Nullable` to `disable`: the plain
// `string` in `LexNullableOn` is non-nullable, the plain `string` in `LexNullableOff` is
// oblivious, and the two are the same six characters of source.

namespace Surface.Lexical.Directives;

#pragma warning disable CS0169 // an unread private field, which the type below wants
#pragma checksum "Phantom/Virtual.cs" "{406ea660-64cf-4c82-b6f0-42d48172a799}" "ab007f1d23d9"

/// <summary>6.5.10: a type whose unread private field CS0169 would otherwise report.</summary>
public sealed class LexPragmaSuppressed
{
    private readonly int _neverRead;

    /// <summary>Something to reference.</summary>
    public const int Tag = 1;
}
#pragma warning restore CS0169

#line 200 "Phantom/Virtual.cs"
/// <summary>
/// 6.5.8: declared at line 200 of a file that is not on disk. Its real position is this
/// file, a few lines below the top.
/// </summary>
public sealed class LexMappedToPhantomLine
{
    /// <summary>Something to reference.</summary>
    public const int Tag = 2;
}

#line 300 "Phantom/Virtual.cs"
/// <summary>6.5.8: declared at line 300 of the same phantom file, a hundred lines further
/// on than the type above and one line further on in reality.</summary>
public sealed class LexMappedToLaterPhantomLine
{
    /// <summary>Something to reference.</summary>
    public const int Tag = 3;
}

#line (7, 3) - (7, 40) 12 "Phantom/Span.cs"
/// <summary>6.5.8: the span form, which maps a character range rather than a line and
/// carries a character offset of its own.</summary>
public sealed class LexMappedToPhantomSpan
{
    /// <summary>Something to reference.</summary>
    public const int Tag = 4;
}

#line hidden
/// <summary>6.5.8: declared in a hidden region, which a debugger steps over.</summary>
public sealed class LexLineHidden
{
    /// <summary>Something to reference.</summary>
    public const int Tag = 5;
}

#line default
/// <summary>6.5.8: declared after the mapping is reset, so its position is its real one.</summary>
public sealed class LexUnmapped
{
    /// <summary>Something to reference.</summary>
    public const int Tag = 6;
}

#nullable enable
/// <summary>
/// 6.5.9: <c>Bare</c> is non-nullable and <c>Annotated</c> is nullable, because this region
/// enables both warnings and annotations.
/// </summary>
public sealed class LexNullableOn
{
    /// <summary>A plain <c>string</c> in an enabled region: not null.</summary>
    public string Bare = string.Empty;

    /// <summary>An annotated <c>string?</c> in an enabled region: may be null.</summary>
    public string? Annotated;

    /// <summary>A generic instantiation whose argument is non-nullable here.</summary>
    public System.Collections.Generic.List<string> Names = [];
}

#nullable disable
/// <summary>
/// 6.5.9: the same six characters of source as <c>LexNullableOn.Bare</c>, and an oblivious
/// type rather than a non-nullable one.
/// </summary>
public sealed class LexNullableOff
{
    /// <summary>A plain <c>string</c> in a disabled region: oblivious.</summary>
    public string Bare = string.Empty;

    /// <summary>The same generic instantiation, whose argument is oblivious here.</summary>
    public System.Collections.Generic.List<string> Names = [];
}

#nullable restore
#nullable enable annotations
/// <summary>6.5.9: annotations on and warnings off, so <c>string?</c> is meaningful and
/// nothing is reported about it.</summary>
public sealed class LexAnnotationsOnly
{
    /// <summary>Nullable, with the warnings that would police it switched off.</summary>
    public string? Annotated;
}

#nullable disable warnings
/// <summary>6.5.9: warnings off on their own, which leaves the annotation context alone.</summary>
public sealed class LexWarningsOff
{
    /// <summary>Assigned nothing, in a region where that is not reported.</summary>
    public string Bare;
}

#nullable restore
/// <summary>6.5.9: after a restore, the project-wide setting is back in force.</summary>
public static class LexNullableRestored
{
    /// <summary>Reads a member from each nullable region, so none is unreferenced.</summary>
    public static int Total() =>
        LexPragmaSuppressed.Tag + LexMappedToPhantomLine.Tag + LexMappedToLaterPhantomLine.Tag
        + LexMappedToPhantomSpan.Tag + LexLineHidden.Tag + LexUnmapped.Tag
        + new LexNullableOn().Bare.Length + new LexNullableOff().Bare.Length
        + new LexAnnotationsOnly().Annotated?.Length ?? 0;
}
