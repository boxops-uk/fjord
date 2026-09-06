using System.Runtime.CompilerServices;

namespace Surface.Modern.Attributes;

/// <summary>C# 13 — Overload resolution priority.</summary>
public static class ResolutionPriority
{
    /// <summary>
    /// C# 13 — <c>[OverloadResolutionPriority(n)]</c>. Without the attribute the array
    /// overload below wins for an argument list of loose integers; with it the span overload
    /// does. The two overloads are otherwise equally applicable, so the *only* thing that
    /// decides which reference a call site records is the attribute.
    /// </summary>
    [OverloadResolutionPriority(1)]
    public static string Render(params ReadOnlySpan<int> values) => $"span:{values.Length}";

    /// <summary>The lower-priority overload — reachable only through an explicit array.</summary>
    public static string Render(params int[] values) => $"array:{values.Length}";

    /// <summary>A negative priority, which pushes an overload behind the unannotated default.</summary>
    [OverloadResolutionPriority(-1)]
    public static string Render(IEnumerable<int> values) => "enumerable";

    /// <summary>The call the attribute redirects, and the one that names the array overload.</summary>
    public static string Both() => Render(1, 2, 3) + "/" + Render(new[] { 1, 2, 3 });
}
