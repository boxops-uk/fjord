using System;
using System.Collections.Generic;
using System.Globalization;

namespace Surface.Expressions.Primary;

/// <summary>
/// 12.8.20 — the <c>checked</c> and <c>unchecked</c> operators do more than change arithmetic
/// behaviour once a type declares a checked operator: the context decides *which declaration*
/// an addition binds to. Both halves are here, and the pair is required to exist together.
/// </summary>
public readonly struct PxCounter
{
    /// <summary>Builds a counter.</summary>
    public PxCounter(int value) => Value = value;

    /// <summary>The counted value.</summary>
    public int Value { get; }

    /// <summary>The unchecked addition, bound outside a checked context.</summary>
    public static PxCounter operator +(PxCounter left, PxCounter right)
        => new(unchecked(left.Value + right.Value));

    /// <summary>The checked addition, bound only inside a checked context — same operator, other declaration.</summary>
    public static PxCounter operator checked +(PxCounter left, PxCounter right)
        => new(checked(left.Value + right.Value));

    /// <inheritdoc />
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// The type operators of 12.8.18 to 12.8.23: <c>typeof</c>, <c>sizeof</c>, <c>checked</c>,
/// <c>unchecked</c>, <c>default</c>, <c>stackalloc</c> and <c>nameof</c>. Each takes a *type* or
/// a *symbol* where every other primary expression takes a value, which is why an index so often
/// records nothing for them.
/// </summary>
public static class PxTypeOperators
{
    /// <summary>A field, so <c>nameof</c> has a member of this type to name.</summary>
    public static readonly int Threshold = 4;

    /// <summary>
    /// 12.8.18 — <c>typeof</c>: a predefined type, a declared type, a constructed type, an
    /// unbound generic type, an array type, a nested type, a delegate type, <c>void</c>, and a
    /// type parameter.
    /// </summary>
    /// <typeparam name="T">Named by the last <c>typeof</c> below.</typeparam>
    public static string TypeOf<T>()
    {
        var predefined = typeof(int);
        var declared = typeof(PxTarget);
        var constructed = typeof(PxBox<int>);
        var unbound = typeof(PxBox<>);
        var constructedTwice = typeof(PxBox<PxBox<string>>);
        var array = typeof(PxPoint[]);
        var jagged = typeof(int[][]);
        var nested = typeof(PxMemberAccess.Marker);
        var nestedEnum = typeof(PxMemberAccess.Depth);
        var delegateType = typeof(PxTransform);
        var interfaceType = typeof(IPxMeasured);
        var nullable = typeof(int?);
        var pointer = typeof(int*);
        var nothing = typeof(void);
        var typeParameter = typeof(T);
        var tuple = typeof((int Left, int Right));

        return string.Join(
            " ",
            predefined.Name,
            declared.Name,
            constructed.Name,
            unbound.Name,
            constructedTwice.Name,
            array.Name,
            jagged.Name,
            nested.Name,
            nestedEnum.Name,
            delegateType.Name,
            interfaceType.Name,
            nullable.Name,
            pointer.Name,
            nothing.Name,
            typeParameter.Name,
            tuple.Name);
    }

    /// <summary>
    /// 12.8.19 — <c>sizeof</c>, which takes an unmanaged type and yields a constant: a
    /// predefined type, an enum, a declared struct, and a type parameter constrained to be
    /// unmanaged.
    /// </summary>
    /// <typeparam name="T">Constrained so that <c>sizeof(T)</c> is legal.</typeparam>
    public static unsafe string SizeOf<T>()
        where T : unmanaged
    {
        var predefined = sizeof(int);
        var predefinedFloating = sizeof(double);
        var declaredStruct = sizeof(PxPoint);
        var declaredEnum = sizeof(PxHue);
        var pointerSize = sizeof(int*);
        var typeParameter = sizeof(T);

        return string.Join(" ", predefined, predefinedFloating, declaredStruct, declaredEnum, pointerSize, typeParameter);
    }

    /// <summary>
    /// 12.8.20 — <c>checked</c> and <c>unchecked</c>: around a constant, around arithmetic,
    /// around a conversion, and — the part with a binding in it — around a user-defined
    /// operator whose checked form is a separate declaration.
    /// </summary>
    public static string CheckedAndUnchecked()
    {
        var large = int.MaxValue;

        var uncheckedArithmetic = unchecked(large + 1);
        var uncheckedConversion = unchecked((short)large);
        var checkedConstant = checked(1 + 2);
        var checkedNested = checked(unchecked(large + 1) - 1);

        var left = new PxCounter(2);
        var right = new PxCounter(3);
        var uncheckedOperator = left + right;                 // binds to operator +
        var checkedOperator = checked(left + right);          // binds to operator checked +

        return string.Join(
            " ",
            uncheckedArithmetic,
            uncheckedConversion,
            checkedConstant,
            checkedNested,
            uncheckedOperator,
            checkedOperator);
    }

    /// <summary>
    /// 12.8.21 — default value expressions: with a type argument, bare and target-typed, for a
    /// reference type, a value type, a nullable, a type parameter, and a tuple.
    /// </summary>
    /// <typeparam name="T">Named by <c>default(T)</c>.</typeparam>
    public static string DefaultValues<T>()
    {
        var ofPredefined = default(int);
        var ofDeclaredStruct = default(PxPoint);
        var ofReference = default(PxTarget);
        var ofNullable = default(int?);
        var ofTypeParameter = default(T);
        var ofTuple = default((int Left, int Right));
        var ofEnum = default(PxHue);
        int bare = default;                                  // target-typed
        PxPoint bareStruct = default;
        var inArgument = Accept(default);                    // target-typed through a parameter
        var inConditional = ofPredefined > 0 ? default : 1;

        return string.Join(
            " ",
            ofPredefined,
            ofDeclaredStruct,
            ofReference?.Seed ?? -1,
            ofNullable ?? -1,
            ofTypeParameter,
            ofTuple.Left,
            ofEnum,
            bare,
            bareStruct.X,
            inArgument,
            inConditional);
    }

    /// <summary>
    /// 12.8.22 — stack allocation: to a <c>Span&lt;T&gt;</c>, with an initializer, to a
    /// <c>ReadOnlySpan&lt;T&gt;</c>, and — the form that needs an unsafe context — to a pointer.
    /// </summary>
    public static unsafe string StackAllocation()
    {
        Span<int> spanned = stackalloc int[3];
        spanned[0] = 1;

        Span<int> initialized = stackalloc int[] { 1, 2, 3 };
        ReadOnlySpan<int> readOnly = stackalloc[] { 4, 5 };
        Span<PxPoint> ofDeclaredStruct = stackalloc PxPoint[2];

        int* pointed = stackalloc int[2];
        pointed[0] = 6;
        pointed[1] = 7;

        var conditional = spanned.Length > 0 ? stackalloc int[1] : default;

        return string.Join(
            " ",
            spanned[0],
            initialized[2],
            readOnly[1],
            ofDeclaredStruct[0].Measure(),
            pointed[0] + pointed[1],
            conditional.Length);
    }

    /// <summary>
    /// 12.8.23 — <c>nameof</c>, which binds a symbol and produces a string. Each argument here
    /// is a reference the index should hold, and none of them is evaluated.
    /// </summary>
    /// <typeparam name="T">Named by <c>nameof(T)</c>.</typeparam>
    /// <param name="argument">Named by <c>nameof(argument)</c>.</param>
    public static string NameOf<T>(int argument)
    {
        var local = 1;

        var ofLocal = nameof(local);
        var ofParameter = nameof(argument);
        var ofTypeParameter = nameof(T);
        var ofType = nameof(PxTarget);
        var ofConstructedType = nameof(PxBox<int>);
        var ofNamespace = nameof(System.Collections);
        var ofField = nameof(Threshold);
        var ofMember = nameof(PxTarget.Seed);
        var ofOverloadedMethod = nameof(PxTarget.Compute);      // a method group, not one method
        var ofGenericMethod = nameof(PxInference.Identity);
        var ofNestedMember = nameof(PxMemberAccess.Marker.Tag);
        var ofEnumMember = nameof(PxHue.Warm);
        var ofExtensionMember = nameof(PxExtensions.Doubled);
        var ofItself = nameof(NameOf);
        var inHole = $"{nameof(local)}={local}";

        return string.Join(
            " ",
            ofLocal,
            ofParameter,
            ofTypeParameter,
            ofType,
            ofConstructedType,
            ofNamespace,
            ofField,
            ofMember,
            ofOverloadedMethod,
            ofGenericMethod,
            ofNestedMember,
            ofEnumMember,
            ofExtensionMember,
            ofItself,
            inHole);
    }

    private static int Accept(int value) => value;
}
