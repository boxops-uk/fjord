// Clause 7.4.8 (delegate members): a delegate type's only members are the ones it inherits
// from `System.Delegate` and the `Invoke` method the declaration itself produces. `Invoke`
// has no declaration in the source — the delegate's parameter list *is* its declaration —
// which makes it the one member here whose reference has no matching definition site.
//
// The hazard is that a delegate is called two ways: `handler(3)` and `handler.Invoke(3)`
// are the same call, and only one of them writes the member's name.

namespace Surface.Lexical.Members;

/// <summary>7.4.8: a delegate declaration, whose parameter list becomes <c>Invoke</c>.</summary>
/// <param name="code">What the target is passed.</param>
/// <returns>What the target returns.</returns>
public delegate int LexSignal(int code);

/// <summary>7.4.8: a second delegate with the identical signature, which is a distinct
/// type — delegates are named, not structural, so no conversion joins the two.</summary>
/// <param name="code">What the target is passed.</param>
/// <returns>What the target returns.</returns>
public delegate int LexSignalTwin(int code);

/// <summary>7.4.8: a generic delegate, so the corpus holds one with a type parameter.</summary>
/// <typeparam name="TCode">What the target is passed.</typeparam>
/// <param name="code">The argument.</param>
/// <returns>Whatever the target returns.</returns>
public delegate int LexTypedSignal<TCode>(TCode code);

/// <summary>7.4.8: references to a delegate type's members.</summary>
public sealed class LexDelegateMembers
{
    /// <summary>A field of delegate type, so the type is instantiated as well as declared.</summary>
    private readonly LexSignal _handler = code => code + 1;

    /// <summary>Calls it without naming <c>Invoke</c>, which is the usual spelling.</summary>
    public int ImplicitInvoke() => _handler(3);

    /// <summary>Calls it by naming <c>Invoke</c>, which is the same member and same call.</summary>
    public int ExplicitInvoke() => _handler.Invoke(3);

    /// <summary>Reads <c>Method</c>, inherited from <c>System.Delegate</c>.</summary>
    public string? TargetMethodName() => _handler.Method.Name;

    /// <summary>Reads <c>Target</c>, likewise inherited.</summary>
    public object? TargetObject() => _handler.Target;

    /// <summary>Reads <c>DynamicInvoke</c>, which reaches <c>Invoke</c> by reflection.</summary>
    public object? Dynamically() => _handler.DynamicInvoke(3);

    /// <summary>
    /// Combines two delegates through the <c>+</c> that <c>System.Delegate</c> declares,
    /// which is an operator reference and not an operator declaration in this corpus.
    /// </summary>
    public int Combined()
    {
        LexSignal chained = _handler;
        chained += code => code + 2;
        return chained(3) + (chained - _handler)!.Invoke(3);
    }

    /// <summary>Instantiates the twin type and the generic one, so both are reached.</summary>
    public int Others()
    {
        LexSignalTwin twin = code => code + 4;
        LexTypedSignal<string> typed = code => code.Length;
        return twin.Invoke(1) + typed("abcde");
    }

    /// <summary>Constructs a delegate from a method group rather than a lambda.</summary>
    public int FromMethodGroup()
    {
        LexSignal fromGroup = Increment;
        return fromGroup.Invoke(3);
    }

    /// <summary>The target of the method-group conversion above.</summary>
    private static int Increment(int code) => code + 1;
}
