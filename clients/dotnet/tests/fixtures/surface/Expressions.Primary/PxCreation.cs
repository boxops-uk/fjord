using System;
using System.Collections.Generic;
using System.Globalization;

namespace Surface.Expressions.Primary;

/// <summary>
/// 12.8.17.2.1 — object creation expressions. <c>new T(A)</c> writes the *type's* name and binds
/// to a *constructor*, which is the one binding in the language whose target has a different
/// name from the name at the use site.
/// </summary>
public sealed class PxCreated
{
    /// <summary>The parameterless constructor, reached by <c>new PxCreated()</c> and by <c>new()</c>.</summary>
    public PxCreated() => Origin = "none";

    /// <summary>An overload, reached by an <c>int</c> argument.</summary>
    public PxCreated(int seed) => Origin = $"int {seed}";

    /// <summary>An overload with a default, so <c>new PxCreated("x")</c> omits an argument.</summary>
    public PxCreated(string label, int weight = 1) => Origin = $"{label} {weight}";

    /// <summary>Which constructor built this instance.</summary>
    public string Origin { get; }

    /// <summary>A settable property, for 12.8.17.2.2.</summary>
    public int Adjusted { get; set; }

    /// <summary>A public field, which an object initializer may also assign.</summary>
    public int Field;

    /// <summary>
    /// 12.8.17.2.1 — one creation per form: with arguments, without, with a named argument,
    /// target-typed, on a constructed generic type, through a type parameter, and nested.
    /// </summary>
    public static string EveryCreationForm<T>()
        where T : new()
    {
        var withArgument = new PxCreated(1).Origin;
        var withNamed = new PxCreated(weight: 2, label: "named").Origin;
        var withDefaulted = new PxCreated("defaulted").Origin;
        PxCreated targetTyped = new();
        var constructed = new PxBox<int>(3).Value;
        var throughTypeParameter = new T();
        var nested = new PxBox<PxCreated>(new PxCreated(4)).Value.Origin;

        return $"{withArgument} {withNamed} {withDefaulted} {targetTyped.Origin} {constructed} {throughTypeParameter} {nested}";
    }

    /// <summary>
    /// 12.8.17.2.2 — object initializers: a property, a field, an init-only property, a nested
    /// initializer with no <c>new</c> of its own, and an indexer initializer.
    /// </summary>
    public static string ObjectInitializers()
    {
        var plain = new PxCreated(1) { Adjusted = 2, Field = 3 };

        var settings = new PxSettings
        {
            Name = "settings",
            Retries = 2,
            Nested = { Depth = 3 },
            ["first"] = 10,
            ["second"] = 20,
        };

        // An initializer on a target-typed new, so the type is not written at all.
        PxSettings targetTyped = new() { Name = "target-typed" };

        return $"{plain.Adjusted} {plain.Field} {settings.Name} {settings.Retries} {settings.Nested.Depth} {settings["first"]} {targetTyped.Name}";
    }

    /// <summary>
    /// 12.8.17.2.3 — collection initializers: each element binds to an <c>Add</c> the use site
    /// does not name, and a braced element binds to the multi-argument overload.
    /// </summary>
    public static string CollectionInitializers()
    {
        // Add(int) once per element, and Add(int, int) for the braced element.
        var basket = new PxBasket { 1, 2, { 3, 4 } };

        // The framework's Add, on a constructed generic type.
        var numbers = new List<int> { 5, 6 };

        // Dictionary, initialized by Add(K, V) and then by its indexer — two different clauses
        // wearing the same braces.
        var byAdd = new Dictionary<string, int> { { "a", 1 }, { "b", 2 } };
        var byIndexer = new Dictionary<string, int> { ["c"] = 3 };

        // A collection expression, which binds to the same Add through a different grammar.
        PxBasket fromExpression = [7, 8];
        List<int> spread = [..numbers, 9];

        return $"{basket.Count} {numbers.Count} {byAdd.Count} {byIndexer.Count} {fromExpression.Count} {spread.Count}";
    }

    /// <summary>
    /// 12.8.17.4 — array creation, in each of its forms. The element type is a reference the
    /// index holds; the array type itself is declared nowhere.
    /// </summary>
    public static string ArrayCreation()
    {
        var sized = new int[3];
        var initialized = new int[] { 1, 2, 3 };
        var inferred = new[] { 1, 2, 3 };
        var rectangular = new int[2, 3];
        var rectangularInitialized = new int[,] { { 1, 2 }, { 3, 4 } };
        var jagged = new int[2][];
        var jaggedInitialized = new int[][] { [1], [2, 3] };
        var ofDeclaredType = new PxPoint[] { new(1, 1), new(2, 2) };
        var ofAnonymous = new[] { new { Left = 1, Right = 2 } };
        int[] fromCollectionExpression = [4, 5];

        return string.Join(
            " ",
            sized.Length,
            initialized.Length,
            inferred.Length,
            rectangular.Length,
            rectangularInitialized.Length,
            jagged.Length,
            jaggedInitialized.Length,
            ofDeclaredType[0].Measure(),
            ofAnonymous[0].Left,
            fromCollectionExpression.Length);
    }
}

/// <summary>
/// 12.8.17.3 — anonymous object creation. The type is declared by the expression itself, and two
/// expressions with the same property names and types in the same assembly declare *one* type.
/// This file writes that shape twice on purpose; <c>PxCrossFileUses.cs</c> writes it a third time.
/// </summary>
public static class PxAnonymous
{
    /// <summary>The first of two identically shaped anonymous object creations.</summary>
    public static string First()
    {
        var record = new { Label = "first", Weight = 1 };
        return $"{record.Label} {record.Weight}";
    }

    /// <summary>
    /// The second: same property names, same property types, same order — so the compiler emits
    /// one anonymous type for both, and two creation sites share one declaration.
    /// </summary>
    public static string Second()
    {
        var record = new { Label = "second", Weight = 2 };
        return $"{record.Label} {record.Weight}";
    }

    /// <summary>
    /// The same property names in a different order, and the same names at different types:
    /// each of these is a *different* anonymous type from the two above.
    /// </summary>
    public static string Variants()
    {
        var reordered = new { Weight = 3, Label = "reordered" };
        var retyped = new { Label = "retyped", Weight = 4.5 };
        var extra = new { Label = "extra", Weight = 5, Extra = true };
        return $"{reordered.Label} {retyped.Weight} {extra.Extra}";
    }

    /// <summary>
    /// 12.8.17.3 — a projection initializer, where the property name is not written at all: it
    /// is taken from the member the initializer reads.
    /// </summary>
    public static string Projected()
    {
        var target = new PxTarget(6);
        var point = new PxPoint(1, 2);
        var projected = new { target.Seed, point.X, Renamed = point.Y };
        var nested = new { Inner = new { projected.Seed } };
        return $"{projected.Seed} {projected.X} {projected.Renamed} {nested.Inner.Seed}";
    }
}

/// <summary>
/// 12.8.17.5 — delegate creation expressions. <c>new D(E)</c> names a delegate type and takes a
/// method group, a lambda, an anonymous method or another delegate — and the created delegate's
/// target is a method whose name is the only thing written.
/// </summary>
public static class PxDelegateCreation
{
    /// <summary>A static method, the target of a method group conversion.</summary>
    public static int Twice(int value) => value * 2;

    /// <summary>A generic method, so a delegate creation has a type argument to infer.</summary>
    /// <typeparam name="T">Inferred from the delegate type.</typeparam>
    public static T Echo<T>(T value) => value;

    /// <summary>Every delegate creation form, in the file that declares the methods they target.</summary>
    public static string EveryDelegateForm()
    {
        var target = new PxTarget(1);

        // From a static method group, spelled with new.
        var fromStatic = new PxTransform(Twice);

        // From an instance method group — the receiver is captured, the method named.
        var fromInstance = new Func<int, int>(target.Compute);

        // From a generic method group, whose type argument is inferred from the delegate type.
        var fromGeneric = new PxTransform(Echo<int>);
        Func<string, string> fromGenericInferred = Echo;

        // From a lambda, and from an anonymous method (12.8.24), and from another delegate.
        var fromLambda = new PxTransform(value => value + 1);
        var fromAnonymousMethod = new PxTransform(delegate(int value) { return value - 1; });
        var fromDelegate = new PxTransform(fromStatic);

        // Without new: the conversion the standard describes as the same binding.
        PxTransform implicitly = Twice;

        return string.Join(
            " ",
            fromStatic(1),
            fromInstance(2),
            fromGeneric(3),
            fromGenericInferred("x"),
            fromLambda(4),
            fromAnonymousMethod(5),
            fromDelegate(6),
            implicitly(7));
    }

    /// <summary>
    /// 12.8.24 — anonymous method expressions, which are primary expressions in their own right:
    /// with a parameter list, with no parameter list at all, and with a captured outer variable.
    /// </summary>
    public static string AnonymousMethods()
    {
        var captured = 10;

        PxTransform withParameters = delegate(int value) { return value + captured; };
        PxNotice withoutParameterList = delegate { captured++; };
        Func<int> ofNoArguments = delegate { return captured; };

        withoutParameterList("ignored");

        return string.Join(
            " ",
            withParameters(1).ToString(CultureInfo.InvariantCulture),
            ofNoArguments().ToString(CultureInfo.InvariantCulture));
    }
}
