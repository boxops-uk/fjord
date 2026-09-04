// Clause 6.4.4 (keywords). A reserved keyword can only be an identifier through the
// verbatim form; a contextual keyword is an ordinary identifier wherever the grammar does
// not give it its special meaning. The second half of this file is about names the CLI
// reserves rather than names C# does: a member whose identifier is literally the metadata
// name an operator, a property accessor, an event accessor or a finalizer compiles to.
// Those pairs sit in *sibling* types on purpose, because inside one type they are CS0082.

namespace Surface.Lexical.Tokens;

/// <summary>6.4.4: a type whose identifier is the reserved keyword <c>class</c>.</summary>
public sealed class @class
{
    /// <summary>Something to reference, so the type is reached and not merely declared.</summary>
    public const int Tag = 1;
}

/// <summary>6.4.4: a type whose identifier is the reserved keyword <c>namespace</c>.</summary>
public sealed class @namespace
{
    /// <summary>Something to reference, so the type is reached and not merely declared.</summary>
    public const int Tag = 2;
}

/// <summary>6.4.4: a type whose identifier is the reserved keyword <c>operator</c>.</summary>
public sealed class @operator
{
    /// <summary>Something to reference, so the type is reached and not merely declared.</summary>
    public const int Tag = 3;
}

/// <summary>6.4.4: a type whose identifier is the reserved keyword <c>this</c>.</summary>
public sealed class @this
{
    /// <summary>Something to reference, so the type is reached and not merely declared.</summary>
    public const int Tag = 4;
}

/// <summary>6.4.4: a type whose identifier is the reserved keyword <c>void</c>.</summary>
public sealed class @void
{
    /// <summary>Something to reference, so the type is reached and not merely declared.</summary>
    public const int Tag = 5;
}

/// <summary>
/// 6.4.4: a type named for the contextual keyword <c>var</c>, spelled without the
/// <c>@</c>. Legal, and CS8981 — the compiler reserves the right to take the name later.
/// </summary>
public sealed class var
{
    /// <summary>Something to reference, so the type is reached and not merely declared.</summary>
    public const int Tag = 6;
}

/// <summary>6.4.4: a type named for the contextual keyword <c>dynamic</c>.</summary>
public sealed class dynamic
{
    /// <summary>Something to reference, so the type is reached and not merely declared.</summary>
    public const int Tag = 7;
}

/// <summary>6.4.4: a type named for the predefined type alias <c>nint</c>.</summary>
public sealed class nint
{
    /// <summary>Something to reference, so the type is reached and not merely declared.</summary>
    public const int Tag = 8;
}

/// <summary>6.4.4: a type named for the contextual keyword <c>record</c>, which is CS8860.</summary>
public sealed class record
{
    /// <summary>Something to reference, so the type is reached and not merely declared.</summary>
    public const int Tag = 9;
}

/// <summary>
/// 6.4.4: reads every keyword-named type above. <c>var</c>, <c>dynamic</c>, <c>nint</c> and
/// <c>record</c> are reached without the <c>@</c>, which is the spelling that makes the
/// reference indistinguishable from a use of the keyword until the name is resolved.
/// </summary>
public static class LexKeywordTypeUses
{
    /// <summary>Sums the tags, reaching each keyword-named type once.</summary>
    public static int Total() =>
        @class.Tag + @namespace.Tag + @operator.Tag + @this.Tag + @void.Tag
        + var.Tag + dynamic.Tag + nint.Tag + record.Tag;
}

/// <summary>
/// 6.4.4: contextual keywords as member identifiers, needing no <c>@</c>. Three of them —
/// <c>file</c>, <c>required</c> and <c>scoped</c> — are legal member names and *illegal*
/// type names (CS9056, CS9029, CS9062), so they appear here and not above.
/// </summary>
public sealed class LexContextualKeywordMembers
{
    /// <summary>The contextual keyword that gates file-local types.</summary>
    public const int file = 1;

    /// <summary>The contextual keyword that marks a required member.</summary>
    public const int required = 2;

    /// <summary>The contextual keyword that marks a scoped reference.</summary>
    public const int scoped = 3;

    /// <summary>The accessor-body keyword, legal as a field name outside a setter.</summary>
    public const int value = 4;

    /// <summary>The iterator keyword.</summary>
    public const int yield = 5;

    /// <summary>The asynchrony keywords, legal here because this member is not async.</summary>
    public const int async = 6;

    /// <summary>The awaiting keyword, likewise.</summary>
    public const int await = 7;

    /// <summary>Query keywords.</summary>
    public const int from = 8;

    /// <summary>Query keywords.</summary>
    public const int where = 9;

    /// <summary>Query keywords.</summary>
    public const int select = 10;

    /// <summary>Query keywords.</summary>
    public const int orderby = 11;

    /// <summary>The declaration-splitting keyword.</summary>
    public const int partial = 12;

    /// <summary>Function-pointer calling-convention keywords.</summary>
    public const int managed = 13;

    /// <summary>Constraint keywords, which are not types.</summary>
    public const int unmanaged = 14;

    /// <summary>Constraint keywords, which are not types.</summary>
    public const int notnull = 15;

    /// <summary>The operator that is spelled like a call.</summary>
    public const int nameof = 16;

    /// <summary>The pattern guard keyword.</summary>
    public const int when = 17;

    /// <summary>The namespace-alias qualifier's left operand.</summary>
    public const int global = 18;

    /// <summary>The extern alias keyword.</summary>
    public const int alias = 19;

    /// <summary>Pattern combinators.</summary>
    public const int and = 20;

    /// <summary>Pattern combinators.</summary>
    public const int or = 21;

    /// <summary>Pattern combinators.</summary>
    public const int not = 22;

    /// <summary>The record-copy expression keyword.</summary>
    public const int with = 23;

    /// <summary>Accessor keywords, legal as field names outside an accessor list.</summary>
    public const int init = 24;

    /// <summary>Accessor keywords.</summary>
    public const int add = 25;

    /// <summary>Accessor keywords.</summary>
    public const int remove = 26;

    /// <summary>Accessor keywords.</summary>
    public const int get = 27;

    /// <summary>Accessor keywords.</summary>
    public const int set = 28;

    /// <summary>The native-size aliases, as member names.</summary>
    public const int nuint = 29;

    /// <summary>The entry point's implicit parameter name.</summary>
    public const int args = 30;

    /// <summary>Reads all of them, so every contextual keyword above has a reference.</summary>
    public int Total() =>
        file + required + scoped + value + yield + async + await + from + where + select
        + orderby + partial + managed + unmanaged + notnull + nameof + when + global + alias
        + and + or + not + with + init + add + remove + get + set + nuint + args;
}

/// <summary>
/// 6.4.4: members whose C# identifiers are the metadata names the CLI reserves. Nothing
/// here is an operator, a property or an event — they are ordinary methods and fields that
/// a name-keyed index cannot tell apart from the real ones in the sibling types below.
/// </summary>
public sealed class LexReservedMetadataNames
{
    /// <summary>Spelled like the metadata name of <c>operator +</c>.</summary>
    public static int op_Addition(int left, int right) => left + right;

    /// <summary>Spelled like the metadata name of a widening conversion operator.</summary>
    public static int op_Implicit(int value) => value;

    /// <summary>Spelled like the metadata name of a property getter.</summary>
    public int get_Held() => 1;

    /// <summary>Spelled like the metadata name of a property setter.</summary>
    public void set_Held(int value) { }

    /// <summary>Spelled like the metadata name of an event's add accessor.</summary>
    public void add_Raised(System.Action handler) { }

    /// <summary>Spelled like the metadata name of an event's remove accessor.</summary>
    public void remove_Raised(System.Action handler) { }

    /// <summary>Spelled like the metadata name of a finalizer. A parameter keeps it from
    /// being an override of <c>object.Finalize</c>, which would be CS0249.</summary>
    private void Finalize(int generation) { }

    /// <summary>Spelled like a compiler-generated backing field.</summary>
    private readonly int _held_BackingField;

    /// <summary>Reads the private members, so nothing here is unreferenced.</summary>
    public int Reached()
    {
        Finalize(0);
        return get_Held() + _held_BackingField;
    }
}

/// <summary>6.4.4: the real <c>operator +</c> whose metadata name the sibling type spells out.</summary>
public readonly struct LexRealOperator
{
    /// <summary>Builds one.</summary>
    public LexRealOperator(int value) => Value = value;

    /// <summary>What it holds.</summary>
    public int Value { get; }

    /// <summary>Compiles to <c>op_Addition</c>.</summary>
    public static LexRealOperator operator +(LexRealOperator left, LexRealOperator right) =>
        new(left.Value + right.Value);

    /// <summary>Compiles to <c>op_Implicit</c>.</summary>
    public static implicit operator int(LexRealOperator value) => value.Value;
}

/// <summary>6.4.4: the real property whose accessors' metadata names the sibling type spells out.</summary>
public sealed class LexRealProperty
{
    /// <summary>Compiles to <c>get_Held</c> and <c>set_Held</c>.</summary>
    public int Held { get; set; }
}

/// <summary>6.4.4: the real event whose accessors' metadata names the sibling type spells out.</summary>
public sealed class LexRealEvent
{
    /// <summary>Compiles to <c>add_Raised</c> and <c>remove_Raised</c>.</summary>
    public event System.Action? Raised;

    /// <summary>Raises it, so the field-like event is written as well as declared.</summary>
    public void Raise() => Raised?.Invoke();
}

/// <summary>6.4.4: the real finalizer whose metadata name the sibling type spells out.</summary>
public sealed class LexRealFinalizer
{
    /// <summary>Compiles to <c>Finalize</c>.</summary>
    ~LexRealFinalizer() { }
}
