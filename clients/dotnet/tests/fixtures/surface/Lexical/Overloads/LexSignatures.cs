// Clause 7.6 (signatures and overloading). A method's signature is its name, its number of
// type parameters and its parameter types and modifiers — and *not* its return type, not
// `params`, and not which parameters are optional. Everything that is in the signature is
// varied below; everything that is not is recorded as a shape that does not compile,
// because a second declaration differing only in one of those is a duplicate.
//
// The row's hazard is generic arity: `Same`, `Same<T>` and `Same<T, U>` are three members
// of one type with one name and no parameters at all. Nothing but the arity separates
// them, which is exactly what a `Name(paramTypes)` identity leaves out.

using System.Collections.Generic;

namespace Surface.Lexical.Overloads;

/// <summary>7.6: overloads that differ only in how many type parameters they have.</summary>
public sealed class LexArityOverloads
{
    /// <summary>No type parameters, no parameters.</summary>
    public int Same() => 0;

    /// <summary>One type parameter, still no parameters.</summary>
    public int Same<TFirst>() => 1;

    /// <summary>Two type parameters, still no parameters.</summary>
    public int Same<TFirst, TSecond>() => 2;

    /// <summary>Calls all three, each disambiguated only by its type argument list.</summary>
    public int All() => Same() + Same<int>() + Same<int, string>();
}

/// <summary>7.6: overloads that differ in the parts of a signature other than arity.</summary>
public sealed class LexSignatureOverloads
{
    /// <summary>One parameter.</summary>
    public int Read(int value) => value;

    /// <summary>Two parameters, so the count differs.</summary>
    public int Read(int first, int second) => first + second;

    /// <summary>One parameter of a different type.</summary>
    public int Read(string value) => value.Length;

    /// <summary>One parameter whose type is a nullable value type, which is a distinct type.</summary>
    public int Read(int? value) => value ?? 0;

    /// <summary>One parameter passed by reference, which is part of the signature.</summary>
    public int Read(ref int value) => value++;

    /// <summary>One parameter passed out, which differs from <c>ref</c> by more than a keyword
    /// only because the parameter count is different here.</summary>
    public int Read(out int value, int seed)
    {
        value = seed;
        return value;
    }

    /// <summary>One parameter passed by readonly reference.</summary>
    public int Read(in long value) => (int)value;

    /// <summary>Two distinct instantiations of one generic type, which are two types.</summary>
    public int Read(List<int> values) => values.Count;

    /// <summary>The other instantiation.</summary>
    public int Read(List<string> values) => values.Count;

    /// <summary>An array parameter, whose rank is part of the type.</summary>
    public int Read(int[] values) => values.Length;

    /// <summary>The rank-two array, which is a different type from the rank-one one.</summary>
    public int Read(int[,] values) => values.Length;

    /// <summary>A generic method whose parameter is its own type parameter, applicable
    /// wherever <c>Read(int)</c> is and less specific than it.</summary>
    public int Read<TValue>(TValue value) => value is null ? 0 : 1;

    /// <summary>
    /// A generic method whose type parameter appears in the second position — distinct
    /// from the one above, and ambiguous with it at a call like <c>Pair(1, 1)</c>, which
    /// is why no such call is written.
    /// </summary>
    public int Pair<TValue>(TValue first, int second) => second;

    /// <summary>The mirror image, whose signature differs by where the type parameter is.</summary>
    public int Pair<TValue>(int first, TValue second) => first;

    /// <summary>
    /// Calls the overloads whose resolution is unambiguous, so each declaration above has
    /// a reference and the ambiguous pair has none.
    /// </summary>
    public int CallThem()
    {
        int byRef = 1;
        return Read(1)
            + Read(1, 2)
            + Read("abc")
            + Read((int?)4)
            + Read(ref byRef)
            + Read(out int written, 5)
            + written
            + Read(in Sixty)
            + Read(new List<int>())
            + Read(new List<string>())
            + Read(new int[1])
            + Read(new int[1, 1])
            + Read<double>(8.0)
            + Pair<string>("a", 9)
            + Pair<string>(10, "b");
    }

    /// <summary>An <c>in</c> parameter wants a variable, so here is one.</summary>
    private static readonly long Sixty = 60;
}

/// <summary>
/// 7.6: constructors and operators overload on the same rule, and an indexer overloads on
/// its parameter list. This type declares one indexer only, because a second
/// <c>this[...]</c> is one of the five shapes the corpus quarantines.
/// </summary>
public sealed class LexOverloadedConstructors
{
    /// <summary>A parameterless constructor.</summary>
    public LexOverloadedConstructors() => Held = 0;

    /// <summary>One parameter.</summary>
    public LexOverloadedConstructors(int held) => Held = held;

    /// <summary>Two parameters.</summary>
    public LexOverloadedConstructors(int held, int extra) => Held = held + extra;

    /// <summary>One parameter of a different type, so the count is not the only difference.</summary>
    public LexOverloadedConstructors(string held) => Held = held.Length;

    /// <summary>What every constructor sets.</summary>
    public int Held { get; }

    /// <summary>The only indexer.</summary>
    public int this[int offset] => Held + offset;

    /// <summary>Reaches each constructor, so none is unreferenced.</summary>
    public static int AllFour() =>
        new LexOverloadedConstructors().Held
        + new LexOverloadedConstructors(1).Held
        + new LexOverloadedConstructors(1, 2).Held
        + new LexOverloadedConstructors("abc")[3];
}

/// <summary>
/// 7.6: the differences that are *not* signature differences, each named with the error a
/// second declaration would produce. Nothing here is a second declaration — the point of
/// the type is that these members' would-be twins cannot be written.
/// </summary>
public sealed class LexNotSignatureDifferences
{
    /// <summary>A second declaration returning <c>string</c> would be CS0111: the return
    /// type is not part of the signature.</summary>
    public int ReturnTypeOnly() => 1;

    /// <summary>A second declaration taking <c>params int[]</c> would be CS0663:
    /// <c>params</c> is not part of the signature.</summary>
    public int ParamsOnly(int[] values) => values.Length;

    /// <summary>A second declaration with <c>value = 0</c> would be CS0111: which
    /// parameters are optional is not part of the signature.</summary>
    public int OptionalOnly(int value) => value;

    /// <summary>A second declaration taking <c>out int</c> would be CS0663: <c>ref</c>
    /// and <c>out</c> differ in the signature only from a by-value parameter, not from
    /// each other.</summary>
    public int RefVersusOut(ref int value) => value;

    /// <summary>A second declaration taking <c>TValue</c> named differently would be
    /// CS0111: a type parameter's name is not part of the signature, only the count is.</summary>
    public int TypeParameterNameOnly<TValue>(TValue value) => value is null ? 0 : 1;

    /// <summary>Calls each, so every declaration above has a reference.</summary>
    public int CallThem()
    {
        int byRef = 2;
        return ReturnTypeOnly()
            + ParamsOnly([1, 2])
            + OptionalOnly(3)
            + RefVersusOut(ref byRef)
            + TypeParameterNameOnly("abc");
    }
}
