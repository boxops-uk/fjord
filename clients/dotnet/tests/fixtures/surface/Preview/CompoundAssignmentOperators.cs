using System;

namespace Surface.Preview;

/// <summary>
/// C# 14 — User-defined compound assignment operators. Before C# 14 <c>x += y</c> always
/// meant <c>x = x + y</c>: a type could declare <c>+</c> and the compound form was derived
/// from it. Now a type may declare <c>operator +=</c> itself, as a <c>void</c> *instance*
/// member that mutates in place — so a compound assignment may resolve to a member that has
/// no corresponding binary operator, and the same statement means a call to a different kind
/// of member depending on the operand's type.
/// </summary>
public sealed class Basket
{
    private readonly System.Collections.Generic.List<string> _items = [];

    /// <summary>How many items the basket holds.</summary>
    public int Count => _items.Count;

    /// <summary>C# 14 — an instance <c>operator +=</c>, which appends rather than rebinding.</summary>
    public void operator +=(string item) => _items.Add(item);

    /// <summary>C# 14 — an instance <c>operator -=</c>.</summary>
    public void operator -=(string item) => _items.Remove(item);

    /// <summary>C# 14 — an instance <c>operator *=</c>, which duplicates every item.</summary>
    public void operator *=(int times)
    {
        var original = _items.ToArray();

        for (var repeat = 1; repeat < times; repeat++)
        {
            _items.AddRange(original);
        }
    }

    /// <summary>C# 14 — an instance increment operator, <c>void operator ++</c>.</summary>
    public void operator ++() => _items.Add($"item{_items.Count}");

    /// <summary>C# 14 — an instance decrement operator.</summary>
    public void operator --()
    {
        if (_items.Count > 0)
        {
            _items.RemoveAt(_items.Count - 1);
        }
    }

    /// <inheritdoc/>
    public override string ToString() => string.Join(',', _items);
}

/// <summary>
/// The pre-C# 14 arrangement, for contrast: a static binary <c>+</c> from which the compiler
/// derives <c>+=</c> by rebinding the variable.
/// </summary>
public readonly struct Weight
{
    /// <summary>Creates a weight.</summary>
    public Weight(double kilograms) => Kilograms = kilograms;

    /// <summary>The mass.</summary>
    public double Kilograms { get; }

    /// <summary>The binary operator the compound form is derived from.</summary>
    public static Weight operator +(Weight left, Weight right) => new(left.Kilograms + right.Kilograms);

    /// <summary>The increment the compound form is derived from.</summary>
    public static Weight operator ++(Weight value) => new(value.Kilograms + 1);
}

/// <summary>Applies both kinds of compound assignment.</summary>
public static class CompoundAssignmentUses
{
    /// <summary>C# 14 — the in-place operators, which never reassign <c>basket</c>.</summary>
    public static string InPlace()
    {
        var basket = new Basket();

        basket += "apple";
        basket += "pear";
        basket -= "apple";
        basket *= 2;
        basket++;
        basket--;

        return $"{basket.Count}:{basket}";
    }

    /// <summary>The derived form, which reassigns the variable each time.</summary>
    public static double Rebound()
    {
        var weight = new Weight(1);

        weight += new Weight(2);
        weight++;

        return weight.Kilograms;
    }
}
