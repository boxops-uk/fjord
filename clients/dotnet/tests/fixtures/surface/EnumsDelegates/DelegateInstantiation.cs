// Clause 21.5 — delegate instantiation. A delegate-creation-expression `new D(E)` takes a
// method group, an anonymous function, or a value of a delegate type; the same three sources
// convert implicitly without `new`, which is how delegates are written in practice. Combining
// two instances with `+` produces a third whose invocation list is the concatenation, and
// `-` produces one with a sublist removed — or null, when nothing is left.

namespace Surface.EnumsDelegates;

/// <summary>21.5 — the methods and method groups the instantiations below target.</summary>
public sealed class EdCalculator
{
    private readonly int _bias;

    /// <summary>21.5 — the state an instance target carries with it.</summary>
    public EdCalculator(int bias) => _bias = bias;

    /// <summary>21.5 — an instance method, so a delegate over it captures this object.</summary>
    public int AddBiased(int left, int right) => left + right + _bias;

    /// <summary>21.5 — a static method with the signature of <see cref="EdCombine"/>.</summary>
    public static int Add(int left, int right) => left + right;

    /// <summary>21.5 — the first of three overloads, and the only compatible one.</summary>
    public static void Print(string text)
    {
    }

    /// <summary>21.5 — an overload the conversion must reject on its parameter type.</summary>
    public static void Print(int number)
    {
    }

    /// <summary>21.5 — an overload the conversion must reject on its parameter count.</summary>
    public static void Print(string text, int number)
    {
    }

    /// <summary>21.5 — a generic method, whose type argument the conversion infers.</summary>
    public static string Describe<T>(T value) => value?.ToString() ?? "none";

    /// <summary>21.5 — a method with no overloads, so a method group of it has a natural type.</summary>
    public static void Solo(string text)
    {
    }
}

/// <summary>
/// 21.5 — every source a delegate instance can be made from, one method each, and the two
/// operators that combine instances.
/// </summary>
public static class EdInstantiation
{
    /// <summary>21.5 — a delegate-creation expression over a static method group.</summary>
    public static EdCombine FromMethodGroupWithNew() => new EdCombine(EdCalculator.Add);

    /// <summary>21.5 — the same conversion without `new`, which is the implicit form.</summary>
    public static EdCombine FromMethodGroup() => EdCalculator.Add;

    /// <summary>
    /// 21.5 — an instance method group. The delegate captures <paramref name="calculator"/>
    /// as its target, and the reference at this site is to the method, not to the object.
    /// </summary>
    public static EdCombine FromInstanceMethodGroup(EdCalculator calculator) =>
        calculator.AddBiased;

    /// <summary>
    /// 21.5 hazard — an *overloaded* method group. Three declarations are named by the single
    /// identifier <c>Print</c> and the conversion resolves to exactly one of them, so a
    /// reference recorded against the group rather than the resolution names two methods too
    /// many.
    /// </summary>
    public static EdNotifyLeft FromOverloadedGroup() => EdCalculator.Print;

    /// <summary>21.5 — a lambda with inferred parameter types and an expression body.</summary>
    public static EdCombine FromLambda() => (left, right) => left + right;

    /// <summary>21.5 — a lambda with explicit parameter types and a block body.</summary>
    public static EdCombine FromTypedLambda() => (int left, int right) => { return left * right; };

    /// <summary>21.5 — a lambda declared `static`, so it can capture nothing.</summary>
    public static EdCombine FromStaticLambda() => static (left, right) => left - right;

    /// <summary>21.5 — an anonymous method, the older spelling with an explicit parameter list.</summary>
    public static EdCombine FromAnonymousMethod() =>
        delegate (int left, int right) { return left + right; };

    /// <summary>
    /// 21.5 hazard — an anonymous method with *no* parameter list, which is compatible with
    /// any delegate whose parameters are all by-value. The method it declares has one
    /// parameter, and no token here says so.
    /// </summary>
    public static EdNotifyLeft FromParameterlessAnonymousMethod() => delegate { };

    /// <summary>
    /// 21.5 hazard — instantiation from another delegate. The argument is a value, not a
    /// method group, and the result's target is the *target of that value*, so the edge from
    /// this site leads to a method no identifier here names.
    /// </summary>
    public static EdNotifyRight FromAnotherDelegate(EdNotifyLeft left) => new EdNotifyRight(left);

    /// <summary>21.5 — a local function group, whose declaration is a statement.</summary>
    public static EdSignal FromLocalFunction()
    {
        void Tick()
        {
        }

        return Tick;
    }

    /// <summary>
    /// 21.5 hazard — a generic method group whose type argument the delegate type supplies.
    /// <c>Describe</c> is referenced here as <c>Describe&lt;int&gt;</c> with neither the angle
    /// brackets nor the `int` written down.
    /// </summary>
    public static EdMapper<int, string> FromGenericMethodGroup() => EdCalculator.Describe;

    /// <summary>
    /// 21.5 hazard — the natural type of a method group. The local's type is
    /// <c>System.Action&lt;string&gt;</c>, a framework delegate type that no token in this
    /// project names.
    /// </summary>
    public static object FromNaturalType()
    {
        var handler = EdCalculator.Solo;
        return handler;
    }

    /// <summary>21.5 — combination with the binary operator.</summary>
    public static EdSignal Sum(EdSignal first, EdSignal second) => first + second;

    /// <summary>21.5 — removal with the binary operator, which can produce null.</summary>
    public static EdSignal? Difference(EdSignal first, EdSignal second) => first - second;

    /// <summary>
    /// 21.5 — combination and removal by compound assignment, which is how a multicast list
    /// is built in practice. Four operator uses, none of which has a declaration to bind to
    /// beyond the language's own definition of `+` on delegate types. Removal warns
    /// (CS8601, CS8603) because the clause lets it produce null when the list empties, which
    /// is the observable effect of `-` being `Delegate.Remove` and not the inverse of `+`.
    /// </summary>
    public static EdSignal Multicast(EdSignal first, EdSignal second)
    {
        EdSignal chain = first;
        chain += second;
        chain += first;
        chain -= second;
        return chain;
    }

    /// <summary>
    /// 21.5 hazard — two lambdas in one method, each declaring a parameter named `n`. The
    /// two synthesized methods differ only by an ordinal the source does not contain, and
    /// the two parameters are distinct declarations of one name in one member.
    /// </summary>
    public static EdSink<int> TwoLambdasOneParameterName()
    {
        EdSink<int> first = n => { };
        EdSink<int> second = n => { };
        return first + second;
    }

    /// <summary>
    /// 21.5 — a lambda that captures a local, so the compiler makes a closure class whose
    /// field is named for the captured variable and whose method is named for nothing.
    /// </summary>
    public static EdCombine Capturing(int offset) => (left, right) => left + right + offset;

    /// <summary>21.5 — a delegate returning a delegate, instantiated from a lambda.</summary>
    public static EdChooser Chooser() =>
        preferSum => preferSum ? EdCalculator.Add : static (left, right) => left * right;

    /// <summary>
    /// 21.5 — a generic delegate instantiated from a lambda rather than a group. The type
    /// argument has to satisfy the declaration's `new()` constraint, so it is `object` and
    /// not <see cref="EdCalculator"/>, which has no parameterless constructor.
    /// </summary>
    public static EdFactory<object> Factory() => static () => new object();

    /// <summary>21.5 — a variadic delegate instantiated from a matching lambda.</summary>
    public static EdLogger Logger() => static (format, args) => { };

    /// <summary>21.5 — a by-reference delegate instantiated from a lambda with the modifier.</summary>
    public static EdMutate Mutator() => static (ref int slot) => slot += 2;

    /// <summary>21.5 — an out-parameter delegate instantiated from a lambda.</summary>
    public static EdTryParse Parser() => static (string text, out int value) =>
        int.TryParse(text, out value);
}
