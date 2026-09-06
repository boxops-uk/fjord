// Clause 7.7.2.3 (hiding through inheritance). A derived member of the same name hides the
// base one, and `new` is what says the hiding was meant — without it the compiler reports
// CS0108 and hides it anyway.
//
// The reference is the interesting half: `Extent` written on a `LexHidingDerived` reaches
// the derived declaration and the *same characters* written on a `LexHidingBase` reach the
// base one, so the pair `(name, static type of the receiver)` is what selects a
// declaration and the name alone is not.
//
// Hiding is not overriding: `LexHidingBase.Read` is virtual, `LexHidingDerived.Read` hides
// it with `new`, and a `LexHidingDerived` seen as a `LexHidingBase` calls the *base* one.

namespace Surface.Lexical.Hiding;

/// <summary>7.7.2.3: the base whose members the derived types hide.</summary>
public class LexHidingBase
{
    /// <summary>A field the derived type hides with a field of a different type.</summary>
    public int Extent = 1;

    /// <summary>A property the derived type hides.</summary>
    public int Computed => 2;

    /// <summary>A virtual method the derived type *hides* rather than overrides.</summary>
    public virtual int Read() => 3;

    /// <summary>A method the derived type hides with a property.</summary>
    public int Shape() => 4;

    /// <summary>A nested type the derived type hides with a nested type of its own.</summary>
    public class Nested
    {
        /// <summary>Something to reference.</summary>
        public const int Tag = 5;
    }

    /// <summary>A member the derived type hides *without* saying <c>new</c>.</summary>
    public int Quiet = 6;
}

/// <summary>
/// 7.7.2.3: the derived type, hiding five inherited members deliberately and one by
/// accident.
/// </summary>
public class LexHidingDerived : LexHidingBase
{
    /// <summary>Hides the base field, and with a different type.</summary>
    public new string Extent = "one";

    /// <summary>Hides the base property.</summary>
    public new int Computed => 20;

    /// <summary>Hides the base's *virtual* method, which is not an override — a
    /// <c>LexHidingDerived</c> seen as a <c>LexHidingBase</c> calls the base one.</summary>
    public new int Read() => 30;

    /// <summary>Hides a base method with a property, which the rule allows.</summary>
    public new int Shape => 40;

    /// <summary>Hides the base's nested type with a nested type.</summary>
    public new class Nested
    {
        /// <summary>A different value from the base's.</summary>
        public const int Tag = 50;
    }

    /// <summary>Hides the base field with no <c>new</c>, which is CS0108 and hides it.</summary>
    public int Quiet = 60;
}

/// <summary>7.7.2.3: a third level, so a hidden member can be hidden again.</summary>
public sealed class LexHidingGrandchild : LexHidingDerived
{
    /// <summary>Hides the derived field, which already hid the base one.</summary>
    public new int Extent = 300;

    /// <summary>Overrides what the middle type hid, reaching past the hiding
    /// declaration to the base's virtual member.</summary>
    public int ReadTheBase() => ((LexHidingBase)this).Read();
}

/// <summary>
/// 7.7.2.3: the references. Every method here writes the same member name against a
/// different static type, and each one resolves to a different declaration.
/// </summary>
public static class LexHidingUses
{
    /// <summary>Reaches the base field, because the receiver's type is the base.</summary>
    public static int ExtentOnTheBase(LexHidingBase receiver) => receiver.Extent;

    /// <summary>Reaches the derived field, whose type is not even the same.</summary>
    public static string ExtentOnTheDerived(LexHidingDerived receiver) => receiver.Extent;

    /// <summary>Reaches the grandchild's field, the third declaration of the name.</summary>
    public static int ExtentOnTheGrandchild(LexHidingGrandchild receiver) => receiver.Extent;

    /// <summary>Reaches the base field again, through a derived instance up-cast.</summary>
    public static int ExtentThroughAnUpcast(LexHidingDerived receiver) =>
        ((LexHidingBase)receiver).Extent;

    /// <summary>
    /// Calls the base's virtual method through a base-typed receiver holding a derived
    /// instance, which is 3 and not 30 — hiding is not overriding.
    /// </summary>
    public static int ReadThroughTheBase() => ((LexHidingBase)new LexHidingDerived()).Read();

    /// <summary>Calls the derived method through a derived-typed receiver, which is 30.</summary>
    public static int ReadThroughTheDerived() => new LexHidingDerived().Read();

    /// <summary>Reads the properties and the accidentally hidden field.</summary>
    public static int TheRest()
    {
        LexHidingDerived derived = new();
        return derived.Computed
            + ((LexHidingBase)derived).Computed
            + derived.Shape
            + ((LexHidingBase)derived).Shape()
            + derived.Quiet
            + ((LexHidingBase)derived).Quiet
            + LexHidingBase.Nested.Tag
            + LexHidingDerived.Nested.Tag
            + new LexHidingGrandchild().ReadTheBase();
    }
}
