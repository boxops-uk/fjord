using System;

namespace Surface.Synthesised;

/// <summary>A class that declares no constructor at all (15.11.5).</summary>
/// <remarks>
/// The compiler supplies <c>public SynImplicitCtor()</c>. It has no location of its own
/// beyond the type's identifier and no declaring syntax reference, so "every class has a
/// constructor" is true of the symbol table and false of the syntax tree. This is the
/// commonest synthesised member in any real corpus, and the one most likely to be simply
/// absent from an index.
/// </remarks>
public class SynImplicitCtor
{
    /// <summary>A field, so the type is not empty.</summary>
    public int Depth;
}

/// <summary>A static class whose static constructor is synthesised (15.12).</summary>
/// <remarks>
/// The initializer on <see cref="Registry"/> has to run somewhere, so the compiler emits a
/// static constructor to hold it — <c>MethodKind.StaticConstructor</c>, no declaration.
/// Compare <see cref="SynStaticInit"/>, which declares one.
/// </remarks>
public static class SynImplicitStaticInit
{
    /// <summary>Initialized, which is what forces the synthesis.</summary>
    public static readonly string[] Registry = ["alpha", "beta"];
}

/// <summary>A class that declares its static constructor (15.12).</summary>
public class SynStaticInit
{
    /// <summary>Set by the declared static constructor below.</summary>
    public static readonly int Ceiling;

    /// <summary>Also set there, from the initializer the compiler moves into it.</summary>
    public static int Floor = -1;

    static SynStaticInit()
    {
        Ceiling = 128;
    }
}

/// <summary>A class with a finalizer (15.13).</summary>
/// <remarks>
/// <c>~SynFinalized()</c> is <c>MethodKind.Destructor</c>. Its metadata name is
/// <c>Finalize</c>, which appears nowhere in source, and it overrides
/// <c>object.Finalize</c> — so the declared name, the metadata name and the overridden
/// name are three different strings for one declaration.
/// </remarks>
public sealed class SynFinalized
{
    private readonly IntPtr _handle = IntPtr.Zero;

    /// <summary>The finalizer.</summary>
    ~SynFinalized()
    {
        GC.KeepAlive(_handle);
    }
}

/// <summary>Automatically implemented properties (15.7.4).</summary>
/// <remarks>
/// Each auto-property here contributes three symbols that share the property's span: the
/// property, its accessors (<c>MethodKind.PropertyGet</c> / <c>MethodKind.PropertySet</c>)
/// and a backing field named <c>&lt;Name&gt;k__BackingField</c>. The accessors have
/// <em>accessor declaration</em> syntax where the body is omitted, so a syntax walk does
/// see them; the backing field has nothing at all. <see cref="Trace"/> uses the C# 14
/// <c>field</c> keyword, where the backing field is <em>named</em> in source but still has
/// no declaration.
/// </remarks>
public class SynAutoProps
{
    /// <summary>Get and set: two accessors, one backing field.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Get only, with an initializer that writes the backing field directly.</summary>
    public int Depth { get; } = 3;

    /// <summary>An <c>init</c> accessor, which is still <c>MethodKind.PropertySet</c>.</summary>
    public bool Sealed { get; init; }

    /// <summary>A required member (C# 11), which changes the constructor's contract only.</summary>
    public required string Key { get; set; }

    /// <summary>
    /// The <c>field</c> keyword (C# 14): a body that names the backing field, so the field
    /// is referenced by source that never declares it.
    /// </summary>
    public string Trace
    {
        get => field ?? "none";
        set => field = value;
    }

    /// <summary>A static auto-property, whose backing field is static too.</summary>
    public static int Instances { get; private set; }

    /// <summary>An expression-bodied property: one accessor, no backing field.</summary>
    public string Summary => $"{Name}/{Depth}";

    /// <summary>Bumps the static property, so its private setter is referenced.</summary>
    public static void Seen() => Instances++;
}

/// <summary>A field-like event and a hand-written one (15.8.1, 15.8.2).</summary>
/// <remarks>
/// <see cref="Changed"/> is field-like, so the compiler synthesises
/// <c>add_Changed</c> (<c>MethodKind.EventAdd</c>), <c>remove_Changed</c>
/// (<c>MethodKind.EventRemove</c>) and a private field — and that field's name is
/// <c>Changed</c>, exactly the event's name, in the same type at the same span. A
/// <c>SymbolKind.Event</c> and a <c>SymbolKind.Field</c> that agree on every component an
/// index is likely to key on is the sharpest hazard in this project.
///
/// <see cref="Reset"/> declares both accessors, so nothing is synthesised and no field
/// appears. The two events are otherwise identical to a caller.
/// </remarks>
public class SynFieldEvent
{
    private EventHandler? _reset;

    /// <summary>Field-like: accessors and a same-named backing field are synthesised.</summary>
    public event EventHandler? Changed;

    /// <summary>Static and field-like: the synthesised field is static.</summary>
    public static event EventHandler? Discarded;

    /// <summary>Hand-written accessors, so no field and no synthesis.</summary>
    public event EventHandler? Reset
    {
        add => _reset += value;
        remove => _reset -= value;
    }

    /// <summary>Raises both, referencing the synthesised and the declared alike.</summary>
    public void Raise()
    {
        Changed?.Invoke(this, EventArgs.Empty);
        Discarded?.Invoke(null, EventArgs.Empty);
        _reset?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>An enum, whose one instance field no source declares (19.4).</summary>
/// <remarks>
/// Every enum carries an instance field named <c>value__</c> of its underlying type: a
/// <c>SymbolKind.Field</c> with no declaration, no doc comment and a name that is not a
/// C# identifier's usual shape. The named members beside it are constants, not fields of
/// the underlying type, and the enum's base type <c>System.Enum</c> appears in no base list.
/// </remarks>
public enum SynEnumBits : byte
{
    /// <summary>The zero value.</summary>
    None = 0,

    /// <summary>The low bit.</summary>
    Low = 1,

    /// <summary>The high bit.</summary>
    High = 2,

    /// <summary>Both, written as a reference to the two above.</summary>
    Both = Low | High,
}

/// <summary>A delegate type — four synthesised members from one declaration (21.2).</summary>
/// <remarks>
/// The compiler emits <c>.ctor(object, IntPtr)</c>, <c>Invoke</c>
/// (<c>MethodKind.DelegateInvoke</c>), <c>BeginInvoke</c> and <c>EndInvoke</c>, all sharing
/// the <c>DelegateDeclarationSyntax</c> as their location. The delegate's parameter list is
/// written once and appears on two of them; <c>BeginInvoke</c> extends it with a callback
/// and a state object that are named nowhere in source.
/// </remarks>
/// <param name="entry">The entry that changed.</param>
/// <param name="depth">How deep the change was.</param>
/// <returns>Whether to keep going.</returns>
public delegate bool SynCallback(SynEntry entry, int depth);

/// <summary>A generic delegate, so the synthesis is generic too.</summary>
/// <typeparam name="TValue">What is carried.</typeparam>
/// <param name="value">The value.</param>
public delegate void SynSink<in TValue>(TValue value);

/// <summary>Accessor shapes, including the type's single indexer (15.9).</summary>
/// <remarks>
/// One indexer per type is deliberate: the corpus's quarantine holds the two-indexer shape,
/// because two <c>this[...]</c> declarations mint one metadata name (<c>Item</c>) and the
/// write is refused. The indexer's accessors are <c>MethodKind.PropertyGet</c> and
/// <c>MethodKind.PropertySet</c>, indistinguishable by kind from a property's.
/// </remarks>
public class SynAccessors
{
    private readonly int[] _cells = new int[4];

    /// <summary>The type's only indexer.</summary>
    /// <param name="index">Which cell.</param>
    public int this[int index]
    {
        get => _cells[index];
        set => _cells[index] = value;
    }

    /// <summary>A property whose accessors have differing accessibility.</summary>
    public int Total
    {
        get;
        private set;
    }

    /// <summary>Sums the cells, so the private setter is referenced.</summary>
    public void Recount()
    {
        var sum = 0;

        foreach (var cell in _cells)
        {
            sum += cell;
        }

        Total = sum;
    }
}
