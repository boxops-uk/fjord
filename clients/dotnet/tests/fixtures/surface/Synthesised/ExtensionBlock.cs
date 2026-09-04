using System.Collections.Generic;

namespace Surface.Synthesised;

/// <summary>An extension block (C# 14) — the only source of <c>TypeKind.Extension</c>.</summary>
/// <remarks>
/// The <c>extension(...)</c> block declares a type. It has a
/// <c>TypeKind</c> of its own, a name no source spells, a synthesised marker method
/// carrying the receiver, and it is a member of <see cref="SynExtensions"/>; each member
/// inside it is also emitted as a static method on the enclosing static class, so one
/// declaration produces two members and one of them is reachable under two names.
///
/// This file is deliberately the only file in the project that uses the syntax. The
/// project is compiled with <c>&lt;LangVersion&gt;preview&lt;/LangVersion&gt;</c>, which is
/// what makes the block parse under a pinned Roslyn 4.14 — the same compiler rejects
/// <c>&lt;LangVersion&gt;14.0&lt;/LangVersion&gt;</c> outright (CS1617), which would fail
/// the whole project rather than one construct. If a reader ever finds this file's
/// declarations missing from an index while the rest of the project is present, the
/// language version is where to look, and nothing else in the corpus is lost with it.
/// </remarks>
public static class SynExtensions
{
    /// <summary>Extension members on <c>int</c>.</summary>
    extension(int value)
    {
        /// <summary>An extension property, which has no explicit receiver parameter.</summary>
        public bool IsOdd => (value & 1) == 1;

        /// <summary>An extension method inside a block.</summary>
        /// <returns>Twice the value.</returns>
        public int Doubled() => value * 2;

        /// <summary>
        /// An extension member that is neither: a property whose body shifts. An extension
        /// <em>indexer</em> is rejected outright (CS9282), so the corpus records that the
        /// C# 14 extension surface is properties, methods and operators and not indexers.
        /// </summary>
        public int Shifted => value << 2;
    }

    /// <summary>Static extension members, which extend the type rather than an instance.</summary>
    extension(int)
    {
        /// <summary>A static extension property.</summary>
        public static int SynSeedValue => 1;

        /// <summary>A static extension method.</summary>
        /// <param name="count">How many.</param>
        /// <returns>The count, tripled.</returns>
        public static int SynTripled(int count) => count * 3;
    }

    /// <summary>A generic extension block, over the project's own entry type.</summary>
    /// <typeparam name="T">What is enumerated.</typeparam>
    extension<T>(IEnumerable<T> source)
        where T : notnull
    {
        /// <summary>Whether anything is there.</summary>
        public bool SynIsEmpty
        {
            get
            {
                foreach (var _ in source)
                {
                    return false;
                }

                return true;
            }
        }
    }

    /// <summary>A classic extension method beside the blocks, for contrast.</summary>
    /// <param name="value">The receiver.</param>
    /// <returns>Half the value.</returns>
    public static int SynHalved(this int value) => value / 2;

    /// <summary>Calls the extension members, so each is also referenced.</summary>
    /// <returns>A digest.</returns>
    public static string Exercise()
    {
        var odd = 3.IsOdd;
        var doubled = 3.Doubled();
        var shifted = 3.Shifted;
        var seed = int.SynSeedValue;
        var tripled = int.SynTripled(2);
        var empty = new[] { 1 }.SynIsEmpty;
        var halved = 4.SynHalved();

        return $"{odd}{doubled}{shifted}{seed}{tripled}{empty}{halved}";
    }
}
