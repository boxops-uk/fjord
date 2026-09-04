namespace Surface.Modern.Lambdas;

/// <summary>
/// An attribute this file applies to a lambda, to a lambda parameter and to a lambda's
/// return value. C# 10 — Attributes on lambda expressions needs somewhere to point, and a
/// framework attribute valid in all three positions is scarcer than one declared here.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Parameter | AttributeTargets.ReturnValue)]
public sealed class LambdaNoteAttribute : Attribute
{
    /// <summary>Creates a note.</summary>
    public LambdaNoteAttribute(string note) => Note = note;

    /// <summary>What the note says.</summary>
    public string Note { get; }
}

/// <summary>C# 10 and C# 12 — everything a lambda gained after C# 9.</summary>
public static class LambdaNaturalTypes
{
    /// <summary>
    /// C# 10 — Lambda natural type: the lambda is assigned to <c>var</c>, so the compiler
    /// infers a delegate type (<c>System.Func&lt;int, int&gt;</c>) for it rather than being
    /// told one.
    /// </summary>
    public static readonly object Inferred = Build();

    /// <summary>
    /// C# 10 — Lambda natural type, method group half: a method group converted to
    /// <c>var</c>. The group has exactly one member, which is what gives it a natural type.
    /// </summary>
    public static object InferredFromMethodGroup()
    {
        var describe = DescribeOnly;

        return describe(2);
    }

    /// <summary>
    /// C# 10 — Lambda explicit return type. Inference would give <c>int</c> here, so the
    /// declared <c>short</c> is doing work: without it the lambda has no natural type that
    /// matches, and the conversion is an error.
    /// </summary>
    public static Func<int, short> Narrowing()
    {
        var narrow = short (int value) => (short)value;

        return narrow;
    }

    /// <summary>
    /// C# 10 — Attributes on lambda expressions, in all three positions the feature reaches:
    /// on the lambda itself, on its return value, and on a parameter.
    /// </summary>
    public static Func<int, int> Annotated()
    {
        var annotated = [LambdaNote("lambda")]
            [return: LambdaNote("return")]
            static ([LambdaNote("parameter")] int value) => value + 1;

        return annotated;
    }

    /// <summary>
    /// C# 12 — Optional parameters in lambda expressions. The default value lives on the
    /// lambda's parameter, so the delegate the compiler synthesizes carries it — an ordinary
    /// <c>Func</c> could not.
    /// </summary>
    public static int Stepped()
    {
        var step = (int value, int by = 4) => value + by;

        return step(1) + step(1, 10);
    }

    /// <summary>C# 12 — a lambda whose optional parameter is <c>params</c>'d over.</summary>
    public static int Defaulted()
    {
        var scale = (int value, double factor = 1.5) => (int)(value * factor);

        return scale(4);
    }

    private static Func<int, int> Build()
    {
        var natural = (int value) => value * 2;

        return natural;
    }

    private static string DescribeOnly(int value) => value.ToString();
}
