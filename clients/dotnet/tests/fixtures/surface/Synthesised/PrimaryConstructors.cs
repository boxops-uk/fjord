namespace Surface.Synthesised;

/// <summary>A primary constructor on a plain class (C# 12).</summary>
/// <remarks>
/// The constructor <c>SynAnchor(string, int)</c> is a declaration with no
/// <c>ConstructorDeclarationSyntax</c>: its declaring syntax reference is the
/// <c>ClassDeclarationSyntax</c> itself, so the type and one of its constructors share a
/// syntax node. An index that keys a declaration by its node therefore has two
/// declarations wanting one key, and an index that walks constructor-declaration nodes
/// misses the constructor entirely.
///
/// <c>name</c> is read from a member body, so it is <em>captured</em> and the compiler adds
/// a private field <c>&lt;name&gt;P</c> — a field with no field declaration, spelled with
/// characters no C# identifier may contain. <c>depth</c> is read only by a property
/// initializer, which runs inside the constructor, so it is <em>not</em> captured and no
/// field appears. Same syntax, different synthesis: the distinction is invisible in source.
/// </remarks>
public class SynAnchor(string name, int depth)
{
    /// <summary>Reads the captured parameter, forcing the synthesised backing field.</summary>
    public string Name => name;

    /// <summary>Reads the parameter once, in an initializer, so nothing is captured.</summary>
    public int Depth { get; } = depth;

    /// <summary>
    /// A secondary constructor. C# requires it to chain to the primary one, so this is the
    /// only constructor declaration in the type whose <c>this</c> initializer names a
    /// constructor with no declaration of its own.
    /// </summary>
    public SynAnchor(string name)
        : this(name, 0)
    {
    }

    /// <summary>Rebinds the parameter inside a lambda, which lifts it into a closure.</summary>
    public System.Func<string> Deferred() => () => name;
}

/// <summary>A primary constructor whose base clause is part of the type header.</summary>
/// <remarks>
/// <c>SynAnchor(name, 1)</c> here is a base-constructor invocation with no statement and no
/// initializer node of the usual shape — it hangs off the base type in the base list, so
/// the reference to <see cref="SynAnchor"/> the constructor and the reference to
/// <see cref="SynAnchor"/> the base type sit at one identifier.
/// </remarks>
public sealed class SynDerivedAnchor(string name) : SynAnchor(name, 1)
{
    /// <summary>Distinguishes this from its base.</summary>
    public string Label => $"derived:{name}";
}

/// <summary>A primary constructor on a struct (C# 12).</summary>
/// <remarks>
/// A struct always has a parameterless constructor, and here it is <em>not</em> the
/// declared one: <c>SynSpan2()</c> is synthesised with the struct declaration as its
/// location, beside the declared-in-the-header <c>SynSpan2(int, int)</c> at the very same
/// location. Two constructors, one span, and neither has a constructor declaration node.
/// </remarks>
public struct SynSpan2(int start, int length)
{
    /// <summary>Where the span begins.</summary>
    public int Start { get; } = start;

    /// <summary>How long the span is.</summary>
    public int Length { get; } = length;

    /// <summary>One past the end, reading the captured parameters.</summary>
    public int End => start + length;
}

/// <summary>A struct that declares its parameterless constructor explicitly (C# 10).</summary>
/// <remarks>
/// The contrast with <see cref="SynSpan2"/>: here <c>SynTally()</c> has a real constructor
/// declaration, so nothing is synthesised and the field initializer runs. A struct is
/// still creatable as <c>default</c> without running it, so the declared constructor is not
/// the only path to an instance.
/// </remarks>
public struct SynTally
{
    /// <summary>Counts, defaulting to one rather than to zero.</summary>
    public int Count;

    /// <summary>The declared parameterless constructor.</summary>
    public SynTally()
    {
        Count = 1;
    }
}

/// <summary>A generic class with a primary constructor and a constraint.</summary>
/// <remarks>
/// The type parameter <c>T</c> is declared on the class header alongside the primary
/// constructor's parameter list, so the type parameter, the constructor and the type all
/// share a declaration node.
/// </remarks>
public sealed class SynCounted<T>(T seed)
    where T : notnull
{
    private int _uses;

    /// <summary>The seed, counted on each read.</summary>
    public T Seed
    {
        get
        {
            _uses++;
            return seed;
        }
    }

    /// <summary>How many times the seed has been read.</summary>
    public int Uses => _uses;
}
