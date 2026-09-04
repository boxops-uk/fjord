using System;
using System.Collections.Generic;
using System.Globalization;

namespace Surface.Expressions.Primary;

/// <summary>
/// 12.6.1 — the categories of function member, each invoked once: a method, a property
/// accessor, an indexer accessor, an event accessor, a constructor, an operator, and a
/// delegate's <c>Invoke</c>. A finalizer is the one category no expression can invoke, which is
/// why none is declared here.
/// </summary>
public sealed class PxFunctionMembers
{
    private int _stored;

    /// <summary>Builds one; the constructor is a function member invoked by <c>new</c>.</summary>
    public PxFunctionMembers(int stored) => _stored = stored;

    /// <summary>A property whose two accessors are separate function members.</summary>
    public int Stored
    {
        get => _stored;
        set => _stored = value;
    }

    /// <summary>An event whose add and remove accessors are function members.</summary>
    public event PxNotice? Changed;

    /// <summary>The one indexer, whose get accessor is a function member.</summary>
    public int this[int scale] => _stored * scale;

    /// <summary>A method.</summary>
    public int Method(int by) => _stored + by;

    /// <summary>An operator, which is a function member invoked without a name node.</summary>
    public static PxFunctionMembers operator +(PxFunctionMembers left, PxFunctionMembers right)
        => new(left._stored + right._stored);

    /// <summary>Invokes one of each category, in this file, on declarations in this file.</summary>
    public string InvokeEachCategory()
    {
        var constructed = new PxFunctionMembers(2);   // constructor
        constructed.Stored = 5;                       // set accessor
        var read = constructed.Stored;                // get accessor
        var element = constructed[3];                 // indexer get accessor
        var method = constructed.Method(1);           // method
        constructed.Changed += Note;                  // add accessor
        constructed.Changed -= Note;                  // remove accessor
        var summed = (constructed + this).Stored;     // operator
        PxTransform through = value => value;
        var delegated = through.Invoke(read);         // a delegate's Invoke
        return $"{element} {method} {summed} {delegated}";
    }

    private static void Note(string message) => GC.KeepAlive(message);
}

/// <summary>
/// 12.6.2 — argument lists. Every parameter-passing mode and every argument form the grammar
/// allows, and the calls that fill them, in one place.
/// </summary>
public static class PxArgumentLists
{
    /// <summary>12.6.2.1 — an <c>out</c> parameter, so an argument can *declare* a variable.</summary>
    public static bool TryRead(string text, out int value)
        => int.TryParse(text, CultureInfo.InvariantCulture, out value);

    /// <summary>12.6.2.1 — two <c>ref</c> parameters.</summary>
    public static void Swap(ref int left, ref int right) => (left, right) = (right, left);

    /// <summary>12.6.2.1 — an <c>in</c> parameter, passed by readonly reference.</summary>
    public static int MeasureIn(in PxPoint point) => point.Measure();

    /// <summary>12.6.2.1 — a <c>ref readonly</c> parameter, which is neither <c>in</c> nor <c>ref</c>.</summary>
    public static int Peek(ref readonly int value) => value;

    /// <summary>12.6.2.1 — a <c>params</c> array, expanded at the call site.</summary>
    public static int Total(params int[] values)
    {
        var total = 0;

        foreach (var value in values)
        {
            total += value;
        }

        return total;
    }

    /// <summary>12.6.2.1 — a <c>params</c> span: the same clause, in its post-standard spelling.</summary>
    public static int TotalSpan(params ReadOnlySpan<int> values)
    {
        var total = 0;

        foreach (var value in values)
        {
            total += value;
        }

        return total;
    }

    /// <summary>12.6.2.2 — three parameters with defaults, so named arguments have names to match.</summary>
    public static string Describe(string text, int width = 8, char pad = '.')
        => text.PadRight(width, pad);

    /// <summary>
    /// 12.6.2.1 and 12.6.2.2 — one call per argument form: positional, named, named out of
    /// order, omitted-optional, <c>out var</c>, <c>ref</c>, <c>in</c>, and both <c>params</c>
    /// expansions plus the un-expanded array form.
    /// </summary>
    public static string EveryArgumentForm()
    {
        // 12.6.2.1 — an out argument that declares its own variable, mid-expression.
        var parsed = TryRead("41", out var value);

        // 12.6.2.1 — ref arguments, which require the variables to exist first.
        var left = 1;
        var right = 2;
        Swap(ref left, ref right);

        // 12.6.2.1 — an in argument, once by keyword and once by plain value.
        var point = new PxPoint(2, 3);
        var byKeyword = MeasureIn(in point);
        var byValue = MeasureIn(point);

        // 12.6.2.1 — a ref readonly argument, which accepts `in` at the call site.
        var peeked = Peek(in left);

        // 12.6.2.1 — params, expanded; params, given an array directly; params, empty.
        var expanded = Total(1, 2, 3);
        var asArray = Total([4, 5]);
        var empty = Total();
        var spanned = TotalSpan(6, 7);

        // 12.6.2.2 — named arguments, out of declaration order, with one optional omitted.
        var named = Describe(width: 4, text: "ab");
        var positional = Describe("cd", 5, '-');

        return $"{parsed} {value} {left} {right} {byKeyword} {byValue} {peeked} {expanded} {asArray} {empty} {spanned} {named} {positional}";
    }

    /// <summary>
    /// 12.6.2.3 — run-time evaluation of an argument list: the arguments are evaluated left to
    /// right, and each has a side effect that says so.
    /// </summary>
    public static string EvaluationOrder()
    {
        var order = new List<string>();
        var first = Note(order, "first");
        var seen = Describe(Note(order, "text"), Note(order, 3), '#');
        return $"{first} {seen} {string.Join(",", order)}";
    }

    private static string Note(List<string> order, string label)
    {
        order.Add(label);
        return label;
    }

    private static int Note(List<string> order, int width)
    {
        order.Add(width.ToString(CultureInfo.InvariantCulture));
        return width;
    }
}

/// <summary>
/// 12.6.3 — type inference. The declarations here are generic; the calls below leave every
/// type argument to be inferred, except where inference is deliberately overridden.
/// </summary>
public static class PxInference
{
    /// <summary>12.6.3.1 — one type parameter, inferred from one argument.</summary>
    /// <typeparam name="T">Inferred from <paramref name="value" />.</typeparam>
    public static T Identity<T>(T value) => value;

    /// <summary>12.6.3.8 — output type inference: <c>TOut</c> comes from the lambda's return type.</summary>
    /// <typeparam name="TIn">Inferred from the source.</typeparam>
    /// <typeparam name="TOut">Inferred from the projection's inferred return type.</typeparam>
    public static List<TOut> Project<TIn, TOut>(IEnumerable<TIn> source, Func<TIn, TOut> project)
    {
        var mapped = new List<TOut>();

        foreach (var item in source)
        {
            mapped.Add(project(item));
        }

        return mapped;
    }

    /// <summary>12.6.3.15 — a generic method that a method group conversion must instantiate.</summary>
    /// <typeparam name="T">Inferred from the delegate type being converted to.</typeparam>
    public static T Echo<T>(T value) => value;

    /// <summary>12.6.3.1 — inference with a constraint, so the inferred argument must satisfy it.</summary>
    /// <typeparam name="T">Inferred, and required to be measurable.</typeparam>
    public static int Sum<T>(T first, T second)
        where T : IPxMeasured => first.Measure() + second.Measure();

    /// <summary>Every inference form, called in the file that declares the generic methods.</summary>
    public static string EveryInferenceForm()
    {
        // 12.6.3.1 — inferred from the argument.
        var inferred = Identity(42);

        // 12.6.3.1 — inference overridden by explicit type arguments.
        var explicitly = Identity<long>(42);

        // 12.6.3.8 — TOut inferred from the lambda's inferred return type (12.6.3.14).
        var projected = Project(new[] { 1, 2, 3 }, number => number.ToString(CultureInfo.InvariantCulture));

        // 12.6.3.9 — explicit parameter types on the lambda, so inference runs the other way.
        var explicitParameters = Project(new[] { 1, 2 }, (int number) => number * 1.5);

        // 12.6.3.14 — a lambda with a natural type of its own, and an inferred return type.
        var natural = (int number) => number * 2;
        var naturalVoid = (string message) => Console.Out.Write(message);

        // 12.6.3.15 — a method group converted to a delegate type, instantiating Echo<int>.
        Func<int, int> fromMethodGroup = Echo;
        PxTransform fromMethodGroupAgain = Echo<int>;

        // 12.6.3.1 with a constraint.
        var constrained = Sum(new PxPoint(1, 2), new PxPoint(3, 4));

        // 12.6.3.16 — best common type of a set of expressions, for an implicitly typed array
        // and for a conditional.
        var bestCommon = new[] { new PxDerivedCounter(), new PxDerivedCounter() };
        var bestCommonWidened = new[] { 1, 2L, 3L };
        object bestCommonObject = inferred > 0 ? "text" : (object)1;

        return $"{explicitly} {projected.Count} {explicitParameters.Count} {natural(1)} {fromMethodGroup(2)} {fromMethodGroupAgain(3)} {constrained} {bestCommon.Length} {bestCommonWidened.Length} {bestCommonObject} {naturalVoid}";
    }
}

/// <summary>
/// 12.6.4 — overload resolution. Each pair of declarations below isolates one step of the
/// algorithm, and each call names the step it is decided by.
/// </summary>
public static class PxOverloadArena
{
    /// <summary>12.6.4.2 — applicable in its normal form for an <c>int</c>.</summary>
    public static string Applicable(int value) => $"int {value}";

    /// <summary>12.6.4.2 — applicable only in its expanded form.</summary>
    public static string Applicable(params string[] values) => $"params {values.Length}";

    /// <summary>12.6.4.2 — never applicable to an <c>int</c>: no conversion exists.</summary>
    public static string Applicable(PxPoint point) => $"point {point}";

    /// <summary>12.6.4.3 — the better candidate for an <c>int</c> argument.</summary>
    public static string Better(int value) => $"int {value}";

    /// <summary>12.6.4.3 — the worse candidate for an <c>int</c>, and the only one for a <c>long</c>.</summary>
    public static string Better(long value) => $"long {value}";

    /// <summary>12.6.4.4 — by value: the better parameter-passing mode when both are applicable.</summary>
    public static string Mode(int value) => $"value {value}";

    /// <summary>12.6.4.4 — by readonly reference: applicable, and the worse mode.</summary>
    public static string Mode(in int value) => $"in {value}";

    /// <summary>12.6.4.5 — the better conversion from a <c>short</c> expression.</summary>
    public static string FromExpression(int value) => $"int {value}";

    /// <summary>12.6.4.5 — the worse conversion from a <c>short</c> expression.</summary>
    public static string FromExpression(double value) => $"double {value}";

    /// <summary>12.6.4.6 — exactly matching for <c>number =&gt; number</c>.</summary>
    public static string Exact(Func<int, int> project) => $"int-returning {project(1)}";

    /// <summary>12.6.4.6 — applicable for the same lambda, but not exactly matching.</summary>
    public static string Exact(Func<int, double> project) => $"double-returning {project(1)}";

    /// <summary>12.6.4.7 — the better conversion target: <c>int</c> converts to <c>long</c>.</summary>
    public static string Target(int value) => $"int {value}";

    /// <summary>12.6.4.7 — the worse conversion target.</summary>
    public static string Target(long value) => $"long {value}";

    /// <summary>12.6.4.1 — a constructor overload set, one of the four contexts of overload resolution.</summary>
    public sealed class Constructed
    {
        /// <summary>Chosen for an <c>int</c> argument.</summary>
        public Constructed(int value) => Kind = $"int {value}";

        /// <summary>Chosen for a <c>string</c> argument.</summary>
        public Constructed(string value) => Kind = $"string {value}";

        /// <summary>Chosen when no argument is given.</summary>
        public Constructed() => Kind = "none";

        /// <summary>Which constructor ran.</summary>
        public string Kind { get; }
    }

    /// <summary>
    /// One call per step. The interesting fact is not that these compile but which declaration
    /// each one points at.
    /// </summary>
    public static string EveryStep()
    {
        short narrow = 1;
        var value = 2;

        var applicableNormal = Applicable(1);
        var applicableExpanded = Applicable("a", "b");
        var better = Better(1);
        var betterLong = Better(1L);
        var mode = Mode(value);
        var fromExpression = FromExpression(narrow);
        var exact = Exact(number => number);
        var target = Target(1);
        var constructor = new Constructed(1).Kind + new Constructed("x").Kind + new Constructed().Kind;

        return $"{applicableNormal} {applicableExpanded} {better} {betterLong} {mode} {fromExpression} {exact} {target} {constructor}";
    }
}

/// <summary>
/// 12.6.6 — function member invocation at run time: virtual dispatch, interface mapping, a
/// default interface member, and invocations on boxed instances.
/// </summary>
public static class PxRuntimeInvocation
{
    /// <summary>An interface with a body — the mapping target when a type does not reimplement it.</summary>
    public interface IPxNamed
    {
        /// <summary>The name, defaulted here.</summary>
        string Name => "unnamed";

        /// <summary>An abstract member, so the mapping has something it must fill.</summary>
        int Rank();
    }

    /// <summary>Takes the default member, and implements the abstract one.</summary>
    public sealed class PxDefaulted : IPxNamed
    {
        /// <inheritdoc />
        public int Rank() => 1;
    }

    /// <summary>Reimplements both, so dispatch must find these and not the defaults.</summary>
    public sealed class PxOverriding : IPxNamed
    {
        /// <inheritdoc />
        public string Name => "named";

        /// <inheritdoc />
        public int Rank() => 2;
    }

    /// <summary>
    /// 12.6.6.1 — the invocation is written once and dispatched three ways: to an override, to
    /// an interface mapping, and to a default interface member reachable only through the
    /// interface.
    /// </summary>
    public static string VirtualDispatch()
    {
        PxBaseCounter asBase = new PxDerivedCounter();
        var throughBase = asBase.Count();                 // the override runs

        IPxNamed defaulted = new PxDefaulted();
        var fromDefault = defaulted.Name;                  // the interface's own body runs

        IPxNamed overriding = new PxOverriding();
        var fromClass = overriding.Name;                   // the class's member runs

        IPxMeasured mapped = new PxExplicitMeasured();
        var explicitly = mapped.Measure();                 // an explicit implementation runs

        return $"{throughBase} {fromDefault} {fromClass} {explicitly} {defaulted.Rank()}";
    }

    /// <summary>
    /// 12.6.6.2 — invocations on boxed instances. Each call here goes through a box, and the
    /// member it reaches is declared on the struct, on the interface, or on <c>object</c>.
    /// </summary>
    public static string BoxedInvocation()
    {
        var point = new PxPoint(2, 3);

        // Boxed to an interface: the call is virtual through the box.
        IPxMeasured boxed = point;
        var throughInterface = boxed.Measure();

        // Boxed to object: ToString is overridden on the struct, so the override runs.
        object asObject = point;
        var throughObject = asObject.ToString();

        // A boxed enum, whose ToString is not overridden by the enum itself.
        object hue = PxHue.Cool;
        var enumText = hue.ToString();

        // A constrained call that does *not* box, written the same way as one that does.
        var unboxed = point.Measure();

        return $"{throughInterface} {throughObject} {enumText} {unboxed} {boxed.Weight}";
    }
}

/// <summary>
/// 12.8.10 — invocation expressions: the primary expression must be a method group or a value
/// of a delegate type, and this file has one of each.
/// </summary>
public static class PxInvocationExpressions
{
    /// <summary>A static method, so an invocation can name a method group with no receiver.</summary>
    public static int Twice(int value) => value * 2;

    /// <summary>A field of delegate type, so an invocation can name a value instead.</summary>
    public static readonly PxTransform Thrice = value => value * 3;

    /// <summary>12.8.10.1 — a method group invocation and a delegate value invocation, side by side.</summary>
    public static string GroupOrValue()
    {
        var fromGroup = Twice(1);                        // primary expression is a method group
        var fromValue = Thrice(1);                       // primary expression is a delegate value
        var fromValueExplicitly = Thrice.Invoke(1);      // the same call, spelling Invoke
        return $"{fromGroup} {fromValue} {fromValueExplicitly}";
    }

    /// <summary>12.8.10.2 — method invocations: instance, static, generic, explicit type argument, nested.</summary>
    public static string MethodInvocations()
    {
        var target = new PxTarget(1);
        var instance = target.Compute(2);
        var statically = PxTarget.Of(3).Measure();
        var generic = target.Lookup<string>();
        var arityZero = target.Lookup();
        var nested = Twice(Twice(1));
        var throughLocal = LocalFunction(2);

        // A local function invocation — the post-standard member kind an invocation can name.
        static int LocalFunction(int value) => value + 1;

        return $"{instance} {statically} {generic} {arityZero} {nested} {throughLocal}";
    }

    /// <summary>
    /// 12.8.10.3 — extension method invocations, in all three spellings: reduced form, static
    /// form, and an extension-block member.
    /// </summary>
    public static string ExtensionInvocations()
    {
        var target = new PxTarget(2);

        var reduced = target.Doubled();                     // receiver becomes the first parameter
        var asStatic = PxExtensions.Doubled(target);        // the same declaration, named directly
        var onPredefined = 4.Padded(by: 1);                 // extension on a predefined type
        var inferred = new List<int> { 1, 2 }.Tally();      // generic extension, T inferred
        var block = new PxPoint(1, 1).Scaled(3);            // C# 14 extension block member

        return $"{reduced} {asStatic} {onPredefined} {inferred} {block}";
    }

    /// <summary>
    /// 12.8.10.4 — delegate invocations: a lambda's delegate, a method group's delegate, a
    /// multicast delegate, and a delegate reached through a property.
    /// </summary>
    public static string DelegateInvocations()
    {
        PxTransform single = value => value + 1;
        PxTransform fromGroup = Twice;
        Action<string> multicast = Console.Out.Write;
        multicast += message => Console.Out.Write(message.Length);

        var holder = new PxDelegateHolder { Transform = single };

        var a = single(1);
        var b = fromGroup(2);
        var c = holder.Transform(3);
        var d = holder.Transform.Invoke(4);
        multicast("x");

        return $"{a} {b} {c} {d}";
    }
}

/// <summary>A property of delegate type, so 12.8.10.4 has a delegate reached through an accessor.</summary>
public sealed class PxDelegateHolder
{
    /// <summary>The delegate, invoked through this property.</summary>
    public PxTransform Transform { get; init; } = value => value;
}
