using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace Surface.Expressions.Primary;

/// <summary>
/// The second use site of everything this project declares. Each declaration reached from here
/// is declared in another file, so every reference in this file takes the indexer's cross-file
/// path, while the same reference written next to its declaration took the same-file one. A
/// query that finds one and not the other has found the defect this file exists for.
/// </summary>
public static class PxCrossFileUses
{
    /// <summary>
    /// 12.8.7.1, 12.8.10.2, 12.8.12.4, 12.8.17.2.1 — the workhorse type, reached across a file
    /// boundary: constructor, field, property, event, indexer, overload set, generic arity, and
    /// static factory.
    /// </summary>
    public static string TheWorkhorse()
    {
        var target = new PxTarget(11);              // 12.8.17.2.1, cross-file constructor
        target.Seed = 12;                            // 12.8.7.1, cross-file field
        target.Scale = 3;                            // cross-file property set accessor
        var element = target[2];                     // 12.8.12.4, cross-file indexer
        var byInt = target.Compute(1);               // 12.6.4.3, cross-file overload set
        var byLong = target.Compute(1L);
        var byNamed = target.Compute(times: 2, value: 3);
        var arityZero = target.Lookup();             // 12.5.1, lookup by arity, cross-file
        var arityOne = target.Lookup<PxPoint>();
        var factory = PxTarget.Of(4).Measure();      // static member access on the type
        var weight = target.Weight;

        target.Noticed += Note;                      // 12.2.1, cross-file event access
        target.Raise("cross file");
        target.Noticed -= Note;

        return string.Join(" ", element, byInt, byLong, byNamed, arityZero, arityOne, factory, weight);
    }

    /// <summary>
    /// 12.8.7.1 on constructed generic types, across files: the member the access binds to is a
    /// substitution of a declaration in <c>PxDeclarations.cs</c>, and its spelling must equal
    /// that declaration's.
    /// </summary>
    public static string ConstructedGenerics()
    {
        var counted = new PxBox<int>(1);
        var value = counted.Value;
        var mapped = counted.Map(number => number.ToString(CultureInfo.InvariantCulture)).Value;
        var wrapped = PxBox<PxPoint>.Wrap(new PxPoint(1, 2)).Value.Measure();
        var nested = new PxBox<PxBox<string>>(new PxBox<string>("x")).Value.Value;

        // 12.6.4.8 — the overload pair that collides under substitution, reached from elsewhere.
        var collidingInt = new PxGenericOverloads<int>().Accept(1);
        var collidingT = new PxGenericOverloads<string>().Accept("two");

        return string.Join(" ", value, mapped, wrapped, nested, collidingInt, collidingT);
    }

    /// <summary>
    /// 12.6.6.1 and 12.6.6.2 — virtual dispatch, interface mapping, explicit implementation and
    /// boxing, all reaching declarations in other files.
    /// </summary>
    public static string DispatchAndBoxing()
    {
        var derived = new PxDerivedCounter();
        PxBaseCounter asBase = derived;

        var throughOverride = asBase.Count();          // the override, cross-file
        var throughIndexer = asBase[1];                 // the overridden indexer
        var throughProperty = asBase.Label;             // the overridden property
        var hidden = asBase.Describe();                 // 12.5.1, the *hidden* base member
        var unhidden = derived.Describe();              // the hiding member

        IPxMeasured boxedStruct = new PxPoint(1, 2);    // 12.6.6.2, a box
        var throughBox = boxedStruct.Measure();
        var explicitly = ((IPxMeasured)new PxExplicitMeasured()).Weight;

        var cross = new PxCrossFileCounter();
        var throughCross = cross.Count() + cross[0] + cross.Inherited().Length + cross.Label.Length;

        return string.Join(" ", throughOverride, throughIndexer, throughProperty, hidden, unhidden, throughBox, explicitly, throughCross);
    }

    /// <summary>
    /// 12.8.12 — element access across files: a declared indexer, a two-argument indexer, a
    /// renamed indexer, an interface indexer, and the index/range members of a sliceable type.
    /// </summary>
    public static string ElementAccess()
    {
        var grid = new PxGrid();
        grid[2, 1] = 4;

        var renamed = new PxRenamedIndexer();
        IPxIndexed indexed = new PxIndexedList();
        var sliceable = new PxSliceable();

        var fromGrid = grid[2, 1];
        var fromRenamed = renamed[2] + renamed.Item;
        var fromInterface = indexed[0] + indexed.Length;
        var fromSliceable = sliceable[0] + sliceable[^1] + sliceable[1..3].Length;
        var fromFunctionMembers = new PxFunctionMembers(2)[3];
        var fromMaybe = new PxMaybe()[1];

        return string.Join(" ", fromGrid, fromRenamed, fromInterface, fromSliceable, fromFunctionMembers, fromMaybe);
    }

    /// <summary>
    /// 12.8.17 — creation across files: constructors and their overloads, object initializers,
    /// collection initializers, array creation, and delegate creation.
    /// </summary>
    public static string Creation()
    {
        var created = new PxCreated("cross", 2) { Adjusted = 1, Field = 2 };
        var targetTyped = CreateSettings(new() { Name = "cross-file", Retries = 1, Nested = { Depth = 2 }, ["k"] = 3 });
        var basket = new PxBasket { 9, { 8, 2 } };
        PxBasket fromExpression = [1, 2, 3];
        var array = new PxPoint[] { new(1, 1), new(2, 2) };
        var delegated = new PxTransform(PxDelegateCreation.Twice);
        var holder = new PxDelegateHolder { Transform = delegated };
        var counter = new PxCounter(1) + new PxCounter(2);
        var checkedCounter = checked(new PxCounter(3) + new PxCounter(4));

        return string.Join(
            " ",
            created.Origin,
            created.Adjusted + created.Field,
            targetTyped,
            basket.Count,
            fromExpression.Count,
            array[1].Measure(),
            holder.Transform(5),
            counter,
            checkedCounter);
    }

    /// <summary>
    /// 12.8.17.3 — the third site of the anonymous type whose other two sites are in
    /// <c>PxCreation.cs</c>: same property names, same types, same order, so all three are one
    /// declaration in the emitted assembly.
    /// </summary>
    public static string TheSharedAnonymousType()
    {
        var record = new { Label = "third", Weight = 3 };
        var alsoHere = new { Label = "fourth", Weight = 4 };
        var projected = new { new PxTarget(1).Seed };
        return $"{record.Label}{record.Weight}{alsoHere.Label}{projected.Seed}";
    }

    /// <summary>
    /// 12.6.2 and 12.6.4 — argument lists and overload resolution across files: every passing
    /// mode, and one call per resolution step.
    /// </summary>
    public static string ArgumentsAndOverloads()
    {
        var read = PxArgumentLists.TryRead("7", out var parsed);
        var left = 1;
        var right = 2;
        PxArgumentLists.Swap(ref left, ref right);
        var measured = PxArgumentLists.MeasureIn(new PxPoint(1, 2));
        var peeked = PxArgumentLists.Peek(in left);
        var total = PxArgumentLists.Total(1, 2, 3);
        var spanned = PxArgumentLists.TotalSpan(4, 5);
        var described = PxArgumentLists.Describe(text: "x", pad: '-');

        short narrow = 1;
        var steps = string.Join(
            " ",
            PxOverloadArena.Applicable(1),
            PxOverloadArena.Applicable("a", "b"),
            PxOverloadArena.Better(1),
            PxOverloadArena.Mode(left),
            PxOverloadArena.FromExpression(narrow),
            PxOverloadArena.Exact(number => number),
            PxOverloadArena.Target(1),
            new PxOverloadArena.Constructed("x").Kind);

        return string.Join(" ", read, parsed, left, right, measured, peeked, total, spanned, described, steps);
    }

    /// <summary>
    /// 12.6.3 — inference across files, including a method group conversion whose type argument
    /// is inferred from the delegate type alone.
    /// </summary>
    public static string Inference()
    {
        var identity = PxInference.Identity(new PxPoint(1, 2)).Measure();
        var projected = PxInference.Project(new[] { 1, 2 }, number => new PxPoint(number, number)).Count;
        var summed = PxInference.Sum(new PxPoint(1, 1), new PxPoint(2, 2));
        Func<string, string> fromGroup = PxInference.Echo;
        PxTransform fromGroupExplicit = PxInference.Echo<int>;

        return string.Join(" ", identity, projected, summed, fromGroup("x"), fromGroupExplicit(1));
    }

    /// <summary>
    /// 12.8.8, 12.8.11 and 12.8.13 — the null-conditional forms against a receiver declared in
    /// another file, including the chain whose receiver type changes at each link.
    /// </summary>
    public static string NullConditional()
    {
        var link = new PxMaybe { Next = new PxMaybe { Seed = 2 }, Point = new PxPoint(1, 2), Transform = value => value + 1 };
        PxMaybe? absent = null;

        var member = link?.Seed;
        var chained = link?.Next?.Seed;
        var invoked = link?.Measure();
        var element = link?[1];
        var throughDelegate = link?.Transform?.Invoke(3);
        var throughStruct = link?.Point?.Measure();
        var shortCircuited = absent?.Next?.Measure();
        var forgiven = link!.Seed;

        return string.Join(" ", member, chained, invoked, element, throughDelegate, throughStruct, shortCircuited ?? -1, forgiven);
    }

    /// <summary>
    /// 12.8.3 — interpolated strings whose handler and whose holes are declared in other files.
    /// </summary>
    public static string Interpolation()
    {
        var target = new PxTarget(5);

        var plain = $"seed {target.Seed} slot {target[0]} measured {target.Measure()}";
        var throughHandler = PxInterpolation.Render($"cross-file {target.Weight} and {nameof(PxTarget)}");
        var throughConditionalHandler = PxInterpolation.RenderIf(true, $"enabled {target.Scale}");
        var constant = PxInterpolation.Constant;

        return string.Join(" ", plain, throughHandler, throughConditionalHandler, constant);
    }

    /// <summary>
    /// 12.8.18, 12.8.21 and 12.8.23 — <c>typeof</c>, <c>default</c> and <c>nameof</c> over
    /// declarations in other files. None of these evaluates its operand, and every one of them
    /// is a reference.
    /// </summary>
    public static string TypeOperators()
    {
        var types = string.Join(
            " ",
            typeof(PxTarget).Name,
            typeof(PxBox<PxPoint>).Name,
            typeof(PxBox<>).Name,
            typeof(IPxMeasured).Name,
            typeof(PxHue).Name,
            typeof(PxMemberAccess.Marker).Name);

        var defaults = string.Join(
            " ",
            default(PxPoint).Measure(),
            default(PxTarget)?.Seed ?? -1,
            default(PxHue));

        var names = string.Join(
            " ",
            nameof(PxTarget),
            nameof(PxTarget.Compute),
            nameof(PxTarget.Seed),
            nameof(PxPoint.Deconstruct),
            nameof(PxExtensions.Tally),
            nameof(PxHue.Cool),
            nameof(PxSettings.Retries));

        return string.Join(" | ", types, defaults, names);
    }

    /// <summary>
    /// 12.8.10.3 — extension invocations across files, in the reduced form, the static form, and
    /// through a C# 14 extension block.
    /// </summary>
    public static string Extensions()
    {
        var target = new PxTarget(2);
        var point = new PxPoint(3, 4);

        var reduced = target.Doubled();
        var asStatic = PxExtensions.Doubled(target);
        var onPredefined = 5.Padded(1);
        var generic = new List<PxPoint> { point }.Tally();
        var blockProperty = point.Sum;
        var blockMethod = point.Scaled(2);

        return string.Join(" ", reduced, asStatic, onPredefined, generic, blockProperty, blockMethod);
    }

    /// <summary>
    /// 12.7 and 12.8.6 — deconstruction and tuple literals against declarations in other files,
    /// including the extension deconstructor.
    /// </summary>
    public static string DeconstructionAndTuples()
    {
        var (x, y) = new PxPoint(1, 2);                       // the struct's own deconstructor
        var (first, second, third) = new PxTriple(3, 4, 5);   // the three-target overload
        var (seed, scale) = new PxTarget(6);                  // the extension deconstructor
        var (left, right) = (PxHue.Warm, PxHue.Cool);         // a tuple literal
        var named = (Point: new PxPoint(7, 8), Label: "cross");

        return string.Join(" ", x + y, first + second + third, seed + scale, left, right, named.Point.Measure(), named.Label);
    }

    /// <summary>
    /// 12.9.9 — awaits of awaitables declared in other files: the instance pattern, the
    /// extension pattern, and the extension on a predefined type.
    /// </summary>
    public static async Task<string> Awaits()
    {
        var fromDeclared = await new PxAwaitable(1);
        var fromCritical = await new PxCriticalAwaitable(2);
        var fromInt = await 3;
        var fromFramework = await PxAwait.ValueLater(4);
        var fromValueTask = await PxAwait.AsyncValueTask();

        return string.Join(" ", fromDeclared, fromCritical, fromInt, fromFramework, fromValueTask);
    }

    /// <summary>
    /// 12.8.4, 12.8.7.2, 12.8.14 and 12.5.1 — simple names, the type/member ambiguity, this
    /// access and hiding, all reached from outside the files that declare them.
    /// </summary>
    public static string NamesAndHiding()
    {
        var palette = new PxPalette { PxHue = PxHue.Cool };
        var ambiguous = palette.PxHue;                    // the property
        var asType = PxHue.Warm;                           // the type of the same spelling
        var named = palette.NameOfTheAmbiguity();

        var leaf = new PxHidingLeaf();
        var walked = leaf.Walk();
        var slot = leaf.Slot + ((PxHidingRoot)leaf).Slot;

        var visible = new PxVisibility().Public() + new PxVisibilityHeir().Reach();
        var cursor = PxCursor.UsedHere();
        var reflected = new PxThisAccess(1).Reflect();

        return string.Join(" ", ambiguous, asType, named, walked, slot, visible, cursor, reflected);
    }

    /// <summary>Runs every cross-file group, so nothing here is dead code by inspection.</summary>
    public static async Task<string> EveryGroup()
    {
        var parts = new List<string>
        {
            TheWorkhorse(),
            ConstructedGenerics(),
            DispatchAndBoxing(),
            ElementAccess(),
            Creation(),
            TheSharedAnonymousType(),
            ArgumentsAndOverloads(),
            Inference(),
            NullConditional(),
            Interpolation(),
            TypeOperators(),
            Extensions(),
            DeconstructionAndTuples(),
            NamesAndHiding(),
            await Awaits(),
        };

        return string.Join(Environment.NewLine, parts);
    }

    private static string CreateSettings(PxSettings settings) => settings.Name + settings["k"];

    private static void Note(string message) => GC.KeepAlive(message);
}
