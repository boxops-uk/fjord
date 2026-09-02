using System.Diagnostics;
using System.Linq;

namespace Boxops.Fjord.Tests;

/// <summary>
/// A fixture solution, copied somewhere it can be built.
/// </summary>
/// <remarks>
/// <para>
/// <b>Copied rather than used where it lies.</b> A design-time build restores, and a
/// restore writes <c>obj/</c> beside the project — so a test run over the checked-in
/// fixture would leave build output in the repository and, worse, would let one test's
/// leftovers decide the next one's answer. The copy is deleted with the fixture.
/// </para>
/// <para>
/// <b>The fixtures reference no packages</b>, so nothing here needs a network: a restore
/// that has to reach nuget.org is a test that fails on a train.
/// </para>
/// </remarks>
internal sealed class Fixture : IDisposable
{
    private Fixture(string root) => Root = root;

    /// <summary>The copy's root — where the solution file is.</summary>
    public string Root { get; }

    public string Path(params string[] parts) =>
        System.IO.Path.Combine([Root, .. parts]);

    /// <summary>Copy the named fixture out of <c>clients/dotnet/tests/fixtures</c>.</summary>
    public static Fixture Copy(string name)
    {
        var source = System.IO.Path.Combine(
            FjordServer.RepositoryRoot, "clients", "dotnet", "tests", "fixtures", name);

        if (!Directory.Exists(source))
        {
            throw new DirectoryNotFoundException($"no fixture named {name} under tests/fixtures");
        }

        var root = Directory.CreateTempSubdirectory($"fjord-fx-{name}-").FullName;

        // The empty `Directory.Build.props` beside the fixtures is copied with them, and
        // has to be: without it the copy would walk up out of the temp directory and pick
        // up whatever build props happen to be above it.
        File.Copy(
            System.IO.Path.Combine(source, "..", "Directory.Build.props"),
            System.IO.Path.Combine(root, "Directory.Build.props"));

        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(
                System.IO.Path.Combine(root, System.IO.Path.GetRelativePath(source, directory)));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, System.IO.Path.Combine(root, System.IO.Path.GetRelativePath(source, file)));
        }

        return new Fixture(root);
    }

    /// <summary>
    /// Compile one project of the fixture for real, so its assembly exists.
    /// </summary>
    /// <remarks>
    /// Only where a test is about what happens when a checkout <i>has</i> been built —
    /// a reference to a project outside the indexed set resolves to that project's
    /// assembly, and there is no assembly until something compiles one.
    /// </remarks>
    public void Build(string project)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        // One node, not reused. A build launched from inside a test host inherits the
        // host's own MSBuild environment, and its worker nodes then spin instead of
        // talking to each other — a two-second build becomes one that does not finish.
        foreach (var argument in new[]
            { "build", project, "--nologo", "-v", "quiet", "-m:1", "-nodeReuse:false" })
        {
            start.ArgumentList.Add(argument);
        }

        // **The test host's MSBuild variables are the test host's.** VSTest exports half a
        // dozen — where the SDK targets are, whether to keep stdout for task processes,
        // whether the MSBuild server is in play — and a child build that inherits them is
        // being configured by a process that is not building anything.
        foreach (var name in start.Environment.Keys
            .Where(key => key.StartsWith("MSBUILD", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("_MSBUILD", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("VSTEST", StringComparison.OrdinalIgnoreCase))
            .ToList())
        {
            start.Environment.Remove(name);
        }

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("dotnet did not start");

        // **Both pipes, at once.** Draining one to the end before starting on the other
        // deadlocks the moment the child fills the one nobody is reading: a pipe buffer is
        // 64 kB, `dotnet build` says more than that between them, and the child blocks
        // writing while this blocks reading. It presents as a build that never finishes.
        var out_ = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();

        var stdout = out_.GetAwaiter().GetResult();
        var stderr = error.GetAwaiter().GetResult();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"building the fixture's {project} failed:\n{stdout}\n{stderr}");
        }
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // A design-time build may still be letting go of a file. The temp directory
            // is the operating system's problem then, not this test's.
        }
    }
}
