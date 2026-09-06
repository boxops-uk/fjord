// Clause 14.5 (using directives) and its three alternatives, all in one compilation unit:
// 14.5.2 using alias directives, 14.5.3 using namespace directives, 14.5.4 using static
// directives. A using directive introduces no member into the namespace it sits in — it only
// changes what names mean inside its own scope — so nothing this file imports can be seen from
// any other file, and nothing this file declares depends on the imports being visible outside.
//
// 14.5.2's targets are the interesting half, and every form a using alias may name is here:
// a namespace, a plain type, a nested type, a constructed generic (twice, once nested inside
// another), a tuple type, an array type, a multi-dimensional array type, a nullable value
// type, a pointer type, and a type reached through `global::`. Only one form is missing —
// a nullable *reference* type, `using X = string?`, which is CS9132 and is recorded
// `unbuildable` in the census rather than commented out here.
//
// The hazard runs through all of them: an alias declares a name that binds to a type nobody
// declared under that name. `NsRowLabel` is a name in this file's scope and `(int, string)`
// is a type in no file's scope, and there is no declaration anywhere in the program whose name
// is `NsRowLabel`. An index that stores aliases as declarations invents a type; one that
// stores them as references has to resolve `NsRowLabel` at each use site to something whose
// spelling never appears there.

extern alias NsPing;

using System.Text;

using Surface.Namespaces.Shared;
using Surface.Namespaces.Declarations.Nested.Deeper.Still;

using static Surface.Namespaces.Members.NsStaticSignals;
using static Surface.Namespaces.Members.NsStaticExtensions;
using static Surface.Namespaces.Members.NsCell<int>;
using static System.DayOfWeek;

using NsGenericCollections = System.Collections.Generic;
using NsTextBuilder = System.Text.StringBuilder;
using NsSignalKindAlias = Surface.Namespaces.Members.NsStaticSignals.NsSignalKind;
using NsIntList = System.Collections.Generic.List<int>;
using NsIndexByLabel = System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<int>>;
using NsCellOfInt = Surface.Namespaces.Members.NsCell<int>;
using NsRowLabel = (int Row, string Label);
using NsRowArray = int[];
using NsGrid = int[,];
using NsMaybeRow = int?;
using NsGlobalQualifiedAlias = global::Surface.Namespaces.Shared.NsSharedFromFileScoped;
using unsafe NsCellPointer = int*;
using NsAliasedPing = NsPing::System.Net.NetworkInformation.Ping;

namespace Surface.Namespaces.Using;

/// <summary>
/// 14.5.3: what a using namespace directive buys — three types named by their simple names,
/// each from a namespace this file imported rather than declared.
/// </summary>
public static class NsUsingNamespaceForms
{
    /// <summary>14.5.3: a type from `System.Text`, named unqualified.</summary>
    public static StringBuilder Builder() => new StringBuilder("14.5.3");

    /// <summary>
    /// 14.5.3: a type from `Surface.Namespaces.Shared`, the namespace three files declare —
    /// so this one directive names a namespace with three declarations and no location.
    /// </summary>
    public static string FromSharedNamespace() => NsSharedFromFileScoped.Origin;

    /// <summary>
    /// 14.5.3: a type from a namespace whose own declaration was a qualified identifier at a
    /// nested position, imported here by the full dotted name it adds up to.
    /// </summary>
    public static string FromDeeplyDeclaredNamespace() => NsDeeperStillHost.Depth;

    /// <summary>
    /// 14.5.3: a type from `System.Collections.Generic`, which no directive in this file
    /// imports — it is in scope because Compilation/NsGlobalUsings.cs made it global.
    /// </summary>
    public static List<int> FromGlobalDirective() => new List<int> { 1, 2, 3 };
}

/// <summary>14.5.4: what a using static directive buys — members with no type name in front.</summary>
public static class NsUsingStaticForms
{
    /// <summary>14.5.4: a constant, imported from a static class.</summary>
    public static string Constant() => NsSignalClause;

    /// <summary>14.5.4: a static field, imported from the same class.</summary>
    public static int Field() => NsSignalWidth;

    /// <summary>14.5.4: a static property.</summary>
    public static string Property() => NsSignalLabel;

    /// <summary>14.5.4: a static method, called with no qualification at all.</summary>
    public static string Method() => NsSignalFor(7);

    /// <summary>14.5.4: a nested type of the imported class, named unqualified.</summary>
    public static NsSignalKind NestedType() => NsSignalKind.Loud;

    /// <summary>
    /// 14.5.4: a static method of a *constructed generic* type. The directive named
    /// `NsCell&lt;int&gt;`, so `CellLabel` is in scope but `NsCell&lt;string&gt;.CellLabel` is not.
    /// </summary>
    public static string ConstructedGenericMember() => CellLabel();

    /// <summary>
    /// 14.5.4: an enum's member, imported by naming the enum type in a using static directive.
    /// </summary>
    public static System.DayOfWeek EnumMember() => Monday;

    /// <summary>
    /// 14.5.4: an extension method, in extension form. This is the one thing a using static
    /// directive does that no other using directive does — the method is invoked on its first
    /// argument, and the class that declares it is named nowhere near the call.
    /// </summary>
    public static string ExtensionInExtensionForm() => 3.NsRowLabel();

    /// <summary>
    /// 14.5.2 against 14.5.4, and the compiler picked the alias. The same extension method in
    /// plain static form cannot be written in this file, because `NsRowLabel` is also a using
    /// alias here and the alias takes the simple name:
    ///
    ///     public static string ExtensionInStaticForm() =&gt; NsRowLabel(4);
    ///     // CS1955: Non-invocable member '(int Row, string Label)' cannot be used like a method.
    ///
    /// The extension form above resolves the same method through its receiver and is
    /// unaffected — so one identifier in one file is a type in one position and a method group
    /// in another, and only the second of the two is reachable.
    /// </summary>
    public static string ExtensionInStaticFormIsHiddenByTheAlias() =>
        Surface.Namespaces.Members.NsStaticExtensions.NsRowLabel(4);

    /// <summary>
    /// 14.5.4: a generic extension method on a corpus type, so the import carries inference
    /// as well as a name.
    /// </summary>
    public static string GenericExtension() =>
        new Surface.Namespaces.Members.NsCell<int>(9).NsCellLabelOf();

    /// <summary>
    /// 14.5.1: a member of `System.Math`, in scope because a *global* using static directive
    /// in another file named it. Nothing in this file mentions Math.
    /// </summary>
    public static double FromGlobalStaticDirective() => Sqrt(16.0);
}

/// <summary>14.5.2: one member per alias target form, each using its alias.</summary>
public static class NsUsingAliasForms
{
    /// <summary>14.5.2: an alias for a namespace, used as a qualifier.</summary>
    public static NsGenericCollections.Queue<int> AliasedNamespace() => new NsGenericCollections.Queue<int>();

    /// <summary>14.5.2: an alias for a plain type.</summary>
    public static NsTextBuilder AliasedType() => new NsTextBuilder("14.5.2");

    /// <summary>14.5.2: an alias for a nested type.</summary>
    public static NsSignalKindAlias AliasedNestedType() => NsSignalKindAlias.Quiet;

    /// <summary>14.5.2: an alias for a constructed generic type.</summary>
    public static NsIntList AliasedConstructedGeneric() => new NsIntList { 1 };

    /// <summary>14.5.2: an alias for a constructed generic type with a constructed argument.</summary>
    public static NsIndexByLabel AliasedNestedConstruction() => new NsIndexByLabel();

    /// <summary>14.5.2: an alias for a corpus type, constructed.</summary>
    public static NsCellOfInt AliasedCorpusGeneric() => new NsCellOfInt(5);

    /// <summary>
    /// 14.5.2: an alias for a tuple type — the alias names element names too, so `Row` and
    /// `Label` below were declared by a using directive.
    /// </summary>
    public static NsRowLabel AliasedTuple() => (Row: 1, Label: "one");

    /// <summary>14.5.2: an alias for an array type.</summary>
    /// <remarks>
    /// The initializer is a collection expression, not `new NsRowArray { 1, 2 }` — an
    /// object-creation expression on an alias for an array type is CS8386, because the alias
    /// stands for `int[]` and an array is not created that way.
    /// </remarks>
    public static NsRowArray AliasedArray() => [1, 2];

    /// <summary>14.5.2: an alias for a multi-dimensional array type.</summary>
    public static NsGrid AliasedGrid() => new int[2, 2];

    /// <summary>14.5.2: an alias for a nullable value type.</summary>
    public static NsMaybeRow AliasedNullableValueType() => null;

    /// <summary>14.5.2: an alias whose target was reached through `global::`.</summary>
    public static string AliasedThroughGlobal() => NsGlobalQualifiedAlias.Origin;

    /// <summary>14.5.2 and 14.4: an alias whose target was reached through an extern alias.</summary>
    public static string AliasedThroughExternAlias() => typeof(NsAliasedPing).Name;

    /// <summary>
    /// 14.5.2: an alias for a pointer type, which needs `using unsafe`. The alias is the
    /// unsafe part; the method only names it.
    /// </summary>
    public static unsafe int AliasedPointer(NsCellPointer cell) => *cell;

    /// <summary>
    /// 14.5.1: a global alias, declared in Compilation/NsGlobalUsings.cs and in force here
    /// with no directive in this file to explain it.
    /// </summary>
    public static NsGlobalPair AliasedGlobally() => new NsGlobalPair("row", 1);
}
