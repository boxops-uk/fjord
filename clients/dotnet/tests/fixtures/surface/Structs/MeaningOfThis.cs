// Clause 16.4.7 — Meaning of this. Within an instance constructor or instance function
// member of a struct, `this` is classified as a reference variable of the struct type —
// not a value, as it is in a class — so `this` can be assigned to, and a whole instance
// can be replaced from inside one of its own methods. In a readonly struct, and in a
// member declared readonly, `this` is a `ref readonly` variable and cannot be assigned.
//
// The clause is `both`: `this` is a reference, and it is also the declaration of a
// variable that no declarator introduces. The hazard is precisely that: `this` is a
// variable with a name, a type and a safe context, and nothing in the file declares it.
// Every method below has one, and they do not all mean the same thing.

using System;

namespace Surface.Structs;

/// <summary>
/// Clause 16.4.7 — a struct whose methods use `this` in every way the clause permits: as a
/// receiver, as an argument, as the target of an assignment, and returned by value.
/// </summary>
public struct StThisMutator
{
    /// <summary>The state the whole instance is made of.</summary>
    public int Ticks;

    /// <summary>A second field, so replacing the instance is observable.</summary>
    public string? Tag;

    /// <summary>
    /// Clause 16.4.7 — in a struct constructor `this` is a reference variable, and
    /// assigning to it is how a constructor may initialize every field at once.
    /// </summary>
    public StThisMutator(int ticks)
    {
        this = default;
        Ticks = ticks;
    }

    /// <summary>
    /// Clause 16.4.7 — assignment to `this` in an instance method. The receiver is
    /// replaced wholesale, which is legal for a struct and an error for a class.
    /// </summary>
    public void Reset()
    {
        this = new StThisMutator(0);
    }

    /// <summary>Clause 16.4.7 — `this` used implicitly as the receiver of a field access.</summary>
    public void Tick() => Ticks++;

    /// <summary>Clause 16.4.7 — `this` written explicitly, to disambiguate from a parameter.</summary>
    public void SetTicks(int Ticks) => this.Ticks = Ticks;

    /// <summary>
    /// Clause 16.4.7 / 16.4.2 — `this` passed as an argument by value, so the callee gets
    /// a copy and cannot change this instance.
    /// </summary>
    public int PassedByValue() => StThisUse.TicksOf(this);

    /// <summary>
    /// Clause 16.4.7 / 16.4.15.6 — `this` passed by reference, which is legal only because
    /// `this` is a reference variable in a struct member.
    /// </summary>
    public int PassedByRef() => StThisUse.BumpAndRead(ref this);

    /// <summary>Clause 16.4.7 — `this` returned by value, which copies.</summary>
    public StThisMutator Copy() => this;

    /// <summary>
    /// Clause 16.4.7 / 16.3.2 — a readonly member of a struct that is not readonly. Inside
    /// it, `this` is a `ref readonly` variable, so the two lines above would not compile
    /// here.
    /// </summary>
    public readonly int PeekTicks() => this.Ticks;
}

/// <summary>
/// Clause 16.4.7 — a readonly struct, in which `this` is a `ref readonly` variable in
/// every instance member. The members read `this` and none of them can write it, and no
/// modifier in the file says so member by member.
/// </summary>
public readonly struct StThisFrozen
{
    /// <summary>The only field.</summary>
    public readonly int Ticks;

    /// <summary>Clause 16.4.7 — `this` is assignable in the constructor of a readonly struct.</summary>
    public StThisFrozen(int ticks)
    {
        this = default;
        Ticks = ticks;
    }

    /// <summary>Clause 16.4.7 — `this` read as a value, which copies out of the readonly reference.</summary>
    public StThisFrozen Copy() => this;

    /// <summary>Clause 16.4.7 — `this` as an `in` argument, which needs no copy.</summary>
    public int PassedByIn() => StThisUse.TicksOfIn(in this);

    /// <summary>Clause 16.4.7 — `this` boxed, which does copy, and which the clause allows.</summary>
    public object Boxed() => this;
}

/// <summary>
/// Clause 16.4.7 — the other end of every call above, so that `this` crossing a call
/// boundary is a reference the index can be asked about.
/// </summary>
public static class StThisUse
{
    /// <summary>Takes the struct by value.</summary>
    public static int TicksOf(StThisMutator mutator) => mutator.Ticks;

    /// <summary>Takes the struct by `in`, so `this` need not be copied at the call.</summary>
    public static int TicksOfIn(in StThisFrozen frozen) => frozen.Ticks;

    /// <summary>
    /// Clause 16.4.15.2 — takes the struct by `ref`, mutates it, and returns what it read.
    /// The caller passed `ref this`.
    /// </summary>
    public static int BumpAndRead(ref StThisMutator mutator)
    {
        mutator.Tick();
        return mutator.Ticks;
    }

    /// <summary>Clause 16.4.7 — the whole-instance replacement, observed from outside.</summary>
    public static int ResetLosesTicks()
    {
        StThisMutator mutator = new StThisMutator(7);
        mutator.Reset();
        return mutator.Ticks;
    }
}
