// Clause 7.1 (application startup). An entry point is a declaration and not merely a
// static method: the four signatures 7.1 admits are all declared below, three of them
// qualify, and which one the runtime calls is decided by `StartupObject` in
// Lexical.csproj rather than by anything in this file. That is the fact worth a query —
// three declarations are indistinguishable from each other in the source, and one of them
// is the entry point.

using System.Threading.Tasks;

namespace Surface.Lexical.Concepts;

/// <summary>
/// 7.1: the chosen entry point. <c>StartupObject</c> names this type, so this
/// <c>Main</c> is the one the runtime calls and the other two are ordinary methods.
/// </summary>
public static class LexEntryPoint
{
    /// <summary>The <c>int Main(string[])</c> form, which returns an exit status.</summary>
    public static int Main(string[] args) => args.Length;
}

/// <summary>
/// 7.1: a second qualifying signature — <c>void Main()</c>, with no parameters. Declaring
/// it beside the one above is CS0017 unless <c>StartupObject</c> picks one.
/// </summary>
public static class LexAlternateEntryPoint
{
    /// <summary>The <c>void Main()</c> form.</summary>
    public static void Main()
    {
    }
}

/// <summary>7.1: a third qualifying signature — the asynchronous form.</summary>
public static class LexAsyncEntryPoint
{
    /// <summary>The <c>Task&lt;int&gt; Main(string[])</c> form.</summary>
    public static async Task<int> Main(string[] args)
    {
        await Task.Yield();
        return args.Length;
    }
}

/// <summary>
/// 7.1: signatures that do *not* qualify, so they can sit here without CS0017 — a
/// <c>Main</c> with the wrong parameter type, one that is not static, and one that is not
/// accessible from outside its type.
/// </summary>
public sealed class LexNotAnEntryPoint
{
    /// <summary>Wrong parameter type, so not a candidate.</summary>
    public static int Main(int code) => code;

    /// <summary>Not static, so not a candidate.</summary>
    public int Main() => 0;

    /// <summary>Generic, so not a candidate.</summary>
    public static void Main<T>(T ignored)
    {
    }
}
