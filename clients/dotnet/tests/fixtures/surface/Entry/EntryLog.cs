using System.Collections.Generic;

namespace Surface.Entry;

/// <summary>
/// Somewhere for the top-level statements to write, so that <c>Program.cs</c> holds statements
/// rather than declarations. This type is ordinary in every way — it is here to be the thing
/// the invented entry point refers to, which is the edge worth checking: a reference *out of*
/// a compiler-synthesized method into a declared type.
/// </summary>
public sealed class EntryLog
{
    private readonly List<string> _lines = [];

    /// <summary>How many lines have been written.</summary>
    public int Count => _lines.Count;

    /// <summary>Writes a line.</summary>
    /// <param name="line">What to write.</param>
    public void Add(string line) => _lines.Add(line);

    /// <summary>Renders every line written so far.</summary>
    public string Render() => string.Join('\n', _lines);
}
