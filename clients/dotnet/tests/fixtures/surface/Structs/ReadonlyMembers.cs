// Clause 16.3.2 — Readonly members. An instance member of a struct other than a
// constructor may be declared `readonly`, which promises the member does not modify the
// state of the instance; every instance member of a `readonly struct` is implicitly
// readonly and may not be declared so explicitly. `readonly` may be applied to a whole
// property, or to a single get accessor of a property that also has a set accessor.
//
// The hazard is that `readonly` on a member changes no name and no signature. A readonly
// method and an ordinary method with the same name are the same identity in every scheme
// that does not record the modifier — and the accessor case is worse, because `readonly
// get` and `set` are two accessors of one property, so the modifier lives one level below
// the member the index is most likely to key on.

using System;

namespace Surface.Structs;

/// <summary>
/// Clause 16.3.2 — a struct that is not readonly, with readonly members in it. Two of its
/// methods differ only by the modifier they carry, and one of its properties has a
/// readonly get beside a mutating set.
/// </summary>
public struct StMutableGauge
{
    /// <summary>An instance field the mutating members write.</summary>
    private int _reading;

    /// <summary>A second field, which the readonly members only read.</summary>
    private readonly int _limit;

    /// <summary>Clause 16.4.9 — the constructor, which cannot be declared readonly.</summary>
    public StMutableGauge(int limit)
    {
        _reading = 0;
        _limit = limit;
    }

    /// <summary>
    /// Clause 16.3.2 — a readonly method. Calling it on a `readonly` variable copies
    /// nothing, which is the difference the modifier buys.
    /// </summary>
    public readonly int Peek() => _reading;

    /// <summary>
    /// Clause 16.3.2 — a method with the same shape and no modifier, which mutates. The
    /// pair exists so that "did the index keep the modifier" is a question about two rows
    /// that are otherwise identical.
    /// </summary>
    public int Take()
    {
        _reading++;
        return _reading;
    }

    /// <summary>Clause 16.3.2 — a readonly property, whole.</summary>
    public readonly int Limit => _limit;

    /// <summary>
    /// Clause 16.3.2 — `readonly` on one accessor only. The get is readonly, the set is
    /// not, and the property itself carries no modifier: three rows, one name.
    /// </summary>
    public int Reading
    {
        readonly get => _reading;
        set => _reading = value < _limit ? value : _limit;
    }

    /// <summary>Clause 16.3.2 — a readonly override of an inherited member.</summary>
    public readonly override string ToString() => $"{_reading}/{_limit}";
}

/// <summary>
/// Clause 16.3.2 — a `readonly struct`, where every instance member is implicitly readonly
/// and none of them may say so. Compare with the struct above: the same promises, none of
/// them written.
/// </summary>
public readonly struct StFrozenGauge
{
    /// <summary>Clause 16.2.2 — an instance field of a readonly struct, which must be readonly.</summary>
    private readonly int _reading;

    /// <summary>A second readonly field.</summary>
    private readonly int _limit;

    /// <summary>Clause 16.4.9 — the constructor, the only place the fields may be written.</summary>
    public StFrozenGauge(int reading, int limit)
    {
        _reading = reading;
        _limit = limit;
    }

    /// <summary>Clause 16.3.2 — implicitly readonly, and it would be an error to say so.</summary>
    public int Peek() => _reading;

    /// <summary>Clause 16.3.2 — an implicitly readonly get-only property.</summary>
    public int Limit => _limit;

    /// <summary>
    /// Clause 16.3.2 — an implicitly readonly auto-property. A readonly struct may declare
    /// one only if it has no set accessor, or an `init` one.
    /// </summary>
    public string? Label { get; init; }

    /// <summary>Clause 16.4.4 — a readonly struct still supports a `with` expression.</summary>
    public StFrozenGauge Advanced() => this with { };

    /// <summary>Clause 16.3.2 — an implicitly readonly override.</summary>
    public override string ToString() => $"{_reading}/{_limit}";
}

/// <summary>
/// Clause 16.3.2 — the uses that make the modifier observable. Calling a non-readonly
/// member through an `in` parameter copies the struct first, so the mutation is lost; the
/// readonly one does not. Both call sites look the same.
/// </summary>
public static class StReadonlyMemberUse
{
    /// <summary>Clause 16.3.2 / 16.4.15.2 — a readonly member through an `in` parameter: no copy.</summary>
    public static int PeekThroughIn(in StMutableGauge gauge) => gauge.Peek();

    /// <summary>
    /// Clause 16.3.2 / 16.4.15.2 — a mutating member through an `in` parameter. The
    /// compiler inserts a defensive copy, so the increment happens to a temporary the
    /// source never names.
    /// </summary>
    public static int TakeThroughIn(in StMutableGauge gauge) => gauge.Take();

    /// <summary>Clause 16.3.2 — the accessor pair, both halves, on a mutable local.</summary>
    public static int RoundTrip()
    {
        StMutableGauge gauge = new StMutableGauge(10);
        gauge.Reading = 4;
        return gauge.Reading;
    }

    /// <summary>Clause 16.3.2 — the readonly struct, whose members promise the same thing silently.</summary>
    public static string FrozenLabel() => new StFrozenGauge(1, 2) { Label = "frozen" }.ToString();
}
