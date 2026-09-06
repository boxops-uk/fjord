// Clause 21.6 — delegate invocation. `d(A)` invokes each method in d's invocation list in
// order with the arguments A, and the value of the expression is the value the last method in
// the list returned; if d is null the invocation throws. The clause defines `d(A)` as an
// invocation of d's `Invoke` method — which means the member being called appears nowhere at
// the use site, and the only identifier present is the delegate variable's own name.

namespace Surface.EnumsDelegates;

/// <summary>21.6 — a static field of delegate type, invoked through a null-conditional below.</summary>
public static class EdInvocationHandlers
{
    /// <summary>21.6 — the field, which is null until something assigns it.</summary>
    public static EdSignal? Ready;

    /// <summary>21.6 — a property of delegate type, so the invocation goes through a getter.</summary>
    public static EdCombine Sum { get; } = static (left, right) => left + right;
}

/// <summary>
/// 21.6 — every form of delegate invocation the clause describes, one method each.
/// </summary>
public static class EdInvocation
{
    /// <summary>
    /// 21.6 hazard — the ordinary invocation. The identifiers here are <c>combine</c> and
    /// nothing else, and the member called is <c>EdCombine.Invoke</c>; an index that
    /// attributes this call to the method the delegate was made from names the wrong
    /// declaration, and one that attributes it to nothing loses the only call there is.
    /// </summary>
    public static int Direct(EdCombine combine) => combine(2, 3);

    /// <summary>
    /// 21.6 hazard — the same call written out. This and <see cref="Direct"/> must resolve to
    /// one member.
    /// </summary>
    public static int ThroughInvoke(EdCombine combine) => combine.Invoke(2, 3);

    /// <summary>21.6 — a null-conditional invocation, which is how an event is raised.</summary>
    public static int? Conditional(EdCombine? combine) => combine?.Invoke(2, 3);

    /// <summary>21.6 — the void-returning form of the same guard.</summary>
    public static void Signal(EdSignal? signal) => signal?.Invoke();

    /// <summary>
    /// 21.6 — a multicast invocation: both methods run, and the value is the last one's. The
    /// combination and the invocation are one expression, so the delegate invoked here is
    /// named by no variable at all.
    /// </summary>
    public static int LastWins(EdCombine first, EdCombine second) => (first + second)(1, 1);

    /// <summary>
    /// 21.6 hazard — the invocation of the result of an invocation. Two `Invoke` members are
    /// called at this site, on two different delegate types, and the source has one argument
    /// list too few to make either name visible.
    /// </summary>
    public static int OfAnInvocationResult(EdChooser chooser) => chooser(true)(4, 5);

    /// <summary>21.6 — invocation with an output argument, which declares a variable.</summary>
    public static bool WithOut(EdTryParse parse, string text) =>
        parse(text, out int value) && value > 0;

    /// <summary>21.6 — invocation with a by-reference argument.</summary>
    public static int WithRef(EdMutate mutate)
    {
        int slot = 1;
        mutate(ref slot);
        return slot;
    }

    /// <summary>21.6 — invocation with a read-only by-reference argument.</summary>
    public static double WithIn(EdScale scale)
    {
        double factor = 2.5;
        return scale(in factor);
    }

    /// <summary>
    /// 21.6 — the expanded and the normal form of a variadic invocation. The first two calls
    /// build an array that no syntax here mentions; the third passes one.
    /// </summary>
    public static void Expanded(EdLogger log)
    {
        log("one {0}", 1);
        log("two {0} {1}", 1, 2);
        log("none");
        log("array", new object?[] { 3 });
    }

    /// <summary>
    /// 21.6 hazard — an invocation that omits an argument, so the value comes from the
    /// *delegate declaration's* default and not from any expression at this site.
    /// </summary>
    public static void Defaulted(EdDefaulted retry)
    {
        retry();
        retry(9);
    }

    /// <summary>
    /// 21.6 — an invocation whose result is a variable reference, forwarded by reference.
    /// </summary>
    public static ref int RefReturn(EdPick pick, int[] slots) => ref pick(slots);

    /// <summary>21.6 — a write through the reference an invocation returned.</summary>
    public static void WriteThrough(EdPick pick, int[] slots)
    {
        pick(slots) = 42;
    }

    /// <summary>21.6 — the reflective invocation, whose arguments are boxed and unchecked.</summary>
    public static object? Dynamically(EdCombine combine) => combine.DynamicInvoke(6, 7);

    /// <summary>21.6 — invocation of a constructed generic delegate.</summary>
    public static string Generic(EdMapper<int, string> map) => map(41);

    /// <summary>21.6 — invocation of a contravariant delegate at a narrower argument type.</summary>
    public static void Narrow(EdSink<object> sink) => sink("boxed");

    /// <summary>21.6 — invocation through a static field, guarded because it may be null.</summary>
    public static void ThroughAField() => EdInvocationHandlers.Ready?.Invoke();

    /// <summary>21.6 — invocation through a property, so the getter runs first.</summary>
    public static int ThroughAProperty() => EdInvocationHandlers.Sum(8, 9);

    /// <summary>
    /// 21.6 — invocation of a delegate held in an array element, where the receiver is an
    /// element access rather than a name.
    /// </summary>
    public static int ThroughAnElement(EdCombine[] combines) => combines[0](10, 11);

    /// <summary>
    /// 21.6 — invocation of a delegate the same expression creates. The lambda is declared
    /// and called in one place, so its only reference is its own declaration.
    /// </summary>
    public static int Immediately() => ((EdCombine)((left, right) => left + right))(12, 13);
}
