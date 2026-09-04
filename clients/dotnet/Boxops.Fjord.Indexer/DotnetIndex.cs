using System;
using System.Collections.Generic;
using Boxops.Fjord.Client;

namespace Boxops.Fjord.Indexer;

/// <summary>
/// <para>
/// <b>The schema set a .NET producer writes — <c>schemas/dotnet.sigla</c>, stated
/// independently.</b>
/// </para>
/// <para>
/// <b>A client may declare only what it writes, and that is the contract rather than a
/// shortcut.</b> Predicate ids here are <i>this client's</i>: a block header carries the
/// predicate's **name**, and a nested reference takes its predicate from the field's
/// declared target — so nothing positional crosses the wire. The fingerprint is the
/// database's, carried and not computed, and it asserts provenance: that this client was
/// written against that schema. What proves the shapes are right is writing a fact of
/// every declared predicate through a real server and reading it back, which is what each
/// layer's gate does.
/// </para>
/// </summary>
internal static class DotnetIndex
{
    /// <summary>
    /// <c>schemas/dotnet.sigla</c>'s fingerprint, as <c>fjord schema check</c> prints it.
    /// </summary>
    /// <remarks>
    /// Carried, not computed — the whole schema's, not this partial statement's. A stale
    /// one fails the handshake loudly, which is the assertion it is for.
    /// </remarks>
    public const ulong SchemaFingerprint = 0xc20dfe719b04e025;

    // ---- src: the shared source layer ------------------------------------------------

    public const uint File = 0;
    public const uint Symbol = 1;
    public const uint FileLanguage = 2;
    public const uint FileDigest = 3;
    public const uint FileOrigin = 4;
    public const uint FileInfo = 5;
    public const uint FileLine = 6;
    public const uint FileLineAt = 7;
    public const uint FileLineStyles = 8;

    // ---- config: what the index was resolved against ---------------------------------

    public const uint Setting = 9;

    // ---- msbuild: the project graph a build evaluates --------------------------------

    public const uint Solution = 10;
    public const uint Project = 11;
    public const uint Assembly = 12;
    public const uint Package = 13;
    public const uint SolutionToProject = 14;
    public const uint ProjectToSolution = 15;
    public const uint ProjectToSourceFile = 16;
    public const uint SourceFileToProject = 17;
    public const uint ProjectReference = 18;
    public const uint ProjectReferencedBy = 19;
    public const uint PackageReference = 20;
    public const uint PackageDependent = 21;
    public const uint AssemblyReference = 22;
    public const uint AssemblyDependent = 23;
    public const uint Compilation = 24;
    public const uint ProjectCompilation = 25;

    // ---- csharp: what Roslyn can see -------------------------------------------------

    public const uint Name = 26;
    public const uint NameLowerCase = 27;
    public const uint Namespace = 28;
    public const uint FullName = 29;
    public const uint Class = 30;
    public const uint Interface = 31;
    public const uint Record = 32;
    public const uint Struct = 33;
    public const uint Implements = 34;
    public const uint TypeTypeParameter = 35;
    public const uint Method = 36;
    public const uint MethodParameter = 37;
    public const uint MethodTypeParameter = 38;
    public const uint Parameter = 39;
    public const uint Field = 40;
    public const uint TypeParameter = 41;
    public const uint Local = 42;
    public const uint Property = 43;
    public const uint PropertyParameter = 44;
    public const uint ArrayType = 45;
    public const uint PointerType = 46;
    public const uint FunctionPointerType = 47;
    public const uint DefinitionLocation = 48;
    public const uint ObjectCreationLocation = 49;
    public const uint MethodInvocationLocation = 50;
    public const uint MemberAccessLocation = 51;
    public const uint TypeLocation = 52;
    public const uint EntityXRef = 53;
    public const uint EntityRef = 54;
    public const uint SymbolOf = 55;
    public const uint DefinitionBySymbol = 56;

    // ---- codemarkup: the surface a UI reads ------------------------------------------

    /// <summary>`codemarkup.Definition` — named to keep it apart from `csharp.Definition`,
    /// which is a union and not a predicate.</summary>
    public const uint MarkupDefinition = 57;
    public const uint SymbolInfo = 58;
    public const uint FileDefinition = 59;
    public const uint FileXRef = 60;
    public const uint SymbolXRef = 61;
    public const uint FileLocalXRef = 62;
    public const uint SearchEntry = 63;
    public const uint SymbolByName = 64;
    public const uint Relation = 65;
    public const uint RelationOf = 66;

    /// <summary>Every predicate id this client holds, in schema order.</summary>
    public static readonly uint[] Predicates =
    [
        File, Symbol, FileLanguage, FileDigest, FileOrigin, FileInfo, FileLine,
        FileLineAt, FileLineStyles, Setting,
        Solution, Project, Assembly, Package, SolutionToProject, ProjectToSolution,
        ProjectToSourceFile, SourceFileToProject, ProjectReference, ProjectReferencedBy,
        PackageReference, PackageDependent, AssemblyReference, AssemblyDependent,
        Compilation, ProjectCompilation,
        Name, NameLowerCase, Namespace, FullName, Class, Interface, Record, Struct,
        Implements, TypeTypeParameter, Method, MethodParameter, MethodTypeParameter,
        Parameter, Field, TypeParameter, Local, Property, PropertyParameter, ArrayType,
        PointerType, FunctionPointerType, DefinitionLocation, ObjectCreationLocation,
        MethodInvocationLocation, MemberAccessLocation, TypeLocation, EntityXRef,
        EntityRef, SymbolOf, DefinitionBySymbol,
        MarkupDefinition, SymbolInfo, FileDefinition, FileXRef, SymbolXRef,
        FileLocalXRef, SearchEntry, SymbolByName, Relation, RelationOf,
    ];

    /// <summary>
    /// <c>src.Language</c>'s named alternatives, in discriminant order — alternative
    /// <c>n + 1</c> is <c>LanguageNames[n]</c>, and <c>other : string = 0</c> is the valve
    /// that is not in this list.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Append only.</b> I10 froze these the day the layer shipped: one added in the
    /// wrong place renumbers every one after it, which reads on disk as every file having
    /// changed language.
    /// </para>
    /// <para>
    /// <b>Declared above <see cref="Schema"/> on purpose.</b> Static field initialisers run
    /// in declaration order and <c>Schema</c>'s reads this array, so moving it below turns
    /// every use of this class into a <c>TypeInitializationException</c> — at run time,
    /// with nothing wrong at the site that fails.
    /// </para>
    /// </remarks>
    public static readonly string[] LanguageNames =
    [
        "csharp", "typescript", "javascript", "tsx", "jsx", "rust", "python",
        "java", "cpp", "c", "go", "json", "yaml", "markdown", "css", "html",
        "sql", "shell", "xml", "proto",
    ];

    /// <summary>
    /// The statement itself. Transcribed from the `.sigla` files rather than generated
    /// from the resolved schema: two independent statements of one schema is what the
    /// fingerprint is for, and a generated one would agree by construction.
    /// </summary>
    public static readonly FjordSchema Schema = new([
        // `src.File` — a path relative to the index root, interned once.
        new FjordPredicate("src.File", FjordType.String, null),

        new FjordPredicate("src.Symbol", FjordType.String, null),

        new FjordPredicate("src.FileLanguage",
            FjordType.Rec(("file", FjordType.Reference(File))),
            FjordType.Rec(("language", LanguageType))),

        new FjordPredicate("src.FileDigest",
            FjordType.Rec(("file", FjordType.Reference(File))),
            FjordType.Rec(("digest", FjordType.String))),

        new FjordPredicate("src.FileOrigin",
            FjordType.Rec(("file", FjordType.Reference(File))),
            FjordType.Rec(
                ("repo", FjordType.String),
                ("revision", FjordType.String))),

        new FjordPredicate("src.FileInfo",
            FjordType.Rec(("file", FjordType.Reference(File))),
            FjordType.Rec(
                ("bytes", FjordType.Integer),
                ("lines", FjordType.Integer),
                ("endsInNewline", BoolType))),

        new FjordPredicate("src.FileLine", FjordType.Rec(
                ("file", FjordType.Reference(File)),
                ("line", FjordType.Integer)),
            FjordType.Rec(
                ("text", FjordType.String),
                ("start", FjordType.Integer),
                ("bytes", FjordType.Integer),
                ("cstart", FjordType.Integer))),

        new FjordPredicate("src.FileLineAt", FjordType.Rec(
            ("file", FjordType.Reference(File)),
            ("start", FjordType.Integer),
            ("line", FjordType.Integer)), null),

        new FjordPredicate("src.FileLineStyles", FjordType.Rec(
                ("file", FjordType.Reference(File)),
                ("line", FjordType.Integer)),
            FjordType.Rec(("styles", FjordType.Blob))),

        // `config.Setting` — key-only and multi-valued, so one dimension may hold
        // several values. Both fields are strings so a new dimension costs nothing.
        new FjordPredicate("config.Setting", FjordType.Rec(
            ("dimension", FjordType.String),
            ("value", FjordType.String)), null),

        // ---- msbuild -----------------------------------------------------------------
        //
        // **A project is its project file and nothing else.** Everything MSBuild
        // *evaluated* is on the value side, so re-evaluating one project under a
        // different SDK does not mint a second — which is the defect the old build layer
        // had, and the reason every reverse question below is its own predicate rather
        // than a sort of the forward one.

        new FjordPredicate("msbuild.Solution",
            FjordType.Rec(("file", FjordType.Reference(File))), null),

        new FjordPredicate("msbuild.Project",
            FjordType.Rec(("file", FjordType.Reference(File))),
            FjordType.Rec(
                ("platformTarget", MaybeString),
                ("targetFramework", MaybeString),
                ("sdk", MaybeString),
                ("outputType", MaybeString),
                ("assemblyName", MaybeString),
                ("rootNamespace", MaybeString))),

        new FjordPredicate("msbuild.Assembly",
            FjordType.Rec(("name", FjordType.String)), null),

        new FjordPredicate("msbuild.Package", FjordType.Rec(
            ("name", FjordType.String),
            ("version", FjordType.String)), null),

        new FjordPredicate("msbuild.SolutionToProject", FjordType.Rec(
            ("solution", FjordType.Reference(Solution)),
            ("project", FjordType.Reference(Project))), null),

        new FjordPredicate("msbuild.ProjectToSolution", FjordType.Rec(
            ("project", FjordType.Reference(Project)),
            ("solution", FjordType.Reference(Solution))), null),

        new FjordPredicate("msbuild.ProjectToSourceFile", FjordType.Rec(
            ("project", FjordType.Reference(Project)),
            ("src", FjordType.Reference(File))), null),

        new FjordPredicate("msbuild.SourceFileToProject", FjordType.Rec(
            ("src", FjordType.Reference(File)),
            ("project", FjordType.Reference(Project))), null),

        new FjordPredicate("msbuild.ProjectReference", FjordType.Rec(
            ("from", FjordType.Reference(Project)),
            ("to", FjordType.Reference(Project))), null),

        new FjordPredicate("msbuild.ProjectReferencedBy", FjordType.Rec(
            ("to", FjordType.Reference(Project)),
            ("from", FjordType.Reference(Project))), null),

        new FjordPredicate("msbuild.PackageReference",
            FjordType.Rec(
                ("project", FjordType.Reference(Project)),
                ("package", FjordType.Reference(Package))),
            FjordType.Rec(("range", MaybeString))),

        new FjordPredicate("msbuild.PackageDependent", FjordType.Rec(
            ("package", FjordType.Reference(Package)),
            ("project", FjordType.Reference(Project))), null),

        new FjordPredicate("msbuild.AssemblyReference", FjordType.Rec(
            ("project", FjordType.Reference(Project)),
            ("assembly", FjordType.Reference(Assembly))), null),

        new FjordPredicate("msbuild.AssemblyDependent", FjordType.Rec(
            ("assembly", FjordType.Reference(Assembly)),
            ("project", FjordType.Reference(Project))), null),

        new FjordPredicate("msbuild.Compilation", FjordType.Rec(
            ("assembly", FjordType.Reference(Assembly)),
            ("framework", FjordType.String),
            ("project", FjordType.Reference(Project))), null),

        new FjordPredicate("msbuild.ProjectCompilation",
            FjordType.Rec(
                ("project", FjordType.Reference(Project)),
                ("framework", FjordType.String)),
            FjordType.Rec(("assembly", FjordType.Reference(Assembly)))),

        // ---- csharp ------------------------------------------------------------------
        //
        // **The three unions are inlined at every use site**, so what is stored is a tag
        // and, for the fact-bearing alternatives, a `FactId`. The recursion is broken by
        // reference: an `AType` names predicates rather than embedding them.

        new FjordPredicate("csharp.Name", FjordType.String, null),

        new FjordPredicate("csharp.NameLowerCase", FjordType.Rec(
            ("nameLowercase", FjordType.String),
            ("name", FjordType.Reference(Name))), null),

        new FjordPredicate("csharp.Namespace", FjordType.Rec(
            ("name", FjordType.Reference(Name)),
            ("containingNamespace", Maybe(FjordType.Reference(Namespace)))), null),

        new FjordPredicate("csharp.FullName", FjordType.Rec(
            ("name", FjordType.Reference(Name)),
            ("containingNamespace", FjordType.Reference(Namespace))), null),

        new FjordPredicate("csharp.Class", FjordType.Rec(
            ("name", FjordType.Reference(FullName)),
            ("baseType", Maybe(FjordType.Reference(Class))),
            ("containingType", Maybe(NamedTypeUnion)),
            ("declaredAccessibility", AccessibilityUnion),
            ("isAbstract", BoolType),
            ("isStatic", BoolType),
            ("isSealed", BoolType)), null),

        new FjordPredicate("csharp.Interface", FjordType.Rec(
            ("name", FjordType.Reference(FullName)),
            ("containingType", Maybe(NamedTypeUnion)),
            ("declaredAccessibility", AccessibilityUnion),
            ("isStatic", BoolType)), null),

        new FjordPredicate("csharp.Record", FjordType.Rec(
            ("name", FjordType.Reference(FullName)),
            ("baseType", Maybe(FjordType.Reference(Record))),
            ("containingType", Maybe(NamedTypeUnion)),
            ("declaredAccessibility", AccessibilityUnion),
            ("isAbstract", BoolType),
            ("isSealed", BoolType)), null),

        new FjordPredicate("csharp.Struct", FjordType.Rec(
            ("name", FjordType.Reference(FullName)),
            ("containingType", Maybe(NamedTypeUnion)),
            ("declaredAccessibility", AccessibilityUnion)), null),

        new FjordPredicate("csharp.Implements", FjordType.Rec(
            ("type", NamedTypeUnion),
            ("interface_", FjordType.Reference(Interface))), null),

        new FjordPredicate("csharp.TypeTypeParameter", FjordType.Rec(
            ("type", NamedTypeUnion),
            ("index", FjordType.Integer),
            ("typeParameter", FjordType.Reference(TypeParameter))), null),

        new FjordPredicate("csharp.Method", FjordType.Rec(
            ("name", FjordType.Reference(Name)),
            ("containingType", NamedTypeUnion),
            ("returnType", ATypeUnion),
            ("isStatic", BoolType),
            ("declaredAccessibility", AccessibilityUnion),
            ("docId", FjordType.String)), null),

        new FjordPredicate("csharp.MethodParameter", FjordType.Rec(
            ("method", FjordType.Reference(Method)),
            ("index", FjordType.Integer),
            ("parameter", FjordType.Reference(Parameter))), null),

        new FjordPredicate("csharp.MethodTypeParameter", FjordType.Rec(
            ("method", FjordType.Reference(Method)),
            ("index", FjordType.Integer),
            ("typeParameter", FjordType.Reference(TypeParameter))), null),

        new FjordPredicate("csharp.Parameter", FjordType.Rec(
            ("name", FjordType.Reference(Name)),
            ("type", ATypeUnion),
            ("refKind", RefKindUnion),
            ("isThis", BoolType),
            ("isParams", BoolType),
            ("isOptional", BoolType)), null),

        new FjordPredicate("csharp.Field", FjordType.Rec(
            ("name", FjordType.Reference(Name)),
            ("type", ATypeUnion),
            ("containingType", NamedTypeUnion),
            ("declaredAccessibility", AccessibilityUnion),
            ("isConst", BoolType),
            ("isReadonly", BoolType),
            ("isVirtual", BoolType)), null),

        new FjordPredicate("csharp.TypeParameter", FjordType.Rec(
            ("name", FjordType.Reference(Name)),
            ("variance", Maybe(VarianceUnion)),
            ("hasNotNullConstraint", BoolType),
            ("hasReferenceTypeConstraint", BoolType),
            ("hasValueTypeConstraint", BoolType)), null),

        new FjordPredicate("csharp.Local", FjordType.Rec(
            ("name", FjordType.Reference(Name)),
            ("type", ATypeUnion),
            ("containingMethod", FjordType.Reference(Method)),
            ("refKind", RefKindUnion),
            ("isConst", BoolType)), null),

        new FjordPredicate("csharp.Property", FjordType.Rec(
            ("name", FjordType.Reference(Name)),
            ("containingType", NamedTypeUnion),
            ("type", ATypeUnion),
            ("getMethod", Maybe(FjordType.Reference(Method))),
            ("setMethod", Maybe(FjordType.Reference(Method))),
            ("isStatic", BoolType),
            ("isIndexer", BoolType),
            ("docId", FjordType.String)), null),

        new FjordPredicate("csharp.PropertyParameter", FjordType.Rec(
            ("property", FjordType.Reference(Property)),
            ("index", FjordType.Integer),
            ("parameter", FjordType.Reference(Parameter))), null),

        new FjordPredicate("csharp.ArrayType", FjordType.Rec(
            ("elementType", ATypeUnion),
            ("rank", FjordType.Integer)), null),

        new FjordPredicate("csharp.PointerType",
            FjordType.Rec(("pointedAtType", ATypeUnion)), null),

        new FjordPredicate("csharp.FunctionPointerType", FjordType.Rec(
            ("name", FjordType.Reference(FullName)),
            ("signature", FjordType.Reference(Method))), null),

        new FjordPredicate("csharp.DefinitionLocation", FjordType.Rec(
            ("definition", DefinitionUnion),
            ("location", LocationType)), null),

        new FjordPredicate("csharp.ObjectCreationLocation", FjordType.Rec(
            ("type", ATypeUnion),
            ("constructor", FjordType.Reference(Method)),
            ("location", LocationType)), null),

        new FjordPredicate("csharp.MethodInvocationLocation", FjordType.Rec(
            ("method", FjordType.Reference(Method)),
            ("location", LocationType),
            ("expression", Maybe(MethodInvocationExpressionType))), null),

        new FjordPredicate("csharp.MemberAccessLocation", FjordType.Rec(
            ("expression", MemberAccessExpressionUnion),
            ("location", LocationType)), null),

        new FjordPredicate("csharp.TypeLocation", FjordType.Rec(
            ("type", ATypeUnion),
            ("location", LocationType)), null),

        new FjordPredicate("csharp.EntityXRef", FjordType.Rec(
            ("file", FjordType.Reference(File)),
            ("use", ByteSpanType),
            ("target", DefinitionUnion)), null),

        new FjordPredicate("csharp.EntityRef", FjordType.Rec(
            ("target", DefinitionUnion),
            ("file", FjordType.Reference(File)),
            ("use", ByteSpanType)), null),

        new FjordPredicate("csharp.SymbolOf", FjordType.Rec(
            ("definition", DefinitionUnion),
            ("symbol", FjordType.Reference(Symbol))), null),

        new FjordPredicate("csharp.DefinitionBySymbol", FjordType.Rec(
            ("symbol", FjordType.Reference(Symbol)),
            ("definition", DefinitionUnion)), null),

        // ---- codemarkup --------------------------------------------------------------
        //
        // **The join key is a string**, `src.Symbol`, and not a union over languages: a
        // union alternative is Breaking (I10), so a C#-only index and a C#-plus-TypeScript
        // one would carry different `Definition` predicates and one UI could not read
        // both. A string also survives leaving the database, which a `FactId` does not.
        //
        // Anything a consumer joins *through* is in the key, which is stricter than it
        // looks: a value can neither be matched nor read field-wise, so a reference behind
        // the `->` is reachable only by projecting the whole value.

        new FjordPredicate("codemarkup.Definition",
            FjordType.Rec(
                ("symbol", FjordType.Reference(Symbol)),
                ("file", FjordType.Reference(File))),
            FjordType.Rec(
                ("span", ByteSpanType),
                ("kind", KindUnion),
                ("name", FjordType.String),
                ("qualified", FjordType.String))),

        new FjordPredicate("codemarkup.SymbolInfo",
            FjordType.Rec(("symbol", FjordType.Reference(Symbol))),
            FjordType.Rec(
                ("signature", FjordType.String),
                ("doc", FjordType.String),
                ("modifiers", FjordType.String))),

        new FjordPredicate("codemarkup.FileDefinition",
            FjordType.Rec(
                ("file", FjordType.Reference(File)),
                ("span", ByteSpanType),
                ("symbol", FjordType.Reference(Symbol))),
            FjordType.Rec(
                ("kind", KindUnion),
                ("name", FjordType.String))),

        new FjordPredicate("codemarkup.FileXRef", FjordType.Rec(
            ("file", FjordType.Reference(File)),
            ("span", ByteSpanType),
            ("target", FjordType.Reference(Symbol)),
            ("role", RoleUnion)), null),

        new FjordPredicate("codemarkup.SymbolXRef", FjordType.Rec(
            ("target", FjordType.Reference(Symbol)),
            ("file", FjordType.Reference(File)),
            ("span", ByteSpanType)), null),

        new FjordPredicate("codemarkup.FileLocalXRef", FjordType.Rec(
            ("file", FjordType.Reference(File)),
            ("span", ByteSpanType),
            ("target", ByteSpanType),
            ("role", RoleUnion)), null),

        new FjordPredicate("codemarkup.SearchEntry", FjordType.Rec(
            ("nameLowercase", FjordType.String),
            ("name", FjordType.String),
            ("kind", KindUnion),
            ("symbol", FjordType.Reference(Symbol)),
            ("file", FjordType.Reference(File)),
            ("line", FjordType.Integer)), null),

        new FjordPredicate("codemarkup.SymbolByName", FjordType.Rec(
            ("name", FjordType.String),
            ("symbol", FjordType.Reference(Symbol))), null),

        new FjordPredicate("codemarkup.Relation", FjordType.Rec(
            ("from", FjordType.Reference(Symbol)),
            ("kind", RelationKindUnion),
            ("to", FjordType.Reference(Symbol))), null),

        new FjordPredicate("codemarkup.RelationOf", FjordType.Rec(
            ("to", FjordType.Reference(Symbol)),
            ("kind", RelationKindUnion),
            ("from", FjordType.Reference(Symbol))), null),
    ], SchemaFingerprint);

    /// <summary>
    /// `codemarkup.Kind` — LSP's `SymbolKind`, 1–26 verbatim, with `other : string = 0`
    /// in the slot LSP does not use.
    /// </summary>
    /// <remarks>
    /// A citation rather than an invention, and it sits in a key: I10 froze these the day
    /// the layer shipped, so every future value arrives through `other`.
    /// </remarks>
    private static FjordType KindUnion => Vocabulary(
        "file", "module_", "namespace_", "package_", "class_", "method_", "property_",
        "field", "constructor_", "enum_", "interface_", "function_", "variable",
        "constant", "string_", "number", "boolean_", "array", "object_", "key", "null_",
        "enumMember", "struct_", "event", "operator", "typeParameter");

    /// <summary>
    /// `codemarkup.Role` — SCIP's `SymbolRole`, projected.
    /// </summary>
    /// <remarks>
    /// **A bitmask is lost here and it is stated rather than hidden.** SCIP's role is a
    /// bitmask — a reference can be a definition *and* an import at once — which a sigla
    /// union cannot express and an `int` of flags would make unmatchable. This is the
    /// mutually-exclusive projection a UI filters by.
    /// </remarks>
    private static FjordType RoleUnion => Vocabulary(
        "definition", "read", "write", "call", "typeRef", "new_", "import_", "export_",
        "heritage", "decorator", "jsx");

    private static FjordType RelationKindUnion => Vocabulary(
        "contains", "extends", "implements", "overrides", "calls", "imports",
        "instantiates", "annotates");

    /// <summary>
    /// A vocabulary with the `other : string = 0` valve, then the names contiguous from 1.
    /// </summary>
    /// <remarks>
    /// Built from a list rather than written out three times over forty-five
    /// alternatives: a transcription slip in a discriminant is what I10 makes permanent.
    /// </remarks>
    private static FjordType Vocabulary(params string[] names)
    {
        var alternatives = new List<(string, uint, FjordType)>
        {
            ("other", 0u, FjordType.String),
        };

        for (var index = 0; index < names.Length; index++)
        {
            alternatives.Add((names[index], (uint)index + 1, FjordType.Rec()));
        }

        return FjordType.OneOf([.. alternatives]);
    }

    /// <summary>`{ nothing = 0 | just : T = 1 }` — the optional, one per payload.</summary>
    /// <remarks>
    /// Structural, so the name does not enter the canonical form: a schema declaring its
    /// own and one importing `src`'s fingerprint the same. fjord has no generics, so the
    /// schema names one per payload type and this builds them all.
    /// </remarks>
    private static FjordType Maybe(FjordType payload) => FjordType.OneOf(
        ("nothing", 0u, FjordType.Rec()),
        ("just", 1u, payload));

    /// <summary>
    /// `src.MaybeString`. Every `msbuild.Project` field is one because MSBuild may leave
    /// any of them unset, and a producer writing the empty string would be claiming a
    /// value.
    /// </summary>
    private static FjordType MaybeString => Maybe(FjordType.String);

    /// <summary>`src.ByteSpan` — a byte offset and a byte length, both zero-based.</summary>
    private static FjordType ByteSpanType => FjordType.Rec(
        ("start", FjordType.Integer),
        ("length", FjordType.Integer));

    /// <summary>`src.Location` — a span in a named file.</summary>
    private static FjordType LocationType => FjordType.Rec(
        ("file", FjordType.Reference(File)),
        ("span", ByteSpanType));

    /// <summary>An alternative with no payload, which is the empty record.</summary>
    private static FjordType Empty => FjordType.Rec();

    /// <summary>Every accessibility combination Roslyn exposes.</summary>
    /// <remarks>
    /// Alphabetical, which is Glean's order and therefore the frozen one (I10) — not a
    /// sensible ordering of visibility, and deliberately not re-sorted into one.
    /// </remarks>
    private static FjordType AccessibilityUnion => FjordType.OneOf(
        ("friend", 0u, Empty),
        ("internal", 1u, Empty),
        ("notApplicable", 2u, Empty),
        ("private", 3u, Empty),
        ("protected", 4u, Empty),
        ("protectedAndFriend", 5u, Empty),
        ("protectedAndInternal", 6u, Empty),
        ("protectedOrFriend", 7u, Empty),
        ("protectedOrInternal", 8u, Empty),
        ("public", 9u, Empty));

    /// <summary>How a parameter is passed. `none_` is the plain by-value case.</summary>
    private static FjordType RefKindUnion => FjordType.OneOf(
        ("in", 0u, Empty),
        ("none_", 1u, Empty),
        ("out", 2u, Empty),
        ("ref", 3u, Empty),
        ("refReadOnly", 4u, Empty));

    /// <summary>Declared variance of a generic type parameter.</summary>
    private static FjordType VarianceUnion => FjordType.OneOf(
        ("in", 0u, Empty),
        ("none_", 1u, Empty),
        ("out", 2u, Empty));

    /// <summary>
    /// A type that is neither an array, a pointer, nor a type parameter. The keyword names
    /// carry a trailing underscore.
    /// </summary>
    private static FjordType NamedTypeUnion => FjordType.OneOf(
        ("class_", 0u, FjordType.Reference(Class)),
        ("interface_", 1u, FjordType.Reference(Interface)),
        ("record_", 2u, FjordType.Reference(Record)),
        ("struct_", 3u, FjordType.Reference(Struct)));

    /// <summary>Any type: an array, a named type, a function pointer, a pointer, or a
    /// type parameter.</summary>
    private static FjordType ATypeUnion => FjordType.OneOf(
        ("arrayType", 0u, FjordType.Reference(ArrayType)),
        ("namedType", 1u, NamedTypeUnion),
        ("functionPointerType", 2u, FjordType.Reference(FunctionPointerType)),
        ("pointerType", 3u, FjordType.Reference(PointerType)),
        ("typeParameter", 4u, FjordType.Reference(TypeParameter)));

    /// <summary>Any symbol the compiler exposes — the union `EntityXRef` targets.</summary>
    private static FjordType DefinitionUnion => FjordType.OneOf(
        ("type", 0u, ATypeUnion),
        ("method", 1u, FjordType.Reference(Method)),
        ("field", 2u, FjordType.Reference(Field)),
        ("parameter", 3u, FjordType.Reference(Parameter)),
        ("typeParameter", 4u, FjordType.Reference(TypeParameter)),
        ("local", 5u, FjordType.Reference(Local)),
        ("property", 6u, FjordType.Reference(Property)));

    private static FjordType MemberAccessExpressionUnion => FjordType.OneOf(
        ("local", 0u, FjordType.Reference(Local)),
        ("parameter", 1u, FjordType.Reference(Parameter)),
        ("field", 2u, FjordType.Reference(Field)),
        ("property", 3u, FjordType.Reference(Property)),
        ("method", 4u, FjordType.Reference(Method)));

    /// <summary>
    /// A method invocation seen through a member access. A <b>record</b> with one field,
    /// not a one-alternative union — the schema writes no `|` and no discriminant.
    /// </summary>
    private static FjordType MethodInvocationExpressionType =>
        FjordType.Rec(("memberAccess", FjordType.Reference(MemberAccessLocation)));

    /// <summary>`{ false_ = 0 | true_ = 1 }`.</summary>
    private static FjordType BoolType => FjordType.OneOf(
        ("false_", 0u, FjordType.Rec()),
        ("true_", 1u, FjordType.Rec()));

    private static FjordType LanguageType
    {
        get
        {
            var alternatives = new List<(string, uint, FjordType)>
            {
                ("other", 0u, FjordType.String),
            };
            for (var index = 0; index < LanguageNames.Length; index++)
            {
                alternatives.Add((LanguageNames[index], (uint)index + 1, FjordType.Rec()));
            }

            return FjordType.OneOf([.. alternatives]);
        }
    }

    public static string NameOf(uint predicate) => Schema[predicate].Name;

    // ---- the facts -------------------------------------------------------------------

    public static FjordFact FileFact(string path) => new(File, FjordValue.Of(path));

    public static FjordFact SymbolFact(string symbol) => new(Symbol, FjordValue.Of(symbol));

    /// <summary>
    /// <c>config.Setting</c>: one axis of the index. Key-only, so the pair is the fact.
    /// </summary>
    public static FjordFact SettingFact(string dimension, string value) =>
        new(Setting, FjordValue.Rec(FjordValue.Of(dimension), FjordValue.Of(value)));

    // ---- src facts -------------------------------------------------------------------

    /// <summary>
    /// <c>src.FileLanguage</c>: what a file is written in, named rather than numbered.
    /// </summary>
    /// <remarks>
    /// The discriminant is resolved through <see cref="LanguageNames"/>, so this producer
    /// holds no numbers of its own and a language the vocabulary does not have goes
    /// through the <c>other</c> valve carrying its own name.
    /// </remarks>
    public static FjordFact FileLanguageFact(FjordFact file, string language)
    {
        var named = Array.IndexOf(LanguageNames, language);

        return new(FileLanguage,
            FjordValue.Rec(FjordValue.Of(FjordRef.To(file))),
            FjordValue.Rec(named < 0
                ? FjordValue.Alt(0u, FjordValue.Of(language))
                : FjordValue.Alt((uint)named + 1, FjordValue.Rec())));
    }

    /// <summary>
    /// <c>src.FileDigest</c>: a content hash of the file, as lowercase hex.
    /// </summary>
    /// <remarks>
    /// The algorithm is <see cref="SourceLayer.Digest"/>'s and is owed
    /// <c>config.Setting {dimension = "digest"}</c> — which this producer can now write,
    /// since `dotnet.sigla` imports `config`.
    /// </remarks>
    public static FjordFact FileDigestFact(FjordFact file, string digest) =>
        new(FileDigest,
            FjordValue.Rec(FjordValue.Of(FjordRef.To(file))),
            FjordValue.Rec(FjordValue.Of(digest)));

    /// <summary>
    /// <c>src.FileOrigin</c>: which repository and revision a file came from.
    /// </summary>
    public static FjordFact FileOriginFact(FjordFact file, string repo, string revision) =>
        new(FileOrigin,
            FjordValue.Rec(FjordValue.Of(FjordRef.To(file))),
            FjordValue.Rec(FjordValue.Of(repo), FjordValue.Of(revision)));

    /// <summary>
    /// <c>src.FileInfo</c>: a file's size in bytes and lines, and whether its last byte
    /// is a terminator.
    /// </summary>
    /// <remarks>
    /// <paramref name="lines"/> must be the number of <c>src.FileLine</c> facts written
    /// for the file: a consumer resolving an offset past the last line's start falls back
    /// to this number, so a count that disagrees sends it to a line that does not exist.
    /// </remarks>
    public static FjordFact FileInfoFact(FjordFact file, long bytes, long lines, bool endsInNewline) =>
        new(FileInfo,
            FjordValue.Rec(FjordValue.Of(FjordRef.To(file))),
            FjordValue.Rec(
                FjordValue.Of(bytes),
                FjordValue.Of(lines),
                FjordValue.Alt(endsInNewline ? 1u : 0u, FjordValue.Rec())));

    /// <summary>One line of a file: its text, and the three offsets that locate it.</summary>
    /// <remarks>
    /// <paramref name="start"/> is a <b>UTF-8 byte</b> offset and <paramref name="cstart"/>
    /// a <b>UTF-16 code-unit</b> one, and they are not the same number: a codepoint above
    /// the BMP is four bytes and two code units.
    /// </remarks>
    public static FjordFact FileLineFact(
        FjordFact file,
        long line,
        string text,
        long start,
        long bytes,
        long cstart) =>
        new(FileLine,
            FjordValue.Rec(FjordValue.Of(FjordRef.To(file)), FjordValue.Of(line)),
            FjordValue.Rec(
                FjordValue.Of(text),
                FjordValue.Of(start),
                FjordValue.Of(bytes),
                FjordValue.Of(cstart)));

    /// <summary>
    /// <c>src.FileLineAt</c>: the line table keyed by offset, all key.
    /// </summary>
    public static FjordFact FileLineAtFact(FjordFact file, long start, long line) =>
        new(FileLineAt,
            FjordValue.Rec(
                FjordValue.Of(FjordRef.To(file)),
                FjordValue.Of(start),
                FjordValue.Of(line)));

    /// <summary>
    /// <c>src.FileLineStyles</c>: one line's syntax highlighting, as opaque bytes.
    /// </summary>
    /// <remarks>
    /// The payload is whatever the producer's <c>style-encoding</c> declares — see
    /// <see cref="SemanticTokens"/> — and the schema asks no questions about it.
    /// </remarks>
    public static FjordFact FileLineStylesFact(FjordFact file, long line, ReadOnlyMemory<byte> styles) =>
        new(FileLineStyles,
            FjordValue.Rec(FjordValue.Of(FjordRef.To(file)), FjordValue.Of(line)),
            FjordValue.Rec(FjordValue.Of(styles)));

    // ---- msbuild facts ---------------------------------------------------------------

    /// <summary>`nothing`, or `just` the string — never the empty string for absence.</summary>
    private static FjordValue Maybe(string? value) => value is null
        ? FjordValue.Alt(0u, FjordValue.Rec())
        : FjordValue.Alt(1u, FjordValue.Of(value));

    public static FjordFact SolutionFact(FjordFact file) =>
        new(Solution, FjordValue.Rec(FjordValue.Of(FjordRef.To(file))));

    /// <summary>
    /// <c>msbuild.Project</c>: the project file is the identity, and everything MSBuild
    /// evaluated is a value.
    /// </summary>
    public static FjordFact ProjectFact(
        FjordFact file,
        string? platformTarget = null,
        string? targetFramework = null,
        string? sdk = null,
        string? outputType = null,
        string? assemblyName = null,
        string? rootNamespace = null) =>
        new(Project,
            FjordValue.Rec(FjordValue.Of(FjordRef.To(file))),
            FjordValue.Rec(
                Maybe(platformTarget),
                Maybe(targetFramework),
                Maybe(sdk),
                Maybe(outputType),
                Maybe(assemblyName),
                Maybe(rootNamespace)));

    public static FjordFact AssemblyFact(string name) =>
        new(Assembly, FjordValue.Rec(FjordValue.Of(name)));

    public static FjordFact PackageFact(string name, string version) =>
        new(Package, FjordValue.Rec(FjordValue.Of(name), FjordValue.Of(version)));

    private static FjordFact Pair(uint predicate, FjordFact first, FjordFact second) =>
        new(predicate, FjordValue.Rec(
            FjordValue.Of(FjordRef.To(first)),
            FjordValue.Of(FjordRef.To(second))));

    public static FjordFact SolutionToProjectFact(FjordFact solution, FjordFact project) =>
        Pair(SolutionToProject, solution, project);

    public static FjordFact ProjectToSolutionFact(FjordFact project, FjordFact solution) =>
        Pair(ProjectToSolution, project, solution);

    public static FjordFact ProjectToSourceFileFact(FjordFact project, FjordFact file) =>
        Pair(ProjectToSourceFile, project, file);

    public static FjordFact SourceFileToProjectFact(FjordFact file, FjordFact project) =>
        Pair(SourceFileToProject, file, project);

    public static FjordFact ProjectReferenceFact(FjordFact from, FjordFact to) =>
        Pair(ProjectReference, from, to);

    public static FjordFact ProjectReferencedByFact(FjordFact to, FjordFact from) =>
        Pair(ProjectReferencedBy, to, from);

    /// <summary>
    /// <c>msbuild.PackageReference</c>: which package a project asks for, and the range
    /// the file wrote.
    /// </summary>
    public static FjordFact PackageReferenceFact(FjordFact project, FjordFact package, string? range) =>
        new(PackageReference,
            FjordValue.Rec(
                FjordValue.Of(FjordRef.To(project)),
                FjordValue.Of(FjordRef.To(package))),
            FjordValue.Rec(Maybe(range)));

    public static FjordFact PackageDependentFact(FjordFact package, FjordFact project) =>
        Pair(PackageDependent, package, project);

    public static FjordFact AssemblyReferenceFact(FjordFact project, FjordFact assembly) =>
        Pair(AssemblyReference, project, assembly);

    public static FjordFact AssemblyDependentFact(FjordFact assembly, FjordFact project) =>
        Pair(AssemblyDependent, assembly, project);

    public static FjordFact CompilationFact(FjordFact assembly, string framework, FjordFact project) =>
        new(Compilation, FjordValue.Rec(
            FjordValue.Of(FjordRef.To(assembly)),
            FjordValue.Of(framework),
            FjordValue.Of(FjordRef.To(project))));

    // ---- csharp facts the walk writes directly ---------------------------------------
    //
    // The entity facts are `CsharpEntities`'; these are the ones that need a span, which
    // is the walk's to give.

    private static FjordValue Span(long start, long length) =>
        FjordValue.Rec(FjordValue.Of(start), FjordValue.Of(length));

    private static FjordValue Location(FjordFact file, long start, long length) =>
        FjordValue.Rec(FjordValue.Of(FjordRef.To(file)), Span(start, length));

    /// <summary>`csharp.DefinitionLocation` — where a definition is declared.</summary>
    public static FjordFact DefinitionLocationFact(
        FjordValue definition, FjordFact file, long start, long length) =>
        new(DefinitionLocation, FjordValue.Rec(definition, Location(file, start, length)));

    /// <summary>
    /// `csharp.ObjectCreationLocation` — where an object is constructed, and which
    /// constructor it calls.
    /// </summary>
    public static FjordFact ObjectCreationLocationFact(
        FjordValue type, FjordFact constructor, FjordFact file, long start, long length) =>
        new(ObjectCreationLocation, FjordValue.Rec(
            type, FjordValue.Of(FjordRef.To(constructor)), Location(file, start, length)));

    /// <summary>
    /// `csharp.MethodInvocationLocation` — where a method is invoked, and the member
    /// access it was invoked through where there was one.
    /// </summary>
    /// <remarks>
    /// The optional is a `maybe` over a <b>record</b> of one field, not a one-alternative
    /// union: `just` carries `{memberAccess = …}`, so the payload is a record whose single
    /// field is the reference — the schema writes no `|` and no discriminant inside it.
    /// </remarks>
    public static FjordFact MethodInvocationLocationFact(
        FjordFact method, FjordFact file, long start, long length, FjordFact? through) =>
        new(MethodInvocationLocation, FjordValue.Rec(
            FjordValue.Of(FjordRef.To(method)),
            Location(file, start, length),
            through is null
                ? FjordValue.Alt(0u, FjordValue.Rec())
                : FjordValue.Alt(1u, FjordValue.Rec(FjordValue.Of(FjordRef.To(through))))));

    /// <summary>
    /// `csharp.MemberAccessLocation` — where a member is accessed, and what the accessed
    /// member resolves to.
    /// </summary>
    public static FjordFact MemberAccessLocationFact(
        FjordValue expression, FjordFact file, long start, long length) =>
        new(MemberAccessLocation, FjordValue.Rec(
            expression, Location(file, start, length)));

    /// <summary>`csharp.TypeLocation` — where a type is written.</summary>
    public static FjordFact TypeLocationFact(
        FjordValue type, FjordFact file, long start, long length) =>
        new(TypeLocation, FjordValue.Rec(type, Location(file, start, length)));

    /// <summary>`csharp.EntityXRef` — a use span and what it targets.</summary>
    public static FjordFact EntityXRefFact(
        FjordFact file, long start, long length, FjordValue target) =>
        new(EntityXRef, FjordValue.Rec(
            FjordValue.Of(FjordRef.To(file)), Span(start, length), target));

    /// <summary>`csharp.EntityRef` — the same reference keyed by what it points at.</summary>
    public static FjordFact EntityRefFact(
        FjordValue target, FjordFact file, long start, long length) =>
        new(EntityRef, FjordValue.Rec(
            target, FjordValue.Of(FjordRef.To(file)), Span(start, length)));

    public static FjordFact SymbolOfFact(FjordValue definition, FjordFact symbol) =>
        new(SymbolOf, FjordValue.Rec(definition, FjordValue.Of(FjordRef.To(symbol))));

    public static FjordFact DefinitionBySymbolFact(FjordFact symbol, FjordValue definition) =>
        new(DefinitionBySymbol, FjordValue.Rec(
            FjordValue.Of(FjordRef.To(symbol)), definition));

    // ---- codemarkup facts ------------------------------------------------------------

    /// <summary>A vocabulary value: a named alternative, or the `other` valve.</summary>
    /// <remarks>
    /// A producer that cannot name something in the frozen list says so through `other`
    /// rather than picking the nearest alternative — a wrong `kind` is a filter answering
    /// wrongly with nothing to notice it.
    /// </remarks>
    public static FjordValue Tagged(uint disc, string? unnamed = null) =>
        disc == 0
            ? FjordValue.Alt(0u, FjordValue.Of(unnamed ?? string.Empty))
            : FjordValue.Alt(disc, FjordValue.Rec());

    public static FjordFact MarkupDefinitionFact(
        FjordFact symbol,
        FjordFact file,
        long start,
        long length,
        FjordValue kind,
        string name,
        string qualified) =>
        new(MarkupDefinition,
            FjordValue.Rec(
                FjordValue.Of(FjordRef.To(symbol)),
                FjordValue.Of(FjordRef.To(file))),
            FjordValue.Rec(
                Span(start, length),
                kind,
                FjordValue.Of(name),
                FjordValue.Of(qualified)));

    public static FjordFact SymbolInfoFact(
        FjordFact symbol, string signature, string doc, string modifiers) =>
        new(SymbolInfo,
            FjordValue.Rec(FjordValue.Of(FjordRef.To(symbol))),
            FjordValue.Rec(
                FjordValue.Of(signature),
                FjordValue.Of(doc),
                FjordValue.Of(modifiers)));

    public static FjordFact FileDefinitionFact(
        FjordFact file, long start, long length, FjordFact symbol, FjordValue kind, string name) =>
        new(FileDefinition,
            FjordValue.Rec(
                FjordValue.Of(FjordRef.To(file)),
                Span(start, length),
                FjordValue.Of(FjordRef.To(symbol))),
            FjordValue.Rec(kind, FjordValue.Of(name)));

    public static FjordFact FileXRefFact(
        FjordFact file, long start, long length, FjordFact target, FjordValue role) =>
        new(FileXRef, FjordValue.Rec(
            FjordValue.Of(FjordRef.To(file)),
            Span(start, length),
            FjordValue.Of(FjordRef.To(target)),
            role));

    public static FjordFact SymbolXRefFact(
        FjordFact target, FjordFact file, long start, long length) =>
        new(SymbolXRef, FjordValue.Rec(
            FjordValue.Of(FjordRef.To(target)),
            FjordValue.Of(FjordRef.To(file)),
            Span(start, length)));

    /// <summary>
    /// <c>codemarkup.FileLocalXRef</c>: a file-local reference, answered span to span.
    /// </summary>
    /// <remarks>
    /// No interned string, because a local has no global name worth minting — and a
    /// jump-to-declaration is then one seek with no symbol table.
    /// </remarks>
    public static FjordFact FileLocalXRefFact(
        FjordFact file,
        long start,
        long length,
        long targetStart,
        long targetLength,
        FjordValue role) =>
        new(FileLocalXRef, FjordValue.Rec(
            FjordValue.Of(FjordRef.To(file)),
            Span(start, length),
            Span(targetStart, targetLength),
            role));

    public static FjordFact SearchEntryFact(
        string name, FjordValue kind, FjordFact symbol, FjordFact file, long line) =>
        new(SearchEntry, FjordValue.Rec(
            FjordValue.Of(name.ToLowerInvariant()),
            FjordValue.Of(name),
            kind,
            FjordValue.Of(FjordRef.To(symbol)),
            FjordValue.Of(FjordRef.To(file)),
            FjordValue.Of(line)));

    public static FjordFact SymbolByNameFact(string name, FjordFact symbol) =>
        new(SymbolByName, FjordValue.Rec(
            FjordValue.Of(name),
            FjordValue.Of(FjordRef.To(symbol))));

    public static FjordFact RelationFact(FjordFact from, FjordValue kind, FjordFact to) =>
        new(Relation, FjordValue.Rec(
            FjordValue.Of(FjordRef.To(from)), kind, FjordValue.Of(FjordRef.To(to))));

    public static FjordFact RelationOfFact(FjordFact to, FjordValue kind, FjordFact from) =>
        new(RelationOf, FjordValue.Rec(
            FjordValue.Of(FjordRef.To(to)), kind, FjordValue.Of(FjordRef.To(from))));

    public static FjordFact ProjectCompilationFact(FjordFact project, string framework, FjordFact assembly) =>
        new(ProjectCompilation,
            FjordValue.Rec(
                FjordValue.Of(FjordRef.To(project)),
                FjordValue.Of(framework)),
            FjordValue.Rec(FjordValue.Of(FjordRef.To(assembly))));
}
