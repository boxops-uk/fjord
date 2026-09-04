// Clause 21.4 — delegate compatibility. A method is compatible with a delegate type when the
// parameter counts agree, each by-value parameter type of the delegate is implicitly
// reference-convertible to the method's, each by-reference parameter type is identical and
// carries the same modifier, and the method's return type is implicitly reference-convertible
// to the delegate's. That is contravariance in the parameters and covariance in the return —
// and separately, two constructed forms of one variant delegate type are convertible to each
// other by the same rule applied to the type arguments.

namespace Surface.EnumsDelegates;

/// <summary>
/// 21.4 — every compatibility rule the clause states, as an assignment that the compiler
/// accepts because of it.
/// </summary>
public static class EdCompatibility
{
    /// <summary>21.4 — a method whose parameter type is a base of the delegate's.</summary>
    public static void AcceptObject(object value)
    {
    }

    /// <summary>
    /// 21.4 hazard — parameter contravariance in a method group conversion. The delegate
    /// takes <c>string</c> and the method takes <c>object</c>, so the signatures are not
    /// identical and the conversion is still exact: an index that pairs a delegate with its
    /// target by signature equality finds nothing here.
    /// </summary>
    public static EdNotifyLeft Contravariant() => AcceptObject;

    /// <summary>21.4 — a method whose return type derives from the delegate's.</summary>
    public static string ProduceString() => "produced";

    /// <summary>
    /// 21.4 hazard — return covariance in a method group conversion. The delegate returns
    /// <c>object</c>, the method returns <c>string</c>.
    /// </summary>
    public static EdMakeObject Covariant() => ProduceString;

    /// <summary>21.4 — one method, compatible with two unrelated delegate types.</summary>
    public static void Announce(string message)
    {
    }

    /// <summary>21.4 hazard — the left of that pair.</summary>
    public static EdNotifyLeft Left() => Announce;

    /// <summary>
    /// 21.4 hazard — the right of it. One method declaration is the target of two delegate
    /// types whose signatures are identical, so three declarations want to be related and
    /// only the two delegate types are distinguishable by name.
    /// </summary>
    public static EdNotifyRight Right() => Announce;

    /// <summary>
    /// 21.4 hazard — a variance conversion between two constructed forms of
    /// <see cref="EdSink{T}"/>. There is no cast token and no method group: the whole
    /// conversion is licensed by the `in` on the declaration's type parameter.
    /// </summary>
    public static EdSink<string> Narrow(EdSink<object> wide) => wide;

    /// <summary>21.4 hazard — the covariant direction, licensed by the `out` on EdSource.</summary>
    public static EdSource<object> Widen(EdSource<string> narrow) => narrow;

    /// <summary>
    /// 21.4 — both directions at once, on the one declaration that annotates a type
    /// parameter of each kind. <see cref="EdMapper{TIn,TOut}"/> is invariant and admits no
    /// such conversion, which is why this needs a second declaration to be shown at all.
    /// </summary>
    public static EdVariantMapper<string, object> Both(EdVariantMapper<object, string> map) =>
        map;

    /// <summary>21.4 — a method whose by-reference modifier matches the delegate's exactly.</summary>
    public static bool TryRead(string text, out int value) => int.TryParse(text, out value);

    /// <summary>
    /// 21.4 — the conversion that requires exactness: an `out` parameter admits no variance,
    /// so this is the only shape of method compatible with <see cref="EdTryParse"/>.
    /// </summary>
    public static EdTryParse Exact() => TryRead;

    /// <summary>21.4 — a method with a `ref` parameter, matching EdMutate.</summary>
    public static void Bump(ref int slot) => slot++;

    /// <summary>21.4 — the conversion, which likewise admits no variance.</summary>
    public static EdMutate Reference() => Bump;

    /// <summary>21.4 — a method with an `in` parameter, matching EdScale.</summary>
    public static double Double(in double factor) => factor * 2;

    /// <summary>21.4 — the conversion of a read-only by-reference parameter.</summary>
    public static EdScale ReadOnlyReference() => Double;

    /// <summary>
    /// 21.4 hazard — two delegate types with one signature and no conversion between them. A
    /// delegate-creation expression is the only way across, and it makes a *new* instance
    /// whose target is the first delegate's target method, not the first delegate itself.
    /// </summary>
    public static EdNotifyRight Recreate(EdNotifyLeft left) => new EdNotifyRight(left);

    /// <summary>
    /// 21.4 — compatibility does not care whether the target is static or instance, so an
    /// instance method group converts to the same delegate type as a static one.
    /// </summary>
    public sealed class EdCompatibleInstance
    {
        /// <summary>21.4 — the instance method the conversion below targets.</summary>
        public void Report(string message)
        {
        }

        /// <summary>21.4 — the conversion, whose result carries this object as its target.</summary>
        public EdNotifyLeft Bound() => Report;
    }
}
