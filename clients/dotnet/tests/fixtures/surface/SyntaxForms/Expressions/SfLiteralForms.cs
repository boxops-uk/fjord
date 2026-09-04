using System;

namespace Surface.SyntaxForms.Expressions;

// The 8 literal expression kinds and the 8 primary-function kinds.
//
// A literal is the one reference-side row with nothing to resolve: `1` and `"s"` bind to no
// symbol, so an index that holds nothing about them is right rather than incomplete. They
// are here because the census counts them and because the *typed* literals are not so
// simple — `"s"u8` has type `ReadOnlySpan<byte>` and `__arglist` has no type expressible in
// C# at all.
//
// The primary-function kinds do reference something: `typeof(SfLedgerClass)` names a type,
// `sizeof(int)` names a predefined one, and `__refvalue(tr, int)` names two. The operand is
// a `TypeSyntax`, so a name operand is visible to a `SimpleNameSyntax` dispatch and a
// predefined-type operand is not.

/// <summary>Every literal form and every primary-function form.</summary>
public static class SfLiteralForms
{
    /// <summary>NumericLiteralExpression in every base and suffix the lexer accepts.</summary>
    /// <returns>Their sum, widened.</returns>
    public static double Numeric()
    {
        var decimalInteger = 42;
        var hexadecimal = 0x2A;
        var binary = 0b101010;
        var separated = 1_000_000;
        var longSuffix = 42L;
        var unsignedSuffix = 42U;
        var floatSuffix = 4.2f;
        var doubleLiteral = 4.2;
        var doubleSuffix = 4.2d;
        var decimalSuffix = 4.2m;
        var exponent = 4.2e3;
        return decimalInteger + hexadecimal + binary + separated + longSuffix
            + unsignedSuffix + floatSuffix + doubleLiteral + doubleSuffix
            + (double)decimalSuffix + exponent;
    }

    /// <summary>
    /// StringLiteralExpression in its three spellings, and Utf8StringLiteralExpression in
    /// two of them — a distinct kind, because the token is distinct.
    /// </summary>
    /// <returns>How many bytes and characters they came to.</returns>
    public static int Strings()
    {
        var plain = "quoted";
        var verbatim = @"C:\ledger\entries";
        var raw = """
            {
              "kind": "raw"
            }
            """;
        var interpolatedIsNotALiteral = $"{plain.Length}";

        // Utf8StringLiteralExpression — one kind, reached from three token kinds.
        var utf8Plain = "quoted"u8;
        var utf8Raw = """raw"""u8;

        return plain.Length + verbatim.Length + raw.Length
            + interpolatedIsNotALiteral.Length + utf8Plain.Length + utf8Raw.Length;
    }

    /// <summary>
    /// CharacterLiteralExpression, TrueLiteralExpression, FalseLiteralExpression and
    /// NullLiteralExpression — four kinds that are each a single token.
    /// </summary>
    /// <returns>A rendering of all four.</returns>
    public static string Singles()
    {
        var character = 'x';
        var escaped = '\u2014';
        var yes = true;
        var no = false;
        string? nothing = null;
        return $"{character}{escaped}{yes}{no}{nothing ?? "null"}";
    }

    /// <summary>
    /// ArgListExpression — the eighth literal kind, which is a keyword standing for the
    /// caller's variable argument list. It needs a <c>__arglist</c> parameter to be legal,
    /// so the declaration and the use are both here.
    /// </summary>
    /// <param name="first">The one fixed argument.</param>
    /// <returns>The fixed argument, since the rest are unreachable without reflection.</returns>
    public static int Varargs(int first, __arglist) => first;

    /// <summary>Calls <see cref="Varargs"/>, which is where the ArgListExpression sits.</summary>
    /// <returns>What the varargs call returned.</returns>
    public static int CallVarargs() => Varargs(1, __arglist(2, 3));

    /// <summary>
    /// The 8 primary-function kinds. Five are ordinary C#; three are the undocumented
    /// <c>TypedReference</c> keywords, which Roslyn parses and gives their own kinds.
    /// </summary>
    /// <returns>A rendering of all eight.</returns>
    public static string PrimaryFunctions()
    {
        var counter = 7;

        var typed = typeof(Declarations.SfLedgerClass);       // TypeOfExpression, name operand
        var typedPredefined = typeof(int);                    // TypeOfExpression, predefined operand
        var size = sizeof(int);                               // SizeOfExpression
        var fallback = default(Declarations.SfLedgerStruct);   // DefaultExpression
        var guarded = checked(counter + 1);                   // CheckedExpression
        var unguarded = unchecked(counter * int.MaxValue);    // UncheckedExpression

        var reference = __makeref(counter);                   // MakeRefExpression
        var referenced = __reftype(reference);                // RefTypeExpression
        var read = __refvalue(reference, int);                // RefValueExpression

        return $"{typed.Name}{typedPredefined.Name}{size}{fallback.Column}{guarded}"
            + $"{unguarded}{referenced.Name}{read}";
    }
}
