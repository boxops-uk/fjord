using System;

namespace Surface.Preview;

/// <summary>
/// C# 14 — The field keyword (field-backed properties), in the shapes that only became legal
/// when the feature shipped: one accessor auto-implemented and the other written out, with
/// both halves agreeing about a backing field that appears in no declaration.
/// </summary>
public sealed class Measurement
{
    /// <summary>C# 14 — an auto <c>get</c> beside a written <c>set</c> that uses <c>field</c>.</summary>
    public int Count
    {
        get;
        set => field = Math.Max(0, value);
    }

    /// <summary>C# 14 — a written <c>get</c> beside an auto <c>set</c>.</summary>
    public double Scale
    {
        get => field == 0 ? 1 : field;
        set;
    }

    /// <summary>C# 14 — <c>field</c> in a property with an initializer, which seeds it.</summary>
    public string Unit
    {
        get => field;
        set => field = string.IsNullOrEmpty(value) ? "m" : value;
    } = "m";

    /// <summary>
    /// C# 14 — a nullable annotation on a field-backed property. The synthesized field's
    /// nullability is inferred from the accessors rather than declared, so this property's
    /// storage is <c>string?</c> while its type is <c>string</c>.
    /// </summary>
    public string Note
    {
        get => field ?? string.Empty;
        set => field = value;
    }

    /// <summary>C# 14 — <c>field</c> in a <c>static</c> property.</summary>
    public static int Instances
    {
        get => field;
        private set => field = value;
    }

    /// <summary>C# 14 — <c>field</c> in a property that also overrides.</summary>
    public override string ToString() => $"{Count}{Unit}@{Scale}{Note}{Instances}";

    /// <summary>Writes every field-backed property, so each accessor runs.</summary>
    public static Measurement Seeded()
    {
        Instances++;

        return new Measurement
        {
            Count = -4,
            Scale = 2.5,
            Unit = string.Empty,
            Note = "seeded",
        };
    }
}

/// <summary>C# 14 — <c>field</c> in a property of a struct, and of a record.</summary>
public readonly struct Threshold
{
    /// <summary>C# 14 — <c>field</c> in a readonly struct's property.</summary>
    public double Limit
    {
        get => field;
        init => field = value < 0 ? 0 : value;
    }
}

/// <summary>C# 14 — <c>field</c> in a record's explicitly-written property.</summary>
/// <param name="Name">The positional member, which is not field-backed by hand.</param>
public record Marker(string Name)
{
    /// <summary>C# 14 — a hand-written property in a record, using <c>field</c>.</summary>
    public int Weight
    {
        get => field;
        init => field = value < 1 ? 1 : value;
    }
}
