// Clause 15.7.6 (virtual, sealed, override and abstract accessors). The modifier goes on the
// *property* and applies to both accessors — there is no way to make a getter virtual and its
// setter not — but an override may implement fewer accessors than the base declares only when
// the base is get-only. So this clause is where a property's two emitted members are forced to
// travel together and a query for "the override of get_Weight" has to answer through a
// declaration whose name is `Weight`.
//
// The hazard: `Weight` is declared three times here with one name and one type, separated only
// by containing type, and the three declarations carry six emitted accessors. `base.Weight` is
// the reference that names the middle one.

namespace Surface.Classes.Members.Properties;

/// <summary>15.7.6: abstract and virtual properties in an abstract type.</summary>
public abstract class MemPropertyRoot
{
    /// <summary>15.7.6: an abstract property — both accessors abstract, both bodiless.</summary>
    public abstract int Weight { get; set; }

    /// <summary>15.7.6: an abstract get-only property, so an override may add nothing.</summary>
    public abstract string Label { get; }

    /// <summary>15.7.6: a virtual property with bodies, which an override may replace and may
    /// call through `base`.</summary>
    public virtual int Scaled
    {
        get => Weight * 2;
        set => Weight = value / 2;
    }

    /// <summary>15.7.6: a virtual auto-property, so the generated accessors are the virtual
    /// ones and the generated field is not.</summary>
    public virtual int Offset { get; set; }

    /// <summary>15.7.6: a non-virtual property, which an override may not target.</summary>
    public int Fixed => 1;
}

/// <summary>15.7.6: the overrides. One of them narrows an accessor's accessibility, which an
/// override may do only in the direction the base already allows.</summary>
public class MemPropertyMiddle : MemPropertyRoot
{
    /// <summary>15.7.6: an override of an abstract property, supplying both accessors as an
    /// automatic pair.</summary>
    public override int Weight { get; set; }

    /// <summary>15.7.6: an override of the get-only abstract property, with an expression
    /// body.</summary>
    public override string Label => $"weight {Weight}";

    /// <summary>15.7.6: an override that calls the base accessors, so both halves of the
    /// replaced pair are referenced.</summary>
    public override int Scaled
    {
        get => base.Scaled + 1;
        set => base.Scaled = value - 1;
    }

    /// <summary>15.7.6: a new virtual property introduced part-way down, so `virtual` and
    /// `override` sit on one type.</summary>
    public virtual int Margin { get; set; }
}

/// <summary>15.7.6: the end of the chain — sealed overrides, which may not be overridden
/// again.</summary>
public sealed class MemPropertyLeaf : MemPropertyMiddle
{
    /// <summary>15.7.6: `sealed override` on a property, so both accessors are sealed by one
    /// modifier.</summary>
    public sealed override int Scaled
    {
        get => base.Scaled * 2;
        set => base.Scaled = value / 2;
    }

    /// <summary>15.7.6: a sealed override of the property introduced one level up.</summary>
    public sealed override int Margin
    {
        get => base.Margin;
        set => base.Margin = value;
    }

    /// <summary>15.7.6: an override of a virtual auto-property that turns it into a computed
    /// one, so the generated field one level up becomes unreachable.</summary>
    public override int Offset
    {
        get => Weight;
        set => Weight = value;
    }

    /// <summary>15.7.6: every accessor reached through the static type that selects it.</summary>
    public string UseAll()
    {
        Weight = 4;
        Scaled = 8;
        Margin = 2;
        Offset = 6;
        MemPropertyRoot root = this;
        root.Weight = 5;
        return $"{Weight} {Label} {Scaled} {Margin} {Offset} {Fixed} {root.Scaled} {root.Offset}";
    }
}
