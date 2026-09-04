// Clause 17.3 — the three ways an array instance comes into existence: an array creation
// expression, a declaration carrying an array initializer, and an argument list that feeds a
// parameter array. The rank and the length of each dimension are fixed for the instance's
// whole lifetime, and every element starts at its default value.
//
// The trap here is an asymmetry that is easy to read as absence of the code rather than
// absence of the fact: `new List<int>()` is a `BaseObjectCreationExpressionSyntax`, which the
// walk visits and turns into an object-creation location, while `new int[3]` is an
// `ArrayCreationExpressionSyntax`, which is not in the walk's switch at all. So this file
// creates dozens of instances and the only creation facts written for it are the ones for the
// two `List<int>` calls below. `NewList` exists to make that difference measurable inside one
// file rather than across two.

using System;
using System.Collections.Generic;

namespace Surface.Ranges;

/// <summary>
/// Every form of array creation 17.3 names, and the one non-array creation beside them that
/// says what a creation fact looks like when it is written (17.3).
/// </summary>
public static class ArrCreation
{
    /// <summary>17.3 — a creation expression with an explicit length.</summary>
    public static readonly int[] Sized = new int[3];

    /// <summary>17.3 — an explicit length and an initializer, whose counts have to agree.</summary>
    public static readonly int[] SizedAndFilled = new int[3] { 0, 1, 2 };

    /// <summary>17.3 — no length, so the initializer decides it.</summary>
    public static readonly int[] Filled = new int[] { 0, 2, 4, 6, 8 };

    /// <summary>17.3 — no element type either: it is inferred from the initializer.</summary>
    public static readonly int[] Inferred = new[] { 0, 2, 4 };

    /// <summary>17.3 — a rank-two creation, where both lengths are established at once.</summary>
    public static readonly int[,] Rectangle = new int[2, 3];

    /// <summary>
    /// 17.3 — creating an array of arrays creates exactly one instance: the outer array,
    /// whose elements are all null until each row is created separately.
    /// </summary>
    public static readonly int[][] Rows = new int[2][];

    /// <summary>17.3 — a creation whose element type is an interface, so every element starts null.</summary>
    public static readonly IReadOnlyList<string>[] Views = new IReadOnlyList<string>[2];

    /// <summary>
    /// A non-array creation, kept beside the array ones so that "no creation fact" and "no
    /// creation" are distinguishable in this file.
    /// </summary>
    /// <returns>An empty list.</returns>
    public static List<int> NewList() => new List<int>();

    /// <summary>
    /// 17.3 — elements of an array created by a creation expression are initialized to their
    /// default value: zero, null, and the all-fields-default struct value.
    /// </summary>
    /// <returns>The three defaults, read straight out of fresh arrays.</returns>
    public static (int Zero, string? Null, ArrCell<int> Default) Defaults()
    {
        var numbers = new int[1];
        var labels = new string?[1];
        var cells = new ArrCell<int>[1];

        return (numbers[0], labels[0], cells[0]);
    }

    /// <summary>
    /// 17.3 — the rank and lengths are fixed at creation, so "resizing" is creating a second
    /// instance and copying into it. `Array.Resize` is that, spelled as a library call.
    /// </summary>
    /// <param name="row">The array to grow a copy of.</param>
    /// <returns>A longer instance holding the same elements.</returns>
    public static int[] Grown(int[] row)
    {
        var longer = new int[row.Length + 1];

        Array.Copy(row, longer, row.Length);

        return longer;
    }

    /// <summary>
    /// 17.3 — <c>System.Array</c> is abstract and cannot be instantiated, so the only way to
    /// reach an instance through that type is a creation of some concrete array type widened
    /// to it. `new Array()` is a compile-time error and is written nowhere.
    /// </summary>
    /// <returns>An instance of an array type, seen as the abstract base.</returns>
    public static Array Abstractly() => new int[1];

    /// <summary>
    /// 17.3 — a nested creation expression, where the element type of the outer creation is
    /// itself written as an array type.
    /// </summary>
    /// <returns>An array of arrays, both levels created here.</returns>
    public static int[][] Nested() =>
        new int[][] { new int[2], new int[] { 1, 2, 3 } };

    /// <summary>
    /// 17.3 — a creation expression whose lengths are constant expressions rather than
    /// literals, which is the only form the standard requires to be constant.
    /// </summary>
    /// <returns>An array as long as <see cref="Width"/> says.</returns>
    public static int[] FromConstant() => new int[Width];

    /// <summary>A constant used as a dimension length, so the length has a reference.</summary>
    public const int Width = 4;

    /// <summary>
    /// 17.3 — a creation expression whose length is *not* constant, which is legal precisely
    /// because there is no initializer beside it.
    /// </summary>
    /// <param name="count">How long the array should be.</param>
    /// <returns>An array that long.</returns>
    public static int[] FromVariable(int count) => new int[count];
}
