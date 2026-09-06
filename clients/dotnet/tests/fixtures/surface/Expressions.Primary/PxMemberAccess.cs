using System;
using System.Collections.Generic;
using System.Globalization;

namespace Surface.Expressions.Primary;

/// <summary>
/// 12.8.7.1 — <c>E.I</c>, once for each thing <c>E</c> is allowed to be: a namespace, a type,
/// a constructed type, a variable, a value, and another member access. The declarations reached
/// from this file live partly here (same-file references) and partly in
/// <c>PxDeclarations.cs</c> (cross-file references).
/// </summary>
public sealed class PxMemberAccess
{
    /// <summary>A const, so a member access on a type can reach something evaluated at compile time.</summary>
    public const int Ceiling = 64;

    /// <summary>A static field, reached as <c>PxMemberAccess.Floor</c>.</summary>
    public static readonly int Floor = 1;

    private readonly PxTarget _target = new(3);

    /// <summary>A nested type, reached as <c>PxMemberAccess.Depth</c> — a member access to a type.</summary>
    public enum Depth
    {
        /// <summary>The shallow end.</summary>
        Shallow = 1,

        /// <summary>The deep end.</summary>
        Deep = 2,
    }

    /// <summary>A nested class, so <c>E.I</c> can name a type and then a member of it.</summary>
    public sealed class Marker
    {
        /// <summary>Reached as <c>PxMemberAccess.Marker.Tag</c>.</summary>
        public static string Tag => "marker";

        /// <summary>An instance member of a nested type.</summary>
        public int Ordinal { get; init; }
    }

    /// <summary>12.8.7.1 — <c>E</c> is a namespace: the name to its left resolves to no symbol value.</summary>
    public static int WhenLeftIsANamespace()
    {
        var list = new System.Collections.Generic.List<int> { 1, 2 };
        var absolute = System.Math.Abs(-3);
        var invariant = System.Globalization.CultureInfo.InvariantCulture.Name.Length;
        return list.Count + absolute + invariant;
    }

    /// <summary>12.8.7.1 — <c>E</c> is a type: a static member, a const, a nested type, an enum member.</summary>
    public static int WhenLeftIsAType()
    {
        var staticField = PxMemberAccess.Floor;
        var constant = PxMemberAccess.Ceiling;
        var nestedStatic = Marker.Tag.Length;
        var enumMember = (int)Depth.Deep;
        var predefined = int.MaxValue / int.MaxValue;
        var factory = PxTarget.Of(1).Seed;
        return staticField + constant + nestedStatic + enumMember + predefined + factory;
    }

    /// <summary>12.8.7.1 — <c>E</c> is a variable, then a value, then another member access.</summary>
    public int WhenLeftIsAValue()
    {
        var fromVariable = _target.Scale;                    // E is a variable
        var fromValue = new PxTarget(2).Measure();            // E is a value
        var chained = new Marker { Ordinal = 4 }.Ordinal;     // E is an object creation
        var throughTwoDots = _target.ToString()!.Length;      // E is itself a member access
        var throughProperty = CultureInfo.InvariantCulture.Calendar.MinSupportedDateTime.Year;
        return fromVariable + fromValue + chained + throughTwoDots + throughProperty;
    }

    /// <summary>
    /// 12.8.7.1 on a constructed generic type: the member the access binds to is a substituted
    /// member, and its spelling has to equal the declaration's in <c>PxBox&lt;T&gt;</c>.
    /// </summary>
    public static string WhenLeftIsConstructed()
    {
        var counted = new PxBox<int>(3);
        var value = counted.Value;                          // substituted property, T := int
        var mapped = counted.Map(number => number + 1);     // substituted method, TOut inferred
        var named = PxBox<string>.Wrap("x");                // static member on a construction
        var nestedConstruction = new PxBox<PxBox<int>>(counted).Value.Value;
        return $"{value} {mapped.Value} {named.Value} {nestedConstruction}";
    }

    /// <summary>
    /// 12.8.7.1 — the same member of one generic declaration, reached through two different
    /// constructions. Two use sites, one declaration.
    /// </summary>
    public static string OneDeclarationTwoConstructions()
    {
        var fromInt = new PxBox<int>(1).Value.ToString(CultureInfo.InvariantCulture);
        var fromString = new PxBox<string>("two").Value;
        return fromInt + fromString;
    }

    /// <summary>12.5.2 — the base types of a type are part of its lookup set, so an inherited member is reachable.</summary>
    public static string WhenTheMemberIsInherited()
    {
        var heir = new PxDerivedCounter();
        var inherited = heir.Describe();     // declared in PxDerivedCounter, hiding the base's
        var virtualCall = heir.Count();      // the override, reached through the derived type
        var throughBase = ((PxBaseCounter)heir).Describe();
        return $"{inherited} {virtualCall} {throughBase}";
    }
}

/// <summary>
/// 12.8.14 — this access, in each context where it is allowed: an instance method, an instance
/// property, a constructor body, and as an argument. In a class <c>this</c> is a value.
/// </summary>
public sealed class PxThisAccess
{
    private int _seed;

    /// <summary>A constructor, where <c>this</c> is already usable as a value.</summary>
    public PxThisAccess(int seed)
    {
        this._seed = seed;
        this.Register(this);
    }

    /// <summary>The registered instances, so <c>this</c> has somewhere to go.</summary>
    public List<PxThisAccess> Registered { get; } = [];

    /// <summary>12.8.14 — <c>this</c> qualifying a field access, disambiguating it from the parameter.</summary>
    public int Adjust(int seed)
    {
        this._seed = seed + this._seed;
        return this._seed;
    }

    /// <summary>12.8.14 — <c>this</c> as the receiver of a method invocation and of an argument.</summary>
    public int Reflect() => this.Adjust(this.Registered.Count) + this.Compare(this);

    /// <summary>12.8.14 — <c>this</c> in a property body.</summary>
    public int Doubled => this._seed * 2;

    private void Register(PxThisAccess instance) => this.Registered.Add(instance);

    private int Compare(PxThisAccess other) => ReferenceEquals(this, other) ? 1 : 0;
}

/// <summary>
/// 12.8.14 — in a struct, <c>this</c> is classified as a *variable*, so it can be assigned to.
/// That is the one place a whole-instance assignment is legal.
/// </summary>
public struct PxCursor
{
    /// <summary>Builds a cursor.</summary>
    public PxCursor(int offset) => Offset = offset;

    /// <summary>Where the cursor sits.</summary>
    public int Offset { get; private set; }

    /// <summary>12.8.14 — assigning through <c>this</c>, which only a struct may do.</summary>
    public void Reset()
    {
        this = new PxCursor(0);
    }

    /// <summary>12.8.14 — <c>this</c> as a variable, mutated field-wise.</summary>
    public void Advance(int by)
    {
        this.Offset += by;
    }

    /// <summary>Same-file use of both.</summary>
    public static int UsedHere()
    {
        var cursor = new PxCursor(4);
        cursor.Advance(2);
        var advanced = cursor.Offset;
        cursor.Reset();
        return advanced + cursor.Offset;
    }
}

/// <summary>
/// 12.8.15 — base access from a *second* file's worth of distance: this type derives from a
/// base declared in <c>PxDeclarations.cs</c>, so each <c>base.X</c> here is a cross-file reference
/// while the copies in <c>PxDerivedCounter</c> are same-file ones.
/// </summary>
public sealed class PxCrossFileCounter : PxBaseCounter
{
    /// <summary>Reaches the base method declared in another file.</summary>
    public override int Count() => base.Count() * 10;

    /// <summary>Reaches the base indexer declared in another file, with no name node at either end.</summary>
    public override int this[int index] => base[index] + 1;

    /// <summary>Reaches the base property declared in another file.</summary>
    public override string Label => "cross:" + base.Label;

    /// <summary>12.8.15 — a base access to a member the derived type does not override at all.</summary>
    public string Inherited() => base.Describe();
}
