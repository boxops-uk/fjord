using System;
using System.Collections.Generic;

namespace Surface.Expressions.Primary;

/// <summary>
/// 12.8.4 — simple names. A simple name is the one primary expression whose target is decided
/// entirely by scope, so the same spelling can name several different declarations in one
/// method body. Every declaration a name here binds to is declared in this file.
/// </summary>
public sealed class PxSimpleNames
{
    /// <summary>A field, shadowed by a local of the same name below.</summary>
    private readonly int _count = 7;

    /// <summary>A field whose name a parameter also uses.</summary>
    private readonly string _label = "field";

    /// <summary>12.8.4 — a simple name that resolves to a parameter, not to the like-named field.</summary>
    public string PreferTheParameter(string label) => label + _label;

    /// <summary>
    /// 12.8.4 — two locals of the same name in disjoint blocks of one method body. They are two
    /// declarations, they share a name and an enclosing member, and neither hides the other.
    /// </summary>
    public int TwiceDeclared()
    {
        var total = 0;

        {
            var reused = _count * 2;
            total += reused;
        }

        {
            var reused = _count * 3;
            total += reused;
        }

        return total;
    }

    /// <summary>
    /// 12.8.4 — a simple name in each of its flavours: local, parameter, member of this type,
    /// type name, namespace name, and a local function's name.
    /// </summary>
    public string EveryFlavour(int seed)
    {
        var local = seed + _count;                 // local and field, both simple names
        var type = typeof(PxTarget).Name;          // a simple name that is a type name
        var qualified = System.Math.Abs(-local);   // a simple name that is a namespace name
        var viaFunction = Local(qualified);        // a simple name that is a local function

        return $"{local} {type} {viaFunction}";

        int Local(int value) => value * 2;
    }

    /// <summary>12.5.1 — accessibility is part of member lookup: only the visible members are found.</summary>
    public int LookupSeesWhatItMay()
    {
        var visible = new PxVisibility();
        return visible.Public() + visible.InternalOnly();
    }
}

/// <summary>
/// 12.5.1 — member lookup by name, arity and accessibility. The private member is invisible to
/// every other type, and the protected one is visible only through a derived type's own body.
/// </summary>
public class PxVisibility
{
    /// <summary>Found from anywhere.</summary>
    public int Public() => 1;

    /// <summary>Found from this assembly.</summary>
    internal int InternalOnly() => 2;

    /// <summary>Found only from this type's own body.</summary>
    private int PrivateOnly() => 3;

    /// <summary>Found from this type and its derivations.</summary>
    protected int ProtectedOnly() => 4;

    /// <summary>Same-file use of the two members no other type may look up.</summary>
    public int UsedHere() => PrivateOnly() + ProtectedOnly();
}

/// <summary>12.5.1 — the derived side of accessibility: <c>ProtectedOnly</c> is in the lookup set here.</summary>
public sealed class PxVisibilityHeir : PxVisibility
{
    /// <summary>Reaches the protected member through the inherited lookup set.</summary>
    public int Reach() => ProtectedOnly() + Public();
}

/// <summary>
/// 12.8.7.2 — identical simple names and type names, the "Color Color" case. The property and
/// the enum share a spelling, and which one a use site means depends on what follows the dot.
/// </summary>
public sealed class PxPalette
{
    /// <summary>A property whose name is exactly the name of its own type.</summary>
    public PxHue PxHue { get; init; } = PxHue.Warm;

    /// <summary>
    /// 12.8.7.2 — the first use is the property, the second is the type: member lookup of
    /// <c>Cool</c> on the property's value finds nothing, so the simple name is re-read as a type.
    /// </summary>
    public string BothMeanings()
    {
        PxHue fromProperty = PxHue;
        var fromType = PxHue.Cool;
        var comparison = fromProperty == fromType ? "same" : "different";
        return $"{fromProperty} {fromType} {comparison}";
    }

    /// <summary>12.8.23 — <c>nameof</c> over the ambiguous spelling binds the property, and yields a string.</summary>
    public string NameOfTheAmbiguity() => nameof(PxHue);
}

/// <summary>
/// 12.5.1 — hiding across three levels: the same name is declared at each, so a lookup has to
/// stop at the first type that declares it.
/// </summary>
public class PxHidingRoot
{
    /// <summary>The name at the root.</summary>
    public virtual string Kind() => "root";

    /// <summary>Hidden, not overridden, in the middle level.</summary>
    public string Fixed() => "root fixed";

    /// <summary>A member the leaf hides with a different member kind entirely.</summary>
    public int Slot => 0;
}

/// <summary>12.5.1 — the middle level hides one member and overrides another.</summary>
public class PxHidingMiddle : PxHidingRoot
{
    /// <inheritdoc />
    public override string Kind() => "middle";

    /// <summary>Hides <see cref="PxHidingRoot.Fixed" /> by name.</summary>
    public new string Fixed() => "middle fixed";
}

/// <summary>12.5.1 — the leaf hides a property with a field, which member lookup must prefer.</summary>
public sealed class PxHidingLeaf : PxHidingMiddle
{
    /// <summary>A field hiding an inherited property of the same name.</summary>
    public new readonly int Slot = 9;

    /// <summary>Same-file uses: each name reaches the nearest declaration, and <c>base</c> the next one.</summary>
    public string Walk()
    {
        var slots = Slot + base.Slot;
        var fixedHere = Fixed() + base.Fixed();
        var kind = Kind();
        var hidden = ((PxHidingRoot)this).Fixed();
        return $"{slots} {fixedHere} {kind} {hidden}";
    }
}

/// <summary>
/// 12.2.1 and 12.2.2 — the classifications an expression can have, one local per classification,
/// and then the values they produce.
/// </summary>
public sealed class PxClassifications
{
    private readonly PxTarget _target = new(5);

    /// <summary>An event, so "event access" is a classification with a target here.</summary>
    public event PxNotice? Signalled;

    /// <summary>
    /// One expression per classification of 12.2.1: value, variable, namespace, type,
    /// method group, null literal, anonymous function, tuple, property access, indexer access,
    /// event access, and "nothing".
    /// </summary>
    public string EveryClassification()
    {
        var value = 1 + 2;                       // a value
        var variable = _target.Seed;             // a variable (a field access)
        var type = typeof(PxPoint);              // a type, via 12.8.18
        PxTransform methodGroup = Twice;         // a method group, converted to a delegate
        string? nullLiteral = null;              // the null literal
        Func<int, int> anonymous = x => x + 1;   // an anonymous function
        var tuple = (left: 1, right: 2);         // a tuple, via 12.8.6
        var property = _target.Scale;            // a property access
        var element = _target[1];                // an indexer access
        Signalled += Notice;                     // an event access
        Nothing();                               // an invocation classified as nothing
        Signalled -= Notice;

        return $"{value} {variable} {type.Name} {methodGroup(2)} {nullLiteral} {anonymous(1)} {tuple.left} {property} {element}";
    }

    /// <summary>
    /// 12.2.2 — every classification but "nothing" has a value, and reading it is what invokes
    /// the accessor a property or indexer access stands for.
    /// </summary>
    public int ValuesOfEach()
    {
        var fromProperty = _target.Scale;           // invokes the get accessor
        var fromIndexer = _target[0];               // invokes the indexer's get accessor
        var fromMethod = _target.Measure();         // the invocation's value
        var fromVariable = _target.Seed;            // reading a variable yields its value
        var fromMethodGroup = new PxTransform(Twice).Invoke(3);
        return fromProperty + fromIndexer + fromMethod + fromVariable + fromMethodGroup;
    }

    private static int Twice(int value) => value * 2;

    private static void Notice(string message) => GC.KeepAlive(message);

    private static void Nothing()
    {
    }
}

/// <summary>
/// 12.3.1 and 12.3.2 — the bound operations, and the two binding times. Every operation in
/// <see cref="StaticallyBound" /> is bound at compile time; the same operations in
/// <see cref="DynamicallyBound" /> are deferred to run time by a <c>dynamic</c> operand.
/// </summary>
public static class PxBinding
{
    /// <summary>12.3.1 — one of each bound operation, with the binding done at compile time.</summary>
    public static string StaticallyBound()
    {
        var target = new PxTarget(2);            // object creation
        var member = target.Seed;                // member access
        var invoked = target.Compute(member);    // method invocation
        var element = target[0];                 // element access
        PxTransform delegated = value => value;
        var throughDelegate = delegated(invoked); // delegate invocation
        var added = member + element;             // an overloadable operator
        var cast = (long)added;                   // a conversion
        var indexed = new List<int> { 1 }[0];     // element access on a constructed type
        return $"{throughDelegate} {cast} {indexed}";
    }

    /// <summary>
    /// 12.3.2 — with a <c>dynamic</c> receiver the same names are bound at run time, so the
    /// compiler records no target for them at all; 12.6.5 is the compile-time checking that
    /// remains.
    /// </summary>
    public static string DynamicallyBound(dynamic receiver)
    {
        var member = receiver.Seed;              // dynamically bound member access
        var invoked = receiver.Compute(1);       // dynamically bound invocation
        var element = receiver[0];               // dynamically bound element access
        var created = new PxTarget((int)member); // statically bound, in the same expression tree
        return $"{invoked} {element} {created.Weight}";
    }

    /// <summary>
    /// 12.6.5 — compile-time checking of dynamic member invocation: the *candidate set* is
    /// checked statically even though the choice is made at run time.
    /// </summary>
    public static int CheckedAtCompileTime()
    {
        dynamic argument = 1;
        var target = new PxTarget(3);
        return (int)target.Compute(argument);
    }
}
