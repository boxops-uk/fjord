namespace Arity.Pair;

/// <summary>An outcome with nothing attached — the non-generic half of the pair.</summary>
public class Result
{
    public bool Ok { get; init; }
}

/// <summary>An outcome carrying a value — the same name, one type parameter.</summary>
public class Result<T>
{
    public Result(T value) => Value = value;

    /// <summary>The carried value: a member whose symbol inherits the arity above it.</summary>
    public T Value { get; }

    public bool Ok => true;
}
