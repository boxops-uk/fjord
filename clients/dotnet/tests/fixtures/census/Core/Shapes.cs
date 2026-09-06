namespace Census.Core;

/// <summary>A shape that knows its own area.</summary>
public interface IShape
{
    double Area { get; }

    string Describe();
}

/// <summary>A record, keyed positionally.</summary>
public record Entry(string Key, int Weight);

/// <summary>A struct that is not a record, so both kinds are in the index.</summary>
public readonly struct Corner(double x, double y)
{
    public double X { get; } = x;

    public double Y { get; } = y;
}

/// <summary>A rectangle: an implementation, and an array field to hold its sides.</summary>
public sealed class Rectangle : IShape
{
    /// <summary>A field somewhere a member access can reach it.</summary>
    public static readonly double Unit = 1d;

    private readonly double[] _sides = new double[2];

    public Rectangle(double width, double height)
    {
        _sides[0] = width;
        _sides[1] = height;
    }

    public double Area => _sides[0] * _sides[1];

    public string Describe() => $"rectangle {_sides[0]}x{_sides[1]}";

    /// <summary>A pointer in a signature, which nothing but unsafe code can write.</summary>
    public static unsafe double Peek(double* side) => *side;
}
