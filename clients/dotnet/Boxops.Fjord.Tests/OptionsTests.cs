using Boxops.Fjord.Indexer;
using Xunit;

namespace Boxops.Fjord.Tests;

/// <summary>
/// <para>
/// <b>The command line, where a run states what it cannot read.</b>
/// </para>
/// <para>
/// Most of what the indexer writes it works out from the code. Provenance is not in the
/// code: which repository and which revision a checkout is are facts about the checkout,
/// so a run either says them or the index does not carry them. These assert the pair is
/// a pair — half of it is a fact that says a file came from revision "" — and that a run
/// which says nothing is still a valid run.
/// </para>
/// </summary>
public sealed class OptionsTests
{
    private static Options Parse(params string[] argv)
    {
        Assert.True(Options.TryParse(argv, out var options, out var error), error);
        return options;
    }

    private static string Refused(params string[] argv)
    {
        Assert.False(Options.TryParse(argv, out _, out var error));
        Assert.NotNull(error);
        return error;
    }

    [Fact]
    public void Provenance_is_absent_unless_a_run_states_it()
    {
        var options = Parse("--source", "/tmp/x.sln");

        Assert.Null(options.Repo);
        Assert.Null(options.Revision);
    }

    [Fact]
    public void A_run_can_state_the_repository_and_the_revision_it_indexed()
    {
        var options = Parse(
            "--source", "/tmp/x.sln",
            "--repo", "github.com/boxops-uk/fjord",
            "--revision", "3fa4961");

        Assert.Equal("github.com/boxops-uk/fjord", options.Repo);
        Assert.Equal("3fa4961", options.Revision);
    }

    /// <summary>
    /// **Half of a pair is worse than neither.** `src.FileOrigin` carries both fields on
    /// one fact, so a run stating one would write a provenance fact asserting the other is
    /// the empty string — a claim, in the index, that nothing can tell from a gap.
    /// </summary>
    [Theory]
    [InlineData("--repo", "github.com/boxops-uk/fjord")]
    [InlineData("--revision", "3fa4961")]
    public void One_half_of_the_provenance_pair_is_refused(string flag, string value)
    {
        var error = Refused("--source", "/tmp/x.sln", flag, value);

        Assert.Contains("--repo", error, System.StringComparison.Ordinal);
        Assert.Contains("--revision", error, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Both_provenance_flags_want_a_value()
    {
        Assert.Contains("wants a value", Refused("--source", "/tmp/x.sln", "--repo"),
            System.StringComparison.Ordinal);
        Assert.Contains("wants a value", Refused("--source", "/tmp/x.sln", "--revision"),
            System.StringComparison.Ordinal);
    }

    /// <summary>
    /// **The degraded mode is gone, and asking for it is an error rather than a no-op.**
    /// A flag the parser quietly ignores is worse than one it refuses: a script that
    /// passed `--syntax-only` would keep running and produce a *different, much larger*
    /// index than the one it asked for, with nothing in the output to say so.
    /// </summary>
    [Theory]
    [InlineData("--syntax-only")]
    [InlineData("--skip-files")]
    public void The_syntax_only_mode_and_its_slicing_flag_are_refused_by_name(string flag)
    {
        var error = Refused("--source", "/tmp/x.sln", flag, "0");

        Assert.Contains(flag, error, System.StringComparison.Ordinal);
        Assert.Contains("unknown flag", error, System.StringComparison.Ordinal);
    }

    [Fact]
    public void The_usage_text_names_neither_of_them()
    {
        Assert.DoesNotContain("syntax-only", Options.Usage, System.StringComparison.Ordinal);
        Assert.DoesNotContain("skip-files", Options.Usage, System.StringComparison.Ordinal);
    }

    [Fact]
    public void The_usage_text_names_the_pair()
    {
        Assert.Contains("--repo", Options.Usage, System.StringComparison.Ordinal);
        Assert.Contains("--revision", Options.Usage, System.StringComparison.Ordinal);
    }
}
