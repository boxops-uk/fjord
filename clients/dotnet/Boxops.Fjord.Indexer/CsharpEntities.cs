using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

using Boxops.Fjord.Client;

using Microsoft.CodeAnalysis;

namespace Boxops.Fjord.Indexer;

/// <summary>
/// <para>
/// <b>A Roslyn symbol as <c>csharp.*</c> facts.</b>
/// </para>
/// <para>
/// The replacement for one <c>src.Decl</c> over every kind of declaration: a predicate per
/// kind, and three unions over them. It is a rewrite rather than a translation, and the
/// shape of the difference is that <b>identity and location are separated</b> — an entity
/// is keyed on what the compiler knows about it, and where it was written is a
/// <c>DefinitionLocation</c> beside it. So a synthesised member — a default constructor, a
/// record's generated <c>Equals</c> — has an entity and no location, where the old model
/// needed a line number and had to name it after its type.
/// </para>
/// <para>
/// <b>Nothing here holds a fact id.</b> Every reference is the target fact nested inline,
/// so the whole entity graph is built bottom-up and the server interns it; the memo exists
/// to stop rebuilding it, not to remember an identity. That is also why a cycle is
/// impossible to write and possible to *build*, and why the memo cannot be what breaks
/// one: a symbol's entry is filled only once its whole nested chain has been walked, so a
/// symbol reached from itself finds nothing there. The per-thread set below is the break,
/// and deleting it on the strength of the memo recurses until the stack runs out.
/// </para>
/// <para>
/// <b>Several walker threads call this at once, and nothing gates them.</b> That is what
/// the concurrent maps, the per-thread in-progress set and the interlocked counter are
/// for. A memo entry is published by whichever thread wins its <c>TryAdd</c> and only
/// that thread emits, so one fact reaches the sink however many threads raced to build
/// it — a gate assumed here instead is how that machinery gets simplified away.
/// </para>
/// </summary>
internal sealed class CsharpEntities(Action<uint, FjordFact> emit)
{
    private readonly ConcurrentDictionary<ISymbol, FjordFact?> _entities =
        new(SymbolEqualityComparer.Default);

    private readonly ConcurrentDictionary<string, FjordFact> _names = new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<ISymbol, FjordFact> _namespaces =
        new(SymbolEqualityComparer.Default);

    /// <summary>
    /// The symbols this thread is in the middle of building.
    /// </summary>
    /// <remarks>
    /// <b>Per thread, and reached only where the memo misses.</b> A cycle is broken by
    /// seeing a symbol already on this thread's own stack —
    /// <c>class C&lt;T&gt; where T : C&lt;T&gt;</c> is the shape — and it must not be
    /// broken by seeing one another thread happens to be working on. A shared in-progress
    /// marker would do exactly that, and the two threads would write facts of different
    /// depths under one key: a conflict this producer created, on a machine with more
    /// cores.
    /// </remarks>
    [ThreadStatic]
    private static HashSet<ISymbol>? _building;

    private int _inexpressible;

    /// <summary>Types this producer could not express, counted rather than hidden.</summary>
    public int Inexpressible => Volatile.Read(ref _inexpressible);

    // ---- the vocabularies ------------------------------------------------------------

    private static FjordValue Tag(uint disc) => FjordValue.Alt(disc, FjordValue.Rec());

    private static FjordValue Bool(bool value) => Tag(value ? 1u : 0u);

    /// <summary>
    /// Roslyn's accessibility as `csharp.Accessibility`.
    /// </summary>
    /// <remarks>
    /// The `friend`, `protectedAndFriend` and `protectedOrFriend` alternatives are Visual
    /// Basic's spelling of the same three things, and Roslyn gives them the *same* enum
    /// values as their C# names — so a C# producer never emits them, and a consumer
    /// filtering on `internal` must not expect `friend` to be absent from another
    /// producer's index.
    /// </remarks>
    private static FjordValue Access(Accessibility accessibility) => accessibility switch
    {
        Accessibility.Internal => Tag(1u),
        Accessibility.Private => Tag(3u),
        Accessibility.Protected => Tag(4u),
        Accessibility.ProtectedAndInternal => Tag(6u),
        Accessibility.ProtectedOrInternal => Tag(8u),
        Accessibility.Public => Tag(9u),
        _ => Tag(2u),
    };

    private static FjordValue Ref(RefKind kind) => kind switch
    {
        RefKind.In => Tag(0u),
        RefKind.Out => Tag(2u),
        RefKind.Ref => Tag(3u),
        RefKind.RefReadOnlyParameter => Tag(4u),
        _ => Tag(1u),
    };

    private static FjordValue Variance(VarianceKind kind) => kind switch
    {
        VarianceKind.In => Tag(0u),
        VarianceKind.Out => Tag(2u),
        _ => Tag(1u),
    };

    private static FjordValue Just(FjordValue payload) => FjordValue.Alt(1u, payload);

    private static readonly FjordValue Nothing = Tag(0u);

    private static FjordValue Maybe(FjordFact? fact) =>
        fact is null ? Nothing : Just(FjordValue.Of(FjordRef.To(fact)));

    // ---- names -----------------------------------------------------------------------

    /// <summary>
    /// `csharp.Name` — an interned identifier.
    /// </summary>
    public FjordFact Name(string text)
    {
        if (_names.TryGetValue(text, out var known))
        {
            return known;
        }

        // **Built outside, published with `TryAdd`, emitted by the winner.** The fact is a
        // function of the text, so two threads that race build the same one and only the
        // thread that published it says so — a factory that emitted would emit once per
        // loser as well, and `ConcurrentDictionary` runs the losing factories too.
        var fact = new FjordFact(DotnetIndex.Name, FjordValue.Of(text));

        if (!_names.TryAdd(text, fact))
        {
            return _names[text];
        }

        emit(DotnetIndex.Name, fact);

        // The search index, written beside the name it lowercases: a case-insensitive
        // prefix seeks on `nameLowercase`, so it has to lead its own predicate.
        emit(DotnetIndex.NameLowerCase, new FjordFact(
            DotnetIndex.NameLowerCase,
            FjordValue.Rec(
                FjordValue.Of(text.ToLowerInvariant()),
                FjordValue.Of(FjordRef.To(fact)))));

        return fact;
    }

    /// <summary>
    /// `csharp.Namespace` — a namespace and the one it is nested in.
    /// </summary>
    /// <remarks>
    /// The global namespace is a fact too, named by the empty string, because
    /// `FullName.containingNamespace` is not optional: every full name needs one, and a
    /// sentinel like `&lt;global&gt;` would invent an identifier C# does not have.
    /// </remarks>
    public FjordFact Namespace(INamespaceSymbol symbol)
    {
        if (_namespaces.TryGetValue(symbol, out var known))
        {
            return known;
        }

        var containing = symbol.ContainingNamespace is { IsGlobalNamespace: false } parent
            ? Namespace(parent)
            : symbol.IsGlobalNamespace ? null : Namespace(symbol.ContainingNamespace);

        var fact = new FjordFact(DotnetIndex.Namespace, FjordValue.Rec(
            FjordValue.Of(FjordRef.To(Name(symbol.Name))),
            Maybe(containing)));

        if (!_namespaces.TryAdd(symbol, fact))
        {
            return _namespaces[symbol];
        }

        emit(DotnetIndex.Namespace, fact);
        return fact;
    }

    /// <summary>
    /// `csharp.FullName` — a simple name plus the namespace containing it.
    /// </summary>
    /// <remarks>
    /// The <i>namespace</i>, not the containing type: a nested type's nesting is
    /// `containingType` on its own predicate, and its full name is the pair a search for
    /// "the class named X in namespace Y" seeks on.
    /// </remarks>
    public FjordFact FullName(ISymbol symbol) =>
        new(DotnetIndex.FullName, FjordValue.Rec(
            FjordValue.Of(FjordRef.To(Name(symbol.Name))),
            FjordValue.Of(FjordRef.To(Namespace(symbol.ContainingNamespace)))));

    // ---- the type unions -------------------------------------------------------------

    /// <summary>
    /// `csharp.NamedType` — a class, interface, record or struct, as the union.
    /// </summary>
    public FjordValue? Named(INamedTypeSymbol type)
    {
        if (Entity(type) is not { } fact)
        {
            return null;
        }

        var disc = type.TypeKind switch
        {
            TypeKind.Interface => 1u,
            TypeKind.Struct when type.IsRecord => 2u,
            TypeKind.Struct => 3u,
            _ when type.IsRecord => 2u,
            _ => 0u,
        };

        return FjordValue.Alt(disc, FjordValue.Of(FjordRef.To(fact)));
    }

    /// <summary>
    /// `csharp.AType` — any type the layer can express.
    /// </summary>
    /// <remarks>
    /// <b>Null where it cannot.</b> `dynamic`, an error type and a type parameter's
    /// unresolved bound have no alternative, and the union is in the key of `Method`,
    /// `Field`, `Parameter` and `Local` — so a signature mentioning one cannot be keyed at
    /// all and the declaration is dropped rather than recorded under a fabricated type.
    /// <see cref="Inexpressible"/> counts them, because a layer that silently loses
    /// declarations is worse than one that says how many.
    /// </remarks>
    public FjordValue? Type(ITypeSymbol type)
    {
        switch (type)
        {
            case IArrayTypeSymbol array:
                if (Type(array.ElementType) is not { } element)
                {
                    return null;
                }

                var arrayFact = new FjordFact(DotnetIndex.ArrayType, FjordValue.Rec(
                    element, FjordValue.Of(array.Rank)));
                emit(DotnetIndex.ArrayType, arrayFact);
                return FjordValue.Alt(0u, FjordValue.Of(FjordRef.To(arrayFact)));

            case IPointerTypeSymbol pointer:
                if (Type(pointer.PointedAtType) is not { } pointed)
                {
                    return null;
                }

                var pointerFact = new FjordFact(
                    DotnetIndex.PointerType, FjordValue.Rec(pointed));
                emit(DotnetIndex.PointerType, pointerFact);
                return FjordValue.Alt(3u, FjordValue.Of(FjordRef.To(pointerFact)));

            case IFunctionPointerTypeSymbol functionPointer:
                if (Entity(functionPointer.Signature) is not { } signature)
                {
                    return null;
                }

                var functionFact = new FjordFact(DotnetIndex.FunctionPointerType, FjordValue.Rec(
                    FjordValue.Of(FjordRef.To(FullName(functionPointer.Signature))),
                    FjordValue.Of(FjordRef.To(signature))));
                emit(DotnetIndex.FunctionPointerType, functionFact);
                return FjordValue.Alt(2u, FjordValue.Of(FjordRef.To(functionFact)));

            case ITypeParameterSymbol parameter:
                return Entity(parameter) is { } parameterFact
                    ? FjordValue.Alt(4u, FjordValue.Of(FjordRef.To(parameterFact)))
                    : null;

            case IErrorTypeSymbol:
            case IDynamicTypeSymbol:
                Interlocked.Increment(ref _inexpressible);
                return null;

            case INamedTypeSymbol named:
                return Named(named) is { } value ? FjordValue.Alt(1u, value) : null;

            default:
                Interlocked.Increment(ref _inexpressible);
                return null;
        }
    }

    /// <summary>
    /// `csharp.Definition` — any symbol the compiler exposes, as the union `EntityXRef`
    /// and `SymbolOf` target.
    /// </summary>
    public FjordValue? Definition(ISymbol symbol)
    {
        if (symbol is ITypeSymbol type)
        {
            return Type(type) is { } value ? FjordValue.Alt(0u, value) : null;
        }

        if (Entity(symbol) is not { } fact)
        {
            return null;
        }

        var disc = symbol switch
        {
            IMethodSymbol => 1u,
            IFieldSymbol => 2u,
            IParameterSymbol => 3u,
            ILocalSymbol => 5u,
            IPropertySymbol => 6u,
            _ => uint.MaxValue,
        };

        return disc == uint.MaxValue ? null : FjordValue.Alt(disc, FjordValue.Of(FjordRef.To(fact)));
    }

    // ---- the entities ----------------------------------------------------------------

    /// <summary>
    /// The entity fact for <paramref name="symbol"/>, built once and memoised.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The memo is filled before the containing chain is walked</b>, which is
    /// load-bearing for the same reason it was in the model this replaces: building a
    /// class reaches its base type and its containing type, and code that does not compile
    /// can make that a cycle. A null in the memo is "asked and cannot be expressed", which
    /// is the answer a second ask must get rather than recursing again.
    /// </para>
    /// <para>
    /// <b>Canonicalisation is narrower than the old model's.</b> `OriginalDefinition`
    /// still collapses `List&lt;int&gt;.Add` onto `List&lt;T&gt;.Add`, and an extension
    /// method's reduced form onto its declaration. What it no longer does is collapse an
    /// accessor onto its property or a synthesised member onto its type: `csharp.Property`
    /// references its `getMethod` and `setMethod`, so an accessor *is* an entity, and an
    /// entity needs no location — which is exactly what the old model could not express.
    /// </para>
    /// </remarks>
    public FjordFact? Entity(ISymbol symbol)
    {
        symbol = Canonical(symbol);

        if (_entities.TryGetValue(symbol, out var known))
        {
            return known;
        }

        // **The cycle break, on this thread's own stack.** A symbol reached from itself
        // has no fact yet and cannot wait for one, so it is answered `null` — the same
        // answer a type this layer cannot express gets, and the same shallower fact
        // results. What must never happen is one thread answering `null` because *another*
        // is mid-build: the two would then write different facts under one key.
        var building = _building ??= new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        if (!building.Add(symbol))
        {
            return null;
        }

        FjordFact? built;

        try
        {
            built = Build(symbol);
        }
        finally
        {
            building.Remove(symbol);
        }

        if (!_entities.TryAdd(symbol, built))
        {
            return _entities[symbol];
        }

        if (built is not null)
        {
            emit(built.Predicate, built);
        }

        return built;
    }

    private static ISymbol Canonical(ISymbol symbol)
    {
        symbol = symbol.OriginalDefinition;

        // `items.Where(...)` calls a static method whose first parameter is `items`.
        return symbol is IMethodSymbol { ReducedFrom: { } reduced }
            ? reduced.OriginalDefinition
            : symbol;
    }

    private FjordFact? Build(ISymbol symbol) => symbol switch
    {
        INamedTypeSymbol type => NamedTypeEntity(type),
        IMethodSymbol method => MethodEntity(method),
        IFieldSymbol field => FieldEntity(field),
        IPropertySymbol property => PropertyEntity(property),
        IParameterSymbol parameter => ParameterEntity(parameter),
        ITypeParameterSymbol parameter => TypeParameterEntity(parameter),
        _ => null,
    };

    private FjordFact? NamedTypeEntity(INamedTypeSymbol type)
    {
        if (type is IErrorTypeSymbol || type.TypeKind is TypeKind.Error)
        {
            Interlocked.Increment(ref _inexpressible);
            return null;
        }

        var name = FjordValue.Of(FjordRef.To(FullName(type)));
        var containing = type.ContainingType is { } outer ? Named(outer) : null;
        var containingType = containing is null ? Nothing : Just(containing);
        var access = Access(type.DeclaredAccessibility);

        // The base type is only carried where the schema has somewhere to put it: a class
        // names a class and a record names a record, because that is the only base either
        // can have that is not `object`'s own kind.
        return type.TypeKind switch
        {
            TypeKind.Interface => new FjordFact(DotnetIndex.Interface, FjordValue.Rec(
                name, containingType, access, Bool(type.IsStatic))),

            TypeKind.Struct when !type.IsRecord => new FjordFact(DotnetIndex.Struct,
                FjordValue.Rec(name, containingType, access)),

            _ when type.IsRecord => new FjordFact(DotnetIndex.Record, FjordValue.Rec(
                name,
                Maybe(BaseOfKind(type, wantRecord: true)),
                containingType,
                access,
                Bool(type.IsAbstract),
                Bool(type.IsSealed))),

            TypeKind.Class or TypeKind.Delegate or TypeKind.Enum => new FjordFact(
                DotnetIndex.Class, FjordValue.Rec(
                    name,
                    Maybe(BaseOfKind(type, wantRecord: false)),
                    containingType,
                    access,
                    Bool(type.IsAbstract),
                    Bool(type.IsStatic),
                    Bool(type.IsSealed))),

            _ => null,
        };
    }

    /// <summary>The base type, when it is the kind the schema's field can hold.</summary>
    private FjordFact? BaseOfKind(INamedTypeSymbol type, bool wantRecord)
    {
        if (type.BaseType is not { } baseType || baseType.SpecialType == SpecialType.System_Object)
        {
            return null;
        }

        return baseType.IsRecord == wantRecord ? Entity(baseType) : null;
    }

    private FjordFact? MethodEntity(IMethodSymbol method)
    {
        if (method.ContainingType is not { } containing
            || Named(containing) is not { } containingType
            || Type(method.ReturnType) is not { } returnType)
        {
            return null;
        }

        return new FjordFact(DotnetIndex.Method, FjordValue.Rec(
            FjordValue.Of(FjordRef.To(Name(method.Name))),
            containingType,
            returnType,
            Bool(method.IsStatic),
            Access(method.DeclaredAccessibility),
            // **The disambiguator, and the reason the key needs one.** A user-defined
            // conversion operator overloads on *return type*, so a signature is not
            // enough; `GetDocumentationCommentId` encodes the parameter list and, for a
            // conversion, the return type as `~T`.
            FjordValue.Of(method.GetDocumentationCommentId() ?? method.ToDisplayString())));
    }

    private FjordFact? FieldEntity(IFieldSymbol field)
    {
        if (field.ContainingType is not { } containing
            || Named(containing) is not { } containingType
            || Type(field.Type) is not { } type)
        {
            return null;
        }

        return new FjordFact(DotnetIndex.Field, FjordValue.Rec(
            FjordValue.Of(FjordRef.To(Name(field.Name))),
            type,
            containingType,
            Access(field.DeclaredAccessibility),
            Bool(field.IsConst),
            Bool(field.IsReadOnly),
            Bool(field.IsVirtual)));
    }

    private FjordFact? PropertyEntity(IPropertySymbol property)
    {
        if (property.ContainingType is not { } containing
            || Named(containing) is not { } containingType
            || Type(property.Type) is not { } type)
        {
            return null;
        }

        return new FjordFact(DotnetIndex.Property, FjordValue.Rec(
            FjordValue.Of(FjordRef.To(Name(property.Name))),
            containingType,
            type,
            Maybe(property.GetMethod is { } get ? Entity(get) : null),
            Maybe(property.SetMethod is { } set ? Entity(set) : null),
            Bool(property.IsStatic),
            Bool(property.IsIndexer),
            FjordValue.Of(property.GetDocumentationCommentId() ?? property.ToDisplayString())));
    }

    private FjordFact? ParameterEntity(IParameterSymbol parameter) =>
        Type(parameter.Type) is not { } type
            ? null
            : new FjordFact(DotnetIndex.Parameter, FjordValue.Rec(
                FjordValue.Of(FjordRef.To(Name(parameter.Name))),
                type,
                Ref(parameter.RefKind),
                Bool(parameter.IsThis),
                Bool(parameter.IsParams),
                Bool(parameter.IsOptional)));

    private FjordFact TypeParameterEntity(ITypeParameterSymbol parameter) =>
        new(DotnetIndex.TypeParameter, FjordValue.Rec(
            FjordValue.Of(FjordRef.To(Name(parameter.Name))),
            parameter.Variance == VarianceKind.None ? Nothing : Just(Variance(parameter.Variance)),
            Bool(parameter.HasNotNullConstraint),
            Bool(parameter.HasReferenceTypeConstraint),
            Bool(parameter.HasValueTypeConstraint)));

    // ---- the edges a list becomes ----------------------------------------------------

    /// <summary>
    /// The ordered edges and interface list a declaration owns: a list in Glean's schema
    /// is one fact per element here, because a length-prefixed array closes the seek for
    /// every key field after it.
    /// </summary>
    public void Edges(ISymbol symbol)
    {
        switch (Canonical(symbol))
        {
            case INamedTypeSymbol type when Named(type) is { } named:
                foreach (var iface in type.Interfaces)
                {
                    if (Entity(iface) is { } target)
                    {
                        emit(DotnetIndex.Implements, new FjordFact(
                            DotnetIndex.Implements,
                            FjordValue.Rec(named, FjordValue.Of(FjordRef.To(target)))));
                    }
                }

                Ordered(DotnetIndex.TypeTypeParameter, named, type.TypeParameters);
                break;

            case IMethodSymbol method when Entity(method) is { } fact:
                var self = FjordValue.Of(FjordRef.To(fact));
                Ordered(DotnetIndex.MethodParameter, self, method.Parameters);
                Ordered(DotnetIndex.MethodTypeParameter, self, method.TypeParameters);
                break;

            case IPropertySymbol property when Entity(property) is { } fact:
                Ordered(
                    DotnetIndex.PropertyParameter,
                    FjordValue.Of(FjordRef.To(fact)),
                    property.Parameters);
                break;
        }
    }

    private void Ordered(uint predicate, FjordValue owner, IEnumerable<ISymbol> members)
    {
        var index = 0L;

        foreach (var member in members)
        {
            if (Entity(member) is { } fact)
            {
                emit(predicate, new FjordFact(predicate, FjordValue.Rec(
                    owner, FjordValue.Of(index), FjordValue.Of(FjordRef.To(fact)))));
            }

            index++;
        }
    }
}
