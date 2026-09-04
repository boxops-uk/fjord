// Clause 15.6.11 (method body). The census marks this row `neither`: a body declares nothing
// and references nothing *as a clause*. What it does hold is every reference in the project,
// and one declaration kind that exists nowhere else — the local function, which is a named
// method-like member of a body rather than of a type.
//
// That is the hazard, and it is written twice over:
//
//   1. `MemBodyLocals` declares a local function named `Helper` in the body of `Route(int)`
//      and another named `Helper` in the body of `Route(string)`. Two declarations, one name,
//      one containing type — and the thing that separates them is not visible in a member list
//      at all, because neither is a member. An identity of (type, name) mints one string for
//      both; an identity of (type, member, name) needs the enclosing method's ordinal, which is
//      the ordinal of an *overload*.
//   2. `MemBodyLocals.Nested` declares `Helper` a third time, and inside it a fourth, so the
//      same name occurs at two depths of one body.
//
// The body *forms* are all here too: a block, an expression body, an expression body that only
// throws, and — in `MemBodyAbsent` — the three declarations that have no body to walk.

using System;
using System.Collections.Generic;

namespace Surface.Classes.Members.Methods;

/// <summary>15.6.11: one type per body form is unnecessary; one method per body form is
/// enough.</summary>
public class MemMethodBodies
{
    private readonly List<int> _seen = [];

    /// <summary>15.6.11: a block body with statements, a local variable and a loop.</summary>
    public int Block(int count)
    {
        var total = 0;

        for (var index = 0; index < count; index++)
        {
            _seen.Add(index);
            total += index;
        }

        return total;
    }

    /// <summary>15.6.11: an expression body, which is a body of one expression and no
    /// statements.</summary>
    public int Expression(int count) => count * _seen.Count;

    /// <summary>15.6.11: an expression body that is a `throw` expression, so the method has a
    /// body and no path that returns.</summary>
    public int Refused(int count) => throw new NotSupportedException($"cannot route {count}");

    /// <summary>15.6.11: a body whose only statement is a `return` of a `switch` expression, so
    /// the body's own control flow is in an expression.</summary>
    public string Branch(int count)
    {
        return count switch
        {
            < 0 => "negative",
            0 => "empty",
            _ => "some",
        };
    }

    /// <summary>15.6.11: a block body that closes over a parameter in a lambda, so the body
    /// holds an anonymous function whose own body is a second body.</summary>
    public Func<int, int> Closure(int factor)
    {
        return value => value * factor;
    }
}

/// <summary>15.6.11: the local-function hazard. Three declarations named <c>Helper</c> in one
/// type, none of them a member of it.</summary>
public class MemBodyLocals
{
    /// <summary>15.6.11: the first `Helper`, in the body of the `int` overload.</summary>
    public int Route(int value)
    {
        int Helper(int inner) => inner + 1;

        return Helper(value);
    }

    /// <summary>15.6.11: the second `Helper`, in the body of the `string` overload. Same name,
    /// same signature, same containing type — and a different declaration.</summary>
    public int Route(string value)
    {
        int Helper(int inner) => inner + 2;

        return Helper(value.Length);
    }

    /// <summary>15.6.11: the third and fourth `Helper`s, one inside the other, so the name
    /// occurs at two depths of one body. A static local function may not capture, which is the
    /// one way the two differ.</summary>
    public int Nested(int value)
    {
        static int Helper(int inner)
        {
            int Helper(int innermost) => innermost * 2;

            return Helper(inner) + 1;
        }

        return Helper(value);
    }

    /// <summary>15.6.11: a local function that is recursive, so its own body holds a reference
    /// to it, and an iterator local function, so a body holds a state machine.</summary>
    public int Recursive(int value)
    {
        int Down(int inner) => inner <= 0 ? 0 : inner + Down(inner - 1);

        IEnumerable<int> Up(int inner)
        {
            for (var index = 0; index < inner; index++)
            {
                yield return index;
            }
        }

        var total = Down(value);

        foreach (var seen in Up(value))
        {
            total += seen;
        }

        return total;
    }

    /// <summary>15.6.11: all four routes, so each local function is reached.</summary>
    public int All() => Route(1) + Route("xy") + Nested(3) + Recursive(2);
}

/// <summary>15.6.11: the declarations with no body at all. Each is a member whose statements
/// are somewhere a walk of this project cannot reach — and none of them is a partial
/// member.</summary>
public abstract class MemBodyAbsent
{
    /// <summary>15.6.7 and 15.6.11: abstract, so the body is a semicolon.</summary>
    public abstract int Missing();

    /// <summary>15.7.4 and 15.6.11: an automatically implemented property, whose two accessors
    /// have bodies the compiler wrote and the source does not contain.</summary>
    public int Generated { get; set; }

    /// <summary>15.8.2 and 15.6.11: a field-like event, whose `add` and `remove` are two more
    /// bodies with no source.</summary>
    public event EventHandler? Raised;

    /// <summary>15.6.11: the one member here with a body, so the generated ones have a
    /// reference.</summary>
    public string Present() => $"{Missing()} {Generated} {Raised is null}";
}
