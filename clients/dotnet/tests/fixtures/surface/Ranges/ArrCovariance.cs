// Clause 17.6 — array covariance: where a reference conversion exists from `A` to `B`, the
// same conversion exists from `A[R]` to `B[R]` for any one rank specifier `R`. So a value of
// an array type may be a reference to an instance of a *different* array type, which is why
// every store into an element of a reference-type array carries a run-time check.
//
// Nothing in this clause declares a member, and the conversion it licenses has no symbol of
// its own: it is not a user-defined operator, so at `object[] widened = derived;` the only
// facts written are the two name references. What an index can say about this clause is
// entirely the heritage edge between `ArrDerived` and `ArrBase` — the array-typed conversion
// that edge licenses is nowhere in the output, and a query about it has to infer it.

using System.Collections.Generic;

namespace Surface.Ranges;

/// <summary>What both sides of the covariant conversion agree on (17.6).</summary>
public interface IArrShape
{
    /// <summary>How the shape is drawn.</summary>
    ArrEdge Edge { get; }
}

/// <summary>The base of the covariant pair (17.6).</summary>
public class ArrBase : IArrShape
{
    /// <inheritdoc/>
    public ArrEdge Edge => ArrEdge.None;

    /// <summary>What to call this one.</summary>
    /// <returns>A label.</returns>
    public virtual string Label() => "base";
}

/// <summary>The derived half of the covariant pair (17.6).</summary>
public sealed class ArrDerived : ArrBase
{
    /// <inheritdoc/>
    public override string Label() => "derived";
}

/// <summary>
/// The conversions array covariance licenses, in both directions and at two ranks, plus the
/// run-time check it forces on element assignment (17.6).
/// </summary>
public static class ArrCovariance
{
    /// <summary>17.6 — the implicit conversion, following the element type's own.</summary>
    /// <param name="items">An array of the derived type.</param>
    /// <returns>The same instance, seen as an array of the base type.</returns>
    public static ArrBase[] Widened(ArrDerived[] items) => items;

    /// <summary>
    /// 17.6 — the conversion holds for any rank specifier, provided it is the same one on
    /// both sides.
    /// </summary>
    /// <param name="grid">A rank-two array of the derived type.</param>
    /// <returns>The same instance, at the same rank, seen as the base type.</returns>
    public static ArrBase[,] WidenedGrid(ArrDerived[,] grid) => grid;

    /// <summary>17.6 — the element type may be widened to an implemented interface too.</summary>
    /// <param name="items">An array of the derived type.</param>
    /// <returns>The same instance, seen as an array of the interface.</returns>
    public static IArrShape[] AsShapes(ArrDerived[] items) => items;

    /// <summary>17.6 — and all the way to <c>object</c>, which is the form the clause's own example uses.</summary>
    /// <param name="labels">An array of strings.</param>
    /// <returns>The same instance, seen as an array of objects.</returns>
    public static object[] AsObjects(string[] labels) => labels;

    /// <summary>
    /// 17.6 — the explicit reference conversion back down, which is the direction that can
    /// fail at run time and so is the one that must be written.
    /// </summary>
    /// <param name="items">An array that may or may not really hold the derived type.</param>
    /// <returns>The same instance, seen as an array of the derived type.</returns>
    public static ArrDerived[] Narrowed(ArrBase[] items) => (ArrDerived[])items;

    /// <summary>
    /// 17.6's own example. The assignment to <c>array[i]</c> carries the run-time check the
    /// clause describes: the third call in <see cref="Mismatch"/> passes a boxed <c>int</c>
    /// for a <c>string[]</c> and throws.
    /// </summary>
    /// <param name="array">The array to fill.</param>
    /// <param name="index">Where to start.</param>
    /// <param name="count">How many elements to write.</param>
    /// <param name="value">What to write into each.</param>
    public static void Fill(object[] array, int index, int count, object value)
    {
        for (var i = index; i < index + count; i++)
        {
            array[i] = value;
        }
    }

    /// <summary>
    /// 17.6's example's <c>Main</c>: two calls that succeed and one that throws
    /// <c>ArrayTypeMismatchException</c>, all three of which compile.
    /// </summary>
    /// <returns>The array that was filled.</returns>
    public static string[] Mismatch()
    {
        var strings = new string[100];

        Fill(strings, 0, 100, "Undefined");
        Fill(strings, 0, 10, null!);
        Fill(strings, 90, 10, 0);

        return strings;
    }

    /// <summary>
    /// 17.6's closing sentence — covariance does not extend to arrays of value types, so
    /// <c>int[]</c> is not an <c>object[]</c>. The conversion that *is* available is the one
    /// to a covariant interface, which boxes on read rather than reinterpreting the array.
    /// </summary>
    /// <param name="numbers">An array of a value type.</param>
    /// <returns>The elements, boxed one at a time through the interface.</returns>
    public static IEnumerable<object> Boxed(int[] numbers)
    {
        foreach (var number in numbers)
        {
            yield return number;
        }
    }

    /// <summary>
    /// 17.6 — the covariant array passed at an argument position, where the conversion is
    /// invisible: the argument names an array of the derived type and the parameter is an
    /// array of the base type.
    /// </summary>
    /// <returns>What the base-typed parameter made of it.</returns>
    public static string ThroughAnArgument() => FirstLabel(new[] { new ArrDerived() });

    /// <summary>The callee whose parameter type is the widened one.</summary>
    /// <param name="items">An array of the base type, whatever it really holds.</param>
    /// <returns>The first element's label.</returns>
    public static string FirstLabel(ArrBase[] items) =>
        items.Length == 0 ? string.Empty : items[0].Label();
}
