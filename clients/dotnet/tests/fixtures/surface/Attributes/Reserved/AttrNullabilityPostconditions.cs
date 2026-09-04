// Clause 23.5.7.6 (MaybeNull), 23.5.7.7 (MaybeNullWhen), 23.5.7.8 (MemberNotNull),
// 23.5.7.9 (MemberNotNullWhen), 23.5.7.10 (NotNull), 23.5.7.11 (NotNullIfNotNull) and
// 23.5.7.12 (NotNullWhen).
//
// These seven are postconditions: they describe the state of an output *after* a call. Two
// distinct hazards run through them.
//
// The first is the target. MaybeNull, NotNull and NotNullIfNotNull are applied through the
// `return:` target to a return value that no source construct declares, and MaybeNullWhen,
// NotNullWhen and NotNull are applied to `out` parameters — so a query for "which
// declarations does MaybeNullAttribute annotate" has to answer with something other than a
// named symbol at four sites below.
//
// The second is sharper, and it is why MemberNotNull, MemberNotNullWhen and
// NotNullIfNotNull sit in one file with the rest. Their arguments are *names of other
// declarations, carried as strings*. `[MemberNotNull(nameof(_items))]` is a syntactic
// reference the compiler resolves and a reader can follow; `[MemberNotNull("_items")]` is
// the same reference to the same field with no syntax linking them, and the compiler
// resolves that one too. Both spellings are below, on the same field, and they are the
// pair an index has to answer for: either it resolves attribute-argument strings to
// members — and then a mistyped one silently resolves to nothing — or it does not, and
// then the `nameof` spelling has a reference the literal spelling lacks, for one identical
// fact.
//
// Two "wrong" names are kept on purpose, and the compiler treats them differently, which
// was found by building this file rather than by reading the clause:
//
//   * `[MemberNotNull("_absent")]` names no field of this type, and Roslyn reports CS8776,
//     "Member '_absent' cannot be used in this attribute" — a warning, so the application
//     still reaches metadata. A dangling reference that is diagnosed.
//   * `[return: NotNullIfNotNull("absent")]` names no parameter of its method, and Roslyn
//     reports nothing at all. A dangling reference that is silent.
//
// So an index that resolves attribute-argument strings has one case where a diagnostic
// would have corroborated it and one where nothing would.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Surface.Attributes.Reserved;

/// <summary>
/// 23.5.7.6, 23.5.7.7 and 23.5.7.12: an output whose nullability the declared type gets
/// wrong, in the three ways the clause distinguishes.
/// </summary>
/// <typeparam name="TValue">What the table holds.</typeparam>
public sealed class AttrNullableTable<TValue>
{
    private readonly Dictionary<string, TValue> _rows = [];

    /// <summary>
    /// 23.5.7.6: the return type is <c>TValue</c>, unconstrained, and the value may still
    /// be null — which the type alone cannot express.
    /// </summary>
    /// <param name="key">Which row.</param>
    /// <returns>The row, or the default, which may be null.</returns>
    [return: MaybeNull]
    public TValue Find(string key) => _rows.TryGetValue(key, out TValue? found) ? found : default!;

    /// <summary>
    /// 23.5.7.6: on a property rather than a return value. Declared non-nullable, may be
    /// null, and the attribute is the only statement of that.
    /// </summary>
    [MaybeNull]
    public TValue Newest { get; private set; } = default!;

    /// <summary>
    /// 23.5.7.7: the out parameter is meaningful only when the method returned true, so
    /// its nullability is conditional on the return value.
    /// </summary>
    /// <param name="key">Which row.</param>
    /// <param name="value">The row, when this returns true.</param>
    /// <returns>Whether the row was there.</returns>
    public bool TryFind(string key, [MaybeNullWhen(false)] out TValue value) =>
        _rows.TryGetValue(key, out value);

    /// <summary>
    /// 23.5.7.12: the other polarity, on a nullable out parameter that is *not* null when
    /// the method returns true.
    /// </summary>
    /// <param name="key">Which row.</param>
    /// <param name="label">The row's label, when this returns true.</param>
    /// <returns>Whether the row was there and had a label.</returns>
    public bool TryLabel(string key, [NotNullWhen(true)] out string? label)
    {
        label = _rows.ContainsKey(key) ? key : null;
        return label is not null;
    }

    /// <summary>
    /// 23.5.7.12: on an ordinary input parameter, which is the form that reads as a
    /// predicate — "if this returns true, the argument was not null".
    /// </summary>
    /// <param name="candidate">Checked for emptiness.</param>
    /// <returns>Whether the candidate is a usable key.</returns>
    public static bool IsUsable([NotNullWhen(true)] string? candidate) =>
        !string.IsNullOrEmpty(candidate);

    /// <summary>Adds a row, so the dictionary is written as well as read.</summary>
    /// <param name="key">Which row.</param>
    /// <param name="value">Its value.</param>
    public void Add(string key, TValue value)
    {
        _rows[key] = value;
        Newest = value;
    }
}

/// <summary>
/// 23.5.7.8 and 23.5.7.9: the two attributes whose arguments are member names, in both
/// spellings and once with a name that resolves to nothing.
/// </summary>
public sealed class AttrNullableCache
{
    private string[]? _items;
    private string? _label;
    private string? _source;

    /// <summary>
    /// 23.5.7.8: <c>nameof</c>, so the argument is a syntactic reference to the field.
    /// </summary>
    [MemberNotNull(nameof(_items))]
    public void Load()
    {
        _items = ["first", "second"];
    }

    /// <summary>
    /// 23.5.7.8: the same guarantee about the same field, written as a string literal.
    /// The compiler resolves this one too, and nothing in the syntax links it to the field.
    /// </summary>
    [MemberNotNull("_items")]
    public void Reload()
    {
        _items = ["reloaded"];
    }

    /// <summary>
    /// 23.5.7.8: the <c>params</c> form, naming two fields at once — one application, two
    /// references.
    /// </summary>
    [MemberNotNull(nameof(_label), nameof(_source))]
    public void Describe()
    {
        _label = "cache";
        _source = "memory";
    }

    /// <summary>
    /// 23.5.7.8: an argument naming no member of this type, which is CS8776 — a warning,
    /// not an error, so the unresolvable name is still in the assembly.
    /// </summary>
    [MemberNotNull("_absent")]
    public void Touch()
    {
        _items ??= [];
    }

    /// <summary>
    /// 23.5.7.9: the conditional form on a property, whose first argument is the return
    /// value it is conditional on and whose second is a member name.
    /// </summary>
    [MemberNotNullWhen(true, nameof(_items))]
    public bool IsLoaded => _items is not null;

    /// <summary>
    /// 23.5.7.9: on a method, with the false polarity and two member names.
    /// </summary>
    /// <returns>Whether the cache is still empty.</returns>
    [MemberNotNullWhen(false, nameof(_label), nameof(_source))]
    public bool IsUndescribed()
    {
        if (_label is null || _source is null)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Reads the guarantees back: after <c>Load</c> and inside <c>IsLoaded</c>'s true
    /// branch, no suppression is needed to index the array.
    /// </summary>
    /// <returns>How the cache describes itself.</returns>
    public string Report()
    {
        Load();
        Reload();
        Touch();

        if (IsUndescribed())
        {
            Describe();
        }

        return IsLoaded ? $"{_items.Length} items, {_label} from {_source}" : "empty";
    }
}

/// <summary>
/// 23.5.7.10 and 23.5.7.11: the unconditional postconditions, on a parameter and on a
/// return value.
/// </summary>
public static class AttrNullableGuards
{
    /// <summary>
    /// 23.5.7.10: after this returns, the argument is not null — which is what lets a
    /// caller stop suppressing warnings on a variable it never reassigned.
    /// </summary>
    /// <param name="posting">Not null once this has returned.</param>
    public static void Require([NotNull] string? posting)
    {
        if (posting is null)
        {
            throw new ArgumentNullException(nameof(posting));
        }
    }

    /// <summary>
    /// 23.5.7.10: on an <c>out</c> parameter, and on the return value of a method whose
    /// declared return type is nullable.
    /// </summary>
    /// <param name="candidate">What to normalise.</param>
    /// <param name="normalised">Never null once this has returned.</param>
    /// <returns>The same value, never null.</returns>
    [return: NotNull]
    public static string? Normalise(string? candidate, [NotNull] out string? normalised)
    {
        normalised = candidate ?? "unnamed";
        return normalised;
    }

    /// <summary>
    /// 23.5.7.11: the return value is null only if the argument was — expressed by naming
    /// the parameter, here through <c>nameof</c>.
    /// </summary>
    /// <param name="input">The value whose nullness decides the result's.</param>
    /// <returns>The trimmed input, null exactly when the input was null.</returns>
    [return: NotNullIfNotNull(nameof(input))]
    public static string? Echo(string? input) => input?.Trim();

    /// <summary>
    /// 23.5.7.11: the same guarantee written as a string literal, and a third application
    /// whose argument names a parameter this method does not have.
    /// </summary>
    /// <param name="input">The value whose nullness decides the result's.</param>
    /// <returns>The input's length as text, null exactly when the input was null.</returns>
    [return: NotNullIfNotNull("input")]
    public static string? Measure(string? input) => input?.Length.ToString();

    /// <summary>
    /// 23.5.7.11: an argument naming a parameter that is not there. Unlike MemberNotNull's
    /// equivalent above, this one draws no diagnostic whatever.
    /// </summary>
    /// <param name="input">The value.</param>
    /// <returns>The input, unchanged.</returns>
    [return: NotNullIfNotNull("absent")]
    public static string? Passthrough(string? input) => input;

    /// <summary>
    /// Uses all four, so each guarantee is relied on somewhere and not merely stated.
    /// </summary>
    /// <param name="posting">A posting that may be null.</param>
    /// <returns>A description of it.</returns>
    public static string Uses(string? posting)
    {
        Require(posting);
        string normal = Normalise(posting, out string? viaOut);
        return $"{posting.Length}:{normal}:{viaOut}:{Echo(posting).Length}:{Measure(posting)}"
            + $":{Passthrough(posting) ?? "none"}";
    }
}
