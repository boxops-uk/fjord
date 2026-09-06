namespace Surface.Conversions.UserDefined;

/// <summary>A temperature in degrees Celsius.</summary>
public readonly struct ConvCelsius
{
    /// <summary>A temperature of <paramref name="degrees"/> degrees Celsius.</summary>
    public ConvCelsius(double degrees) => Degrees = degrees;

    /// <summary>How many degrees.</summary>
    public double Degrees { get; }
}

/// <summary>A temperature in degrees Fahrenheit.</summary>
public readonly struct ConvFahrenheit
{
    /// <summary>A temperature of <paramref name="degrees"/> degrees Fahrenheit.</summary>
    public ConvFahrenheit(double degrees) => Degrees = degrees;

    /// <summary>How many degrees.</summary>
    public double Degrees { get; }
}

/// <summary>A temperature in kelvin.</summary>
public readonly struct ConvKelvin
{
    /// <summary>A temperature of <paramref name="degrees"/> kelvin.</summary>
    public ConvKelvin(double degrees) => Degrees = degrees;

    /// <summary>How many kelvin.</summary>
    public double Degrees { get; }
}

/// <summary>A temperature in degrees Rankine.</summary>
public readonly struct ConvRankine
{
    /// <summary>A temperature of <paramref name="degrees"/> degrees Rankine.</summary>
    public ConvRankine(double degrees) => Degrees = degrees;

    /// <summary>How many degrees.</summary>
    public double Degrees { get; }
}

/// <summary>
/// A raw thermometer reading, held in Celsius and convertible to four scales.
///
/// This is the corpus's <b>overload on return type</b>. Clauses 10.5.1 and 10.5.2 make a
/// conversion operator the one member kind whose signature the language distinguishes by what
/// it returns: all four operators below take one parameter of type <see cref="ConvReading"/>,
/// so their parameter lists are letter-for-letter identical, and only the target type tells
/// them apart. Two of them are <c>implicit</c> (clauses 10.2.14 and 10.5.4) and two are
/// <c>explicit</c> (clauses 10.3.9 and 10.5.5), which puts two in each metadata name —
/// <c>op_Implicit</c> and <c>op_Explicit</c> — with nothing but the return type left to
/// separate the members within a name.
///
/// The last operator converts the other way, so the type also holds a conversion whose
/// parameter list <em>is</em> distinct, as a control.
/// </summary>
public readonly struct ConvReading
{
    /// <summary>A reading of <paramref name="celsius"/> degrees Celsius.</summary>
    public ConvReading(double celsius) => Celsius = celsius;

    /// <summary>The reading, in degrees Celsius.</summary>
    public double Celsius { get; }

    /// <summary>Clauses 10.2.14 and 10.5.4 — the first of the two implicit operators.</summary>
    public static implicit operator ConvCelsius(ConvReading reading) => new(reading.Celsius);

    /// <summary>
    /// Clauses 10.2.14 and 10.5.4 — the second. Its parameter list is identical to the
    /// operator above; it is a different member because it returns a different type.
    /// </summary>
    public static implicit operator ConvFahrenheit(ConvReading reading) =>
        new((reading.Celsius * 9 / 5) + 32);

    /// <summary>Clauses 10.3.9 and 10.5.5 — the first of the two explicit operators.</summary>
    public static explicit operator ConvKelvin(ConvReading reading) => new(reading.Celsius + 273.15);

    /// <summary>Clauses 10.3.9 and 10.5.5 — the second, again differing only in return type.</summary>
    public static explicit operator ConvRankine(ConvReading reading) =>
        new((reading.Celsius + 273.15) * 9 / 5);

    /// <summary>
    /// Clause 10.5.2 — a conversion <em>to</em> the enclosing type, so that the type has one
    /// operator whose parameter list differs from the other four.
    /// </summary>
    public static implicit operator ConvReading(ConvCelsius celsius) => new(celsius.Degrees);

    /// <summary>
    /// Clause 10.5.2 — a conversion whose target is a tuple type, which is permitted because
    /// a tuple is neither <c>object</c> nor an interface nor a base of the enclosing type.
    /// </summary>
    public static explicit operator (double Celsius, double Fahrenheit)(ConvReading reading) =>
        (reading.Celsius, (reading.Celsius * 9 / 5) + 32);
}
