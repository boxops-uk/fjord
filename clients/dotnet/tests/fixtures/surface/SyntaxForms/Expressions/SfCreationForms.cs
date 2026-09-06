using System;
using System.Collections;
using System.Collections.Generic;
using Surface.SyntaxForms.Declarations;

namespace Surface.SyntaxForms.Expressions;

// The creation forms: `new T(…)`, `new()`, `new { … }`, and the two initializer kinds that
// only exist inside one of them.
//
// `ObjectCreationExpressionSyntax` and `ImplicitObjectCreationExpressionSyntax` share the
// base the walk's `BaseObjectCreationExpressionSyntax` case names, so both are seen as
// constructions — but only the first has a type name in it, so only the first also produces
// a reference to the type. `new()` is the smallest expression in C# that references a
// constructor and contains no identifier.
//
// `Argument` is a census row twice over: a named argument in a call (`Post(amount: 1)`) and
// a named element in a tuple literal, where the name *declares* a field rather than
// selecting a parameter.

/// <summary>A collection with an <c>Add</c> method, for the collection-initializer row.</summary>
public sealed class SfBag : IEnumerable<int>
{
    private readonly List<int> _items = new List<int>();

    /// <summary>What the initializer syntax calls.</summary>
    /// <param name="item">The item to hold.</param>
    public void Add(int item) => _items.Add(item);

    /// <summary>How many items are held.</summary>
    public int Count => _items.Count;

    /// <inheritdoc/>
    public IEnumerator<int> GetEnumerator() => _items.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>Every creation form, and the initializer kinds inside them.</summary>
public static class SfCreationForms
{
    /// <summary>
    /// ObjectCreationExpression in each of its shapes: with arguments, with an object
    /// initializer, with a collection initializer, and with neither.
    /// </summary>
    /// <returns>A fold of what was built.</returns>
    public static int Explicit()
    {
        // With an argument list, which is what makes this a reference to a constructor and
        // not only to a type.
        var host = new SfMemberHost(5);

        // With an argument list and an object initializer, so the `Label =` inside is an
        // assignment to a member rather than an argument.
        var labelled = new SfMemberHost(5) { Label = "explicit" };

        // With no argument list at all, which still references the parameterless constructor.
        var bare = new SfMemberHost();

        // CollectionInitializerExpression — `{ 1, 2, 3 }`, whose every element is an
        // argument to a call of `SfBag.Add` that no name in this expression mentions.
        var bag = new SfBag { 1, 2, 3 };

        // A nested collection initializer, over a framework type, so the `Add` being called
        // takes two arguments and the braces are the argument list.
        var pairs = new Dictionary<string, int> { { "one", 1 }, { "two", 2 } };

        // A generic construction, so the created type is a `GenericName`.
        var box = new SfLedgerBox<string>("held");

        return host.Size + labelled.Label.Length + bare.Size + bag.Count
            + pairs.Count + box.Held.Length;
    }

    /// <summary>
    /// ImplicitObjectCreationExpression — <c>new()</c>, in each position where the target
    /// type is known: a local with a stated type, a field initializer, an argument, and a
    /// return.
    /// </summary>
    /// <returns>A fold of what was built.</returns>
    public static int Implicit()
    {
        SfMemberHost host = new(7);
        SfBag bag = new() { 4, 5 };
        SfLedgerBox<int> box = new(9);
        var described = Describe(new(11));

        return host.Opening + bag.Count + box.Held + described.Length;
    }

    /// <summary>Takes a host, so <c>new(11)</c> has a target type as an argument.</summary>
    /// <param name="host">The host.</param>
    /// <returns>Its description.</returns>
    private static string Describe(SfMemberHost host) => host.Describe();

    /// <summary>
    /// AnonymousObjectCreationExpression and AnonymousObjectMemberDeclarator.
    /// </summary>
    /// <remarks>
    /// <b>Each shape is written once, and that is deliberate.</b> Two anonymous-object
    /// creations with the same member names and types in one compilation are the *same*
    /// symbol — one type, one set of properties, two declaring syntaxes. A producer that
    /// keys a definition on the symbol and takes its value from the syntax would mint one
    /// identity twice with two different values, which is a refused write rather than a
    /// merge. Nothing in the walk reaches an `AnonymousObjectMemberDeclaratorSyntax` today,
    /// so the shape is a prediction rather than an observation — and a prediction recorded
    /// beside three distinct shapes is worth more than a run that dies proving it.
    /// </remarks>
    /// <returns>A rendering of each anonymous object.</returns>
    public static string Anonymous()
    {
        // Member declarators with `NameEquals`, which is where the property name is
        // declared. The identifier in `Name =` is an `IdentifierNameSyntax` bound to the
        // synthesised property — a reference to a declaration the index does not hold.
        var named = new { Label = "first", Weight = 1 };

        // A declarator with no `NameEquals`: the property name is inferred from the
        // expression, so `Total` below declares a property called `Total` with no
        // identifier of its own in the declarator.
        var host = new SfMemberHost(2);
        var inferred = new { host.Total, host.Opening };

        // A third shape, so no two of the three unify.
        var mixed = new { Key = "third", host.Size };

        return $"{named.Label}{named.Weight}{inferred.Total}{inferred.Opening}"
            + $"{mixed.Key}{mixed.Size}";
    }

    /// <summary>
    /// Argument in its two census readings: a named argument, which selects a parameter, and
    /// a named tuple element, which declares a field.
    /// </summary>
    /// <returns>A fold of both.</returns>
    public static int Arguments()
    {
        var forms = new SfParameterForms(seed: 11);

        int byRef = 0;
        var every = forms.Every(
            plain: 1,
            byRef: ref byRef,
            produced: out var produced,
            borrowed: in byRef,
            pinned: in byRef,
            optional: 2,
            rest: new[] { 3, 4 });

        // A named element in a tuple *literal*: `Fees:` and `Net:` declare fields on the
        // constructed `ValueTuple<int, int>` rather than naming parameters.
        var split = (Fees: every / 10, Net: every - (every / 10));

        return produced + split.Fees + split.Net + forms[0];
    }
}
