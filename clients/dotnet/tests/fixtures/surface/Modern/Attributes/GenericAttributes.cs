namespace Surface.Modern.Attributes;

/// <summary>
/// C# 11 — Generic attributes: a generic class deriving from <see cref="Attribute"/>. The
/// type argument is part of the attribute's identity at every use site, which is the whole
/// point of the feature and the thing an index has to keep distinct.
/// </summary>
/// <typeparam name="T">The type the tag is about.</typeparam>
[AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
public sealed class SurfaceTagAttribute<T> : Attribute
{
    /// <summary>Creates a tag.</summary>
    public SurfaceTagAttribute() => Tagged = typeof(T);

    /// <summary>The type argument, reified.</summary>
    public Type Tagged { get; }
}

/// <summary>
/// C# 11 — Generic attributes, used. Two uses of one generic attribute at different type
/// arguments: <c>[SurfaceTag&lt;string&gt;]</c> and <c>[SurfaceTag&lt;int&gt;]</c> are
/// distinct attribute applications of the same declaration.
/// </summary>
[SurfaceTag<string>]
[SurfaceTag<int>]
public sealed class TaggedByGenericAttribute
{
    /// <summary>A member carrying the generic attribute at a constructed type argument.</summary>
    [SurfaceTag<List<int>>]
    public int Count { get; init; }

    /// <summary>A method whose parameter and return value both carry it.</summary>
    [return: SurfaceTag<bool>]
    public bool Holds([SurfaceTag<double>] double value) => value > Count;
}
