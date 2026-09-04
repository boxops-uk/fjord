using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Surface.Synthesised;

/// <summary>
/// One nested type per numbered <c>LanguageVersion</c>, holding a construct that first
/// became legal at that version.
/// </summary>
/// <remarks>
/// A <c>LanguageVersion</c> member is not a declaration and not a reference: it is a
/// compiler input, and Roslyn maps a <em>specified</em> version onto an
/// <em>effective</em> one before a single token is parsed, so no syntax tree and no symbol
/// in any indexed compilation ever carries <c>Default</c>, <c>Latest</c> or
/// <c>LatestMajor</c>. What an index can hold about a version is the surface that version
/// admits, which is what this ladder is: if the corpus reaches every rung, then whatever
/// version the pinned parser is set to, a rung that vanishes says which one.
///
/// The project's own effective version is <c>Preview</c>, set in the csproj. Under the
/// pinned Roslyn 4.14 the three ceiling spellings — <c>default</c>, <c>latest</c> and
/// <c>latestmajor</c> — all resolve to <c>CSharp13</c>, and <c>14.0</c> resolves to
/// nothing at all (CS1617). Only <c>preview</c> both parses C# 14 and is a version that
/// pinned compiler knows.
/// </remarks>
public static class SynVersionLadder
{
    /// <summary>C# 1: classes, interfaces, <c>virtual</c>/<c>override</c>, <c>params</c>.</summary>
    public class SynV1
    {
        /// <summary>Overridable, which is the whole of C# 1's polymorphism.</summary>
        /// <param name="parts">However many, as the only C# 1 variadic form.</param>
        /// <returns>A label.</returns>
        public virtual string Join(params object[] parts) => string.Concat(parts);

        /// <summary><c>is</c>, <c>as</c> and <c>typeof</c>, all C# 1.</summary>
        /// <param name="value">The value.</param>
        /// <returns>A label.</returns>
        public string Probe(object value)
        {
            var text = value as string;

            if (value is int)
            {
                return typeof(int).Name;
            }

            return text ?? typeof(SynV1).Name;
        }
    }

    /// <summary>C# 2: generics, nullable value types, iterators, anonymous methods, <c>??</c>.</summary>
    /// <typeparam name="T">What is boxed.</typeparam>
    /// <remarks>
    /// C# 2's other feature, the partial type, is in <c>PartialSplitA.cs</c> and
    /// <c>PartialSplitB.cs</c> — a nested one would be a partial type in one file, which
    /// the corpus quarantines.
    /// </remarks>
    public class SynV2<T>
        where T : struct
    {
        /// <summary>A nullable value type, which C# 1 could not spell.</summary>
        public T? Held { get; set; }

        /// <summary>An iterator and an anonymous method in one method.</summary>
        /// <param name="count">How many.</param>
        /// <returns>The values.</returns>
        public IEnumerable<int> Counted(int count)
        {
            Predicate<int> keep = delegate(int value) { return value % 2 == 0; };

            for (var i = 0; i < count; i++)
            {
                if (keep(i))
                {
                    yield return i;
                }
            }
        }

        /// <summary><c>??</c> and <c>default(T)</c>.</summary>
        /// <returns>The held value or a default.</returns>
        public T Coalesced() => Held ?? default(T);
    }

    /// <summary>C# 3: <c>var</c>, initializers, anonymous types, lambdas, queries.</summary>
    /// <remarks>
    /// C# 3's extension methods are in <see cref="SynReduced"/>, because an extension
    /// method's containing class may not be nested. Its partial methods are quarantined:
    /// a declaring half and an implementing half mint one identity.
    /// </remarks>
    public class SynV3
    {
        /// <summary>An automatically implemented property, new in C# 3.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>An object initializer, a collection initializer and an anonymous type.</summary>
        /// <returns>A digest.</returns>
        public string Initialize()
        {
            var entries = new List<SynEntry> { new("alpha", 1), new("beta", 2) };
            var seeded = new SynV3 { Name = "seeded" };
            var anonymous = new { seeded.Name, Count = entries.Count };
            var lambda = (int value) => value + 1;

            var queried = from entry in entries
                          where entry.Weight > 0
                          select entry.Key;

            return $"{anonymous.Name}{anonymous.Count}{lambda(1)}{string.Concat(queried)}";
        }
    }

    /// <summary>C# 4: <c>dynamic</c>, named and optional arguments, generic variance.</summary>
    public class SynV4
    {
        /// <summary>A covariant interface, which C# 3 could not declare.</summary>
        /// <typeparam name="TOut">What comes out.</typeparam>
        public interface ISynV4Source<out TOut>
        {
            /// <summary>The value.</summary>
            TOut Value { get; }
        }

        /// <summary>A contravariant delegate.</summary>
        /// <typeparam name="TIn">What goes in.</typeparam>
        /// <param name="value">The value.</param>
        public delegate void SynV4Sink<in TIn>(TIn value);

        /// <summary>Optional parameters, to be called by name.</summary>
        /// <param name="scale">How much.</param>
        /// <param name="offset">From where.</param>
        /// <returns>The result.</returns>
        public static int Scaled(int scale = 1, int offset = 0) => (scale * 10) + offset;

        /// <summary>Named arguments out of order, and a <c>dynamic</c> receiver.</summary>
        /// <returns>A digest.</returns>
        public static string Call()
        {
            var named = Scaled(offset: 3, scale: 2);
            dynamic loose = "text";
            var length = loose.Length;

            return $"{named}{length}";
        }
    }

    /// <summary>C# 5: <c>async</c>/<c>await</c> and the caller-info attributes.</summary>
    public class SynV5
    {
        /// <summary>An async method, C# 5's one large feature.</summary>
        /// <param name="value">What to carry.</param>
        /// <returns>The value.</returns>
        public static async Task<int> CarryAsync(int value)
        {
            await Task.Yield();

            return value;
        }

        /// <summary>Caller info, filled in by the compiler at each call site.</summary>
        /// <param name="message">What happened.</param>
        /// <param name="caller">Supplied by the compiler.</param>
        /// <param name="file">Supplied by the compiler.</param>
        /// <param name="line">Supplied by the compiler.</param>
        /// <returns>A digest.</returns>
        public static string Located(
            string message,
            [CallerMemberName] string caller = "",
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0) => $"{message}{caller}{file.Length}{line}";

        /// <summary>Calls it, so the three defaults are actually synthesised at a site.</summary>
        /// <returns>A digest.</returns>
        public static string Locate() => Located("here");
    }

    /// <summary>C# 6: interpolation, <c>nameof</c>, expression bodies, <c>?.</c>, filters.</summary>
    public class SynV6
    {
        /// <summary>An auto-property initializer, new in C# 6.</summary>
        public string Name { get; } = "six";

        /// <summary>An expression-bodied member.</summary>
        public int Length => Name.Length;

        /// <summary>Interpolation, <c>nameof</c>, <c>?.</c>, an exception filter, an index initializer.</summary>
        /// <param name="text">Something to measure.</param>
        /// <returns>A digest.</returns>
        public string Measure(string? text)
        {
            var byIndex = new Dictionary<int, string> { [1] = "one", [2] = "two" };

            try
            {
                var length = text?.Length ?? 0;

                return $"{nameof(Measure)}:{length}:{byIndex[1]}";
            }
            catch (KeyNotFoundException caught) when (caught.Message.Length > 0)
            {
                return nameof(KeyNotFoundException);
            }
        }
    }

    /// <summary>C# 7.0: out variables, tuples, patterns, local functions, <c>ref</c> returns.</summary>
    public class SynV7
    {
        private int _slot;

        /// <summary>A <c>ref</c> return, which hands out an alias to a field.</summary>
        /// <returns>The field, by reference.</returns>
        public ref int Slot() => ref _slot;

        /// <summary>An out variable, a tuple, a deconstruction and a pattern switch.</summary>
        /// <param name="text">Something to parse.</param>
        /// <returns>A digest.</returns>
        public string Parse(string text)
        {
            if (!int.TryParse(text, out var parsed))
            {
                throw new ArgumentException(nameof(text), nameof(text));
            }

            (int Row, int Column) at = (parsed, parsed + 1);
            var (row, column) = at;

            int Doubled(int value) => value * 2;

            var separated = 1_000_000;
            var binary = 0b1010_1010;

            switch ((object)parsed)
            {
                case int small when small < 10:
                    return $"{row}{column}{Doubled(small)}{separated}{binary}";

                default:
                    return $"{at.Row}{at.Column}";
            }
        }
    }

    /// <summary>C# 7.1: the <c>default</c> literal, inferred tuple names, generic patterns.</summary>
    /// <typeparam name="T">Anything.</typeparam>
    public class SynV7_1<T>
    {
        /// <summary>The bare <c>default</c> literal, with no type after it.</summary>
        public T Held { get; set; } = default!;

        /// <summary>Inferred tuple element names and a pattern over a type parameter.</summary>
        /// <param name="value">The value.</param>
        /// <returns>A digest.</returns>
        public string Infer(T value)
        {
            var count = 1;
            var label = "one";
            var pair = (count, label);

            if (value is T typed)
            {
                return $"{pair.count}{pair.label}{typed}";
            }

            return $"{pair.count}";
        }
    }

    /// <summary>C# 7.2: <c>in</c> parameters, <c>readonly struct</c>, <c>private protected</c>.</summary>
    public class SynV7_2
    {
        /// <summary>A <c>readonly struct</c>, whose members may not mutate it.</summary>
        public readonly struct SynV7_2Weight
        {
            /// <summary>How much.</summary>
            public readonly int Amount;

            /// <summary>Constructs.</summary>
            /// <param name="amount">How much.</param>
            public SynV7_2Weight(int amount) => Amount = amount;
        }

        /// <summary>The accessibility C# 7.2 added.</summary>
        private protected int Guarded => 2;

        /// <summary>An <c>in</c> parameter and a <c>ref readonly</c> return.</summary>
        /// <param name="weight">Read only, by reference.</param>
        /// <param name="cells">Where to point.</param>
        /// <returns>The first cell, by read-only reference.</returns>
        public static ref readonly int Bounded(in SynV7_2Weight weight, int[] cells)
        {
            var leading = 0b_1010;
            cells[0] = weight.Amount + leading + 0;

            return ref cells[0];
        }

        /// <summary>A non-trailing named argument, which C# 7.2 allowed.</summary>
        /// <returns>A digest.</returns>
        public string Call() => $"{SynV4.Scaled(scale: 3, 4)}{Guarded}";
    }

    /// <summary>C# 7.3: ref reassignment, <c>stackalloc</c> initializers, <c>unmanaged</c>.</summary>
    public class SynV7_3
    {
        /// <summary>An attribute on the backing field of an auto-property.</summary>
        [field: Obsolete("the backing field, not the property")]
        public int Tagged { get; set; }

        /// <summary>Ref reassignment, a stackalloc initializer and tuple equality.</summary>
        /// <param name="cells">Where to point.</param>
        /// <returns>A digest.</returns>
        public static unsafe string Reassign(int[] cells)
        {
            ref var head = ref cells[0];
            head = ref cells[1];

            Span<int> stacked = stackalloc int[] { 1, 2, 3 };
            var equal = (1, 2) == (1, 2);

            return $"{head}{stacked[2]}{equal}{Sized<int>()}";
        }

        /// <summary>An <c>unmanaged</c> constraint.</summary>
        /// <typeparam name="T">Unmanaged.</typeparam>
        /// <returns>Its size.</returns>
        public static unsafe int Sized<T>()
            where T : unmanaged => sizeof(T);
    }

    /// <summary>C# 8: switch expressions, patterns, ranges, <c>??=</c>, default interface members.</summary>
    public class SynV8
    {
        /// <summary>A default interface member, which C# 8 allowed.</summary>
        public interface ISynV8Named
        {
            /// <summary>The name.</summary>
            string Name { get; }

            /// <summary>Implemented in the interface itself.</summary>
            /// <returns>The name, upper-cased.</returns>
            string Shout() => Name.ToUpperInvariant();
        }

        /// <summary>A <c>readonly</c> member on a mutable struct.</summary>
        public struct SynV8Cell
        {
            /// <summary>How much.</summary>
            public int Weight;

            /// <summary>Readonly, so it may not mutate the struct.</summary>
            /// <returns>The weight.</returns>
            public readonly int Read() => Weight;
        }

        /// <summary>A switch expression over four pattern forms.</summary>
        /// <param name="value">The value.</param>
        /// <returns>A label.</returns>
        public static string Match(object? value) => value switch
        {
            null => "none",
            int i when i > 0 => "positive",
            (int row, int column) => $"{row}:{column}",
            SynEntry { Weight: > 1, Key: var key } => key,
            _ => "other",
        };

        /// <summary>Ranges, a using declaration, a static local function and <c>??=</c>.</summary>
        /// <param name="cells">The cells.</param>
        /// <param name="label">Possibly nothing.</param>
        /// <returns>A digest.</returns>
        public static string Slice(int[] cells, string? label)
        {
            label ??= "unlabelled";

            using var reader = new System.IO.StringReader(label);
            var tail = cells[1..];
            var last = cells[^1];
            var whole = cells[..];

            static int Sum(int[] values) => values.Sum();

            var verbatim = @$"{label}\notanescape";

            return $"{tail.Length}{last}{whole.Length}{Sum(cells)}{verbatim}{reader.Peek()}";
        }

        /// <summary>An async stream, consumed with <c>await foreach</c>.</summary>
        /// <returns>How many arrived.</returns>
        public static async Task<int> StreamAsync()
        {
            var seen = 0;

            await foreach (var item in Produce())
            {
                seen += item;
            }

            return seen;

            static async IAsyncEnumerable<int> Produce()
            {
                await Task.Yield();
                yield return 1;
            }
        }
    }

    /// <summary>C# 9: records, <c>init</c>, relational patterns, target-typed <c>new</c>.</summary>
    /// <remarks>
    /// C# 9's records are declared at file scope in <c>Records.cs</c>, and its function
    /// pointers in <c>TypeKinds.cs</c>; what is left here is the rest.
    /// </remarks>
    public class SynV9
    {
        /// <summary>A base for the covariant return below.</summary>
        public class SynV9Base
        {
            /// <summary>Copies.</summary>
            /// <returns>A copy.</returns>
            public virtual SynV9Base Duplicate() => new();
        }

        /// <summary>A covariant return type, which C# 9 allowed.</summary>
        public sealed class SynV9Leaf : SynV9Base
        {
            /// <inheritdoc/>
            public override SynV9Leaf Duplicate() => new();
        }

        /// <summary>An <c>init</c>-only property.</summary>
        public int Weight { get; init; }

        /// <summary>A native-sized integer.</summary>
        public nint Handle { get; init; }

        /// <summary>Relational, logical and negated patterns; target-typed <c>new</c>.</summary>
        /// <param name="value">The value.</param>
        /// <returns>A label.</returns>
        public static string Match(int value)
        {
            SynV9 targeted = new() { Weight = 1, Handle = 2 };
            Func<int, int> lambda = static v => v;

            var label = value switch
            {
                > 0 and < 10 => "small",
                >= 10 or < -10 => "far",
                not 0 => "near",
                _ => "zero",
            };

            return $"{label}{targeted.Weight}{targeted.Handle}{lambda(value)}";
        }

        /// <summary>A module initializer, which runs before any other code in the assembly.</summary>
        [ModuleInitializer]
        internal static void Initialize() => GC.KeepAlive(nameof(SynV9));
    }

    /// <summary>C# 10: extended property patterns, constant interpolation, lambda improvements.</summary>
    /// <remarks>
    /// C# 10's record structs are in <c>Records.cs</c>, its file-scoped namespaces are the
    /// form every other file in this project uses, its global using is in
    /// <c>Aliases.cs</c>, and its parameterless struct constructor is
    /// <see cref="SynTally"/>.
    /// </remarks>
    public class SynV10
    {
        /// <summary>A constant interpolated string, folded at compile time.</summary>
        public const string Prefix = $"{nameof(SynV10)}/";

        /// <summary>An extended property pattern, which C# 10 flattened.</summary>
        /// <param name="stamped">The value.</param>
        /// <returns>A label.</returns>
        public static string Match(SynStamped? stamped) => stamped switch
        {
            { Key.Length: > 3, Stamp: > 0 } => Prefix + "long",
            { Key.Length: 0 } => Prefix + "empty",
            _ => Prefix + "other",
        };

        /// <summary>A lambda with an explicit return type and an attribute (C# 10).</summary>
        /// <returns>The lambda.</returns>
        public static Func<int, int> Annotated() =>
            [Obsolete("a lambda may wear an attribute")] static int (int value) => value + 1;

        /// <summary>An argument expression captured by the compiler as a string.</summary>
        /// <param name="condition">What was checked.</param>
        /// <param name="expression">The source text of it.</param>
        /// <returns>The source text.</returns>
        public static string Captured(bool condition, [CallerArgumentExpression(nameof(condition))] string expression = "")
            => condition ? expression : string.Empty;

        /// <summary>Calls it, so the compiler synthesises the argument text.</summary>
        /// <returns>The source text of the argument.</returns>
        public static string Capture() => Captured(1 + 1 == 2);
    }

    /// <summary>C# 11: raw strings, list patterns, generic attributes, <c>u8</c> literals.</summary>
    /// <remarks>
    /// C# 11's <c>required</c> is in <see cref="SynAutoProps"/>, its <c>ref</c> field in
    /// <see cref="SynRefStruct"/>, its static abstract interface members in
    /// <see cref="ISynSeeded{TSelf}"/>, and its checked and <c>&gt;&gt;&gt;</c> operators in
    /// <see cref="SynOperators"/>. Its <c>file</c> types are quarantined: a file-local type
    /// is one of the five shapes that kills an indexing run.
    /// </remarks>
    public class SynV11
    {
        /// <summary>A generic attribute, which C# 11 allowed.</summary>
        /// <typeparam name="T">What is tagged.</typeparam>
        [AttributeUsage(AttributeTargets.All)]
        public sealed class SynTaggedAttribute<T> : Attribute
        {
            /// <summary>What was tagged with.</summary>
            public Type Tagged => typeof(T);
        }

        /// <summary>A struct whose constructor leaves a field auto-defaulted (C# 11).</summary>
        public struct SynV11Partial
        {
            /// <summary>Assigned by the constructor.</summary>
            public int Assigned;

            /// <summary>Left to the compiler to default.</summary>
            public int Defaulted;

            /// <summary>Assigns one field of two.</summary>
            /// <param name="assigned">The one.</param>
            public SynV11Partial(int assigned) => Assigned = assigned;
        }

        /// <summary>A raw string literal, with an interpolation holding a newline.</summary>
        [SynTaggedAttribute<int>]
        public static string Raw(int value) =>
            $$"""
            {
              "value": {{
                  value
              }},
              "quoted": "{ not a hole }"
            }
            """;

        /// <summary>A list pattern, a slice pattern and a UTF-8 literal.</summary>
        /// <param name="cells">The cells.</param>
        /// <returns>A label.</returns>
        public static string Match(int[] cells)
        {
            ReadOnlySpan<byte> utf8 = "surface"u8;

            var label = cells switch
            {
                [] => "empty",
                [var only] => $"one:{only}",
                [1, .., var last] => $"leading-one:{last}",
                [.. var all] => $"any:{all.Length}",
            };

            return $"{label}{utf8.Length}";
        }

        /// <summary>A <c>ReadOnlySpan&lt;char&gt;</c> matched against a string constant.</summary>
        /// <param name="window">The window.</param>
        /// <returns>Whether it said so.</returns>
        public static bool Spelled(ReadOnlySpan<char> window) => window is "surface";

        /// <summary><c>nameof</c> of a parameter, inside an attribute on the method.</summary>
        /// <param name="input">Possibly nothing.</param>
        /// <returns>The input.</returns>
        [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull(nameof(input))]
        public static string? Echo(string? input) => input;
    }

    /// <summary>C# 12: collection expressions and spreads.</summary>
    /// <remarks>
    /// C# 12's primary constructors are in <c>PrimaryConstructors.cs</c>, its inline array
    /// in <see cref="SynInlineArray"/>, its aliased types in <c>Aliases.cs</c>, its
    /// <c>ref readonly</c> parameter and default lambda parameter in
    /// <see cref="SynParameters"/>.
    /// </remarks>
    public class SynV12
    {
        /// <summary>Collection expressions, targeting four different types.</summary>
        /// <returns>A digest.</returns>
        public static string Collections()
        {
            int[] array = [1, 2, 3];
            List<int> list = [4, 5];
            Span<int> span = [6];
            int[] spread = [.. array, .. list, 7];
            SynEntry[] ofEntries = [new("alpha", 1)];
            int[] empty = [];

            return $"{array.Length}{list.Count}{span.Length}{spread.Length}" +
                $"{ofEntries.Length}{empty.Length}";
        }

        /// <summary>Reads an inline array through its indexer and its span conversion.</summary>
        /// <returns>A digest.</returns>
        public static string Inline()
        {
            var buffer = default(SynInlineArray);
            buffer[0] = 1;
            buffer[3] = 4;

            Span<int> window = buffer;

            return $"{buffer[0]}{buffer[3]}{window.Length}";
        }
    }

    /// <summary>C# 13: <c>\e</c>, implicit indexer access, <c>ref</c> in iterators.</summary>
    /// <remarks>
    /// C# 13's <c>params</c> collections are in <see cref="SynOverloads.Total"/> and its
    /// <c>allows ref struct</c> constraint in <see cref="SynConstrained{T1, T2, T3}"/>. Its
    /// partial properties are quarantined, for the same reason as partial methods.
    /// </remarks>
    public class SynV13
    {
        /// <summary>A holder whose indexer is reached from an object initializer.</summary>
        public sealed class SynV13Holder
        {
            /// <summary>Get-only, and still writable through its indexer.</summary>
            public int[] Cells { get; } = new int[4];
        }

        /// <summary>The escape character, which C# 13 gave a shorthand.</summary>
        public const string Reset = "\e[0m";

        /// <summary>An implicit indexer access inside an object initializer.</summary>
        /// <returns>A digest.</returns>
        public static string Implicit()
        {
            var holder = new SynV13Holder { Cells = { [0] = 1, [^1] = 9 } };

            return $"{holder.Cells[0]}{holder.Cells[3]}{Reset.Length}";
        }

        /// <summary>A <c>ref</c> local inside an iterator, which C# 13 allowed.</summary>
        /// <param name="cells">The cells.</param>
        /// <returns>The cells, one at a time.</returns>
        public static IEnumerable<int> RefInIterator(int[] cells)
        {
            {
                ref var head = ref cells[0];
                head = 1;
            }

            yield return cells[0];
        }

        /// <summary>The dedicated lock type, which C# 13 gave a statement form.</summary>
        /// <returns>What the guarded region computed.</returns>
        public static int Guarded()
        {
            var gate = new System.Threading.Lock();

            lock (gate)
            {
                return 1;
            }
        }

        /// <summary>The overload the compiler is told to prefer.</summary>
        /// <param name="values">However many.</param>
        /// <returns>Which overload ran.</returns>
        [OverloadResolutionPriority(1)]
        public static string Preferred(params ReadOnlySpan<int> values) => $"span:{values.Length}";

        /// <summary>The overload it is told to avoid.</summary>
        /// <param name="values">However many.</param>
        /// <returns>Which overload ran.</returns>
        public static string Preferred(params int[] values) => $"array:{values.Length}";
    }

    /// <summary>C# 14: null-conditional assignment and unbound-generic <c>nameof</c>.</summary>
    /// <remarks>
    /// C# 14's extension blocks are in <c>ExtensionBlock.cs</c> and its <c>field</c>
    /// keyword in <see cref="SynAutoProps.Trace"/>. Its partial constructors and partial
    /// events are quarantined. This rung is the one that does not exist in the pinned
    /// Roslyn 4.14 as a <em>version</em>: <c>LanguageVersion.CSharp14</c> is absent there,
    /// so the project asks for <c>preview</c>, which that compiler does have.
    /// </remarks>
    public class SynV14
    {
        /// <summary>Somewhere to assign through.</summary>
        public sealed class SynV14Slot
        {
            /// <summary>Assignable.</summary>
            public int Weight { get; set; }
        }

        /// <summary>A null-conditional assignment, which C# 14 allowed on the left.</summary>
        /// <param name="slot">Possibly nothing.</param>
        /// <returns>What was there afterwards.</returns>
        public static int Assign(SynV14Slot? slot)
        {
            slot?.Weight = 7;
            slot?.Weight += 1;

            return slot?.Weight ?? 0;
        }

        /// <summary><c>nameof</c> of an unbound generic type, which C# 14 allowed.</summary>
        /// <returns>The names.</returns>
        public static string Unbound() => $"{nameof(List<>)}{nameof(Dictionary<,>)}";
    }
}
