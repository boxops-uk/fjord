// Clause 13.6.3 — local constant declarations.
//
// A local constant is not a variable: it has no storage, its value is substituted at every
// use, and it cannot be the target of an assignment or of `ref`. So a use of one leaves a
// reference to a declaration that has no runtime existence at all — an index built from
// source can hold the edge and an index built from IL cannot, because there is nothing in the
// IL to hold it against.
//
// The hazard is `Limit`, which is declared three times over: as a const *field* of the type,
// as an int local constant in one block, and as a string local constant in a sibling block.
// The third block then uses the simple name `Limit` and means the field, because no local
// constant is in scope there. One identifier, three declarations, three different containers,
// and the resolution of the use in the third block is the fact worth asking about.

using System;

namespace Surface.Statements;

/// <summary>Clause 13.6.3 — the local constant, and what shadows it.</summary>
public static class StmtLocalConstants
{
    /// <summary>
    /// A constant field whose name two local constants below take.
    /// </summary>
    private const int Limit = 100;

    /// <summary>The field's value, so a caller can compare it with what the locals say.</summary>
    public static int FieldLimit => Limit;

    /// <summary>
    /// Declares <c>Limit</c> as a local constant twice, and then uses the field of that name.
    /// </summary>
    /// <remarks>
    /// 13.6.3, and its hazard. The two local declarations are in sibling blocks and have
    /// different types; the use in the third block binds to <see cref="Limit"/>, the field.
    /// </remarks>
    /// <param name="n">A number to clamp.</param>
    /// <returns>A total that touches all three bindings.</returns>
    public static int Bounded(int n)
    {
        int total = 0;

        // 13.6.3 — a local_constant_declaration with two declarators. One statement, two
        // constants, and the scope of each is the whole block, as a variable's is.
        {
            const int Limit = 10, Floor = 1;
            total += Math.Clamp(n, Floor, Limit);
        }

        // 13.6.3 — `Limit` again, a `string` this time, in a sibling block. A local constant
        // of reference type may only be `string` or a null constant, which is why this is the
        // only other type available for the collision.
        {
            const string Limit = "ten";
            total += Limit.Length;
        }

        // 13.6.3 — no local `Limit` here, so this use of the simple name binds to the const
        // *field*, and the constant it is initialized from is 100 rather than 10 or "ten".
        {
            const int Scaled = Limit * 2;
            total += Scaled;
        }

        return total;
    }

    /// <summary>
    /// The constant types 13.6.3 admits, one declaration each.
    /// </summary>
    /// <remarks>
    /// 13.6.3. A constant's initializer must be a constant expression, so every one below is
    /// computed at compile time — including <c>Derived</c>, which is a reference to
    /// <c>Doubled</c> that leaves no trace in the emitted code. <c>Absent</c> is the null
    /// constant, the only other reference type a local constant can have.
    /// </remarks>
    /// <returns>A description of every constant, which is itself constant-folded.</returns>
    public static string EveryConstantType()
    {
        const int Doubled = 2 * 21;
        const long Wide = Doubled * 1_000L;
        const double Ratio = 1.5;
        const decimal Money = 9.99m;
        const char Marker = 'x';
        const bool Enabled = true;
        const string Label = "limit";
        const string? Absent = null;
        const StmtConstantGate Gate = StmtConstantGate.Open;

        // A constant whose initializer is a reference to another constant, which is where the
        // reference edge with no runtime counterpart lives.
        const int Derived = Doubled + 1;

        return $"{Derived} {Wide} {Ratio} {Money} {Marker} {Enabled} {Label} {Absent ?? "none"} {Gate}";
    }

    /// <summary>
    /// What a local constant may not do, recorded because the corpus cannot hold it: it may
    /// not be assigned (CS0131), it may not be the operand of <c>ref</c> (CS0199-shaped), and
    /// its initializer may not read a variable (CS0133). Each of those is a fact about the
    /// declaration form rather than about any one declaration.
    /// </summary>
    /// <param name="seed">A number that a constant initializer may not read.</param>
    /// <returns>The seed, plus a constant.</returns>
    public static int WhatAConstantCannotDo(int seed)
    {
        const int Step = 5;

        return seed + Step;
    }
}

/// <summary>An enum, so that a local constant can have an enum type.</summary>
public enum StmtConstantGate
{
    /// <summary>Shut.</summary>
    Closed = 0,

    /// <summary>Not shut.</summary>
    Open = 1,
}
