// Clause 16.4.9 — Constructors. A struct is not required to declare an instance
// constructor: it always has a parameterless one, which returns the value with every field
// at its default. The standard says that constructor is implicitly provided and may not be
// declared; post-standard C# 10 lets a struct declare it, in which case the declared body
// runs for `new S()` and NOT for `default(S)`.
//
// THIS IS THE HEADLINE HAZARD OF CLAUSE 16. A struct with one declared constructor has two
// constructors in the compiler's member list, and the second has no declaration anywhere in
// the source. Anything that numbers members off that list — an ordinal, an index into a
// member array, "the second constructor of this type" — counts a member the index never
// saw, and every number after it in the type is off by one. `StTwoConstructorsOneWritten`
// below is the smallest case: one line of source, two members.

using System;

namespace Surface.Structs;

/// <summary>
/// Clause 16.4.9 hazard — one declared constructor, two constructors. The parameterless
/// one exists, is callable, and is written nowhere; a member list numbered from source and
/// a member list numbered from the symbol table disagree about which constructor is which.
/// </summary>
public struct StTwoConstructorsOneWritten
{
    /// <summary>The field the declared constructor sets.</summary>
    public int Weight;

    /// <summary>
    /// Clause 16.4.9 — the only constructor in the file. It is the SECOND constructor of
    /// the type: the parameterless one sorts before it in every ordering the compiler
    /// uses.
    /// </summary>
    public StTwoConstructorsOneWritten(int weight) => Weight = weight;

    /// <summary>Clause 16.4.12 — one method, so the ordinal drift has something after it to move.</summary>
    public int Doubled() => Weight * 2;
}

/// <summary>
/// Clause 16.4.9, post-standard — a declared parameterless constructor. Now the member
/// list and the source agree on the count, and they disagree about the semantics instead:
/// this body runs for `new`, and never for `default`.
/// </summary>
public struct StDeclaredParameterless
{
    /// <summary>The field the declared constructor sets to something other than zero.</summary>
    public int Weight;

    /// <summary>Clause 16.4.9 — the explicitly declared parameterless constructor.</summary>
    public StDeclaredParameterless()
    {
        Weight = 1;
    }

    /// <summary>Clause 16.4.9 — a second constructor beside it, so the type has exactly two.</summary>
    public StDeclaredParameterless(int weight)
    {
        Weight = weight;
    }
}

/// <summary>
/// Clause 16.4.9 — constructor initializers. A struct constructor may chain to another
/// constructor of the same struct with `: this(...)`, and chaining to `this()` is how a
/// constructor zeroes every field before assigning some of them.
/// </summary>
public struct StChainedConstructors
{
    /// <summary>The first field.</summary>
    public int Left;

    /// <summary>The second field, which the one-argument constructor never mentions.</summary>
    public int Right;

    /// <summary>Clause 16.4.9 — chains to the parameterless constructor, which no line declares.</summary>
    public StChainedConstructors(int left)
        : this()
    {
        Left = left;
    }

    /// <summary>Clause 16.4.9 — chains to the constructor above, which chains again.</summary>
    public StChainedConstructors(int left, int right)
        : this(left)
    {
        Right = right;
    }

    /// <summary>Clause 16.4.9 — a third overload, chaining across a type conversion.</summary>
    public StChainedConstructors(double left)
        : this((int)left, 0)
    {
    }
}

/// <summary>
/// Clause 16.4.9, post-standard — a primary constructor on a struct, which C# 12 added.
/// The parameter list is on the type declaration; the parameters are in scope in every
/// member body, and each one that is captured becomes a field the source does not declare.
/// </summary>
/// <param name="factor">The scale factor, captured by the property below.</param>
public struct StPrimaryScaled(double factor)
{
    /// <summary>Clause 16.4.9 — a property that captures the primary constructor parameter.</summary>
    public double Factor => factor;

    /// <summary>Clause 16.4.8 — a field initializer that reads a primary constructor parameter.</summary>
    public double Doubled = factor * 2.0;

    /// <summary>
    /// Clause 16.4.9 — a second constructor, which the language requires chain to the
    /// primary one.
    /// </summary>
    public StPrimaryScaled()
        : this(1.0)
    {
    }

    /// <summary>Clause 16.4.12 — a method that reads the captured parameter.</summary>
    public double Apply(double value) => value * factor;
}

/// <summary>
/// Clause 16.4.9 — a struct constructor that leaves a field unassigned. C# 11 and later
/// implicitly default the rest of the instance, so the field below is zero and the
/// assignment that makes it so is written nowhere.
/// </summary>
public struct StPartlyAssigned
{
    /// <summary>Assigned by the constructor.</summary>
    public int Assigned;

    /// <summary>Never assigned by any constructor, and therefore zero.</summary>
    public int Unassigned;

    /// <summary>Clause 16.4.9 — assigns one of two fields and returns.</summary>
    public StPartlyAssigned(int assigned) => Assigned = assigned;
}

/// <summary>
/// Clause 16.4.9 — the invocations. Every constructor declared above is called from here,
/// and so is one that is not declared above.
/// </summary>
public static class StConstructorUse
{
    /// <summary>Clause 16.4.9 — the declared constructor, called with an argument.</summary>
    public static int Declared() => new StTwoConstructorsOneWritten(5).Weight;

    /// <summary>
    /// Clause 16.4.9 hazard — the constructor with no declaration, invoked. The `new`
    /// expression references a member that has no declaration site, so a reference here
    /// either resolves to the type or resolves to nothing.
    /// </summary>
    public static int Undeclared() => new StTwoConstructorsOneWritten().Weight;

    /// <summary>Clause 16.4.9 — the same value again, spelled with `default`, which calls nothing.</summary>
    public static int ViaDefault() => default(StTwoConstructorsOneWritten).Weight;

    /// <summary>
    /// Clause 16.4.9 — the declared parameterless constructor, whose body runs, beside
    /// `default`, whose body does not. The two lines differ by one and read the same.
    /// </summary>
    public static int NewMinusDefault() =>
        new StDeclaredParameterless().Weight - default(StDeclaredParameterless).Weight;

    /// <summary>Clause 16.4.9 — the chain, entered at its longest end.</summary>
    public static int Chained() => new StChainedConstructors(2, 3).Right;

    /// <summary>Clause 16.4.9 — the chain entered through the overload that converts.</summary>
    public static int ChainedFromDouble() => new StChainedConstructors(2.9).Left;

    /// <summary>Clause 16.4.9 — the primary constructor, and then the one that chains to it.</summary>
    public static double Primary() => new StPrimaryScaled(3.0).Apply(2.0) + new StPrimaryScaled().Factor;

    /// <summary>Clause 16.4.9 — an object initializer after a constructor call, which is a third shape.</summary>
    public static int WithInitializer() => new StPartlyAssigned(1) { Unassigned = 2 }.Unassigned;

    /// <summary>Clause 16.4.9 / 16.4.5 — construction through a generic `new()` constraint.</summary>
    public static T Fresh<T>()
        where T : struct => new T();

    /// <summary>Clause 16.4.9 — and through Activator, where the constructor is named by no token.</summary>
    public static object? Reflected() => Activator.CreateInstance(typeof(StTwoConstructorsOneWritten));
}
