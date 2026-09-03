using System.Diagnostics;

namespace Boxops.Fjord.Tests;

/// <summary>A `fjord` server over a scratch store root, killed when the test ends.</summary>
/// <remarks>
/// <para>
/// **The socket path is the trap, and it is not the tool's.** A Unix socket address is a
/// fixed-size struct — <c>SUN_LEN</c>, 108 bytes on Linux including the terminator — so a
/// server rooted under a long temp path fails to bind with a message about the path rather
/// than about the length. A test runner's temp directory is exactly the sort of long path
/// that trips it, so this roots itself directly under <c>/tmp</c> with a short name and
/// cleans up after itself.
/// </para>
/// <para>
/// **The readiness file is the synchronisation, not a sleep.** It appears only once the
/// listener is accepting, so waiting on it is a signal; a sleep here would be a race
/// dressed as a wait, which is the exact race the flag exists to remove.
/// </para>
/// </remarks>
public sealed class FjordServer : IDisposable
{
    private readonly Process process;

    public string Root { get; }
    public string Socket { get; }

    private FjordServer(Process process, string root, string socket)
    {
        this.process = process;
        Root = root;
        Socket = socket;
    }

    /// <summary>Where the repository root is, from this assembly's own location.</summary>
    internal static string RepositoryRoot
    {
        get
        {
            var here = new DirectoryInfo(AppContext.BaseDirectory);
            while (here is not null && !File.Exists(Path.Combine(here.FullName, "Cargo.toml")))
            {
                here = here.Parent;
            }

            return here?.FullName
                ?? throw new InvalidOperationException(
                    "no Cargo.toml above the test assembly, so the repository root is unknown");
        }
    }

    /// <summary>The `fjord` binary, release first — a debug executor is several times slower.</summary>
    public static string Binary
    {
        get
        {
            var root = RepositoryRoot;
            foreach (var profile in new[] { "release", "debug" })
            {
                var path = Path.Combine(root, "target", profile, "fjord");
                if (File.Exists(path))
                {
                    return path;
                }
            }

            throw new InvalidOperationException(
                "no `fjord` binary under target/{release,debug} — run `cargo build --bin fjord`");
        }
    }

    public static string Schema(string name) => Path.Combine(RepositoryRoot, "schemas", name);

    /// <summary>Run `fjord` to completion and return its stdout, or throw with its stderr.</summary>
    public static string Run(string root, params string[] args)
    {
        var start = new ProcessStartInfo(Binary)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("--data-dir");
        start.ArgumentList.Add(root);
        foreach (var arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("the binary did not start");

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"`fjord {string.Join(' ', args)}` failed with {process.ExitCode}:\n{stderr}");
        }

        return stdout;
    }

    /// <summary>Create a database from a shipped schema, then serve the root it is in.</summary>
    public static FjordServer Serving(string database, string schema) =>
        ServingAll(schema, database);

    /// <summary>
    /// Create several databases from one schema, then serve the root they are in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Several because a run that compares one index with another needs both of them at
    /// once — and a second server over the same root is refused, as it should be.
    /// </para>
    /// <para>
    /// <b>A name of its own rather than an overload.</b> Two strings bind to
    /// <c>(database, schema)</c> ahead of <c>(schema, params[])</c>, so the pair delegating
    /// to the list called itself — which the compiler is right not to warn about and which
    /// presents as a stack overflow in the test host.
    /// </para>
    /// </remarks>
    public static FjordServer ServingAll(string schema, params string[] databases)
    {
        // Short, and directly under /tmp: see the SUN_LEN note above.
        var root = Path.Combine("/tmp", $"fjt-{Guid.NewGuid():N}"[..14]);
        Directory.CreateDirectory(root);

        foreach (var database in databases)
        {
            Run(root, "--schema-path", Path.Combine(RepositoryRoot, "schemas"),
                "create", database, "--schema", Schema(schema));
        }

        var ready = Path.Combine(root, "ready");
        var start = new ProcessStartInfo(Binary)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("--data-dir");
        start.ArgumentList.Add(root);
        start.ArgumentList.Add("serve");
        start.ArgumentList.Add("--ready-file");
        start.ArgumentList.Add(ready);

        var process = Process.Start(start)
            ?? throw new InvalidOperationException("the server did not start");

        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (!File.Exists(ready))
        {
            if (DateTime.UtcNow > deadline || process.HasExited)
            {
                var stderr = process.HasExited ? process.StandardError.ReadToEnd() : "(still running)";
                try { process.Kill(entireProcessTree: true); } catch { /* already gone */ }
                throw new InvalidOperationException($"the server never became ready:\n{stderr}");
            }

            Thread.Sleep(20);
        }

        return new FjordServer(process, root, Path.Combine(root, "fjord.sock"));
    }

    public void Dispose()
    {
        try { process.Kill(entireProcessTree: true); } catch { /* already gone */ }
        process.WaitForExit(5000);
        process.Dispose();

        try { Directory.Delete(Root, recursive: true); } catch { /* best effort */ }
    }
}
