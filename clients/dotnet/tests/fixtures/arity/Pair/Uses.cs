namespace Arity.Pair;

/// <summary>A plain method beside its generic overload, and nothing else.</summary>
public static class Overloads
{
    public static void M()
    {
    }

    public static void M<T>()
    {
    }
}

/// <summary>Both arities of both names referenced, so each mints a use as well.</summary>
public static class Uses
{
    public static bool Plain(Result result) => result.Ok;

    public static int Wrapped(Result<int> result) => result.Value;

    public static void Called()
    {
        Overloads.M();
        Overloads.M<int>();
    }
}
