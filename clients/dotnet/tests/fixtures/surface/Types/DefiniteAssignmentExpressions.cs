// Clause 9.4.4.22 through 9.4.4.34 — the definite assignment rules for expressions, and so
// the expression forms that declare variables: out-variable declarations (9.4.4.24),
// deconstructing assignment (9.4.4.25), anonymous functions (9.4.4.31), local functions
// (9.4.4.33) and is-pattern designations (9.4.4.34). The boolean operator clauses declare
// nothing and appear here for the expressions themselves.

namespace Surface.Types;

/// <summary>
/// 9.4.4.24 hazard — invocation and object creation expressions. An out-variable
/// declaration is a variable declared in the middle of an expression, and a target-typed
/// <c>new()</c> is a constructor reference with no type name written anywhere.
/// </summary>
public static class VarInvocationExpressions
{
    /// <summary>9.4.4.24 — an out variable declared at the call site, explicitly typed.</summary>
    public static int OutVariableTyped(string text) =>
        int.TryParse(text, out int parsed) ? parsed : 0;

    /// <summary>9.4.4.24 — the same with `var`, so the variable has no type token.</summary>
    public static int OutVariableInferred(string text) =>
        int.TryParse(text, out var parsed) ? parsed : 0;

    /// <summary>
    /// 9.4.4.24 hazard — two out variables named <c>parsed</c> in one method, in the two
    /// arms of a conditional. Two declarations, one name, sibling scopes inside one
    /// expression.
    /// </summary>
    public static int TwoOutVariables(string first, string second) =>
        int.TryParse(first, out int parsed)
            ? parsed
            : int.TryParse(second, out int fallback) ? fallback : 0;

    /// <summary>9.4.4.24 hazard — target-typed `new()`, whose constructor is chosen from the
    /// target type and named by no token in the expression.</summary>
    public static TyPoint2D TargetTyped()
    {
        TyPoint2D point = new(1, 2);
        return point;
    }

    /// <summary>9.4.4.24 — the same construction with the type named, for contrast.</summary>
    public static TyPoint2D Explicitly() => new TyPoint2D(1, 2);

    /// <summary>9.4.4.24 — an object creation expression with an object initializer, which
    /// assigns members rather than passing arguments.</summary>
    public static VarInstanceInClass WithInitializer() =>
        new("labelled") { Unset = 1, Reference = "set", Position = new TyPoint2D(0, 1) };

    /// <summary>9.4.4.24 — a collection initializer, and a collection expression
    /// (post-standard) which is not an object creation expression at all.</summary>
    public static (int Initializer, int Expression) Collections()
    {
        System.Collections.Generic.List<int> byInitializer = new() { 1, 2, 3 };
        System.Collections.Generic.List<int> byExpression = [1, 2, 3];
        return (byInitializer.Count, byExpression.Count);
    }

    /// <summary>9.4.4.24 — a `with` expression over a record, which copies and then assigns.</summary>
    public static TyLedgerEntry Adjusted(TyLedgerEntry entry) => entry with { Amount = 0 };

    /// <summary>9.4.4.24 — an anonymous object creation expression, whose type has no name
    /// in the source at all and whose properties are still declarations.</summary>
    public static string Anonymous()
    {
        var record = new { Account = "a", Amount = 1m };
        return $"{record.Account}{record.Amount}";
    }
}

/// <summary>
/// 9.4.4.25 hazard — simple and deconstructing assignment. This clause is the one row in
/// clause 9 that both declares and references: <c>var (row, column) = ...</c> declares two
/// variables, and <c>(row, column) = ...</c> with the same names declares none.
/// </summary>
public static class VarAssignmentExpressions
{
    /// <summary>9.4.4.25 — simple assignment, whose result is the value assigned.</summary>
    public static int SimpleAssignment()
    {
        int first;
        int second = first = 3;
        return first + second;
    }

    /// <summary>9.4.4.25 — compound assignment, which reads and writes one variable.</summary>
    public static int CompoundAssignment(int seed)
    {
        int total = seed;
        total += 1;
        total -= 2;
        total *= 3;
        total /= 4;
        total %= 5;
        total <<= 1;
        total >>= 1;
        total &= 0xFF;
        total |= 0x01;
        total ^= 0x10;
        return total;
    }

    /// <summary>9.4.4.29 / 9.4.4.25 — null-coalescing assignment, which writes only when null.</summary>
    public static string CoalescingAssignment(string? candidate)
    {
        candidate ??= "default";
        return candidate;
    }

    /// <summary>
    /// 9.4.4.25 hazard — the declaring form and the non-declaring form of the same names in
    /// one method. The first statement declares <c>row</c> and <c>column</c>; the third
    /// assigns to those same variables and declares nothing.
    /// </summary>
    public static int BothForms()
    {
        var (row, column) = new VarDeconstructable(1, 2);
        int total = row + column;
        (row, column) = (column, row);
        return total + row + column;
    }

    /// <summary>9.4.4.25 — a mixed deconstruction: one element declared, one assigned.</summary>
    public static int MixedForms()
    {
        int row = 0;
        (row, int column) = new VarDeconstructable(3, 4);
        return row + column;
    }

    /// <summary>9.4.4.25 — deconstruction with a discard (9.2.9.2) in the declaring form.</summary>
    public static int RowOnly()
    {
        var (row, _) = new VarDeconstructable(5, 6);
        return row;
    }

    /// <summary>9.4.4.25 — nested deconstruction, which recurses into an element.</summary>
    public static int Nested()
    {
        var ((left, right), tag) = new VarNestedDeconstructable();
        return left + right + tag.Length;
    }
}

/// <summary>
/// 9.4.4.25 — a type with a user-defined deconstructor. The two <c>Deconstruct</c>
/// overloads differ only in arity, which is how the language chooses between a
/// two-element and a three-element deconstruction of the same value.
/// </summary>
public sealed class VarDeconstructable
{
    public VarDeconstructable(int row, int column)
    {
        Row = row;
        Column = column;
    }

    public int Row { get; }

    public int Column { get; }

    /// <summary>9.4.4.25 — the two-element deconstructor.</summary>
    public void Deconstruct(out int row, out int column)
    {
        row = Row;
        column = Column;
    }

    /// <summary>9.4.4.25 — the three-element overload, chosen by the shape of the target.</summary>
    public void Deconstruct(out int row, out int column, out int sum)
    {
        row = Row;
        column = Column;
        sum = Row + Column;
    }
}

/// <summary>9.4.4.25 — a deconstructor whose first element is itself deconstructable.</summary>
public sealed class VarNestedDeconstructable
{
    public void Deconstruct(out VarDeconstructable inner, out string tag)
    {
        inner = new VarDeconstructable(7, 8);
        tag = "nested";
    }
}

/// <summary>
/// 9.4.4.26 through 9.4.4.30 — the boolean and conditional operators, whose definite
/// assignment rules are about which branch assigns what. None of them declares a variable.
/// </summary>
public static class VarBooleanExpressions
{
    /// <summary>9.4.4.26 — the &amp;&amp; operator, whose right operand may not run.</summary>
    public static bool AndAlso(string? text) => text is not null && text.Length > 0;

    /// <summary>9.4.4.27 — the || operator, likewise.</summary>
    public static bool OrElse(string? text) => text is null || text.Length == 0;

    /// <summary>9.4.4.28 — the ! operator, which swaps the two branches of the analysis.</summary>
    public static bool Not(bool flag) => !flag;

    /// <summary>9.4.4.29 — the ?? operator over a reference type and a nullable value type.</summary>
    public static (string Text, int Number) Coalesce(string? text, int? number) =>
        (text ?? "none", number ?? 0);

    /// <summary>9.4.4.30 — the ?: operator, whose two arms must agree on what they assign.</summary>
    public static string Conditional(int seed)
    {
        string label;
        label = seed > 0 ? "positive" : "other";
        return label;
    }

    /// <summary>9.4.4.26 — the non-short-circuiting &amp; and | over bool, which always
    /// evaluate both operands and so have a different rule.</summary>
    public static bool Eager(bool left, bool right) => (left & right) | (left ^ right);
}

/// <summary>
/// 9.4.4.31 hazard — anonymous functions. A lambda's parameters are declarations inside a
/// container that has no name, and two lambdas in one member may declare the same parameter
/// name. Captured variables belong to the enclosing method and outlive it.
/// </summary>
public static class VarAnonymousFunctions
{
    /// <summary>9.4.4.31 hazard — two lambdas in one method, each with a parameter named
    /// <c>n</c>. Two parameter declarations, one name, two unnamed containers.</summary>
    public static int TwoLambdas(int seed)
    {
        System.Func<int, int> twice = n => n * 2;
        System.Func<int, int> thrice = n => n * 3;
        return twice(seed) + thrice(seed);
    }

    /// <summary>9.4.4.31 — an anonymous method, the older syntax, whose parameter is
    /// declared the same way.</summary>
    public static int AnonymousMethod(int seed)
    {
        System.Func<int, int> negate = delegate(int n) { return -n; };
        return negate(seed);
    }

    /// <summary>9.4.4.31 — an anonymous method with no parameter list at all, which is legal
    /// only for this syntax and matches any signature.</summary>
    public static void ParameterlessAnonymousMethod() =>
        TyDelegateUse.Chain(delegate { }, delegate { })("ignored");

    /// <summary>9.4.4.31 — a lambda with a block body, explicit parameter types, and an
    /// explicit return type (post-standard).</summary>
    public static int ExplicitLambda(int seed)
    {
        var scale = int (int n, int factor) =>
        {
            int scaled = n * factor;
            return scaled;
        };
        return scale(seed, 2);
    }

    /// <summary>9.4.4.31 — a static lambda, which cannot capture, and a lambda with a
    /// default parameter value (post-standard).</summary>
    public static int StaticAndDefaulted(int seed)
    {
        System.Func<int, int> pure = static n => n + 1;
        var withDefault = (int n, int step = 5) => n + step;
        return pure(seed) + withDefault(seed);
    }

    /// <summary>
    /// 9.4.4.31 — a captured local. <c>captured</c> is declared once, read inside the
    /// lambda, and written after it, so the lambda and the method share one variable.
    /// </summary>
    public static int Captured(int seed)
    {
        int captured = seed;
        System.Func<int> read = () => captured;
        captured += 1;
        return read() + captured;
    }

    /// <summary>9.4.4.31 — an async lambda, whose body is a state machine of its own.</summary>
    public static System.Threading.Tasks.Task<int> AsyncLambda(int seed)
    {
        System.Func<int, System.Threading.Tasks.Task<int>> work =
            async n =>
            {
                await System.Threading.Tasks.Task.Yield();
                return n * 2;
            };
        return work(seed);
    }

    /// <summary>9.4.4.31 — a lambda whose parameters are both discards (9.2.9.2), so neither
    /// declares a variable. A lambda cannot be invoked in place, so it is named first.</summary>
    public static int DiscardedParameters()
    {
        System.Func<int, int, int> ignoreBoth = (int _, int _) => 0;
        return ignoreBoth(1, 2);
    }
}

/// <summary>
/// 9.4.4.33 hazard — local functions. A local function's name lives in its method's scope,
/// so it may be the same as a member of the enclosing type, and the same name may be
/// declared as a local function in two different methods.
/// </summary>
public sealed class VarLocalFunctions
{
    /// <summary>9.4.4.33 hazard — a member method named <c>Describe</c> on the enclosing type.</summary>
    public string Describe() => "member";

    /// <summary>
    /// 9.4.4.33 hazard — a local function of exactly the same simple name, which hides the
    /// member for the rest of this body.
    /// </summary>
    public string ShadowingLocalFunction()
    {
        return Describe();

        string Describe() => "local";
    }

    /// <summary>9.4.4.33 hazard — the same local function name again, in a second method.</summary>
    public string SecondShadowingLocalFunction()
    {
        return Describe() + Describe();

        string Describe() => "second";
    }

    /// <summary>9.4.4.33 — a static local function, which cannot capture, beside a capturing
    /// one, and a nested local function inside a local function.</summary>
    public int Nested(int seed)
    {
        int captured = seed;

        return Capturing() + Pure(seed);

        int Capturing()
        {
            return Inner() + captured;

            int Inner() => captured * 2;
        }

        static int Pure(int n) => n + 1;
    }

    /// <summary>9.4.4.33 — a generic local function with a constraint (8.4.5), and one with
    /// an out parameter that the enclosing body reads after the call.</summary>
    public string GenericLocalFunction<TValue>(TValue value)
        where TValue : notnull
    {
        if (TryRender(value, out string rendered))
        {
            return rendered;
        }

        return string.Empty;

        static bool TryRender<TInner>(TInner inner, out string text)
            where TInner : notnull
        {
            text = inner.ToString() ?? string.Empty;
            return text.Length > 0;
        }
    }

    /// <summary>9.4.4.33 — a local function that is an iterator, and one that is async.</summary>
    public async System.Threading.Tasks.Task<int> IteratorAndAsyncLocals()
    {
        int total = 0;
        foreach (int value in Counted())
        {
            total += value;
        }

        return total + await Delayed();

        static System.Collections.Generic.IEnumerable<int> Counted()
        {
            yield return 1;
            yield return 2;
        }

        static async System.Threading.Tasks.Task<int> Delayed()
        {
            await System.Threading.Tasks.Task.Yield();
            return 3;
        }
    }
}

/// <summary>
/// 9.4.4.34 hazard — is-pattern expressions. A pattern designation is a variable declared
/// inside an expression, and two of them may carry one name in sibling scopes of a method.
/// </summary>
public static class VarPatternExpressions
{
    /// <summary>
    /// 9.4.4.34 hazard — two type-pattern designations named <c>held</c> in one method. A
    /// designation is scoped to the enclosing block rather than to the <c>if</c>, so the two
    /// need sibling blocks to coexist: written without them the second is a redeclaration.
    /// </summary>
    public static string TwoDesignations(object subject)
    {
        {
            if (subject is int held)
            {
                return $"int {held}";
            }
        }

        {
            if (subject is string held)
            {
                return $"string {held.Length}";
            }
        }

        return "other";
    }

    /// <summary>9.4.4.34 — a declaration pattern, a constant pattern, and `not null`.</summary>
    public static string SimplePatterns(object? subject) => subject switch
    {
        null => "null",
        42 => "the answer",
        int value => $"int {value}",
        not null => "something",
    };

    /// <summary>9.4.4.34 — relational patterns joined by `and` and `or`.</summary>
    public static string RelationalPatterns(int value) => value switch
    {
        < 0 => "negative",
        >= 0 and < 10 => "small",
        >= 10 or int.MaxValue => "large",
    };

    /// <summary>9.4.4.34 — a property pattern with designations at two levels, so one
    /// expression declares three variables.</summary>
    public static string PropertyPattern(object subject) =>
        subject is TyLedgerEntry { Account: { Length: > 0 } name, Amount: var amount } entry
            ? $"{name}{amount}{entry.Account}"
            : "no";

    /// <summary>9.4.4.34 — a positional pattern, which uses the Deconstruct methods of
    /// 9.4.4.25 and declares a variable per element.</summary>
    public static int PositionalPattern(object subject) =>
        subject is VarDeconstructable(var row, var column) ? row + column : 0;

    /// <summary>9.4.4.34 — a list pattern with a slice designation (post-standard).</summary>
    public static int ListPattern(int[] values) =>
        values is [var first, .. var middle, var last]
            ? first + middle.Length + last
            : 0;

    /// <summary>9.4.4.34 — a `var` pattern, which always matches and always declares.</summary>
    public static int VarPattern(object subject) => subject is var anything && anything is not null ? 1 : 0;

    /// <summary>9.4.4.34 — a negated pattern whose designation is therefore unusable, so the
    /// pattern declares nothing at all.</summary>
    public static bool NegatedPattern(object? subject) => subject is not TyLedgerEntry;

    /// <summary>9.4.4.32 — a throw expression, which is an expression with no value and so
    /// no definite assignment consequences of its own.</summary>
    public static string ThrowExpression(string? candidate) =>
        candidate ?? throw new System.ArgumentNullException(nameof(candidate));

    /// <summary>9.4.4.32 — a throw expression in a conditional arm and in a switch arm.</summary>
    public static int ThrowInArms(int seed) => seed switch
    {
        > 0 => seed,
        0 => throw new System.ArgumentOutOfRangeException(nameof(seed)),
        _ => seed < -100 ? throw new System.OverflowException() : -seed,
    };
}
