using System;
using System.Linq.Expressions;

namespace Surface.Preview;

/// <summary>
/// C# 14 — Optional and named arguments in Expression trees. An expression lambda's body is
/// converted to a tree rather than to code, and the conversion used to reject a call with a
/// named or an omitted argument outright. Now it accepts them — and the *tree* records the
/// arguments in parameter order with the defaults filled in, so the tree and the source no
/// longer agree about what the call looked like.
/// </summary>
public static class ExpressionTreeArguments
{
    /// <summary>The method the trees below call.</summary>
    /// <param name="left">The first addend, which has no default.</param>
    /// <param name="right">The second, which does.</param>
    /// <param name="scale">The third, which the trees omit.</param>
    public static int Add(int left, int right = 2, int scale = 1) => (left + right) * scale;

    /// <summary>C# 14 — a named argument inside an expression tree.</summary>
    public static Expression<Func<int>> Named() => () => Add(1, right: 5);

    /// <summary>C# 14 — an omitted optional argument inside an expression tree.</summary>
    public static Expression<Func<int>> Omitted() => () => Add(3);

    /// <summary>
    /// C# 14 — every supplied argument named, with the trailing optional one omitted.
    /// A name that moves an argument away from its parameter's position is still CS9307
    /// ("an expression tree may not contain a named argument specification out of position"),
    /// and so is naming the third parameter while skipping the second — so this is exactly as
    /// far as the feature goes inside a tree.
    /// </summary>
    public static Expression<Func<int>> NamedAndOmitted() => () => Add(left: 4, right: 5);

    /// <summary>C# 14 — a named argument on a constructor call inside a tree.</summary>
    public static Expression<Func<Register>> Constructed() => () => new Register { Name = "tree" };

    /// <summary>The positional spelling, which every version accepted.</summary>
    public static Expression<Func<int>> Positional() => () => Add(1, 2, 3);

    /// <summary>Compiles and runs each tree, so the trees are not merely built.</summary>
    public static string All() =>
        $"{Named().Compile()()}{Omitted().Compile()()}{NamedAndOmitted().Compile()()}"
        + $"{Positional().Compile()()}{Constructed().Compile()().Name}";
}
