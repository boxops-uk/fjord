using System.Collections.Generic;

namespace Surface.Expressions.Operators.Query;

/// <summary>A part, for the queries below to run over.</summary>
public sealed record OpPart(string Name, int Weight, int BinId);

/// <summary>A bin, so the join clauses have a second sequence.</summary>
public sealed record OpBin(int Id, string Label);

/// <summary>
/// The query expressions of 12.22, one method per clause of the translation.
/// </summary>
/// <remarks>
/// <para>Every clause keyword here is a reference to a method in
/// <see cref="OpQueryPattern"/> that the source does not name, and every range variable is
/// a declaration whose only syntax is the identifier after <c>from</c>, <c>let</c>,
/// <c>join</c> or <c>into</c>.</para>
/// <para>No file in this project imports <c>System.Linq</c>, so a query that resolved to
/// <c>Enumerable.Select</c> would not compile. Every reference below therefore lands
/// inside the corpus, where a gate can assert what it landed on.</para>
/// </remarks>
public static class OpQueryExpressions
{
    /// <summary>The parts every query below draws from.</summary>
    public static OpQuerySource<OpPart> Parts { get; } = new(
        new OpPart("bolt", 3, 1),
        new OpPart("nut", 1, 1),
        new OpPart("washer", 1, 2),
        new OpPart("screw", 2, 2));

    /// <summary>The bins the join clauses match against.</summary>
    public static OpQuerySource<OpBin> Bins { get; } = new(new OpBin(1, "fasteners"), new OpBin(2, "flat"));

    /// <summary>
    /// 12.22.1 — the smallest query: one <c>from</c> declaring one range variable, and one
    /// <c>select</c>. The range variable <c>part</c> is a declaration with no type in the
    /// source and no keyword introducing it.
    /// </summary>
    public static OpQuerySource<string> Minimal() =>
        from part in Parts
        select part.Name;

    /// <summary>
    /// 12.22.3.4 — a degenerate query, whose <c>select</c> names the range variable itself.
    /// The translation still emits a <c>Select</c> call, so the query is not the same
    /// expression as its source.
    /// </summary>
    public static OpQuerySource<OpPart> Degenerate() =>
        from part in Parts
        select part;

    /// <summary>12.22.3.5 — a <c>where</c> clause, whose predicate becomes a lambda.</summary>
    public static OpQuerySource<string> Filtered() =>
        from part in Parts
        where part.Weight > 1
        select part.Name;

    /// <summary>
    /// 12.22.3.5 / 12.22.3.8 — a <c>let</c> clause, which introduces a second range
    /// variable. The two are carried together in a transparent identifier: an anonymous
    /// type the source never writes and cannot name.
    /// </summary>
    public static OpQuerySource<string> Let() =>
        from part in Parts
        let doubled = part.Weight * 2
        where doubled > 2
        select $"{part.Name}:{doubled}";

    /// <summary>
    /// 12.22.3.5 / 12.22.3.8 — two <c>from</c> clauses, which become a
    /// <c>SelectMany</c> whose result selector builds the transparent identifier.
    /// </summary>
    public static OpQuerySource<string> TwoFroms() =>
        from part in Parts
        from bin in Bins
        where part.BinId == bin.Id
        select $"{part.Name}@{bin.Label}";

    /// <summary>
    /// 12.22.3.5 — two <c>from</c> clauses followed immediately by a <c>select</c>, which
    /// becomes the two-argument <c>SelectMany</c> and needs no transparent identifier.
    /// </summary>
    public static OpQuerySource<string> TwoFromsThenSelect() =>
        from part in Parts
        from bin in Bins
        select part.Name + bin.Label;

    /// <summary>12.22.3.5 — an <c>orderby</c> clause with two keys and both directions.</summary>
    public static OpQuerySource<string> Ordered() =>
        from part in Parts
        orderby part.Weight descending, part.Name ascending
        select part.Name;

    /// <summary>
    /// 12.22.3.5 — a <c>join</c> clause, whose <c>on ... equals ...</c> becomes the two
    /// key selectors of a <c>Join</c> call.
    /// </summary>
    public static OpQuerySource<string> Joined() =>
        from part in Parts
        join bin in Bins on part.BinId equals bin.Id
        select $"{part.Name}/{bin.Label}";

    /// <summary>
    /// 12.22.3.5 — a <c>join ... into</c> clause, which becomes <c>GroupJoin</c> and
    /// declares a range variable bound to a whole sub-sequence.
    /// </summary>
    public static OpQuerySource<string> GroupJoined() =>
        from bin in Bins
        join part in Parts on bin.Id equals part.BinId into inBin
        select $"{bin.Label}={inBin.Count}";

    /// <summary>
    /// 12.22.3.7 — a <c>group ... by</c> clause with no projection, which becomes the
    /// two-argument <c>GroupBy</c>.
    /// </summary>
    public static OpQuerySource<OpQueryGrouping<int, OpPart>> Grouped() =>
        from part in Parts
        group part by part.BinId;

    /// <summary>
    /// 12.22.3.7 — a <c>group ... by</c> clause with a projection, which becomes the
    /// three-argument <c>GroupBy</c>.
    /// </summary>
    public static OpQuerySource<OpQueryGrouping<int, string>> GroupedWithProjection() =>
        from part in Parts
        group part.Name by part.BinId;

    /// <summary>
    /// 12.22.3.2 — a query continuation. <c>into</c> ends the first query and declares a
    /// new range variable over its result, so the method holds two range-variable scopes
    /// and the second one shadows nothing.
    /// </summary>
    public static OpQuerySource<string> Continuation() =>
        from part in Parts
        group part.Weight by part.BinId into byBin
        where byBin.Count > 1
        orderby byBin.Key
        select $"{byBin.Key}:{byBin.Total()}";

    /// <summary>
    /// 12.22.3.2 — a continuation after a <c>select</c> rather than a <c>group</c>, which
    /// is the other form the clause allows.
    /// </summary>
    public static OpQuerySource<int> SelectContinuation() =>
        from part in Parts
        select part.Weight into weight
        where weight > 1
        select weight * 10;

    /// <summary>
    /// 12.22.3.3 — an explicitly typed range variable, which inserts a <c>Cast</c> call
    /// before the rest of the translation. The type in the <c>from</c> clause is a type
    /// reference sitting inside an expression.
    /// </summary>
    public static OpQuerySource<int> ExplicitRangeVariableType()
    {
        OpQuerySource<object> boxed = new(1, 2, 3);

        return from int number in boxed
               where number > 1
               select number * 2;
    }

    /// <summary>
    /// 12.22.1 — a query whose body is a single expression across several clauses, written
    /// twice in one method with the same range-variable name. Two declarations named
    /// <c>part</c> in one containing member, distinguished only by which query they belong
    /// to.
    /// </summary>
    public static (OpQuerySource<string> Heavy, OpQuerySource<string> Light) RepeatedRangeVariableName()
    {
        var heavy =
            from part in Parts
            where part.Weight > 1
            select part.Name;

        var light =
            from part in Parts
            where part.Weight <= 1
            select part.Name;

        return (heavy, light);
    }

    /// <summary>
    /// 12.22.3.1 — a query nested inside another query's <c>select</c>, so the inner
    /// query's range variable is declared inside a lambda that the outer translation
    /// produced.
    /// </summary>
    public static OpQuerySource<OpQuerySource<string>> Nested() =>
        from bin in Bins
        select from part in Parts
               where part.BinId == bin.Id
               select part.Name;

    /// <summary>
    /// 12.22.2 — the query keywords are contextual: each is a legal identifier outside a
    /// query and a keyword inside one. This method declares a local for every one of them
    /// and then writes a query in the same body, so the same word is a declaration in one
    /// statement and a clause keyword in the next.
    /// </summary>
    /// <remarks>
    /// The ambiguity the clause is about is not resolved everywhere in favour of the
    /// identifier: inside a query, <c>from part in group</c> reads <c>group</c> as the
    /// keyword and will not compile, so a local named <c>group</c> is unreachable from a
    /// <c>from</c> clause even though it is perfectly declarable beside one.
    /// </remarks>
    public static int Ambiguities()
    {
        int from = 1;
        int where = 2;
        int select = 3;
        int let = 4;
        int join = 5;
        int on = 6;
        int equals = 7;
        int into = 8;
        int by = 9;
        int group = 10;
        int orderby = 11;
        int ascending = 12;
        int descending = 13;

        var counted =
            from part in Parts
            where part.Weight >= 1
            select part.Weight;

        int total = from + where + select + let + join + on + equals + into + by + group
            + orderby + ascending + descending;

        foreach (int weight in counted)
        {
            total += weight;
        }

        return total;
    }

    /// <summary>
    /// 12.22.4 — the pattern is structural, so the same query text runs over any type that
    /// provides the methods. This overload of the minimal query runs over a grouping,
    /// which is a different type with the same pattern.
    /// </summary>
    public static IEnumerable<string> PatternIsStructural(OpQueryGrouping<int, OpPart> bucket) =>
        from part in bucket
        orderby part.Name
        select part.Name;
}
