// Clause 16.2.5 — Struct interfaces. A struct declaration may specify a list of
// implemented interfaces, and the struct must provide implementations of all their
// members. A struct has no base class part in that list — 16.4.3 — so every name after
// the colon is an interface, which is the one place in C# where that is guaranteed.
//
// This clause is `both`: the interface list is a set of references, and satisfying it is a
// set of declarations. The hazard is the explicit interface implementation, whose declared
// name is qualified — `IStArea.Area` — while the implicit implementation beside it is
// called `Area` too. Two members, one simple name, one type.

using System;

namespace Surface.Structs;

/// <summary>Clause 16.2.5 — an interface a struct implements implicitly.</summary>
public interface IStArea
{
    /// <summary>The area enclosed.</summary>
    double Area { get; }
}

/// <summary>Clause 16.2.5 — a second interface, to make the list a list.</summary>
public interface IStNamed
{
    /// <summary>The name of the shape.</summary>
    string Name { get; }

    /// <summary>Renders the shape as text.</summary>
    string Render();
}

/// <summary>
/// Clause 16.2.5, post-standard — an interface with a default implementation. A struct that
/// implements it and does not override the member inherits an implementation it never
/// declares, reachable only through the interface, which boxes.
/// </summary>
public interface IStGauged
{
    /// <summary>The reading.</summary>
    int Reading { get; }

    /// <summary>A default implementation: no struct below declares this member.</summary>
    string Report() => $"reading {Reading}";
}

/// <summary>Clause 16.2.5 — a generic interface, closed by the implementing struct.</summary>
/// <typeparam name="T">The scaled result type.</typeparam>
public interface IStScalable<T>
{
    /// <summary>Scales by a factor.</summary>
    T ScaleBy(double factor);
}

/// <summary>
/// Clause 16.2.5 — a struct with a four-interface list, including a generic interface
/// closed over itself and a framework interface. Every member is implemented implicitly.
/// </summary>
public struct StSquare : IStArea, IStNamed, IStScalable<StSquare>, IEquatable<StSquare>
{
    /// <summary>The length of a side.</summary>
    public double Side;

    /// <summary>Clause 16.4.9 — the declared constructor.</summary>
    public StSquare(double side) => Side = side;

    /// <summary>Clause 16.2.5 — implements <c>IStArea.Area</c> implicitly.</summary>
    public double Area => Side * Side;

    /// <summary>Clause 16.2.5 — implements <c>IStNamed.Name</c> implicitly.</summary>
    public string Name => "square";

    /// <summary>Clause 16.2.5 — implements <c>IStNamed.Render</c> implicitly.</summary>
    public string Render() => $"{Name} {Side}";

    /// <summary>Clause 16.2.5 — implements the generic interface, closed over this struct.</summary>
    public StSquare ScaleBy(double factor) => new StSquare(Side * factor);

    /// <summary>Clause 16.2.5 — implements <c>IEquatable&lt;StSquare&gt;</c>.</summary>
    public bool Equals(StSquare other) => Side.Equals(other.Side);

    /// <summary>Clause 16.4.3 — the override that keeps the equality contract honest.</summary>
    public override bool Equals(object? obj) => obj is StSquare other && Equals(other);

    /// <summary>Clause 16.4.3 — and its hash code.</summary>
    public override int GetHashCode() => Side.GetHashCode();
}

/// <summary>
/// Clause 16.2.5 hazard — explicit interface implementation in a struct. This type has a
/// member declared as <c>IStArea.Area</c> and a separate public member declared as
/// <c>Area</c>; they have different values, and only the interface one is reachable
/// through <c>IStArea</c>. An identity keyed on the simple member name merges them, and
/// then the struct appears to declare one `Area` with two bodies.
/// </summary>
public struct StTriangleExplicit : IStArea, IStNamed
{
    /// <summary>The base of the triangle.</summary>
    public double Base;

    /// <summary>The height of the triangle.</summary>
    public double Height;

    /// <summary>Clause 16.4.9 — the declared constructor.</summary>
    public StTriangleExplicit(double baseLength, double height)
    {
        Base = baseLength;
        Height = height;
    }

    /// <summary>
    /// Clause 16.2.5 — the explicit implementation, whose declared name is qualified and
    /// which has no accessibility modifier because it cannot have one.
    /// </summary>
    double IStArea.Area => Base * Height / 2.0;

    /// <summary>
    /// Clause 16.2.5 — a public member with the same simple name as the explicit one and a
    /// deliberately different value: the bounding rectangle, not the triangle.
    /// </summary>
    public double Area => Base * Height;

    /// <summary>Clause 16.2.5 — an explicit implementation of a property from the second interface.</summary>
    string IStNamed.Name => "triangle";

    /// <summary>Clause 16.2.5 — an explicit implementation of a method.</summary>
    string IStNamed.Render() => $"triangle {Base}x{Height}";
}

/// <summary>
/// Clause 16.2.5 — a struct that implements an interface and leaves the default
/// implementation alone. <c>Report</c> is a member of this type that this file does not
/// declare, and the only way to call it is through the interface.
/// </summary>
public struct StDefaultedGauge : IStGauged
{
    /// <summary>Clause 16.2.5 — the one member the struct does declare.</summary>
    public int Reading { get; set; }
}

/// <summary>
/// Clause 16.2.5 — the uses, which are where the difference between implicit and explicit
/// implementation becomes visible: the same expression spelled twice gives two answers.
/// </summary>
public static class StStructInterfaceUse
{
    /// <summary>Reads the public `Area`, which is the bounding rectangle.</summary>
    public static double DirectArea(StTriangleExplicit triangle) => triangle.Area;

    /// <summary>
    /// Clause 16.4.6 — boxes the struct to reach the explicit implementation, which is
    /// half the value the line above returns.
    /// </summary>
    public static double InterfaceArea(StTriangleExplicit triangle) => ((IStArea)triangle).Area;

    /// <summary>Clause 16.2.5 — a constrained generic call, which reaches the member without boxing.</summary>
    public static double AreaOf<T>(T shape)
        where T : struct, IStArea => shape.Area;

    /// <summary>Reaches the default interface implementation, which nothing declares in source.</summary>
    public static string GaugeReport()
    {
        IStGauged gauge = new StDefaultedGauge { Reading = 3 };
        return gauge.Report();
    }

    /// <summary>Clause 16.2.5 — the generic interface, called through the struct.</summary>
    public static double ScaledArea(double side) => new StSquare(side).ScaleBy(2.0).Area;
}
