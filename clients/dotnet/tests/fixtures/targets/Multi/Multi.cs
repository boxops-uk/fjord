namespace Fixture.Multi;

public class Shared
{
    public string Name => "shared";

#if NET10_0_OR_GREATER
    public string Only10() => "ten";
#endif
}
