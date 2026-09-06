// Clause 16.4.10 — Static constructors. A static constructor of a struct follows the same
// rules as one of a class, except for when it is guaranteed to run: it is triggered by a
// static member being referenced or by an instance constructor being called explicitly,
// and NOT by creating a default value of the struct type. So `default(S)` and `new S[10]`
// can both produce instances of a struct whose static constructor has never run.
//
// The hazard is the name. A static constructor's declared name is the type name, which is
// also the instance constructor's name and also the type's own name — three declarations
// competing for one identifier, told apart only by their kind and their modifiers.

namespace Surface.Structs;

/// <summary>
/// Clause 16.4.10 — a struct with a static constructor, static fields it initializes, and
/// an instance constructor whose declared name is spelled identically to it.
/// </summary>
public struct StStaticInitialized
{
    /// <summary>Clause 16.3.1 — a static readonly field the static constructor assigns.</summary>
    public static readonly string Stamp;

    /// <summary>Clause 16.3.1 — a static field with a variable initializer, which runs first.</summary>
    public static int Constructions = 0;

    /// <summary>An instance field, untouched by any of the above.</summary>
    public int Serial;

    /// <summary>
    /// Clause 16.4.10 — the static constructor. It has no accessibility, no parameters,
    /// and the same name as the instance constructor below.
    /// </summary>
    static StStaticInitialized()
    {
        Stamp = "16.4.10";
    }

    /// <summary>
    /// Clause 16.4.9 — the instance constructor, whose name is the same identifier as the
    /// static constructor's and whose kind is not.
    /// </summary>
    public StStaticInitialized(int serial)
    {
        Serial = serial;
        Constructions++;
    }

    /// <summary>Clause 16.4.12 — a static method, whose reference also triggers the static constructor.</summary>
    public static string Describe() => $"{Stamp}: {Constructions}";
}

/// <summary>
/// Clause 16.4.10 — a struct with a static constructor and no instance constructor at all,
/// so the only trigger for the static constructor is a static member reference. Making a
/// million of these by `default` runs it never.
/// </summary>
public struct StLazilyInitialized
{
    /// <summary>The static state.</summary>
    public static readonly int Answer;

    /// <summary>Clause 16.4.10 — the static constructor, with nothing to trigger it locally.</summary>
    static StLazilyInitialized()
    {
        Answer = 42;
    }

    /// <summary>An instance field, whose default value owes nothing to the static constructor.</summary>
    public int Ignored;
}

/// <summary>
/// Clause 16.4.10 — the triggers, and the non-triggers. The two methods that do not
/// trigger it are the point of the clause.
/// </summary>
public static class StStaticConstructorUse
{
    /// <summary>Clause 16.4.10 — a static field reference, which triggers the static constructor.</summary>
    public static string Triggered() => StStaticInitialized.Stamp;

    /// <summary>Clause 16.4.10 — a static method call, which triggers it too.</summary>
    public static string TriggeredByMethod() => StStaticInitialized.Describe();

    /// <summary>Clause 16.4.10 — an explicit instance constructor call, which triggers it.</summary>
    public static int TriggeredByNew() => new StStaticInitialized(1).Serial;

    /// <summary>
    /// Clause 16.4.10 — `default`, which does not trigger it. This method reads an
    /// instance of a type whose static constructor may never have run.
    /// </summary>
    public static int NotTriggeredByDefault() => default(StLazilyInitialized).Ignored;

    /// <summary>Clause 16.4.10 — an array of the struct, which does not trigger it either.</summary>
    public static int NotTriggeredByArray() => new StLazilyInitialized[8].Length;

    /// <summary>Clause 16.4.10 — and the static field that does, on the same type.</summary>
    public static int TriggeredAtLast() => StLazilyInitialized.Answer;
}
