namespace Surface.Modern.Deconstruction;

/// <summary>A pair with a <c>Deconstruct</c>, so the deconstructions below have a target.</summary>
public readonly struct Bearing
{
    /// <summary>Creates a bearing.</summary>
    public Bearing(int degrees, int minutes)
    {
        Degrees = degrees;
        Minutes = minutes;
    }

    /// <summary>Whole degrees.</summary>
    public int Degrees { get; }

    /// <summary>Minutes of arc.</summary>
    public int Minutes { get; }

    /// <summary>The deconstructor both forms below go through.</summary>
    public void Deconstruct(out int degrees, out int minutes)
    {
        degrees = Degrees;
        minutes = Minutes;
    }
}

/// <summary>
/// C# 10 — Assignment and declaration in the same deconstruction. Before C# 10 a
/// deconstruction either declared every target or assigned every target; the mixed form
/// declares some and assigns others, and the declared ones are the interesting half — they
/// are locals with no declaration statement of their own.
/// </summary>
public static class MixedDeconstruction
{
    /// <summary>C# 10 — <c>(existing, int fresh) = pair;</c>: one assignment, one declaration.</summary>
    public static int Total(Bearing bearing)
    {
        var degrees = 0;

        (degrees, int minutes) = bearing;

        return (degrees * 60) + minutes;
    }

    /// <summary>The mixed form nested inside a tuple pattern's worth of targets.</summary>
    public static string Render(Bearing first, Bearing second)
    {
        int firstDegrees;
        var secondDegrees = -1;

        ((firstDegrees, var firstMinutes), (secondDegrees, int secondMinutes)) = (first, second);

        return $"{firstDegrees}.{firstMinutes} {secondDegrees}.{secondMinutes}";
    }

    /// <summary>The all-declaring and all-assigning forms, so the mixed one is a contrast.</summary>
    public static int Spread(Bearing bearing)
    {
        var (declaredDegrees, declaredMinutes) = bearing;

        int assignedDegrees;
        int assignedMinutes;
        (assignedDegrees, assignedMinutes) = bearing;

        return declaredDegrees + declaredMinutes + assignedDegrees + assignedMinutes;
    }
}
