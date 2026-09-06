// Clause 8.9 — reference types and nullability. The project's default context is
// `enable` (see Types.csproj); this file turns each half of it off and on again by hand, so
// the same type name appears under every nullable context the language has. Nothing here
// changes the types: `string` and `string?` are one type in metadata, separated by an
// annotation the context decides how to read. The warnings this file raises are deliberate
// — they are what clauses 8.9.4.4 and 8.9.5 are about.

namespace Surface.Types;

/// <summary>
/// 8.9.1 hazard — the same type spelled annotated and unannotated in one type declaration.
/// Both fields' types resolve to System.String; only the annotation differs.
/// </summary>
public sealed class TyReferenceNullability
{
    /// <summary>8.9.2 — a non-nullable reference type, which must be initialized.</summary>
    public string Required = string.Empty;

    /// <summary>8.9.3 hazard — the same type, annotated. One identity, two annotations.</summary>
    public string? Optional;

    /// <summary>8.9.3 — the annotation on a constructed type's argument rather than the type.</summary>
    public System.Collections.Generic.List<string?> ListOfOptional = [];

    /// <summary>8.9.3 — and on the constructed type itself, which is a different statement.</summary>
    public System.Collections.Generic.List<string>? OptionalList;

    /// <summary>8.9.3 — an annotated array type, and an array of annotated elements.</summary>
    public string[]? OptionalArray;

    public string?[] ArrayOfOptional = [];

    /// <summary>8.9.3 — an annotated type parameter on a generic method (8.5).</summary>
    public TValue? Widen<TValue>(TValue? candidate)
        where TValue : class => candidate;

    /// <summary>8.9.2 — a non-nullable parameter and return type, which is the default here.</summary>
    public string Echo(string text) => text;

    /// <summary>8.9.3 — an annotated parameter and an annotated return type.</summary>
    public string? EchoOrNull(string? text) => text;
}

/// <summary>
/// 8.9.5.2 — flow analysis. The null state of <c>candidate</c> changes across the body
/// without any declaration or annotation changing; the state itself is the compiler's, and
/// nothing is emitted for it.
/// </summary>
public static class TyNullStates
{
    /// <summary>8.9.5.1 — a maybe-null value narrowed to not-null by a test.</summary>
    public static int LengthOrZero(string? candidate)
    {
        if (candidate is null)
        {
            return 0;
        }

        return candidate.Length;
    }

    /// <summary>8.9.5.2 — narrowing by pattern, which reaches the same state a different way.</summary>
    public static int LengthByPattern(string? candidate) =>
        candidate is string present ? present.Length : 0;

    /// <summary>8.9.5.2 — narrowing by assignment, so the state is not-null with no test at all.</summary>
    public static int LengthAfterAssignment(string? candidate)
    {
        candidate = "assigned";
        return candidate.Length;
    }

    /// <summary>
    /// 8.9.5.3 hazard — the null-forgiving operator. It changes the null state and nothing
    /// else: the type of <c>candidate!</c> is the type of <c>candidate</c>.
    /// </summary>
    public static int LengthForgiven(string? candidate) => candidate!.Length;

    /// <summary>8.9.5.3 — a conversion from annotated to unannotated, which warns and compiles.</summary>
    public static string Unwrap(string? candidate)
    {
#pragma warning disable CS8603 // Possible null reference return — 8.9.4.4's warning, on purpose.
        return candidate;
#pragma warning restore CS8603
    }

    /// <summary>8.9.5.3 — a conversion from unannotated to annotated, which is always safe.</summary>
    public static string? Wrap(string candidate) => candidate;

    /// <summary>8.9.5.3 — the nullability attributes, which state a contract flow analysis
    /// cannot infer. Each is a real attribute reference on a real parameter.</summary>
    public static bool TryDescribe(
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] string? candidate,
        [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out string description)
    {
        if (candidate is null)
        {
            description = null!;
            return false;
        }

        description = candidate;
        return true;
    }

    /// <summary>8.9.5.3 — AllowNull on a setter-like parameter whose type is non-nullable.</summary>
    public static void Accept([System.Diagnostics.CodeAnalysis.AllowNull] string text) =>
        System.GC.KeepAlive(text);

    /// <summary>8.9.5.3 — DisallowNull, the opposite statement on an annotated type.</summary>
    public static void Insist([System.Diagnostics.CodeAnalysis.DisallowNull] string? text) =>
        System.GC.KeepAlive(text);

    /// <summary>8.9.5.3 — NotNull, which promises the caller a not-null state on return.</summary>
    public static void Fill([System.Diagnostics.CodeAnalysis.NotNull] ref string? slot) =>
        slot ??= string.Empty;
}

/// <summary>
/// 8.9.5.3 — MemberNotNull, which lets a constructor delegate its initialization and still
/// satisfy 8.9.2's rule that a non-nullable field be assigned.
/// </summary>
public sealed class TyLateInitialized
{
    /// <summary>8.9.2 — a non-nullable field not assigned in the constructor body.</summary>
    public string Name;

    public TyLateInitialized() => Reset();

    /// <summary>8.9.5.3 — the attribute that makes the constructor above warning-free.</summary>
    [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(Name))]
    public void Reset() => Name = "reset";
}

#nullable disable

/// <summary>
/// 8.9.4.2 hazard — the nullable context is disabled from here. <c>Legacy</c> below has the
/// same type as <see cref="TyReferenceNullability.Required"/> and the same type as
/// <see cref="TyReferenceNullability.Optional"/>: one identity, and an annotation state that is
/// neither annotated nor unannotated but oblivious.
/// </summary>
public sealed class TyObliviousContext
{
    /// <summary>8.9.4.2 — an oblivious reference type: no warning for leaving it null.</summary>
    public string Legacy;

    /// <summary>8.9.4.2 — assigning null to it, which is the warning the disable suppresses.</summary>
    public void Clear() => Legacy = null;

    /// <summary>8.9.4.2 — dereferencing it, likewise unwarned.</summary>
    public int Length() => Legacy.Length;
}

#nullable restore

/// <summary>
/// 8.9.4.1 hazard — the restored context. This class's <c>Text</c> field spells the same
/// type as the oblivious <c>Legacy</c> above and is read under the project default again.
/// </summary>
public sealed class TyRestoredContext
{
    public string Text = string.Empty;

    public string? MaybeText;
}

#nullable enable annotations
#nullable disable warnings

/// <summary>
/// 8.9.4.3 — annotations enabled, 8.9.4.4 — warnings disabled. The <c>?</c> below is read
/// as an annotation, and the dereference that contradicts it raises nothing.
/// </summary>
public sealed class TyAnnotationsOnly
{
    /// <summary>8.9.4.3 — the annotation is recorded in metadata even with warnings off.</summary>
    public string? Annotated;

    /// <summary>8.9.4.4 — a dereference of a maybe-null value, with no warning to show for it.</summary>
    public int Length() => Annotated.Length;
}

#nullable disable annotations
#nullable enable warnings

/// <summary>
/// 8.9.4.3 — annotations disabled while warnings are enabled: the `?` on a reference type
/// would now itself be a warning, so this class annotates nothing and still gets flow
/// warnings for what it does with null.
/// </summary>
public sealed class TyWarningsOnly
{
    /// <summary>8.9.4.3 — an unannotated reference type in a context with no annotations.</summary>
    public string Text = string.Empty;

    /// <summary>8.9.4.4 — a nullable value type is unaffected by the annotations context.</summary>
    public int? Count;
}

#nullable enable

/// <summary>
/// 8.9.4.5 — the context fully enabled, which is where the file ends so that nothing after
/// it inherits a half-disabled state.
/// </summary>
public sealed class TyEnabledContext
{
    /// <summary>8.9.2 — non-nullable, and the compiler insists it be assigned.</summary>
    public string Required;

    /// <summary>8.9.3 — nullable, and it need not be.</summary>
    public string? Optional;

    /// <summary>8.9.4.5 — the constructor that satisfies 8.9.2 for the field above.</summary>
    public TyEnabledContext(string required) => Required = required;

    /// <summary>8.9.1 — every reference type is one of the two, and neither is a new type.</summary>
    public bool BothSpellingsAreOneType() => Required.GetType() == (Optional ?? Required).GetType();
}
