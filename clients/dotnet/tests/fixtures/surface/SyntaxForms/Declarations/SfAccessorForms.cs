namespace Surface.SyntaxForms.Declarations;

// The five accessor kinds a compiling file can hold — get, set, init, add, remove — each
// written with a block body so the accessor node is the declaring syntax rather than an
// `ArrowExpressionClause`. `UnknownAccessorDeclaration` is the sixth, and it exists only in
// error recovery: an accessor whose keyword the parser did not recognise is CS1014, so no
// compiling file can carry one.
//
// `AccessorDeclarationSyntax` derives from none of the six bases the walk's declaration
// switch names, so every declaration in this file is one the indexer reaches as neither a
// definition nor a counted loss.

/// <summary>
/// A settings record whose every accessor is spelled out, so each of the five accessor
/// kinds has a node of its own.
/// </summary>
public sealed class SfAccessorHost
{
    private int _limit;
    private string _name = "default";
    private System.EventHandler? _changed;

    /// <summary>GetAccessorDeclaration and SetAccessorDeclaration, both with block bodies.</summary>
    public int Limit
    {
        get
        {
            return _limit;
        }

        set
        {
            _limit = value;
        }
    }

    /// <summary>GetAccessorDeclaration and InitAccessorDeclaration.</summary>
    public string Name
    {
        get
        {
            return _name;
        }

        init
        {
            _name = value;
        }
    }

    /// <summary>AddAccessorDeclaration and RemoveAccessorDeclaration.</summary>
    public event System.EventHandler Changed
    {
        add
        {
            _changed += value;
        }

        remove
        {
            _changed -= value;
        }
    }

    /// <summary>
    /// An indexer with a get accessor, so the get kind is present under a
    /// <c>BasePropertyDeclarationSyntax</c> that is not a property. One indexer only.
    /// </summary>
    /// <param name="index">Which slot.</param>
    /// <returns>The slot's value.</returns>
    public int this[int index]
    {
        get
        {
            return index + _limit;
        }
    }

    /// <summary>Raises <see cref="Changed"/> so the field-like backing is not dead.</summary>
    public void Raise() => _changed?.Invoke(this, System.EventArgs.Empty);
}

/// <summary>
/// The C# 14 <c>field</c> keyword: a property whose accessors name a backing field that has
/// no declaration anywhere in source.
/// </summary>
/// <remarks>
/// <b>An identifier that is a reference to something nobody declared.</b> <c>field</c>
/// parses as an <c>IdentifierNameSyntax</c>, so a reference dispatch keyed on
/// <c>SimpleNameSyntax</c> visits it and <c>GetSymbolInfo</c> hands back the synthesised
/// backing field — a symbol whose only location is inside the accessor that used it. The
/// index therefore holds a reference whose target was never declared, which is a different
/// failure from an unresolved name and is counted as neither.
/// </remarks>
public sealed class SfFieldKeywordHost
{
    /// <summary>A property whose get and set both name <c>field</c>.</summary>
    public int Counted
    {
        get => field;
        set => field = value < 0 ? 0 : value;
    }

    /// <summary>
    /// A property where only the setter names <c>field</c> — an auto-implemented getter
    /// beside a hand-written setter, which is the shape the <c>field</c> keyword exists for.
    /// </summary>
    public string Trimmed
    {
        get;
        set => field = value.Trim();
    } = string.Empty;
}
