namespace Surface.Conversions.UserDefined;

/// <summary>
/// A length in whole millimetres.
///
/// Clause 10.5.2 sets out what a user-defined conversion may say. This type is what is left
/// once those rules are obeyed, and the comments name the four forms that are <em>not</em>
/// here because the compiler refuses them:
///
/// <list type="bullet">
///   <item>to or from <c>object</c> — CS0553 and CS0554;</item>
///   <item>to or from an interface type — CS0552;</item>
///   <item>to or from a base or derived class of the enclosing type — CS0553;</item>
///   <item>a second operator with the same source and target, whichever way round — CS0557.</item>
/// </list>
///
/// The deliberate hazard is the <c>checked</c> pair. A checked user-defined operator is only
/// legal beside a matching unchecked one, so the two declarations agree in name, in parameter
/// list <em>and</em> in return type; the only difference between them is a keyword.
/// </summary>
public readonly struct ConvLength
{
    /// <summary>A length of <paramref name="millimetres"/> millimetres.</summary>
    public ConvLength(long millimetres) => Millimetres = millimetres;

    /// <summary>How long it is.</summary>
    public long Millimetres { get; }

    /// <summary>Clause 10.5.2 — the unchecked half of the pair, which wraps.</summary>
    public static explicit operator int(ConvLength length) => (int)length.Millimetres;

    /// <summary>Clause 10.5.2 — the checked half, which throws. Same signature, same return type.</summary>
    public static explicit operator checked int(ConvLength length) =>
        checked((int)length.Millimetres);

    /// <summary>Clause 10.5.2 — a widening conversion out, declared implicit because it cannot fail.</summary>
    public static implicit operator long(ConvLength length) => length.Millimetres;

    /// <summary>Clause 10.5.2 — and one in, so the type is reachable without a cast.</summary>
    public static implicit operator ConvLength(long millimetres) => new(millimetres);
}

/// <summary>
/// A one-value wrapper whose conversions mention its own type parameter, which clause 10.5.2
/// permits and which the framework's own <c>Nullable&lt;T&gt;</c> relies on.
/// </summary>
/// <typeparam name="T">What is wrapped.</typeparam>
public readonly struct ConvBoxed<T>
{
    /// <summary>Wraps <paramref name="value"/>.</summary>
    public ConvBoxed(T value) => Value = value;

    /// <summary>What was wrapped.</summary>
    public T Value { get; }

    /// <summary>Clause 10.5.2 — in from the type parameter.</summary>
    public static implicit operator ConvBoxed<T>(T value) => new(value);

    /// <summary>Clause 10.5.2 — out to the type parameter, which has to be explicit.</summary>
    public static explicit operator T(ConvBoxed<T> boxed) => boxed.Value;
}
