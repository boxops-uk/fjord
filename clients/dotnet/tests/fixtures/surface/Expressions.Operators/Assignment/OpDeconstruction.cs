using System;

namespace Surface.Expressions.Operators.Assignment;

/// <summary>
/// A point with a <c>Deconstruct</c> method, which is the member 12.7 reaches from a
/// deconstructing assignment or a positional pattern.
/// </summary>
/// <remarks>
/// The member is found by name and shape, but no use site writes the name: a
/// deconstruction is a parenthesised list of targets and an <c>=</c>.
/// </remarks>
public readonly struct OpCoordinate
{
    /// <summary>Constructs a coordinate.</summary>
    public OpCoordinate(int x, int y)
    {
        X = x;
        Y = y;
    }

    /// <summary>The horizontal component.</summary>
    public int X { get; }

    /// <summary>The vertical component.</summary>
    public int Y { get; }

    /// <summary>12.7 — the two-element deconstructor.</summary>
    public void Deconstruct(out int x, out int y)
    {
        x = X;
        y = Y;
    }

    /// <summary>
    /// 12.7 — a second deconstructor at a different arity. Deconstruction picks by the
    /// number of targets on the left, which is the only thing that tells these two apart.
    /// </summary>
    public void Deconstruct(out int x, out int y, out int sum)
    {
        x = X;
        y = Y;
        sum = X + Y;
    }
}

/// <summary>A type with no deconstructor of its own.</summary>
public sealed class OpMeasurement
{
    /// <summary>Constructs a measurement.</summary>
    public OpMeasurement(double magnitude, string unit)
    {
        Magnitude = magnitude;
        Unit = unit;
    }

    /// <summary>The magnitude.</summary>
    public double Magnitude { get; }

    /// <summary>The unit.</summary>
    public string Unit { get; }
}

/// <summary>The extension deconstructor 12.7 admits for <see cref="OpMeasurement"/>.</summary>
public static class OpMeasurementExtensions
{
    /// <summary>
    /// 12.7 — an extension <c>Deconstruct</c>, so a type can be deconstructed from outside
    /// itself entirely.
    /// </summary>
    public static void Deconstruct(this OpMeasurement value, out double magnitude, out string unit)
    {
        magnitude = value.Magnitude;
        unit = value.Unit;
    }
}

/// <summary>
/// The deconstruction of 12.7 and the deconstructing assignment of 12.23.3.
/// </summary>
public static class OpDeconstruction
{
    /// <summary>
    /// 12.23.3.1 — a deconstructing assignment whose targets already exist. Nothing is
    /// declared; the <c>Deconstruct</c> call is the whole of what an index can record.
    /// </summary>
    public static int ToExistingTargets(OpCoordinate point)
    {
        int x;
        int y;

        (x, y) = point;

        return x + y;
    }

    /// <summary>
    /// 12.23.3.2 — the abridged form, where one <c>var</c> stands for the type of every
    /// target and the assignment declares all of them.
    /// </summary>
    public static int Abridged(OpCoordinate point)
    {
        var (x, y) = point;

        return x + y;
    }

    /// <summary>
    /// 12.23.3.2 — the unabridged form, where each target names its own type. Same
    /// deconstructor, three declarations, and a different syntax for them.
    /// </summary>
    public static int Unabridged(OpCoordinate point)
    {
        (int x, int y, int sum) = point;

        return x + y + sum;
    }

    /// <summary>
    /// 12.23.3.1 — a mixed deconstruction, part declaration and part assignment to targets
    /// that already exist, with a discard for the element nobody wants.
    /// </summary>
    public static int Mixed(OpCoordinate point)
    {
        int x = 0;

        (x, int y, _) = point;

        return x + y;
    }

    /// <summary>
    /// 12.23.3.1 — a nested deconstruction, where one target is itself a target list.
    /// </summary>
    public static string Nested((OpCoordinate Position, string Label) pair)
    {
        var ((x, y), label) = pair;

        return $"{label}:{x},{y}";
    }

    /// <summary>
    /// 12.7 — deconstruction through an extension method, and deconstruction of a tuple,
    /// which needs no member at all because the elements are the tuple's own fields.
    /// </summary>
    public static string ExtensionAndTuple(OpMeasurement measurement, (int Count, string Name) tuple)
    {
        var (magnitude, unit) = measurement;
        var (count, name) = tuple;

        return $"{magnitude}{unit}{count}{name}";
    }

    /// <summary>
    /// 12.7 — the same deconstructor reached from a positional pattern rather than an
    /// assignment. One member, two syntaxes, neither of which names it.
    /// </summary>
    public static string ViaPattern(OpCoordinate point) => point switch
    {
        (0, 0) => "origin",
        (var x, 0) => $"on x at {x}",
        (0, var y) => $"on y at {y}",
        var (x, y) => $"{x},{y}",
    };

    /// <summary>
    /// 12.7 — deconstruction inside a <c>foreach</c>, where the declaration is the
    /// iteration variable and the deconstructor runs once per element.
    /// </summary>
    public static int InForeach(OpCoordinate[] points)
    {
        int total = 0;

        foreach (var (x, y) in points)
        {
            total += x + y;
        }

        return total;
    }

    /// <summary>
    /// 12.23.3.1 — a deconstructing assignment whose right operand is an anonymous
    /// function invocation, so the deconstructor runs on a value with no name.
    /// </summary>
    public static int FromAnInvocation(Func<OpCoordinate> make)
    {
        var (x, y) = make();

        return x + y;
    }
}
