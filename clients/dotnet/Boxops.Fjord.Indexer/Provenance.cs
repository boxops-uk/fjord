using Boxops.Fjord.Client;

namespace Boxops.Fjord.Indexer;

/// <summary>
/// What a database was built <i>against</i> — one <c>config.Setting</c> per axis.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every one of these was implicit before, and one of them cost real time.</b> A
/// database whose <c>src.File</c> paths are relative to a root nobody wrote down can only
/// be matched to a checkout by inference: this index's own provenance had to be worked out
/// once from which checkout happened to be the only one in <c>$HOME</c>.
/// </para>
/// <para>
/// <b>A value rather than a side effect</b>, so the axes a run records can be asserted
/// without one. What is written here is what a consumer has to agree with before it can
/// read a single fact, and a producer that gets it wrong is wrong about the whole database.
/// </para>
/// </remarks>
internal static class Provenance
{
    /// <summary>The settings one target framework's database carries.</summary>
    /// <remarks>
    /// <b>Exactly one framework</b>, which is the fan-out's whole claim: a database holds
    /// one target's facts, so a consumer joining two of them knows what it is joining.
    /// </remarks>
    public static IReadOnlyList<FjordFact> Of(
        Options options,
        string root,
        string framework,
        string version)
    {
        var settings = new List<FjordFact>();

        void Say(string dimension, string? value)
        {
            if (value is { Length: > 0 })
            {
                settings.Add(DotnetIndex.SettingFact(dimension, value));
            }
        }

        Say("framework", framework);
        Say("configuration", options.Configuration);
        Say("index-root", Path.GetFullPath(root));

        // UTF-8 bytes, because that is what `src.FileLine.start` counts. The UTF-16 column
        // travels beside it rather than instead of it, and `cstart` is how a consumer that
        // counts code units converts.
        Say("position-encoding", "utf8");

        // **Only when styles are written, and never otherwise.** `src.FileLineStyles` is
        // an opaque payload whose format and vocabulary this value is the only statement
        // of, so a database that writes styles and does not say this holds highlighting
        // no consumer can identify — every one of them renders the file plain, which is
        // exactly what an index with no highlighter looks like. Claiming the encoding
        // when nothing wrote a style fact would be the opposite lie.
        if (options.Styles)
        {
            Say("style-encoding", SemanticTokens.Encoding);
        }
        Say("symbol-scheme", ScipSymbols.Scheme);
        Say("language", "csharp");
        Say("producer", $"boxops-fjord-indexer/{version}");

        // Provenance is not in the code: a run either states it or the index does not carry
        // it. `src.FileOrigin` says the same thing per file where an index spans several
        // checkouts; this says it once for the database.
        Say("repo", options.Repo);
        Say("revision", options.Revision);

        return settings;
    }
}
