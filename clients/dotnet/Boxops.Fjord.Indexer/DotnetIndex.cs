using System.Collections.Generic;
using Boxops.Fjord.Client;

namespace Boxops.Fjord.Indexer;

/// <summary>
/// <para>
/// <b>The schema set a .NET producer writes — <c>schemas/dotnet.sigla</c>, stated
/// independently.</b>
/// </para>
/// <para>
/// This replaces <see cref="CodeIndex"/>, whose schema is being retired. It arrives a
/// layer at a time rather than as one 67-predicate paste, because each layer's emission
/// lands with its own gate and a declaration nothing writes is a name in a file.
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
    /// <b>Its own copy, not <see cref="CodeIndex"/>'s.</b> That class is being deleted, and
    /// a statement that borrows from it would break when it goes.
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
    ], SchemaFingerprint);

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

    public static FjordFact ProjectCompilationFact(FjordFact project, string framework, FjordFact assembly) =>
        new(ProjectCompilation,
            FjordValue.Rec(
                FjordValue.Of(FjordRef.To(project)),
                FjordValue.Of(framework)),
            FjordValue.Rec(FjordValue.Of(FjordRef.To(assembly))));
}
