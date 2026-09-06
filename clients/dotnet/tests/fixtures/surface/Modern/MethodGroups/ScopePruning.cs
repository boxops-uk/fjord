namespace Surface.Modern.MethodGroups;

/// <summary>The receiver both extension scopes below extend.</summary>
public sealed class Beacon
{
    /// <summary>Whatever was last traced onto this beacon.</summary>
    public string Trace { get; set; } = string.Empty;
}

/// <summary>
/// An extension in the *outer* namespace scope. The use site in
/// <c>Surface.Modern.MethodGroups.Inner</c> can see this method, but only after the inner
/// namespace's own candidates have failed — which is the pruning C# 13 introduced.
/// </summary>
public static class OuterBeaconExtensions
{
    /// <summary>The outer scope's candidate, which takes an <see cref="int"/>.</summary>
    public static void Mark(this Beacon beacon, int value) => beacon.Trace = value.ToString();
}

/// <summary>A type with a static and an instance member of one name.</summary>
public sealed class StaticAndInstance
{
    /// <summary>The static overload.</summary>
    public static void Trace(int value) => Traced = value;

    /// <summary>The instance overload, which a static member access cannot reach.</summary>
    public void Trace(string text) => Traced = text.Length;

    /// <summary>Whatever was traced last.</summary>
    public static int Traced { get; private set; }
}

/// <summary>Two overloads of different arity, for the arity pruning.</summary>
public static class Emitters
{
    /// <summary>The non-generic candidate.</summary>
    public static void Emit(int value) => Emitted = value;

    /// <summary>The generic candidate.</summary>
    /// <typeparam name="TValue">What to emit.</typeparam>
    public static void Emit<TValue>(TValue value) => Emitted = value?.ToString()?.Length ?? 0;

    /// <summary>Whatever was emitted last.</summary>
    public static int Emitted { get; private set; }
}

/// <summary>
/// C# 13 — Method group natural type improvements, the two rules that do not need a second
/// namespace: candidates of the wrong static-ness are removed before the natural type is
/// computed, and so are candidates whose arity does not match the type arguments supplied.
/// Both conversions below are errors in C# 12, where every candidate in the group was
/// considered and a group of two had no natural type.
/// </summary>
public static class ScopePruning
{
    /// <summary>
    /// C# 13 — the static-ness pruning: the static access drops
    /// <see cref="StaticAndInstance.Trace(string)"/>, leaving <c>Action&lt;int&gt;</c>.
    /// </summary>
    public static int ByStaticness()
    {
        var tracer = StaticAndInstance.Trace;
        tracer(7);

        return StaticAndInstance.Traced;
    }

    /// <summary>
    /// C# 13 — the arity pruning: the type argument drops
    /// <see cref="Emitters.Emit(int)"/>, leaving <c>Action&lt;string&gt;</c>.
    /// </summary>
    public static int ByArity()
    {
        var emitter = Emitters.Emit<string>;
        emitter("emitted");

        return Emitters.Emitted;
    }

    /// <summary>
    /// The case the improvements did *not* reach, recorded so the corpus does not imply
    /// otherwise: an instance method group with two overloads across a base and a derived
    /// type still has no natural type, and the conversion needs a target.
    /// </summary>
    public static int Targeted()
    {
        Action<int> targeted = StaticAndInstance.Trace;
        targeted(3);

        return StaticAndInstance.Traced;
    }
}
