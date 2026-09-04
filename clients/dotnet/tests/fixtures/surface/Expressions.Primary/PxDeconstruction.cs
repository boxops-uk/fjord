using System;
using System.Collections.Generic;

namespace Surface.Expressions.Primary;

/// <summary>
/// 12.7 — deconstruction binds by convention: a <c>Deconstruct</c> method with the right number
/// of <c>out</c> parameters, found by name, arity and nothing else. Two arities are declared
/// here, so a deconstruction has to pick one on the count of its own targets.
/// </summary>
public sealed class PxTriple
{
    /// <summary>Builds a triple.</summary>
    public PxTriple(int first, int second, int third)
    {
        First = first;
        Second = second;
        Third = third;
    }

    /// <summary>The first component.</summary>
    public int First { get; }

    /// <summary>The second component.</summary>
    public int Second { get; }

    /// <summary>The third component.</summary>
    public int Third { get; }

    /// <summary>12.7 — the two-target deconstructor.</summary>
    public void Deconstruct(out int first, out int second)
    {
        first = First;
        second = Second;
    }

    /// <summary>12.7 — the three-target deconstructor, chosen by the count of targets alone.</summary>
    public void Deconstruct(out int first, out int second, out int third)
    {
        first = First;
        second = Second;
        third = Third;
    }

    /// <summary>Same-file uses: one deconstruction per arity.</summary>
    public int UsedHere()
    {
        var (first, second) = this;
        var (a, b, c) = this;
        return first + second + a + b + c;
    }
}

/// <summary>
/// 12.7 — a deconstructor supplied from outside the type, as an extension method. The
/// deconstruction site names neither this class nor the method.
/// </summary>
public static class PxDeconstructExtensions
{
    /// <summary>Deconstructs the workhorse type, which declares no <c>Deconstruct</c> of its own.</summary>
    public static void Deconstruct(this PxTarget target, out int seed, out int scale)
    {
        seed = target.Seed;
        scale = target.Scale;
    }
}

/// <summary>
/// 12.7 and 12.8.6 — deconstruction in each form the language allows, and the tuple literals
/// that most deconstructions are written against.
/// </summary>
public static class PxDeconstruction
{
    /// <summary>
    /// 12.7 — deconstruction of a declared type: with <c>var</c>, with explicit types, into
    /// existing variables, with a discard, and nested.
    /// </summary>
    public static string OfDeclaredTypes()
    {
        var point = new PxPoint(1, 2);
        var triple = new PxTriple(3, 4, 5);
        var target = new PxTarget(6);

        // A single var covering every target, and then per-target types.
        var (x, y) = point;
        (int first, int second) = point;

        // Into variables that already exist — an assignment, not a declaration.
        int existingLeft;
        int existingRight;
        (existingLeft, existingRight) = point;

        // A discard, which declares nothing at all.
        var (kept, _) = triple;
        (_, _, var third) = triple;

        // Through an extension deconstructor declared in another type.
        var (seed, scale) = target;

        // Mixed: a declaration and an existing variable in one deconstruction.
        (var declared, existingLeft) = point;

        // Nested deconstruction, where the inner target is a tuple.
        var ((left, right), label) = (point, "pair");

        return string.Join(
            " ",
            x + y,
            first + second,
            existingLeft + existingRight,
            kept,
            third,
            seed + scale,
            declared,
            left + right,
            label);
    }

    /// <summary>
    /// 12.7 — deconstruction in the other two places the grammar allows it: a positional
    /// pattern, and a <c>foreach</c> over a sequence of deconstructible values.
    /// </summary>
    public static string InPatternsAndLoops()
    {
        var points = new List<PxPoint> { new(1, 2), new(3, 4) };
        var total = 0;

        foreach (var (x, y) in points)
        {
            total += x + y;
        }

        var matched = points[0] is (1, 2) ? "matched" : "not matched";
        var byProperty = points[1] is (var left, var right) && left < right ? "ordered" : "unordered";

        return $"{total} {matched} {byProperty}";
    }

    /// <summary>
    /// 12.8.6 — tuple literals: unnamed, named, with inferred names, nested, long enough to
    /// need a nested rest, and read back both by name and by <c>ItemN</c>.
    /// </summary>
    public static string TupleLiterals()
    {
        var point = new PxPoint(5, 6);

        var unnamed = (1, 2);
        var named = (Left: 3, Right: 4);
        var inferred = (point.X, point.Y);
        var mixed = (Label: "m", point.X);
        var nested = ((1, 2), (3, 4));
        var long8 = (1, 2, 3, 4, 5, 6, 7, 8);
        var ofDeclaredTypes = (Point: point, Target: new PxTarget(7));
        (int Left, int Right) annotated = (8, 9);

        var byName = named.Left + named.Right;
        var byItem = named.Item1 + unnamed.Item2;
        var byInferredName = inferred.X + inferred.Y;
        var throughNesting = nested.Item1.Item2 + nested.Item2.Item1;
        var throughRest = long8.Item8;
        var throughMember = ofDeclaredTypes.Point.Measure() + ofDeclaredTypes.Target.Seed;

        return string.Join(
            " ",
            byName,
            byItem,
            byInferredName,
            throughNesting,
            throughRest,
            throughMember,
            annotated.Left,
            mixed.Label);
    }

    /// <summary>
    /// 12.8.6 — a tuple literal in every position a value can take: an argument, a return
    /// value, an element of a collection, and a deconstruction source.
    /// </summary>
    public static (int Sum, string Label) TuplesInEveryPosition()
    {
        var accepted = Accept((1, 2));
        var listed = new List<(int Left, string Label)> { (1, "a"), (Left: 2, Label: "b") };
        var (sum, label) = (accepted + listed[0].Left, listed[1].Label);
        return (sum, label);
    }

    private static int Accept((int Left, int Right) pair) => pair.Left + pair.Right;
}
