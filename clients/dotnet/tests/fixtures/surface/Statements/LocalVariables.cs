// Clause 13.6.2 — local variable declarations: 13.6.2.1 (declarators and scope), 13.6.2.2
// (implicitly typed) and 13.6.2.3 (explicitly typed). The ref form, 13.6.2.4, is in
// `RefLocals.cs`, and the `var` *ambiguity* half of 13.6.2.1 is in `VarAmbiguity.cs`, which
// needs a namespace of its own to hold it.
//
// Three hazards live here, one per subclause:
//
//   13.6.2.1 — one declaration statement, three declarators. A statement is one syntax node
//              and three declarations, so an index that keys on the statement records one
//              third of what was written.
//   13.6.2.2 — `inferred` declared twice by `var`, in sibling blocks, inferring two different
//              types. The identifier is the same, the spelled type is the same token, and the
//              declared types are `string` and `int`.
//   13.6.2.3 — `typed` declared twice explicitly, in sibling blocks, at two types. The same
//              collision with the type written down, so that the pair can be compared with
//              the pair above.

using System;
using System.Collections.Generic;

namespace Surface.Statements;

/// <summary>Clause 13.6.2 — the declarator, the inferred type, and the written one.</summary>
public sealed class StmtLocalVariables
{
    /// <summary>A field whose name a local below takes, deliberately.</summary>
    private int _depth = 3;

    /// <summary>The field's value, so that reading it is not dead code.</summary>
    public int Depth => _depth;

    /// <summary>
    /// One declaration statement with three declarators, and one with a declarator that has
    /// no initializer.
    /// </summary>
    /// <remarks>
    /// 13.6.2.1. `int first = 1, second = first + 1, third;` is a single
    /// local_variable_declaration whose declarator list has three entries — and the second
    /// declarator reads the first, which is legal because a declarator's scope starts at its
    /// own declarator and not at the end of the statement.
    /// </remarks>
    /// <param name="seed">A number to work from.</param>
    /// <returns>A total over all the declarators.</returns>
    public int Declarators(int seed)
    {
        // 13.6.2.1 — three declarators, one statement. `third` has no initializer, so it is
        // declared here and definitely assigned two lines below.
        int first = seed, second = first + 1, third;

        third = second * 2;

        // 13.6.2.1 — a local that takes the name of a field of the enclosing type. Both are
        // in scope in this method; the local wins, and `this._depth` is how the field is
        // still reachable. Two declarations, one simple name, and a *containing type* rather
        // than a containing block between them.
        int _depth = first + third;

        return first + second + third + _depth + this._depth;
    }

    /// <summary>
    /// Declares <c>inferred</c> twice with <c>var</c>, at two different types.
    /// </summary>
    /// <remarks>
    /// 13.6.2.2, and its hazard. Neither declaration writes a type; the declared types are
    /// `string` and `List&lt;int&gt;`. An index that records the *spelled* type records the
    /// token `var` twice and has lost the fact entirely.
    /// </remarks>
    /// <param name="seed">A number to work from.</param>
    /// <returns>A total that touches both.</returns>
    public int TwoInferred(int seed)
    {
        int total = 0;

        // 13.6.2.2 — inferred as `string`.
        {
            var inferred = seed.ToString();
            total += inferred.Length;
        }

        // 13.6.2.2 — inferred as `List<int>`, from a collection expression's target type,
        // which is inferred from... nothing, so it has to be written on the left. `var` and a
        // collection expression cannot both decline to name the type.
        {
            var inferred = new List<int> { seed, seed + 1 };
            total += inferred.Count;
        }

        return total;
    }

    /// <summary>
    /// The rest of 13.6.2.2's forms: an anonymous type, a target-typed <c>new</c>, a lambda
    /// with an inferred delegate type, and a <c>var</c> over a tuple.
    /// </summary>
    /// <param name="seed">A number to work from.</param>
    /// <returns>A total that touches all four.</returns>
    public int EveryInferredForm(int seed)
    {
        // 13.6.2.2 — an anonymous type. The declared type of `record` has no name a query
        // can be given, which makes `var` mandatory rather than convenient.
        var record = new { Key = "seed", Weight = seed };

        // 13.6.2.2 with the post-standard target-typed `new`: the type is written on the
        // left, and `new()` on the right names nothing. The mirror image of `var`.
        List<int> cells = new() { seed };

        // 13.6.2.2 — a lambda whose natural type is a `Func<int, int>` the source never
        // writes. Post-standard: before natural types, this needed the delegate type on the
        // left.
        var scale = (int factor) => factor * seed;

        // 13.6.2.2 — a tuple, whose element names are part of the type.
        var pair = (Key: "seed", Weight: seed);

        return record.Weight + cells.Count + scale(2) + pair.Weight;
    }

    /// <summary>
    /// Declares <c>typed</c> twice, explicitly, at two different types.
    /// </summary>
    /// <remarks>
    /// 13.6.2.3, and its hazard: the same shape as <see cref="TwoInferred"/> with the types
    /// written down, so that an index which gets one of the two pairs right and the other
    /// wrong says which half of the declaration it was reading.
    /// </remarks>
    /// <param name="seed">A number to work from.</param>
    /// <returns>A total that touches both.</returns>
    public int TwoTyped(int seed)
    {
        int total = 0;

        // 13.6.2.3 — explicitly `string`.
        {
            string typed = seed.ToString();
            total += typed.Length;
        }

        // 13.6.2.3 — explicitly `int?`, which is a different type again and nullable, so the
        // annotation is part of what the declaration says.
        {
            int? typed = seed > 0 ? seed : null;
            total += typed ?? 0;
        }

        return total;
    }

    /// <summary>
    /// The explicit forms 13.6.2.3 admits that are easy to miss: an array type, a nullable
    /// reference type, a fully qualified type, an alias, a tuple type and a nested generic.
    /// </summary>
    /// <param name="seed">A number to work from.</param>
    /// <returns>A total that touches all of them.</returns>
    public int EveryExplicitForm(int seed)
    {
        // 13.6.2.3 — an array type, whose initializer is a collection expression.
        int[] cells = [seed, seed + 1];

        // 13.6.2.3 — a nullable *reference* type. The annotation is a fact about the
        // declaration that erases to nothing at runtime.
        string? absent = seed > 0 ? null : "present";

        // 13.6.2.3 — a namespace-qualified type name, resolved without a using directive.
        System.Text.StringBuilder builder = new();
        builder.Append(cells[0]);

        // 13.6.2.3 — a tuple type written out, with element names in the declaration.
        (string Key, int Weight) pair = ("seed", seed);

        // 13.6.2.3 — a nested constructed type. One declaration, four type names.
        Dictionary<string, List<int>> index = new() { [pair.Key] = [.. cells] };

        return cells.Length + (absent?.Length ?? 0) + builder.Length + index.Count;
    }

    /// <summary>
    /// The scope half of 13.6.2.1: a local declared after a use of the same simple name.
    /// </summary>
    /// <remarks>
    /// 13.6.2.1. The scope of a local is the whole enclosing block, including the text before
    /// its declaration — so the first `Console.Write` below could not be a use of the *field*
    /// `_depth` even though a reader might take it for one. Using the name before its
    /// declaration is CS0841, which is why the first statement writes `this._depth`
    /// explicitly. The corpus cannot hold the error, so the comment is where it lives.
    /// </remarks>
    /// <param name="seed">A number to work from.</param>
    /// <returns>The local's value.</returns>
    public int ScopeIsTheWholeBlock(int seed)
    {
        int before = this._depth;

        int _depth = seed + before;

        return _depth;
    }
}
