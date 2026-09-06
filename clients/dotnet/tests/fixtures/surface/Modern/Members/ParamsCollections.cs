namespace Surface.Modern.Members;

/// <summary>
/// C# 13 — params collections: <c>params</c> is no longer restricted to an array. Each
/// declaration below is a <c>params</c> parameter of a different collection type, and the
/// compiler builds the collection at the call site by a different route for each — a stack
/// allocation for the span, a builder for the interface, an initializer for the list.
/// </summary>
public static class ParamsCollections
{
    /// <summary>C# 13 — <c>params ReadOnlySpan&lt;T&gt;</c>, which allocates nothing.</summary>
    public static int Total(params ReadOnlySpan<int> values)
    {
        var total = 0;

        foreach (var value in values)
        {
            total += value;
        }

        return total;
    }

    /// <summary>C# 13 — <c>params Span&lt;T&gt;</c>, the writable span form.</summary>
    public static void Zero(params Span<int> values) => values.Clear();

    /// <summary>C# 13 — <c>params IEnumerable&lt;T&gt;</c>, an interface the compiler builds for.</summary>
    public static string Join(params IEnumerable<string> parts) => string.Join('/', parts);

    /// <summary>C# 13 — <c>params List&lt;T&gt;</c>, a concrete collection type.</summary>
    public static int Count(params List<double> values) => values.Count;

    /// <summary>C# 13 — <c>params IReadOnlyList&lt;T&gt;</c>, beside a leading ordinary parameter.</summary>
    public static string Label(string prefix, params IReadOnlyList<int> values) => $"{prefix}:{values.Count}";

    /// <summary>The pre-C# 13 array form, so the corpus holds both.</summary>
    public static int TotalArray(params int[] values) => Total(values);

    /// <summary>
    /// The call sites, each in the expanded form — which is the form that makes the
    /// parameter's collection type a fact about the *call* rather than the declaration.
    /// </summary>
    public static string All()
    {
        Span<int> scratch = stackalloc int[3];
        Zero(scratch);

        return $"{Total(1, 2, 3)}{Join("a", "b")}{Count(1.5, 2.5)}{Label("n", 1)}{TotalArray(4, 5)}";
    }
}
