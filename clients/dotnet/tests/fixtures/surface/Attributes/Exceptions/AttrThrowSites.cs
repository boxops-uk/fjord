// Clause 22.2 (causes of exceptions) and 22.5 (common exception classes).
//
// 22.2 names exactly two causes, and the two are unlike each other in the index: a `throw`
// statement carries a reference to a constructor, while an abnormal condition carries no
// reference at all — the type that will be raised appears nowhere in the source. So the
// methods below come in two families, and a query that counts references to
// DivideByZeroException must find the ones in `Raises` and none in `Provokes`.
//
// The hazard is in `Raises`: three `throw new AttrLedgerFault(message)` statements sit in
// one method with one argument shape. Three references, one enclosing member, one target
// constructor — an identity string minted from (enclosing member, target) is one string
// for three of them, and only a source position separates them.
//
// 22.5's list is referenced rather than declared, and it is referenced three ways on
// purpose: constructed and thrown, named in a `catch`, and named in `typeof`. The three
// are different syntax reaching one declaration, which is the thing worth asserting.

using System;

namespace Surface.Attributes.Exceptions;

/// <summary>22.2, first cause: a <c>throw</c> statement, which names its exception.</summary>
public static class AttrThrowSites
{
    /// <summary>
    /// 22.2: three throw statements of one shape, plus a throw *expression*, which is the
    /// same cause reached through 12.17 rather than through a statement.
    /// </summary>
    public static int Raises(int mode, string? label)
    {
        if (mode == 1)
        {
            throw new AttrLedgerFault("mode one is refused");
        }

        if (mode == 2)
        {
            throw new AttrLedgerFault("mode two is refused");
        }

        if (mode == 3)
        {
            throw new AttrLedgerFault("mode three is refused");
        }

        // The throw expression: a reference to the same constructor from an expression
        // position, where there is no statement to hang a source range on.
        return (label ?? throw new AttrLedgerFault("a label is required")).Length;
    }

    /// <summary>
    /// 22.2: the wrapping form, where the exception being raised references the one caught.
    /// </summary>
    public static void RaisesWrapped(string posting)
    {
        try
        {
            _ = int.Parse(posting);
        }
        catch (FormatException malformed)
        {
            throw new AttrPostingFault($"posting {posting} is malformed", malformed.Message.Length);
        }
    }
}

/// <summary>
/// 22.2, second cause: an abnormal condition. Every method here raises an exception of a
/// 22.5 class that its own source never names.
/// </summary>
public static class AttrImplicitCauses
{
    /// <summary>The field is null, so the dereference raises NullReferenceException.</summary>
    private static readonly string? Missing = null;

    /// <summary>22.5: <c>NullReferenceException</c>, named nowhere in this method.</summary>
    public static int Dereferences() => Missing!.Length;

    /// <summary>22.5: <c>DivideByZeroException</c>, from integer division.</summary>
    public static int Divides(int numerator, int denominator) => numerator / denominator;

    /// <summary>22.5: <c>OverflowException</c>, because the context is checked.</summary>
    public static int Overflows(int step) => checked(int.MaxValue + step);

    /// <summary>22.5: <c>IndexOutOfRangeException</c>, from an element access.</summary>
    public static int Indexes(int[] cells, int index) => cells[index];

    /// <summary>22.5: <c>InvalidCastException</c>, from an unboxing reference conversion.</summary>
    public static string Casts(object boxed) => (string)boxed;

    /// <summary>
    /// 22.5: <c>ArrayTypeMismatchException</c> — the covariant reference is legal and the
    /// store through it is not, which is a check only the runtime can make.
    /// </summary>
    public static void Stores(string[] labels)
    {
        object[] widened = labels;
        widened[0] = 42;
    }
}

/// <summary>
/// 22.5: every class the clause lists, reached by <c>catch</c> and by <c>typeof</c> as well
/// as by construction, and one reached only by name.
/// </summary>
public static class AttrCommonExceptions
{
    /// <summary>The classes 22.5 lists, as type references rather than as throws.</summary>
    public static Type[] TheList() =>
    [
        typeof(Exception),
        typeof(SystemException),
        typeof(ArgumentException),
        typeof(ArgumentNullException),
        typeof(ArgumentOutOfRangeException),
        typeof(ArithmeticException),
        typeof(ArrayTypeMismatchException),
        typeof(DivideByZeroException),
        typeof(IndexOutOfRangeException),
        typeof(InvalidCastException),
        typeof(InvalidOperationException),
        typeof(NullReferenceException),
        typeof(NotSupportedException),
        typeof(OutOfMemoryException),
        typeof(OverflowException),
        typeof(StackOverflowException),
        typeof(TypeInitializationException),
    ];

    /// <summary>
    /// 22.5: the same classes constructed and thrown. Each arm is a reference to a
    /// constructor of a framework type, which is the reference kind 23.4.2 also produces.
    /// </summary>
    public static void RaiseOneOf(int which) => throw which switch
    {
        0 => new ArgumentException("a bad argument", nameof(which)),
        1 => new ArgumentNullException(nameof(which)),
        2 => new ArgumentOutOfRangeException(nameof(which), which, "out of the allowed range"),
        3 => new ArithmeticException("arithmetic refused"),
        4 => new ArrayTypeMismatchException("the element type does not match"),
        5 => new DivideByZeroException("a zero denominator"),
        6 => new IndexOutOfRangeException("past the end"),
        7 => new InvalidCastException("no such conversion"),
        8 => new InvalidOperationException("not in a state to do that"),
        9 => new NullReferenceException("a null reference"),
        10 => new NotSupportedException("not supported here"),
        11 => new OutOfMemoryException("no room"),
        12 => new OverflowException("does not fit"),
        13 => new StackOverflowException("too deep"),
        14 => new TypeInitializationException(nameof(AttrCommonExceptions), null),
        _ => new Exception("something else"),
    };
}
