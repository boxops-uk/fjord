using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Surface.Expressions.Primary;

/// <summary>
/// Something with a size. Gives clause 12.6.6.1 (interface mapping) and clause 12.6.6.2
/// (invocations on boxed instances) an interface to dispatch through.
/// </summary>
public interface IPxMeasured
{
    /// <summary>A method, so a call through the interface has somewhere to map.</summary>
    int Measure();

    /// <summary>A property, so 12.2.1's "property access" classification has a target.</summary>
    int Weight { get; }
}

/// <summary>A colour, used by 12.8.7.2's "identical simple names and type names" case.</summary>
public enum PxHue
{
    Warm = 1,
    Cool = 2,
}

/// <summary>A delegate type, for 12.8.10.4 (delegate invocations) and 12.8.17.5 (delegate creation).</summary>
public delegate int PxTransform(int value);

/// <summary>An event's delegate type, so 12.2.1's "event access" has a target.</summary>
public delegate void PxNotice(string message);

/// <summary>
/// The workhorse operand of this project: a class carrying one of every member kind a
/// primary expression can bind to — field, property, event, indexer, constructor,
/// method overload set, static factory. Exactly one <c>this[...]</c>, deliberately:
/// two indexers in one type is a quarantined shape.
/// </summary>
public class PxTarget : IPxMeasured
{
    private readonly List<int> _slots = [1, 2, 3];

    /// <summary>A field, so 12.2.1's "variable" classification has a target.</summary>
    public int Seed;

    /// <summary>A property with both accessors — 12.6.1 lists an accessor as a function member.</summary>
    public int Scale { get; set; } = 2;

    /// <summary>An event, whose add/remove accessors are function members (12.6.1).</summary>
    public event PxNotice? Noticed;

    /// <summary>12.8.17.2.1 — a constructor that chains to another with a <c>this</c> initializer.</summary>
    public PxTarget()
        : this(0)
    {
    }

    /// <summary>12.8.17.2.1 — the constructor <c>new PxTarget(A)</c> binds to.</summary>
    public PxTarget(int seed) => Seed = seed;

    /// <summary>12.8.12.4 — the one indexer of this type; an element access has no name node.</summary>
    public int this[int slot] => _slots[slot];

    /// <inheritdoc />
    public int Measure() => _slots.Count;

    /// <inheritdoc />
    public int Weight => Seed * Scale;

    /// <summary>12.6.4.2 — an applicable candidate for <c>Compute(int)</c>.</summary>
    public int Compute(int value) => value + Seed;

    /// <summary>12.6.4.3 — a worse candidate for an <c>int</c> argument, a better one for <c>long</c>.</summary>
    public int Compute(long value) => (int)value - Seed;

    /// <summary>12.6.2.2 — two parameters, so a named argument has a name to correspond to.</summary>
    public int Compute(int value, int times) => (value + Seed) * times;

    /// <summary>12.5.1 — lookup is by name *and* arity: this is the arity-0 member.</summary>
    public string Lookup() => "arity 0";

    /// <summary>12.5.1 — and this is the arity-1 member of the same name and parameter list.</summary>
    public string Lookup<T>() => $"arity 1: {typeof(T).Name}";

    /// <summary>A static factory, so a member access on the type (not an instance) has a target.</summary>
    public static PxTarget Of(int seed) => new(seed);

    /// <summary>12.8.11 — a null-conditional invocation of an event's delegate field.</summary>
    public void Raise(string message) => Noticed?.Invoke(message);

    /// <summary>
    /// Every reference in here is a *same-file* reference to a declaration above it, which
    /// takes a different path through the indexer than the cross-file copies in
    /// <c>PxCrossFileUses.cs</c>.
    /// </summary>
    public int UsedHere()
    {
        // 12.8.17.2.1 object creation, same file as the constructor it binds to.
        var target = new PxTarget(3);

        // 12.8.7.1 member access to a field, and 12.2.1's "variable" classification.
        target.Seed = 4;

        // 12.8.12.4 element access, binding to the indexer declared above with no name node.
        var slot = target[0];

        // 12.8.10.2 method invocation, resolving to Compute(int) over Compute(long).
        var computed = target.Compute(slot);

        // 12.6.2.2 named arguments, given out of declaration order.
        var scaled = target.Compute(times: 2, value: 5);

        // 12.2.1 event access, and a method group converted to a delegate (12.6.3.15).
        target.Noticed += Log;
        target.Raise(nameof(UsedHere));
        target.Noticed -= Log;

        // 12.8.14 this access: the receiver of the indexer and of Measure() is written `this`.
        return computed + scaled + target.Weight + this[1] + Measure();
    }

    private static void Log(string message) => GC.KeepAlive(message);
}

/// <summary>
/// 12.8.15 — the base half of base access: a virtual method, a virtual indexer and a
/// virtual property, each overridden in <see cref="PxDerivedCounter" />.
/// </summary>
public class PxBaseCounter
{
    private readonly int[] _cells = [10, 20, 30];

    /// <summary>Overridden below; <c>base.Count()</c> must bind here, not to the override.</summary>
    public virtual int Count() => _cells.Length;

    /// <summary>One indexer, virtual, so <c>base[i]</c> has a target.</summary>
    public virtual int this[int index] => _cells[index];

    /// <summary>A virtual property, so <c>base.Label</c> has a target.</summary>
    public virtual string Label => "base";

    /// <summary>12.5.1 — hidden (not overridden) below, so member lookup has hiding to do.</summary>
    public string Describe() => "base counter";
}

/// <summary>
/// 12.8.15 — the derived half: every member here reaches its own base declaration through
/// <c>base</c>, in the same file as that declaration.
/// </summary>
public class PxDerivedCounter : PxBaseCounter
{
    /// <summary>12.8.15 — base method access.</summary>
    public override int Count() => base.Count() + 1;

    /// <summary>12.8.15 — base element access; the receiver is <c>base</c>, the target the base indexer.</summary>
    public override int this[int index] => base[index] * 2;

    /// <summary>12.8.15 — base property access.</summary>
    public override string Label => base.Label + "+derived";

    /// <summary>12.5.1 — hiding by name: this declaration hides the inherited <c>Describe</c>.</summary>
    public new string Describe() => "derived counter";
}

/// <summary>
/// 12.8.7.1 on a constructed generic type: a member access on <c>PxBox&lt;int&gt;</c> binds to a
/// substituted member whose spelling must equal this declaration's. There is deliberately no
/// non-generic <c>PxBox</c> anywhere in the corpus — one name at two arities is a quarantined shape.
/// </summary>
/// <typeparam name="T">The boxed value's type.</typeparam>
public class PxBox<T>
{
    /// <summary>12.8.17.2.1 — the constructor a <c>new PxBox&lt;string&gt;("x")</c> binds to.</summary>
    public PxBox(T value) => Value = value;

    /// <summary>The substituted property a member access on a constructed type resolves to.</summary>
    public T Value { get; }

    /// <summary>12.6.3.1 — a generic method on a generic type, so inference has two scopes to fill.</summary>
    /// <typeparam name="TOut">Inferred from the projection's return type (12.6.3.14).</typeparam>
    public PxBox<TOut> Map<TOut>(Func<T, TOut> project) => new(project(Value));

    /// <summary>A static member, so <c>PxBox&lt;int&gt;.Wrap(1)</c> is a member access on a type.</summary>
    public static PxBox<T> Wrap(T value) => new(value);

    /// <summary>Same-file uses of the members above, on the *constructed* type.</summary>
    public static string UsedHere()
    {
        PxBox<int> counted = PxBox<int>.Wrap(7);
        PxBox<string> named = counted.Map(value => value.ToString());
        return named.Value;
    }
}

/// <summary>
/// 12.6.4.8 — overloading in generic classes: <c>Accept(T)</c> and <c>Accept(int)</c> are distinct
/// declarations that would have identical signatures in <c>PxGenericOverloads&lt;int&gt;</c>.
/// </summary>
/// <typeparam name="T">The type parameter that can collide with <c>int</c>.</typeparam>
public class PxGenericOverloads<T>
{
    /// <summary>The type-parameter overload.</summary>
    public string Accept(T value) => $"T:{value}";

    /// <summary>The <c>int</c> overload, preferred when the argument is an <c>int</c> literal.</summary>
    public string Accept(int value) => $"int:{value}";

    /// <summary>Same-file uses: one call per declaration, on the colliding construction.</summary>
    public static string UsedHere()
    {
        var arena = new PxGenericOverloads<int>();

        // Resolves to Accept(int) — the T overload is applicable too, and is the worse candidate.
        var chosen = arena.Accept(1);

        // Reaches Accept(T) at a construction where T is not int.
        var other = new PxGenericOverloads<string>().Accept("two");
        return chosen + other;
    }
}

/// <summary>
/// A small unmanaged struct: the operand of 12.8.19 (<c>sizeof</c>), 12.8.21 (<c>default</c>),
/// 12.7 (deconstruction) and 12.6.6.2 (invocations on boxed instances).
/// </summary>
public readonly struct PxPoint : IPxMeasured
{
    /// <summary>Builds a point.</summary>
    public PxPoint(int x, int y)
    {
        X = x;
        Y = y;
    }

    /// <summary>The abscissa.</summary>
    public int X { get; }

    /// <summary>The ordinate.</summary>
    public int Y { get; }

    /// <inheritdoc />
    public int Measure() => X + Y;

    /// <inheritdoc />
    public int Weight => X * Y;

    /// <summary>12.7 — the deconstructor a positional deconstruction binds to, by convention only.</summary>
    public void Deconstruct(out int x, out int y)
    {
        x = X;
        y = Y;
    }

    /// <inheritdoc />
    public override string ToString() => $"({X},{Y})";
}

/// <summary>
/// 12.6.6.1 — explicit interface implementations: the member has no name a member access can
/// spell, so a call must go through the interface.
/// </summary>
public sealed class PxExplicitMeasured : IPxMeasured
{
    /// <summary>Explicitly implemented method; only reachable through <c>IPxMeasured</c>.</summary>
    int IPxMeasured.Measure() => 41;

    /// <summary>Explicitly implemented property.</summary>
    int IPxMeasured.Weight => 1;

    /// <summary>Same-file use: the cast is what makes the interface member reachable.</summary>
    public int UsedHere() => ((IPxMeasured)this).Measure() + ((IPxMeasured)this).Weight;
}

/// <summary>
/// 12.9.9.2 — an awaitable: not an interface, a *pattern*. <c>await</c> binds to
/// <c>GetAwaiter</c>, then to three members of the awaiter that no call site names.
/// </summary>
public sealed class PxAwaitable
{
    private readonly int _value;

    /// <summary>Wraps a value that a continuation will observe.</summary>
    public PxAwaitable(int value) => _value = value;

    /// <summary>The member <c>await</c> looks up by name, with no name node at the use site.</summary>
    public PxAwaiter GetAwaiter() => new(_value);
}

/// <summary>
/// 12.9.9.4 — the awaiter: <c>IsCompleted</c>, <c>OnCompleted</c> and <c>GetResult</c> are all
/// bound by the run-time evaluation of an <c>await</c>, and none of them is written at the
/// <c>await</c> site.
/// </summary>
public readonly struct PxAwaiter : INotifyCompletion
{
    private readonly int _value;

    /// <summary>Wraps the value <see cref="GetResult" /> will hand back.</summary>
    public PxAwaiter(int value) => _value = value;

    /// <summary>Read first by the generated state machine.</summary>
    public bool IsCompleted => true;

    /// <summary>Read to produce the value of the <c>await</c> expression.</summary>
    public int GetResult() => _value;

    /// <inheritdoc />
    public void OnCompleted(Action continuation) => continuation();
}

/// <summary>
/// 12.8.3 — an interpolated string handler. The compiler rewrites an interpolated string into
/// a constructor call plus one <c>AppendLiteral</c> or <c>AppendFormatted</c> call per part,
/// so every member here is bound from a site that spells none of them.
/// </summary>
[InterpolatedStringHandler]
public struct PxLogHandler
{
    private readonly StringBuilder _text;

    /// <summary>The shape the compiler requires: literal length and hole count.</summary>
    public PxLogHandler(int literalLength, int formattedCount)
        => _text = new StringBuilder(literalLength + (formattedCount * 8));

    /// <summary>Bound once per literal run of the interpolated string.</summary>
    public void AppendLiteral(string value) => _text.Append(value);

    /// <summary>Bound for a hole whose type is not <c>string</c> — a generic method inferred per hole.</summary>
    /// <typeparam name="T">The hole's type.</typeparam>
    public void AppendFormatted<T>(T value) => _text.Append(value);

    /// <summary>Bound, in preference to the generic overload, for a <c>string</c> hole.</summary>
    public void AppendFormatted(string? value) => _text.Append(value);

    /// <inheritdoc />
    public override string ToString() => _text.ToString();
}

/// <summary>
/// 12.8.17.2.3 — a collection initializer target: <c>Add</c> is bound by name, once per element,
/// and an element with braces binds to the multi-argument overload.
/// </summary>
public sealed class PxBasket : IEnumerable<int>
{
    private readonly List<int> _items = [];

    /// <summary>Bound by a one-element initializer entry.</summary>
    public void Add(int item) => _items.Add(item);

    /// <summary>Bound by a <c>{ item, repeat }</c> initializer entry.</summary>
    public void Add(int item, int repeat)
    {
        for (var index = 0; index < repeat; index++)
        {
            _items.Add(item);
        }
    }

    /// <summary>How many items were added.</summary>
    public int Count => _items.Count;

    /// <inheritdoc />
    public IEnumerator<int> GetEnumerator() => _items.GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>12.8.17.2.2 — the inner object an initializer reaches without writing <c>new</c>.</summary>
public sealed class PxNestedSettings
{
    /// <summary>Set through a nested object initializer.</summary>
    public int Depth { get; set; }
}

/// <summary>
/// 12.8.17.2.2 — an object initializer target: a settable property, an init-only property, a
/// read-only property reached by a nested initializer, and an indexer reached by <c>[key] =</c>.
/// </summary>
public sealed class PxSettings
{
    private readonly Dictionary<string, int> _values = [];

    /// <summary>Assigned by <c>Name = ...</c> in an initializer.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Assigned by an initializer only — an init accessor is a function member (12.6.1).</summary>
    public int Retries { get; init; }

    /// <summary>Reached by <c>Nested = { Depth = 1 }</c>, which binds the getter, not a setter.</summary>
    public PxNestedSettings Nested { get; } = new();

    /// <summary>The one indexer of this type, reached by <c>["key"] = 1</c> in an initializer.</summary>
    public int this[string key]
    {
        get => _values.TryGetValue(key, out var found) ? found : 0;
        set => _values[key] = value;
    }
}

/// <summary>
/// 12.8.10.3 — extension method invocations, classic form: the receiver at the call site is the
/// first parameter here, and the call spells a name this static class does not appear in.
/// </summary>
public static class PxExtensions
{
    /// <summary>Extends the workhorse type.</summary>
    public static int Doubled(this PxTarget target) => target.Measure() * 2;

    /// <summary>Extends a predefined type, with an argument, so inference has work to do.</summary>
    public static int Padded(this int value, int by) => value + by;

    /// <summary>12.6.3.1 — a generic extension method whose type argument is inferred from the receiver.</summary>
    /// <typeparam name="T">Inferred from the receiver's element type.</typeparam>
    public static int Tally<T>(this IEnumerable<T> items) => items is ICollection<T> known ? known.Count : 0;

    /// <summary>Same-file uses, in receiver position.</summary>
    public static int UsedHere() => new PxTarget(1).Doubled() + 3.Padded(by: 4) + new[] { 1, 2 }.Tally();
}

/// <summary>
/// 12.8.10.3 in its post-standard form: a C# 14 extension block. The members are declared
/// with no <c>this</c> parameter, and a call site still spells only the receiver and the name.
/// </summary>
public static class PxExtensionBlock
{
    extension(PxPoint point)
    {
        /// <summary>An extension property — there is no such thing in the standard's grammar.</summary>
        public int Sum => point.X + point.Y;

        /// <summary>An extension method declared inside the block.</summary>
        public int Scaled(int by) => (point.X + point.Y) * by;
    }

    /// <summary>Same-file uses of both extension members.</summary>
    public static int UsedHere()
    {
        var point = new PxPoint(2, 3);
        return point.Sum + point.Scaled(2);
    }
}
