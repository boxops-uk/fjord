namespace Fixture.A;

/// <summary>Uses B, which the solution lists, and C, which it does not.</summary>
public class Caller
{
    public string Call() => new Fixture.B.Thing().Describe();
}
