namespace Ledger.Core;

/// <summary>A shape that knows its own area.</summary>
public interface IShape
{
    double Area { get; }

    string Describe();
}

/// <summary>How a shape is drawn.</summary>
public enum Stroke
{
    Thin = 1,
    Thick = 2,
}

/// <summary>A rectangle, with a nested corner type.</summary>
public sealed class Rectangle : IShape
{
    private readonly double _width;

    public Rectangle(double width, double height)
    {
        _width = width;
        Height = height;
    }

    public double Height { get; }

    public double Area => _width * Height;

    public Stroke Stroke { get; set; } = Stroke.Thin;

    public string Describe() => $"rectangle {_width}x{Height}";

    public static Rectangle operator +(Rectangle left, Rectangle right) =>
        new(left._width + right._width, left.Height + right.Height);

    /// <summary>A corner of the rectangle, indexed clockwise from the origin.</summary>
    public readonly struct Corner(double x, double y)
    {
        public double X { get; } = x;

        public double Y { get; } = y;
    }
}
