// Annex D.4.1 (processing, in general), D.4.2 (ID string format) and D.4.3 (ID string
// examples) — the subclauses this whole project exists for.
//
// D.4.2 defines a name for every declaration a documentation comment can hang from, and this
// indexer uses that name as the sort key that assigns overload ordinals. So the ID string is
// not documentation here: it is identity, and anything the ID string does not distinguish is
// two declarations arriving at one identity string.
//
// The format, as the annex gives it, and as `Docs.xml` confirms member by member:
//
//   * A single-character kind prefix and a colon. `N:` namespace, `T:` type, `F:` field,
//     `P:` property (indexers included), `M:` method (constructors, finalizers and operators
//     included), `E:` event, and `!:` for a name the generator could not resolve.
//   * Then the fully qualified name from the root of the namespace, each part separated by a
//     period — **including the separator between a type and a type nested inside it**, which
//     is the same period a namespace uses.
//   * A member name containing periods — only an explicit interface implementation can —
//     has each period replaced by `#`.
//   * A generic type carries a backtick and its type-parameter count: ``DocIdGenerics`2``.
//     A generic method carries two backticks and its own count after the method name.
//   * An argument list in parentheses, comma-separated with no spaces, omitted entirely when
//     there are no arguments. Argument types are fully qualified; a constructed generic type
//     uses `{` and `}` where C# uses angle brackets; a type parameter of the *containing
//     type* is a backtick and its index, and one of the *method* is two backticks and its
//     index; `@` marks a by-reference argument; `*` a pointer; `[]` a vector, and
//     `[lowerbound:size,…]` a multi-dimensional array, which the compiler always writes
//     `[0:,0:]` for a two-dimensional one.
//   * A conversion operator, alone among members, ends in `~` and the ID of its return type.
//
// D.4.1's own fact is that the generator emits an entry for every documented declaration and
// records nothing else about it: no accessibility, no source file, no assembly, no order.
// `DocIdAccess` is that — four `Emit` overloads at four accessibilities, which the ID strings
// separate by parameter list and never by visibility.
//
// Four encodings are, on the evidence of these files, not injective, and three of them cannot
// be provoked in one type because C# refuses the overload before the annex gets a chance:
//
//   * `params int[]` and `int[]` are both `System.Int32[]` — CS0111 as overloads.
//   * `ref int`, `out int`, `in int` and `ref readonly int` are all `System.Int32@` —
//     CS0663 as overloads. `WithRef`, `WithOut`, `WithIn` and `WithRefReadonly` below have
//     four names and one parameter encoding between them.
//   * `(int, int)`, `(int a, int b)` and `ValueTuple<int, int>` are all
//     `System.ValueTuple{System.Int32,System.Int32}` — CS0111 as overloads, and tuple element
//     names leave no trace at all.
//   * A nested type and a type in a same-named namespace produce the *same* ID string, and
//     C# permits both. That one is not written here; the note at the end of README.md says
//     why.

namespace Surface.Docs.Ids;

/// <summary>
/// D.4.2 — one declaration of every kind the ID string format names, with the string each
/// one mints written beside it. ID string <c>T:Surface.Docs.Ids.DocIdShapes</c>.
/// </summary>
public class DocIdShapes
{
    /// <summary>D.4.2 — a field. <c>F:Surface.Docs.Ids.DocIdShapes.Slot</c>.</summary>
    public int Slot;

    /// <summary>
    /// D.4.2 — a constant, which is a field: <c>F:Surface.Docs.Ids.DocIdShapes.Limit</c>.
    /// The format has no prefix of its own for a constant, so an index that wants to tell one
    /// from a field has to look somewhere other than the ID string.
    /// </summary>
    public const int Limit = 16;

    /// <summary>
    /// D.4.2 — a static readonly field, which is also just <c>F:</c>:
    /// <c>F:Surface.Docs.Ids.DocIdShapes.Shared</c>.
    /// </summary>
    public static readonly int Shared = Limit / 2;

    /// <summary>
    /// D.4.2 — a static constructor. <c>M:Surface.Docs.Ids.DocIdShapes.#cctor</c>: the member
    /// name is a word no C# identifier could be, and there is no argument list because there
    /// are no arguments.
    /// </summary>
    static DocIdShapes()
    {
        Started = true;
    }

    /// <summary>
    /// D.4.2 — an instance constructor with no arguments.
    /// <c>M:Surface.Docs.Ids.DocIdShapes.#ctor</c>, the argument list omitted.
    /// </summary>
    public DocIdShapes()
    {
        Slot = 0;
    }

    /// <summary>
    /// D.4.2 — an instance constructor with arguments.
    /// <c>M:Surface.Docs.Ids.DocIdShapes.#ctor(System.Int32)</c>. Two constructors, one
    /// member name, separated by the argument list alone.
    /// </summary>
    /// <param name="slot">Where to start.</param>
    public DocIdShapes(int slot)
    {
        Slot = slot;
    }

    /// <summary>
    /// D.4.2 — a finalizer. <c>M:Surface.Docs.Ids.DocIdShapes.Finalize</c>: the ID string
    /// gives it the name the runtime gives it, not the name the source does, so a type
    /// declaring both a finalizer and a nullary method called <c>Finalize</c> would mint one
    /// string for two members — and does not get to, because that is CS0111. The annex is
    /// rescued here by the language rather than by the format.
    /// </summary>
    ~DocIdShapes()
    {
        Slot = -1;
    }

    /// <summary>D.4.2 — set by the static constructor, so that it has run.</summary>
    public static bool Started { get; private set; }

    /// <summary>D.4.2 — a property. <c>P:Surface.Docs.Ids.DocIdShapes.Depth</c>.</summary>
    public int Depth
    {
        get => Slot + 1;
        set => Slot = value - 1;
    }

    /// <summary>
    /// D.4.2 — an indexer, which is a property with an argument list:
    /// <c>P:Surface.Docs.Ids.DocIdShapes.Item(System.Int32)</c>. The member name is
    /// <c>Item</c> because nothing said otherwise.
    /// </summary>
    /// <param name="index">Which slot.</param>
    /// <returns>The index, offset by the slot.</returns>
    public int this[int index] => index + Slot;

    /// <summary>
    /// D.4.2 — an event. <c>E:Surface.Docs.Ids.DocIdShapes.Changed</c>. Declared in the
    /// field-like form, so the compiler also mints a private field of the same name;
    /// <c>DocSignal</c> in DocIdHazards.cs is that shape written on purpose.
    /// </summary>
    public event System.Action Changed;

    /// <summary>D.4.2 — raises <see cref="Changed"/>, so the event is used.</summary>
    public void Touch()
    {
        Slot++;
        Changed?.Invoke();
    }

    /// <summary>
    /// D.4.2 — a method with no arguments. <c>M:Surface.Docs.Ids.DocIdShapes.Take</c>, with
    /// no parentheses at all: an unqualified `cref` to a set of `Take` overloads would mint
    /// this same string.
    /// </summary>
    /// <returns>The slot.</returns>
    public int Take() => Slot;

    /// <summary>
    /// D.4.2 — the same method name with one argument.
    /// <c>M:Surface.Docs.Ids.DocIdShapes.Take(System.Int32)</c>.
    /// </summary>
    /// <param name="count">How many.</param>
    /// <returns>The count.</returns>
    public int Take(int count) => count;

    /// <summary>
    /// D.4.2 — and with two, so that the ordinal a doc-ID sort assigns to these three has
    /// something to order. <c>M:Surface.Docs.Ids.DocIdShapes.Take(System.Int32,System.String)</c>.
    /// </summary>
    /// <param name="count">How many.</param>
    /// <param name="label">What to call them.</param>
    /// <returns>The count plus the label's length.</returns>
    public int Take(int count, string label) => count + label.Length;

    /// <summary>
    /// D.4.2 — a generic method. <c>M:Surface.Docs.Ids.DocIdShapes.Map``1(System.Int32)</c>:
    /// two backticks and the method's own type-parameter count, after the name and before
    /// the arguments.
    /// </summary>
    /// <typeparam name="TResult">What to produce.</typeparam>
    /// <param name="count">How many to produce.</param>
    /// <returns>A default-valued array of that length.</returns>
    public TResult[] Map<TResult>(int count) => new TResult[count];

    /// <summary>
    /// D.4.2 — a generic method of arity two.
    /// <c>M:Surface.Docs.Ids.DocIdShapes.Map``2(System.Int32)</c>: same name, same argument
    /// list, and the arity is the whole of the difference.
    /// </summary>
    /// <typeparam name="TFirst">The first thing to produce.</typeparam>
    /// <typeparam name="TSecond">The second.</typeparam>
    /// <param name="count">How many pairs.</param>
    /// <returns>The count, unchanged.</returns>
    public int Map<TFirst, TSecond>(int count) => count;

    /// <summary>
    /// D.4.2 — a constructed generic argument type, written with braces:
    /// <c>M:…DocIdShapes.WithConstructed(System.Collections.Generic.Dictionary{System.String,System.Int32})</c>.
    /// </summary>
    /// <param name="byName">The dictionary to count.</param>
    /// <returns>How many entries it has.</returns>
    public int WithConstructed(System.Collections.Generic.Dictionary<string, int> byName) => byName.Count;

    /// <summary>
    /// D.4.2 — nested construction, braces inside braces:
    /// <c>M:…DocIdShapes.WithNested(System.Collections.Generic.List{System.Collections.Generic.List{System.Int32}})</c>.
    /// </summary>
    /// <param name="rows">The rows to count.</param>
    /// <returns>How many rows there are.</returns>
    public int WithNested(System.Collections.Generic.List<System.Collections.Generic.List<int>> rows) => rows.Count;

    /// <summary>
    /// D.4.2 — a nullable value type is a constructed generic type like any other:
    /// <c>M:…DocIdShapes.WithNullable(System.Nullable{System.Int32})</c>. The <c>int?</c>
    /// spelling leaves no trace.
    /// </summary>
    /// <param name="count">How many, or nothing.</param>
    /// <returns>The count, or zero.</returns>
    public int WithNullable(int? count) => count ?? 0;

    /// <summary>
    /// D.4.2 — a tuple is <c>System.ValueTuple</c> constructed:
    /// <c>M:…DocIdShapes.WithTuple(System.ValueTuple{System.Int32,System.Int32})</c>. The
    /// element names <c>first</c> and <c>second</c> are nowhere in the ID string, so
    /// <c>(int, int)</c> and <c>(int a, int b)</c> mint the same one.
    /// </summary>
    /// <param name="pair">The pair to add up.</param>
    /// <returns>Their sum.</returns>
    public int WithTuple((int first, int second) pair) => pair.first + pair.second;

    /// <summary>
    /// D.4.2 — a vector. <c>M:…DocIdShapes.WithArray(System.Int32[])</c>.
    /// </summary>
    /// <param name="counts">The numbers to add up.</param>
    /// <returns>Their sum.</returns>
    public int WithArray(int[] counts)
    {
        int total = 0;
        foreach (int count in counts)
        {
            total += count;
        }

        return total;
    }

    /// <summary>
    /// D.4.2 — a two-dimensional array, written with its bounds:
    /// <c>M:…DocIdShapes.WithRank2(System.Int32[0:,0:])</c>. A `cref` cannot be written in
    /// that form — <c>System.Int32[,]</c> is what the source says and CS1584 is what
    /// <c>[0:,0:]</c> gets — so the ID string and the C# spelling differ here in both
    /// directions.
    /// </summary>
    /// <param name="grid">The grid to measure.</param>
    /// <returns>Its total length.</returns>
    public int WithRank2(int[,] grid) => grid.Length;

    /// <summary>
    /// D.4.2 — a jagged array is a vector of vectors:
    /// <c>M:…DocIdShapes.WithJagged(System.Int32[][])</c>.
    /// </summary>
    /// <param name="rows">The rows to count.</param>
    /// <returns>How many rows there are.</returns>
    public int WithJagged(int[][] rows) => rows.Length;

    /// <summary>
    /// D.4.2 — a <c>params</c> array. <c>M:…DocIdShapes.WithParams(System.Int32[])</c>: the
    /// same string an ordinary <c>int[]</c> parameter mints, which is why
    /// <c>WithArray</c> above had to be given a different name.
    /// </summary>
    /// <param name="counts">Any number of numbers.</param>
    /// <returns>How many there were.</returns>
    public int WithParams(params int[] counts) => counts.Length;

    /// <summary>
    /// D.4.2 — a pointer. <c>M:…DocIdShapes.WithPointer(System.Int32*)</c>, and the only
    /// character in the format that needs <c>AllowUnsafeBlocks</c> to produce.
    /// </summary>
    /// <param name="count">Where the number is.</param>
    /// <returns>The number there, or zero when there is nowhere.</returns>
    public unsafe int WithPointer(int* count) => count is null ? 0 : *count;

    /// <summary>
    /// D.4.2 — <c>ref</c>. <c>M:…DocIdShapes.WithRef(System.Int32@)</c>.
    /// </summary>
    /// <param name="count">Read and written.</param>
    public void WithRef(ref int count) => count += Slot;

    /// <summary>
    /// D.4.2 — <c>out</c>, which encodes identically: <c>M:…DocIdShapes.WithOut(System.Int32@)</c>.
    /// </summary>
    /// <param name="count">Written only.</param>
    public void WithOut(out int count) => count = Slot;

    /// <summary>
    /// D.4.2 — <c>in</c>, which encodes identically again:
    /// <c>M:…DocIdShapes.WithIn(System.Int32@)</c>.
    /// </summary>
    /// <param name="count">Read only.</param>
    /// <returns>What was read.</returns>
    public int WithIn(in int count) => count;

    /// <summary>
    /// D.4.2 — <c>ref readonly</c>, the fourth spelling of <c>@</c>:
    /// <c>M:…DocIdShapes.WithRefReadonly(System.Int32@)</c>. A method's *return* being by
    /// reference leaves no trace in the ID string at all.
    /// </summary>
    /// <param name="count">Read only, by reference.</param>
    /// <returns>The same storage, by reference.</returns>
    public ref readonly int WithRefReadonly(ref readonly int count) => ref count;

    /// <summary>
    /// D.4.2 — an operator, under its emitted name:
    /// <c>M:…DocIdShapes.op_Addition(Surface.Docs.Ids.DocIdShapes,System.Int32)</c>.
    /// </summary>
    /// <param name="shapes">The left operand.</param>
    /// <param name="count">The right operand.</param>
    /// <returns>A new instance, moved along by the count.</returns>
    public static DocIdShapes operator +(DocIdShapes shapes, int count) => new(shapes.Slot + count);

    /// <summary>
    /// D.4.2 — a nested type, separated from its container by a period, exactly as a
    /// namespace part is: <c>T:Surface.Docs.Ids.DocIdShapes.DocIdInner</c>.
    /// </summary>
    public sealed class DocIdInner
    {
        /// <summary>D.4.2 — a member of a nested type. <c>F:…DocIdShapes.DocIdInner.Mark</c>.</summary>
        public int Mark;
    }
}

/// <summary>
/// D.4.2 — the backtick forms, which need a generic type and a generic method inside it to
/// show at once. ID string <c>T:Surface.Docs.Ids.DocIdGenerics`2</c>.
/// </summary>
/// <typeparam name="TFirst">The first type parameter, which is <c>`0</c>.</typeparam>
/// <typeparam name="TSecond">The second, which is <c>`1</c>.</typeparam>
public sealed class DocIdGenerics<TFirst, TSecond>
{
    /// <summary>
    /// D.4.2 — the containing type's parameters, by index:
    /// <c>M:Surface.Docs.Ids.DocIdGenerics`2.Pick(`0,`1)</c>.
    /// </summary>
    /// <param name="first">A <typeparamref name="TFirst"/>.</param>
    /// <param name="second">A <typeparamref name="TSecond"/>.</param>
    /// <returns>The first of the two.</returns>
    public TFirst Pick(TFirst first, TSecond second) => first;

    /// <summary>
    /// D.4.2 — a method's own parameter beside its type's:
    /// <c>M:Surface.Docs.Ids.DocIdGenerics`2.Blend``1(`1,``0)</c>. One backtick counts from
    /// the containing type, two from the method, and the two index spaces are independent.
    /// </summary>
    /// <typeparam name="TThird">The method's own type parameter, which is <c>``0</c>.</typeparam>
    /// <param name="second">A <typeparamref name="TSecond"/>, <c>`1</c>.</param>
    /// <param name="third">A <typeparamref name="TThird"/>, <c>``0</c>.</param>
    /// <returns>The third of the three.</returns>
    public TThird Blend<TThird>(TSecond second, TThird third) => third;

    /// <summary>
    /// D.4.2 — the containing type, constructed, as an argument type:
    /// <c>M:…DocIdGenerics`2.Rewrap(Surface.Docs.Ids.DocIdGenerics{`1,`0})</c> — braces and
    /// backticks together.
    /// </summary>
    /// <param name="swapped">The same type with its arguments the other way round.</param>
    /// <returns>Always true.</returns>
    public bool Rewrap(DocIdGenerics<TSecond, TFirst> swapped) => swapped is not null;

    /// <summary>
    /// D.4.2 — a generic type nested in a generic type. Its arity is its **own**:
    /// <c>T:Surface.Docs.Ids.DocIdGenerics`2.DocIdGenericsInner`1</c>, not `3`.
    /// </summary>
    /// <typeparam name="TThird">The nested type's own parameter, which is <c>`2</c>
    /// inside it — the index space of a nested type continues its container's.</typeparam>
    public sealed class DocIdGenericsInner<TThird>
    {
        /// <summary>
        /// D.4.2 — a member of the nested type, whose argument list reaches all three:
        /// <c>M:…DocIdGenerics`2.DocIdGenericsInner`1.All(`0,`1,`2)</c>.
        /// </summary>
        /// <param name="first">A <typeparamref name="TFirst"/>.</param>
        /// <param name="second">A <typeparamref name="TSecond"/>.</param>
        /// <param name="third">A <typeparamref name="TThird"/>.</param>
        /// <returns>Always three.</returns>
        public int All(TFirst first, TSecond second, TThird third) => 3;
    }

    /// <summary>
    /// D.4.2 — a non-generic type nested in a generic one, which carries no backtick of its
    /// own: <c>T:Surface.Docs.Ids.DocIdGenerics`2.DocIdGenericsPlain</c>.
    /// </summary>
    public sealed class DocIdGenericsPlain
    {
        /// <summary>D.4.2 — and its members still reach the container's parameters,
        /// <c>M:…DocIdGenerics`2.DocIdGenericsPlain.Hold(`0)</c>.</summary>
        /// <param name="first">A <typeparamref name="TFirst"/>.</param>
        /// <returns>Always true.</returns>
        public bool Hold(TFirst first) => first is not null;
    }
}

/// <summary>
/// D.4.2 — a delegate is a type, so its ID string is a <c>T:</c> and its signature is
/// nowhere in it: <c>T:Surface.Docs.Ids.DocIdHandler</c>.
/// </summary>
/// <param name="message">What happened.</param>
/// <returns>How many listeners cared.</returns>
public delegate int DocIdHandler(string message);

/// <summary>
/// D.4.2 — a generic delegate carries an arity like any other type:
/// <c>T:Surface.Docs.Ids.DocIdHandler`1</c>.
/// </summary>
/// <typeparam name="TPayload">What is carried.</typeparam>
/// <param name="payload">The thing carried.</param>
/// <returns>Whether it was wanted.</returns>
public delegate bool DocIdPayloadHandler<TPayload>(TPayload payload);

/// <summary>
/// D.4.2 — an enumeration is a type and its members are fields:
/// <c>T:Surface.Docs.Ids.DocIdFlags</c>.
/// </summary>
public enum DocIdFlags
{
    /// <summary>D.4.2 — <c>F:Surface.Docs.Ids.DocIdFlags.None</c>.</summary>
    None = 0,

    /// <summary>D.4.2 — <c>F:Surface.Docs.Ids.DocIdFlags.Documented</c>.</summary>
    Documented = 1,

    /// <summary>D.4.2 — <c>F:Surface.Docs.Ids.DocIdFlags.Included</c>.</summary>
    Included = 2,
}

/// <summary>D.4.2 — an interface, whose members have ID strings and no bodies.</summary>
public interface IDocIdSink
{
    /// <summary>
    /// D.4.2 — <c>M:Surface.Docs.Ids.IDocIdSink.Accept(System.String)</c>: an interface
    /// member's ID string looks like any other method's.
    /// </summary>
    /// <param name="text">What to accept.</param>
    void Accept(string text);

    /// <summary>D.4.2 — an interface property, <c>P:Surface.Docs.Ids.IDocIdSink.Accepted</c>.</summary>
    int Accepted { get; }
}

/// <summary>
/// D.4.2 — the <c>#</c> substitution. An explicit interface implementation is the only member
/// whose *name* contains periods, and each becomes a <c>#</c>:
/// <c>M:Surface.Docs.Ids.DocIdExplicit.Surface#Docs#Ids#IDocIdSink#Accept(System.String)</c>.
/// The namespace of the interface is inside the member name, so moving <c>IDocIdSink</c> to
/// another namespace changes this member's ID string without touching this file.
/// </summary>
public sealed class DocIdExplicit : IDocIdSink
{
    private int _accepted;

    /// <summary>D.4.2 — the explicit implementation, whose ID string is quoted above.</summary>
    /// <param name="text">What to accept.</param>
    void IDocIdSink.Accept(string text) => _accepted += text.Length;

    /// <summary>
    /// D.4.2 — an explicitly implemented property:
    /// <c>P:…DocIdExplicit.Surface#Docs#Ids#IDocIdSink#Accepted</c>.
    /// </summary>
    int IDocIdSink.Accepted => _accepted;

    /// <summary>D.4.2 — how much has been accepted, reachable without a cast.</summary>
    public int Total => _accepted;
}

/// <summary>
/// D.4.2 — an extension method's ID string says nothing about the <c>this</c> modifier:
/// <c>M:Surface.Docs.Ids.DocIdExtensions.Doubled(System.Int32)</c> is what an ordinary static
/// method of that signature would mint too.
/// </summary>
public static class DocIdExtensions
{
    /// <summary>D.4.2 — the extension method.</summary>
    /// <param name="self">The number to double.</param>
    /// <returns>Twice the number.</returns>
    public static int Doubled(this int self) => self * 2;
}

/// <summary>
/// D.4.1 hazard — the documentation file is a flat list of ID strings and nothing else. These
/// four members are one name at four accessibilities; the ID strings separate them by
/// argument list, and no part of any of them records that one is private and one is public.
/// An index that wants accessibility has to get it from the compiler, not from the annex.
/// </summary>
public class DocIdAccess
{
    /// <summary>
    /// D.4.1 — private, and documented, and emitted. <c>M:…DocIdAccess.Emit</c>: the
    /// generator does not filter by accessibility, so a private member's documentation is in
    /// the file beside a public one's.
    /// </summary>
    /// <returns>Zero.</returns>
    private int Emit() => 0;

    /// <summary>D.4.1 — internal. <c>M:…DocIdAccess.Emit(System.Int32)</c>.</summary>
    /// <param name="count">How many.</param>
    /// <returns>The count.</returns>
    internal int Emit(int count) => count;

    /// <summary>D.4.1 — protected. <c>M:…DocIdAccess.Emit(System.String)</c>.</summary>
    /// <param name="text">What to emit.</param>
    /// <returns>Its length.</returns>
    protected int Emit(string text) => text.Length;

    /// <summary>D.4.1 — public. <c>M:…DocIdAccess.Emit(System.Boolean)</c>.</summary>
    /// <param name="flag">Whether to emit.</param>
    /// <returns>One when the flag is set, zero otherwise.</returns>
    public int Emit(bool flag) => flag ? 1 : 0;

    /// <summary>D.4.1 — uses the private and protected overloads, so none is dead.</summary>
    /// <returns>The sum of what all four return.</returns>
    public int EmitAll() => Emit() + Emit(1) + Emit("x") + Emit(true);
}
