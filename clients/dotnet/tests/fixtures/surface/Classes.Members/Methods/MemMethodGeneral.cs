// Clause 15.6.1 (methods — general): a method declaration is a name, an optional type
// parameter list, a parameter list, a return type and a body. Clause 15.6.3 (static and
// instance methods): `static` decides whether the method is entered with a `this`.
//
// The hazard on both rows is the same one: every method here is a member of one type, and an
// index that keys a method by (containing type, name) alone cannot separate `Describe` from
// `Describe<TValue>`, nor the three `Measure` overloads from each other. The full overload
// matrix is `MemOverloads.cs`; this file holds the plain declarations, one of each shape.

namespace Surface.Classes.Members.Methods;

/// <summary>15.6.1: one type holding every shape of method declaration that is neither a
/// modifier variation (see <see cref="MemModifierRoot"/>) nor a parameter variation (see
/// <see cref="MemParameters"/>).</summary>
public class MemMethodGeneral
{
    private readonly int[] _slots = new int[4];
    private int _calls;

    /// <summary>15.6.1: a void method with a block body.</summary>
    public void Reset()
    {
        _calls = 0;
    }

    /// <summary>15.6.1: a value-returning method with an expression body.</summary>
    public int Calls() => _calls;

    /// <summary>15.6.3: an instance method — it reads and writes <c>this</c>.</summary>
    public int Bump() => ++_calls;

    /// <summary>15.6.3: a static method, entered with no instance. Same type, same
    /// declaration space, and nothing but the modifier separates its identity from
    /// <see cref="Bump"/>'s.</summary>
    public static int Zero() => 0;

    /// <summary>15.6.1: a generic method with one type parameter and a constraint. The
    /// constraint is a keyword rather than a type, so it is the one name in the signature
    /// with nothing to resolve to.</summary>
    public string Describe<TValue>(TValue value)
        where TValue : notnull
        => value.ToString() ?? string.Empty;

    /// <summary>15.6.1: the arity-zero sibling of <see cref="Describe{TValue}"/> — same name,
    /// no parameters, and no type parameters either.</summary>
    public string Describe() => $"{_calls} call(s)";

    /// <summary>15.6.1: two type parameters, one constrained by the other.</summary>
    public TSecond? Convert<TFirst, TSecond>(TFirst value)
        where TFirst : notnull
        where TSecond : class
        => value as TSecond;

    /// <summary>15.6.1: a tuple return type, whose element names are part of the declaration
    /// and not of the underlying type.</summary>
    public (int Calls, bool Fresh) Split() => (_calls, _calls == 0);

    /// <summary>15.6.1: optional parameters. Which parameters have defaults is not part of the
    /// signature (7.6), so this is one member and not three.</summary>
    public int Measure(int width, int height = 1, string unit = "px") => (width * height) + unit.Length;

    /// <summary>15.6.1: an overload differing in parameter type alone.</summary>
    public int Measure(double width) => (int)width;

    /// <summary>15.6.1: an overload differing in parameter count alone.</summary>
    public int Measure(int width, int height, int depth) => width * height * depth;

    /// <summary>15.6.1: a method returning a reference, which is part of the return type and
    /// not of the signature.</summary>
    public ref int Slot(int index) => ref _slots[index];

    /// <summary>15.6.1: a method returning a readonly reference to the same storage.</summary>
    public ref readonly int FrozenSlot(int index) => ref _slots[index];

    /// <summary>15.6.1: a reference that reaches every declaration above, so none of them is
    /// declared and never reached. Named arguments and a `ref` call site included.</summary>
    public string UseAll()
    {
        Reset();
        Bump();
        Slot(0) = Zero();
        _ = FrozenSlot(index: 0);
        _ = Convert<string, string>("x");
        var (calls, fresh) = Split();
        return $"{Describe()} {Describe(calls)} {fresh} {Measure(2)} {Measure(2.5)} {Measure(1, 2, 3)} {Calls()}";
    }
}
