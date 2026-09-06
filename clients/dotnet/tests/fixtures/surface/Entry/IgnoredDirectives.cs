#:property SurfaceIgnoredDirective=recorded
#:property SurfaceSecondDirective=alsoRecorded

// C# 14 — Ignored directives. The two lines above use the `#:` prefix that `dotnet run app.cs`
// reads to configure a file-based program: `#:sdk`, `#:package` and `#:property` are consumed
// by the *launcher* before the compiler ever runs, and the compiler itself only has to not
// choke on them. They must precede every C# token in the file, they are the one directive form
// with no `#if`-style semantics at all, and — the point of the row — they declare nothing and
// affect nothing here. An index has exactly one decision to make about them: whether the file
// still parses. If the `Features=FileBasedProgram` flag in `Entry.csproj` does not reach the
// indexer's compilation, it does not, and every declaration below this line disappears with
// no other symptom.
namespace Surface.Entry;

/// <summary>Something after the ignored directives, so their file is not empty of declarations.</summary>
public static class IgnoredDirectives
{
    /// <summary>What the top-level statements print, to prove this file was compiled.</summary>
    public const string Note = "ignored directives parsed";
}
