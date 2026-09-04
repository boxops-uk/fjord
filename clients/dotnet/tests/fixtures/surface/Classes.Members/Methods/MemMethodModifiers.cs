// Clauses 15.6.4 (virtual methods), 15.6.5 (override methods), 15.6.6 (sealed methods) and
// 15.6.7 (abstract methods). One three-deep hierarchy, so that every modifier appears on a
// member that is really in the relation the modifier claims: virtual introduces, override
// re-implements, sealed override stops the chain, abstract has no body at all.
//
// The hazard on all four rows is that an override *is* the base member, seen from lower down.
// `Area` and `Describe` are each declared three times here with one name, one arity and one
// parameter list; only the containing type separates them, and a query that flattens the
// hierarchy — which is what "find the implementation" means — has to return the right one of
// the three for a receiver of each static type. `base.Describe()` is the reference that names
// the one an ordinary call would skip.

namespace Surface.Classes.Members.Methods;

/// <summary>15.6.7 and 15.6.4: the root. Abstract members with no body beside a virtual one
/// with a body, in a type that cannot be instantiated.</summary>
public abstract class MemModifierRoot
{
    /// <summary>15.6.7: an abstract method — implicitly virtual, and a semicolon in place of a
    /// body. Every concrete descendant must have an override of this.</summary>
    public abstract double Area();

    /// <summary>15.6.7: an abstract method with parameters, so the signature an override has to
    /// match is not the empty one.</summary>
    public abstract string Format(string prefix, int places);

    /// <summary>15.6.4: a virtual method with a body — the body is the default an override may
    /// replace and may also call.</summary>
    public virtual string Describe() => $"shape of area {Area()}";

    /// <summary>15.6.4: a virtual method that is never overridden anywhere in the corpus, so a
    /// query for its overrides must answer with none rather than with nothing.</summary>
    public virtual int Rank() => 0;

    /// <summary>15.6.3: a non-virtual instance method, which an override may not target and a
    /// derived declaration can only hide.</summary>
    public string Kind() => GetType().Name;
}

/// <summary>15.6.5: the middle of the chain. Overrides both abstract members, overrides the
/// virtual one and calls the version it replaced.</summary>
public class MemModifierMiddle : MemModifierRoot
{
    private readonly double _side;

    /// <summary>Stores the side the area is computed from.</summary>
    public MemModifierMiddle(double side) => _side = side;

    /// <summary>15.6.5: an override of an abstract method. Same name, same arity, same
    /// parameter list as <see cref="MemModifierRoot.Area"/> — the containing type is the
    /// difference.</summary>
    public override double Area() => _side * _side;

    /// <summary>15.6.5: an override of the abstract method that takes parameters.</summary>
    public override string Format(string prefix, int places) => $"{prefix}{Area().ToString($"F{places}")}";

    /// <summary>15.6.5: an override of a virtual method that calls the base implementation. The
    /// `base.` reference names the member an ordinary virtual call would never reach.</summary>
    public override string Describe() => $"square: {base.Describe()}";

    /// <summary>15.6.4: a new virtual method introduced part-way down the chain, so `virtual`
    /// and `override` appear on the same type.</summary>
    public virtual double Perimeter() => 4 * _side;
}

/// <summary>15.6.6: the end of the chain. A sealed override may not itself be overridden, and a
/// second override of the same member in a further derived type is CS0239.</summary>
public sealed class MemModifierLeaf : MemModifierMiddle
{
    /// <summary>Passes the side up.</summary>
    public MemModifierLeaf(double side)
        : base(side)
    {
    }

    /// <summary>15.6.6: `sealed override` — the third declaration of `Area` with this exact
    /// signature, and the one a call on a `MemModifierLeaf` receiver reaches.</summary>
    public sealed override double Area() => base.Area() * 2;

    /// <summary>15.6.6: a sealed override of the method introduced one level up rather than at
    /// the root.</summary>
    public sealed override double Perimeter() => base.Perimeter() * 2;

    /// <summary>15.6.5: the third `Describe`, which chains through the second to the first.</summary>
    public override string Describe() => $"double square: {base.Describe()}";
}

/// <summary>15.6.4 through 15.6.7: the references that pick one of the three `Area`
/// declarations, once per static type of the receiver.</summary>
public static class MemModifierUse
{
    /// <summary>The same instance read through all three static types. Every call is the same
    /// name and the same arity; the answer differs three ways.</summary>
    public static string Dispatch()
    {
        var leaf = new MemModifierLeaf(2);
        MemModifierMiddle middle = leaf;
        MemModifierRoot root = leaf;
        return $"{leaf.Area()} {middle.Area()} {root.Area()} {root.Describe()} {root.Rank()} " +
               $"{root.Kind()} {root.Format("=", 2)} {middle.Perimeter()}";
    }

    /// <summary>A receiver whose most derived `Area` is the middle one, so the middle
    /// declaration is reached by a call and not only by a `base.` reference.</summary>
    public static double Middle() => new MemModifierMiddle(3).Area();
}
