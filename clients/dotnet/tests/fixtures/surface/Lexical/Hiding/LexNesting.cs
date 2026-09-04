// Clause 7.7.2.2 (hiding through nesting). A name declared in a nested scope hides the
// same name from the enclosing one, wherever the two scopes are nested: a nested type
// inside a type, a type parameter inside a type parameter's scope, a local or a parameter
// inside a member.
//
// The pair to check is `LexOuterScope<T>.Inner<T>`. Two type parameters spelled `T`, one
// nested inside the other's scope, is CS0693 and legal — and an index that names a type
// parameter by its own name plus the name of the type that declares it has to reach for
// the *nested* type's name to keep them apart.

namespace Surface.Lexical.Hiding;

/// <summary>7.7.2.2: a type whose name a nested type also uses.</summary>
public class LexHidden
{
    /// <summary>Something to reference, so the outer type is reached.</summary>
    public const int Tag = 1;
}

/// <summary>
/// 7.7.2.2: an outer type parameter hidden by a nested one of the same name. Inside
/// <c>Inner</c>, <c>T</c> is <c>Inner</c>'s parameter and the outer one is unreachable.
/// </summary>
/// <typeparam name="T">The outer type parameter.</typeparam>
public class LexOuterScope<T>
{
    /// <summary>The outer parameter's own field, so both parameters are used.</summary>
    public T? Outer;

    /// <summary>
    /// 7.7.2.2: the nested type whose parameter hides the outer one — CS0693, and legal.
    /// </summary>
    /// <typeparam name="T">The nested type parameter, which hides the outer one.</typeparam>
    public class Inner<T>
    {
        /// <summary>Typed by the *nested* parameter, which is the one in scope here.</summary>
        public T? Held;
    }

    /// <summary>
    /// 7.7.2.2: a nested type that does *not* hide the outer parameter, so both are
    /// reachable and the difference is visible in one file.
    /// </summary>
    public class Sibling
    {
        /// <summary>Typed by the outer parameter, which is still in scope here.</summary>
        public T? Held;
    }

    /// <summary>7.7.2.2: a nested type whose name hides the namespace-level
    /// <c>LexHidden</c>, so the simple name resolves differently inside this type.</summary>
    public class LexHidden
    {
        /// <summary>A different value from the namespace-level type's.</summary>
        public const int Tag = 2;
    }

    /// <summary>Resolves <c>LexHidden</c> to the nested type, because it is nearer.</summary>
    public int NearerName() => LexHidden.Tag;

    /// <summary>Reaches the hidden namespace-level type, which needs the qualification.</summary>
    public int FurtherName() => Surface.Lexical.Hiding.LexHidden.Tag;
}

/// <summary>
/// 7.7.2.2: a field hidden by a parameter, by a local and by a pattern variable in turn.
/// Each hiding scope is smaller than the last, and all four declarations are spelled
/// <c>held</c>.
/// </summary>
public sealed class LexNestedHiding
{
    /// <summary>The field every declaration below hides.</summary>
    private readonly int held = 1;

    /// <summary>The parameter hides the field for the whole of this method's body.</summary>
    public int HiddenByParameter(int held) => held + this.held;

    /// <summary>The local hides the field from its declaration to the end of the block.</summary>
    public int HiddenByLocal()
    {
        int held = 2;
        return held + this.held;
    }

    /// <summary>The pattern variable hides the field inside the <c>if</c> only.</summary>
    public int HiddenByPattern(object value)
    {
        if (value is int held)
        {
            return held + this.held;
        }

        return this.held;
    }

    /// <summary>The lambda's parameter hides the field inside the lambda only.</summary>
    public int HiddenByLambdaParameter()
    {
        System.Func<int, int> f = held => held + 1;
        return f(this.held);
    }
}
