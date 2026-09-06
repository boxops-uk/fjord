using System;

namespace Surface.SyntaxForms.Declarations;

// `Parameter` in every position the grammar allows one: a method, a constructor, an
// indexer, a delegate, a lambda, an anonymous method, a local function, an operator, a
// conversion operator, and a primary constructor. One row in the census, ten containers —
// and `ParameterSyntax` is not one of the six bases the walk switches on, so a parameter is
// a declaration the index holds no definition for regardless of which container it sits in.

/// <summary>A primary constructor's parameters, which are also the type's own scope.</summary>
/// <param name="host">Where the ledger lives.</param>
/// <param name="port">Which port it answers on.</param>
public sealed class SfParameterPrimary(string host, int port)
{
    /// <summary>Renders the endpoint the primary-constructor parameters describe.</summary>
    /// <returns>Host and port.</returns>
    public string Endpoint() => $"{host}:{port}";
}

/// <summary>Every other parameter position, gathered.</summary>
public sealed class SfParameterForms
{
    private readonly int _seed;

    /// <summary>A constructor's parameter, with a default value.</summary>
    /// <param name="seed">Where to start.</param>
    public SfParameterForms(int seed = 7) => _seed = seed;

    /// <summary>An indexer's parameter.</summary>
    /// <param name="slot">Which slot.</param>
    /// <returns>The slot, offset by the seed.</returns>
    public int this[int slot] => slot + _seed;

    /// <summary>
    /// A method's parameters in every modifier form the grammar has: plain, <c>ref</c>,
    /// <c>out</c>, <c>in</c>, <c>ref readonly</c>, <c>this</c>-less optional, and
    /// <c>params</c>.
    /// </summary>
    /// <param name="plain">No modifier.</param>
    /// <param name="byRef">Passed by reference.</param>
    /// <param name="produced">Written by the callee.</param>
    /// <param name="borrowed">Read-only by reference.</param>
    /// <param name="pinned">Read-only by reference, spelled the newer way.</param>
    /// <param name="optional">Has a default.</param>
    /// <param name="rest">Variable arity.</param>
    /// <returns>Something derived from all of them.</returns>
    public int Every(
        int plain,
        ref int byRef,
        out int produced,
        in int borrowed,
        ref readonly int pinned,
        int optional = 3,
        params int[] rest)
    {
        produced = plain + borrowed + pinned + optional + rest.Length;
        byRef = produced;
        return produced;
    }

    /// <summary>An operator's parameters.</summary>
    /// <param name="left">One.</param>
    /// <param name="right">The other.</param>
    /// <returns>The sum of their seeds.</returns>
    public static int operator +(SfParameterForms left, SfParameterForms right) =>
        left._seed + right._seed;

    /// <summary>A conversion operator's parameter.</summary>
    /// <param name="forms">The value to convert.</param>
    public static explicit operator int(SfParameterForms forms) => forms._seed;

    /// <summary>A local function's parameter, and a lambda's, and an anonymous method's.</summary>
    /// <returns>All three applied to the seed.</returns>
    public int Nested()
    {
        // A local function's parameter list.
        int Doubled(int value) => value * 2;

        // A simple lambda's parameter, with no parentheses and no type.
        Func<int, int> tripled = value => value * 3;

        // A parenthesized lambda's parameters, one of them explicitly typed and one with a
        // default value — both C# 12 forms of a lambda parameter. The target is `var`, since
        // a default value on a lambda parameter is only kept by the synthesised delegate
        // type: assigning to `Func<int, int, int>` drops it and warns CS9099.
        var summed = (int first, int second = 1) => first + second;

        // An anonymous method's parameter list.
        Func<int, int> negated = delegate(int value) { return -value; };

        return Doubled(_seed) + tripled(_seed) + summed(_seed, 2) + negated(_seed);
    }
}
