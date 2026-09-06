// Clauses 21.2 and 21.5 against delegate types the corpus does not declare. `System.Action`
// and `System.Func` are the delegate declarations every C# program actually uses, and they
// come from metadata rather than from source: the references below are the only way this
// corpus exercises a delegate type it did not write. They are also, taken together, the
// arity hazard arriving from outside — `Action` and `Action<T>` are one name at two arities,
// and `Func` has seventeen — which no source file here could contain without killing the
// indexing run.

using System;

namespace Surface.EnumsDelegates;

/// <summary>
/// 21.5 — instantiation of framework delegate types from lambdas and from method groups, at
/// enough arities to show that the arity is part of the type's identity.
/// </summary>
public static class EdFrameworkDelegates
{
    /// <summary>21.5 — <c>Action</c>, the nullary void delegate.</summary>
    public static Action Nothing() => static () => { };

    /// <summary>21.5 hazard — <c>Action&lt;T&gt;</c>, the same name at arity one.</summary>
    public static Action<string> One() => static text => { };

    /// <summary>21.5 hazard — <c>Action&lt;T1, T2&gt;</c>, the same name at arity two.</summary>
    public static Action<int, int> Two() => static (left, right) => { };

    /// <summary>21.5 — <c>Func&lt;TResult&gt;</c>, whose one type argument is the return type.</summary>
    public static Func<int> Produce() => static () => 1;

    /// <summary>21.5 hazard — <c>Func&lt;T, TResult&gt;</c>, the same name at arity two.</summary>
    public static Func<int, string> Map() => static value => value.ToString();

    /// <summary>21.5 hazard — <c>Func&lt;T1, T2, TResult&gt;</c>, at arity three.</summary>
    public static Func<int, int, int> Fold() => static (left, right) => left + right;

    /// <summary>21.5 — <c>Predicate&lt;T&gt;</c>, which is Func&lt;T, bool&gt; under another name.</summary>
    public static Predicate<int> Positive() => static value => value > 0;

    /// <summary>21.5 — <c>Comparison&lt;T&gt;</c>, a delegate the framework declares for sorting.</summary>
    public static Comparison<int> Descending() => static (left, right) => right.CompareTo(left);

    /// <summary>21.5 — <c>Converter&lt;TIn, TOut&gt;</c>, structurally EdMapper's twin.</summary>
    public static Converter<int, string> Convert() => static value => value.ToString();

    /// <summary>21.5 — <c>EventHandler</c>, the delegate the event pattern is built on.</summary>
    public static EventHandler Ignore() => static (sender, args) => { };

    /// <summary>
    /// 21.5 hazard — <c>EventHandler&lt;T&gt;</c> constructed over an enum declared in this
    /// project, so a framework generic and a corpus enum meet in one type argument.
    /// </summary>
    public static EventHandler<EdColor> Typed() => static (sender, color) => { };

    /// <summary>21.5 — a framework delegate over two of this project's enums.</summary>
    public static Func<EdColor, EdAccess> Bridge() => static color =>
        color == EdColor.Red ? EdAccess.Read : EdAccess.None;

    /// <summary>
    /// 21.5 hazard — a method group from metadata, and an overloaded one:
    /// <c>Console.WriteLine</c> has some twenty declarations and the conversion resolves to
    /// the one taking a string.
    /// </summary>
    public static Action<string> FromMetadataGroup() => Console.WriteLine;

    /// <summary>21.6 — invocation of a framework delegate, which binds to its `Invoke`.</summary>
    public static int Apply(Func<int, int, int> fold) => fold(14, 15);

    /// <summary>21.5 — a corpus delegate and a framework delegate over one method group.</summary>
    public static (EdCombine Corpus, Func<int, int, int> Framework) BothKinds() =>
        (EdCalculator.Add, EdCalculator.Add);

    /// <summary>
    /// 21.4 — a framework delegate reached from a corpus one by a delegate-creation
    /// expression, which is the only conversion between two identical signatures.
    /// </summary>
    public static Func<int, int, int> Recreate(EdCombine combine) =>
        new Func<int, int, int>(combine);
}
