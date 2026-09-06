using System;
using System.Collections.Generic;

namespace Surface.SyntaxForms.Expressions;

// The access forms: `a.b`, `a?.b`, `a[i]`, `[i] = v`, `f(x)`, `this` and `base`.
//
// These are where a reference dispatch keyed on `SimpleNameSyntax` is at its most uneven,
// and the unevenness is the point of the file:
//
//   `a.b`      the `b` is a `SimpleNameSyntax`, so the member is seen — and the walk also
//              has a `MemberAccessExpressionSyntax` case, so it is seen twice, once as a
//              cross-reference and once as a member-access location.
//   `a?.b`     the `b` is a `SimpleNameSyntax` too, so the cross-reference is written — but
//              `MemberBindingExpressionSyntax` does not derive from
//              `MemberAccessExpressionSyntax`, so the *location* fact is not. The same
//              member read, written two ways, produces two different sets of rows.
//   `a[i]`     the indexer is named by nothing. `a` and `i` are seen; the `get_Item` the
//              expression calls is not.
//   `[i] = v`  an `ImplicitElementAccessSyntax` inside an object initializer, which has no
//              receiver *and* no name.
//   `this`     a reference to the enclosing instance, which is a parameter symbol with a
//              declaration and no identifier in this expression.
//   `base`     a reference to the base type, likewise.

/// <summary>The base half of the BaseExpression row.</summary>
public class SfAccessBase
{
    /// <summary>A virtual member, so <c>base.Describe()</c> is not the same call as <c>this.Describe()</c>.</summary>
    /// <returns>What the base says.</returns>
    public virtual string Describe() => "base";

    /// <summary>A field the derived type reads through <c>base</c>.</summary>
    protected int Seed = 3;
}

/// <summary>A type with exactly one indexer, settable, for the element-access rows.</summary>
public sealed class SfIndexTarget
{
    private readonly Dictionary<int, string> _slots = new Dictionary<int, string>();

    /// <summary>The only <c>this[…]</c> on this type.</summary>
    /// <param name="slot">Which slot.</param>
    /// <returns>What is in it.</returns>
    public string this[int slot]
    {
        get => _slots.TryGetValue(slot, out var found) ? found : string.Empty;
        set => _slots[slot] = value;
    }

    /// <summary>How many slots are filled.</summary>
    public int Count => _slots.Count;
}

/// <summary>Every access form, and the two keyword expressions.</summary>
public sealed class SfAccessForms : SfAccessBase
{
    private readonly SfIndexTarget _target = new SfIndexTarget();

    /// <summary>A member to reach through <c>this</c>.</summary>
    public int Total { get; set; }

    /// <summary>ThisExpression, as a receiver, as an argument, and bare.</summary>
    /// <returns>A rendering built through <c>this</c>.</returns>
    public string Through()
    {
        // ThisExpression as the receiver of a member access.
        this.Total = 1;

        // ThisExpression as an argument, so the reference is not part of a member access.
        var described = Render(this);

        // BaseExpression as the receiver of a call and of a field read.
        var fromBase = base.Describe() + base.Seed;

        return $"{this.Total}{described}{fromBase}";
    }

    /// <summary>An override, so <c>base.Describe()</c> resolves somewhere <c>this</c> does not.</summary>
    /// <returns>What this type says.</returns>
    public override string Describe() => "derived:" + base.Describe();

    /// <summary>Renders any access host.</summary>
    /// <param name="host">The host.</param>
    /// <returns>Its description.</returns>
    private static string Render(SfAccessBase host) => host.Describe();

    /// <summary>
    /// SimpleMemberAccessExpression, MemberBindingExpression, ElementAccessExpression,
    /// ImplicitElementAccess and InvocationExpression, in one place so the pairs can be read
    /// against each other.
    /// </summary>
    /// <returns>A rendering of everything reached.</returns>
    public static string Reach()
    {
        var forms = new SfAccessForms();

        // SimpleMemberAccessExpression: a property read, a property write, a static member,
        // and a namespace-qualified type.
        forms.Total = 2;
        var read = forms.Total;
        var maxima = int.MaxValue;
        var builder = new System.Text.StringBuilder();

        // InvocationExpression: through a member access, through a bare name, and through a
        // delegate variable.
        var described = forms.Describe();
        var rendered = Render(forms);
        Func<string> viaDelegate = forms.Describe;
        var indirect = viaDelegate();

        // MemberBindingExpression: the `.Length` of `?.Length`, and the `.Describe` of
        // `?.Describe()`. Also an ElementBindingExpression, `?[0]`, which is the
        // element-access form of the same conditional.
        SfAccessForms? maybe = forms;
        var maybeDescribed = maybe?.Describe();
        var maybeTotal = maybe?.Total;
        SfIndexTarget? maybeTarget = forms._target;
        var maybeSlot = maybeTarget?[0];

        // ElementAccessExpression over a user-declared indexer, over an array, and over a
        // framework indexer.
        forms._target[0] = "first";
        var slot = forms._target[0];
        var window = new[] { 1, 2, 3 };
        var element = window[1];
        var lookup = new Dictionary<string, int> { ["key"] = 4 };
        var found = lookup["key"];

        // ImplicitElementAccess: `[1] =` inside an object initializer, which is an element
        // access with no receiver written at all.
        var seeded = new SfIndexTarget { [1] = "second", [2] = "third" };

        return $"{read}{maxima}{builder.Length}{described}{rendered}{indirect}"
            + $"{maybeDescribed}{maybeTotal}{maybeSlot}{slot}{element}{found}"
            + $"{seeded.Count}{forms.Through()}";
    }
}
