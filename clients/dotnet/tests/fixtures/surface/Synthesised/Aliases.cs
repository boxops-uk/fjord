extern alias SynRuntime;

global using SynGlobalText = System.String;

using System.Collections.Generic;
using SynAliasedInt = System.Int32;
using SynAliasedList = System.Collections.Generic.List<int>;
using SynAliasedNamespace = System.Collections.Generic;
using SynAliasedTuple = (int Row, int Column);
using SynAliasedArray = int[];
using unsafe SynAliasedPointer = int*;
using static System.Math;

namespace Surface.Synthesised;

/// <summary><c>SymbolKind.Alias</c> — every alias form C# has (14.5, 14.6.2).</summary>
/// <remarks>
/// An alias is a symbol whose identity is <em>file-scoped</em>: the name <c>SynAliasedInt</c>
/// is declared here and again in <c>Namespaces.cs</c>, and the two are different symbols
/// that happen to denote the same type. An index that keys an alias by (project, name)
/// mints one identity twice; an index that keys it by (file, name) does not. Nothing in
/// either file distinguishes them.
///
/// <c>extern alias SynRuntime</c> is the other half of the kind: it names an assembly
/// rather than a type, its declaration binds to an <c>/reference</c> option rather than to
/// anything in source, and the project's build has to supply that option — the
/// <c>SynAddExternAlias</c> target in the csproj adds <c>Aliases="global,SynRuntime"</c> to
/// <c>System.Runtime</c>. Without it this file does not compile, so the alias's dependency
/// on the build is not an aside.
///
/// <c>using static</c> declares no alias at all: it adds members to a scope and produces
/// no symbol an index can point at, so <c>Sqrt</c> below is a reference whose enclosing
/// directive names nothing.
/// </remarks>
public class SynAliasUse
{
    /// <summary>A type reached through the extern alias, from a specific assembly.</summary>
    public SynRuntime::System.Guid Id { get; init; } = SynRuntime::System.Guid.Empty;

    /// <summary>A type reached through the <c>global</c> alias, which is always in scope.</summary>
    public global::System.DateTime Stamp { get; init; }

    /// <summary>An aliased predefined type.</summary>
    public SynAliasedInt Count { get; init; }

    /// <summary>An aliased constructed generic type.</summary>
    public SynAliasedList Cells { get; init; } = [];

    /// <summary>An aliased namespace, used to qualify a type.</summary>
    public SynAliasedNamespace::Queue<int> Pending { get; init; } = new();

    /// <summary>An aliased tuple type (C# 12), whose element names come from the alias.</summary>
    public SynAliasedTuple At { get; init; }

    /// <summary>An aliased array type (C# 12).</summary>
    public SynAliasedArray Cursor { get; init; } = [0, 0];

    /// <summary>The global-using alias, which is declared in no file's using list.</summary>
    public SynGlobalText Label { get; init; } = string.Empty;

    /// <summary>An aliased pointer type, which needs the C# 12 <c>using unsafe</c> form.</summary>
    /// <param name="head">Where to start.</param>
    /// <returns>What is there.</returns>
    public static unsafe int Deref(SynAliasedPointer head) => *head;

    /// <summary>A member brought into scope by <c>using static</c>.</summary>
    /// <param name="value">The value.</param>
    /// <returns>Its root.</returns>
    public static double Root(double value) => Sqrt(value);
}
