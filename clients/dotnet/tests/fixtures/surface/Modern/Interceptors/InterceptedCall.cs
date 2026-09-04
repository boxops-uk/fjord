namespace Surface.Modern.Interceptors;

/// <summary>
/// C# 12 — Interceptors (preview). The call in <see cref="Call"/> is replaced at compile time
/// by <c>CallInterceptor.Replacement</c>, which names it by file, line and character rather
/// than by any name in this file. Nothing here says it is intercepted.
/// </summary>
/// <remarks>
/// <b>The position of the call below is load-bearing.</b> <c>CallInterceptor</c> points at it
/// with a literal line and character offset, so inserting a line above it — including in this
/// comment — breaks the build with CS9141. That fragility is the row's point: the reference an
/// index records from that call site is decided by an attribute in another file that mentions
/// neither the caller nor the callee.
/// </remarks>
public static class InterceptedCall
{
    /// <summary>The method the call names, and which never runs.</summary>
    public static string Original() => "original";

    /// <summary>The call site that is intercepted.</summary>
    public static string Call() => Original();

    /// <summary>A second call to the same method, at a position nothing intercepts.</summary>
    public static string CallAgain() => Original();
}
