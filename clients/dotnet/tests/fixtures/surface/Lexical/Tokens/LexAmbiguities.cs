// Clause 6.2.5 (grammar ambiguities): two token sequences that the grammar alone cannot
// resolve. Both are settled by what the identifiers *mean*, which is exactly the fact an
// index has to hold — the same characters point at a type in one file position and at a
// variable in another.

namespace Surface.Lexical.Tokens;

/// <summary>A type argument for the generic reading of <c>G&lt;LexA, LexB&gt;(7)</c>.</summary>
public sealed class LexA
{
    /// <summary>Something to read, so the type is not merely a name.</summary>
    public const int Tag = 1;
}

/// <summary>The second type argument for the generic reading.</summary>
public sealed class LexB
{
    /// <summary>Something to read, so the type is not merely a name.</summary>
    public const int Tag = 2;
}

/// <summary>
/// 6.2.5: <c>F(G&lt;LexA, LexB&gt;(7))</c> — with <c>G</c> a generic method and
/// <c>LexA</c>/<c>LexB</c> types, the token sequence is one argument.
/// </summary>
public static class LexAmbiguityAsGenericCall
{
    /// <summary>The one-argument target.</summary>
    public static int F(int a) => a;

    /// <summary>The two-argument target, present so the choice is a real one.</summary>
    public static int F(bool left, bool right) => (left ? 2 : 0) + (right ? 1 : 0);

    /// <summary>The generic method that makes the angle brackets a type argument list.</summary>
    public static int G<TFirst, TSecond>(int n) => n + LexA.Tag + LexB.Tag;

    /// <summary>Resolves to <c>F(int)</c> with a single argument.</summary>
    public static int OneArgument() => F(G<LexA, LexB>(7));
}

/// <summary>
/// 6.2.5: the *same* token sequence with <c>G</c>, <c>LexA</c> and <c>LexB</c> naming
/// fields is two comparisons, so it is two arguments.
/// </summary>
public static class LexAmbiguityAsComparison
{
    /// <summary>The one-argument target, present so the choice is a real one.</summary>
    public static int F(int a) => a;

    /// <summary>The two-argument target this reading selects.</summary>
    public static int F(bool left, bool right) => (left ? 2 : 0) + (right ? 1 : 0);

    /// <summary>A field, not a method — which is what changes the parse.</summary>
    public static int G = 7;

    /// <summary>A field whose name is also a type in this namespace, and hides it here.</summary>
    public static int LexA = 1;

    /// <summary>A field whose name is also a type in this namespace, and hides it here.</summary>
    public static int LexB = 2;

    /// <summary>
    /// Resolves to <c>F(bool, bool)</c> with two arguments. The token after the
    /// <c>&gt;</c> has to be a literal: 6.2.5 retains the type argument list whenever a
    /// <c>(</c> follows, so <c>G &lt; LexA, LexB &gt; (7)</c> is CS0307 here — the parse
    /// is decided by the *following token* and not by what <c>G</c> denotes.
    /// </summary>
    public static int TwoArguments() => F(G < LexA, LexB > 7);
}

/// <summary>A type with an explicit conversion, so its name can head a cast.</summary>
public readonly struct LexTally
{
    /// <summary>Builds a tally.</summary>
    public LexTally(int value) => Value = value;

    /// <summary>What was counted.</summary>
    public int Value { get; }

    /// <summary>Lets <c>(LexTally)</c> read as a cast operator.</summary>
    public static explicit operator LexTally(int value) => new(value);
}

/// <summary>
/// 6.2.5: <c>(LexTally)(-x)</c>. A parenthesised identifier followed by <c>(</c> is a
/// cast when the identifier names a type and an invocation when it names a value, so
/// the same token shape needs the identifier resolved before it can be parsed. When
/// both a type and a local of that name are in scope the *type* wins here, which is the
/// surprising half: a local named <c>LexTally</c> does not turn this into a call, it
/// turns it into CS0029.
/// </summary>
public static class LexCastOrInvoke
{
    /// <summary>The cast reading — <c>LexTally</c> resolves to the struct above.</summary>
    public static LexTally AsCast(int x) => (LexTally)(-x);

    /// <summary>
    /// The invocation reading, spelled so that it is reachable at all. 6.2.5 makes
    /// <c>(x)(y)</c> a cast only when <c>x</c> names a type, but Roslyn commits to the cast
    /// on the token shape alone: <c>(negate)(-x)</c> is CS0118 and the qualified
    /// <c>(Holder.Negate)(-x)</c> is CS0426, both of them looking for a *type*. One token
    /// that cannot appear in a type name — here the null-forgiving <c>!</c> — is what makes
    /// the call reachable, so the invocation reading of the bare shape is unbuildable.
    /// </summary>
    public static int AsInvocation(int x)
    {
        System.Func<int, int> negate = value => -value;
        return (negate!)(-x);
    }

    /// <summary>
    /// 6.2.5 again, and the reading a reader gets wrong: <c>(name)-x</c> is a
    /// subtraction whatever <c>name</c> denotes, because <c>-</c> is not one of the
    /// tokens that keep the parenthesised name a cast. Casting a negation is CS0075
    /// unless it is parenthesised the way <c>AsCast</c> parenthesises it.
    /// </summary>
    public static int AsSubtraction(int x)
    {
        int LexTally = 10;
        return (LexTally) - x;
    }
}
