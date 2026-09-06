// Clause 19.4.10 — the most specific implementation. When more than one interface in a type's
// interface set provides an implementation of a member, the one in the most derived interface
// wins; when two are equally derived, there is no most specific implementation and the type must
// supply its own. Both cases are here, and the second is the one that would otherwise be an
// error.

namespace Surface.Interfaces;

/// <summary>
/// 19.4.10 — the base of the chain. Its member has a default implementation, so it is the
/// implementation of last resort for every type below.
/// </summary>
public interface IfaceGreeter
{
    /// <summary>19.4.10 — a member with a body in the least derived interface.</summary>
    string Greet() => "IfaceGreeter";
}

/// <summary>
/// 19.4.10 hazard — a derived interface overriding the default. This is an explicit interface
/// member implementation inside an interface: its name is the qualified name
/// <c>IfaceGreeter.Greet</c>, and it is a second implementation of one declared member.
/// </summary>
public interface IfacePoliteGreeter : IfaceGreeter
{
    /// <summary>19.4.10 — more derived than <see cref="IfaceGreeter"/>'s, so it is the most
    /// specific implementation for any type whose interface set holds both.</summary>
    string IfaceGreeter.Greet() => "IfacePoliteGreeter";
}

/// <summary>19.4.10 — a derived interface that overrides nothing, so it contributes no
/// implementation to the set.</summary>
public interface IfaceLoudGreeter : IfaceGreeter
{
    /// <summary>19.4.10 — a member of its own, which is not an implementation of anything.</summary>
    string Shout();
}

/// <summary>
/// 19.4.10 — a class whose interface set holds three interfaces and two implementations of
/// <c>Greet</c>. The most specific is <see cref="IfacePoliteGreeter"/>'s, and this class declares
/// no <c>Greet</c> at all: the member it answers with is declared in an interface.
/// </summary>
public sealed class IfaceGreeterHost : IfacePoliteGreeter, IfaceLoudGreeter
{
    /// <summary>19.4.10 — the only member this class declares.</summary>
    public string Shout() => "HOST";
}

/// <summary>19.4.10 hazard — the left of two equally derived overrides.</summary>
public interface IfaceLeftGreeter : IfaceGreeter
{
    /// <summary>19.4.10 — an implementation of <c>IfaceGreeter.Greet</c>…</summary>
    string IfaceGreeter.Greet() => "IfaceLeftGreeter";
}

/// <summary>19.4.10 hazard — …and the right one, at the same depth.</summary>
public interface IfaceRightGreeter : IfaceGreeter
{
    /// <summary>19.4.10 — the third implementation of the same declared member. Three
    /// interfaces in this file implement <c>IfaceGreeter.Greet</c>, and all three qualified
    /// names are spelled identically.</summary>
    string IfaceGreeter.Greet() => "IfaceRightGreeter";
}

/// <summary>
/// 19.4.10 — a class whose interface set holds two equally derived implementations. Neither is
/// most specific, so the class member below is: a class implementation is more specific than
/// any interface implementation, and without it this declaration would not compile.
/// </summary>
public sealed class IfaceAmbiguousHost : IfaceLeftGreeter, IfaceRightGreeter
{
    /// <summary>19.4.10 — the class member that resolves the ambiguity, and the fourth
    /// implementation of <see cref="IfaceGreeter.Greet"/> in this file.</summary>
    public string Greet() => "IfaceAmbiguousHost";
}

/// <summary>
/// 19.4.10 — the two hosts called through the base interface, which is the only place the
/// choice of implementation is observable.
/// </summary>
public static class IfaceGreeterUse
{
    /// <summary>19.4.10 — resolves to <see cref="IfacePoliteGreeter"/>'s implementation.</summary>
    public static string FromHost()
    {
        IfaceGreeter greeter = new IfaceGreeterHost();
        return greeter.Greet();
    }

    /// <summary>19.4.10 — resolves to the class's own member, not to either interface's.</summary>
    public static string FromAmbiguous()
    {
        IfaceGreeter greeter = new IfaceAmbiguousHost();
        return greeter.Greet();
    }

    /// <summary>19.4.10 — the same class member reached without the interface, which is a
    /// different binding to the same declaration.</summary>
    public static string DirectFromAmbiguous() => new IfaceAmbiguousHost().Greet();

    /// <summary>19.4.10 — the base interface's own implementation, reached through a type whose
    /// interface set holds no override of it.</summary>
    public static string FromBase()
    {
        IfaceGreeter greeter = new IfaceBareGreeter();
        return greeter.Greet();
    }

    /// <summary>19.4.10 — a class implementing only the base interface, so the least derived
    /// implementation is also the most specific one.</summary>
    private sealed class IfaceBareGreeter : IfaceGreeter
    {
    }
}
