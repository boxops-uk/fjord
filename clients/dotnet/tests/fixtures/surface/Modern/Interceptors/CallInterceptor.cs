namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// C# 12 — Interceptors (preview) require this attribute, and the framework does not ship
    /// it: a generator that wants to intercept a call declares it itself, in this exact
    /// namespace, and the compiler finds it by full name. So this is a type whose *identity*
    /// is a contract with the compiler rather than with any caller.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    internal sealed class InterceptsLocationAttribute : Attribute
    {
        /// <summary>Names a call site by file, line and character.</summary>
        /// <param name="filePath">The file holding the call.</param>
        /// <param name="line">Its one-based line.</param>
        /// <param name="character">The one-based character of the method name.</param>
        public InterceptsLocationAttribute(string filePath, int line, int character)
        {
            FilePath = filePath;
            Line = line;
            Character = character;
        }

        /// <summary>The file holding the intercepted call.</summary>
        public string FilePath { get; }

        /// <summary>The line of the intercepted call.</summary>
        public int Line { get; }

        /// <summary>The character of the intercepted call.</summary>
        public int Character { get; }
    }
}

namespace Surface.Modern.Interceptors
{
    using System.Runtime.CompilerServices;

    /// <summary>
    /// C# 12 — the interceptor. Its signature matches
    /// <see cref="InterceptedCall.Original"/>, and the attribute redirects one specific call
    /// to <c>Original</c> — the one on line 21 of <c>InterceptedCall.cs</c> — to this method.
    /// The other call to <c>Original</c>, three lines further down, is untouched, so the same
    /// syntax in the same file resolves to two different methods.
    /// </summary>
    public static class CallInterceptor
    {
        /// <summary>C# 12 — <c>[InterceptsLocation(path, line, character)]</c>.</summary>
        [InterceptsLocation("InterceptedCall.cs", 21, 36)]
        public static string Replacement() => "intercepted";
    }
}
