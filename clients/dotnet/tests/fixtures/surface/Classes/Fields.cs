// Clause 15.5 — fields. 15.5.1 is the declaration and its modifier list; 15.5.2 the
// static/instance division; 15.5.3.1–15.5.3.3 `readonly`, static readonly as a stand-in for a
// constant, and the versioning difference between the two; 15.5.4 `volatile`; 15.5.5 the
// default-value initialization every field gets whether or not it says so; and 15.5.6.1–.3
// variable initializers, static and instance.

namespace Surface.Classes;

/// <summary>
/// 15.5.1 hazard — every field modifier the standard lists, on one container. `First`,
/// `Second` and `Third` share one declaration and are three members; `ConstLimit` and
/// `StaticReadonlyLimit` share a value and differ in kind, which is 15.5.3.3's whole subject.
/// </summary>
public class ClsFieldModifiers
{
    /// <summary>15.4 and 15.5.3.3 — the constant half of the versioning pair.</summary>
    public const int ConstLimit = 12;

    /// <summary>15.5.3.3 — the static readonly half: the same value, resolved at run time
    /// instead of being baked into every reference.</summary>
    public static readonly int StaticReadonlyLimit = 12;

    /// <summary>15.5.2 — a static field.</summary>
    public static int StaticMutable;

    /// <summary>15.5.2 — an instance field, which is the same declaration minus a keyword.</summary>
    public int Instance;

    /// <summary>15.5.3.1 — a readonly instance field, assignable only in a constructor, and
    /// this class declares none, so it keeps its default value (CS0649).</summary>
    public readonly int ReadonlyInstance;

    /// <summary>15.5.3.1 — a readonly field with an initializer, which is the other place
    /// the standard permits an assignment.</summary>
    public readonly int ReadonlyInitialized = ConstLimit;

    /// <summary>15.5.3.2 — a static readonly field standing in for a constant of a type no
    /// constant may have: a class type's value cannot be a compile-time constant unless it
    /// is null, so this is how such a "constant" is written.</summary>
    public static readonly ClsPlain SharedDefault = new ClsPlain();

    /// <summary>15.5.3.2 — the same idea at an array type, which is mutable through the
    /// reference the `readonly` protects.</summary>
    public static readonly int[] SharedRow = new int[ConstLimit];

    /// <summary>15.5.4 — `volatile` on `int`, one of the types the standard permits it on.</summary>
    private volatile int _ticket;

    /// <summary>15.5.4 — `volatile` on `bool`.</summary>
    private volatile bool _ready;

    /// <summary>15.5.4 — `volatile` on a reference type.</summary>
    private volatile ClsPlain? _target;

    /// <summary>15.5.4 — `volatile` on an enumeration type with a permitted base type.</summary>
    private volatile ClsGrade _grade;

    /// <summary>15.5.4 — `volatile` on `System.IntPtr`, which the list names explicitly.</summary>
    private volatile System.IntPtr _handle;

    /// <summary>15.5.4 — `volatile` and `static` together, which is the combination the
    /// modifier is usually written for.</summary>
    private static volatile int _sharedTicket;

    // 15.5.4 — `volatile` on `long`, `ulong`, `double` or `decimal` is an error (CS0677):
    // the permitted list stops at the types with atomic reads on every conforming
    // implementation, so a 64-bit field cannot carry the modifier.

    /// <summary>15.5.1 — `required`, a post-standard field modifier: the field must be set
    /// in an object initializer, and the compiler stops asking for a constructor to do it.</summary>
    public required string RequiredName;

    /// <summary>15.5.5 — a field with no initializer, which is initialized to the default
    /// value of its type before any other initialization runs.</summary>
    public int NoInitializer;

    /// <summary>15.5.5 — the same for a reference type, whose default is `null`.</summary>
    public ClsPlain? NoInitializerReference;

    /// <summary>
    /// 15.5.1 hazard — one `field_declaration`, three `variable_declarator`s, three members.
    /// The three have one type and one modifier list and three names, and only one syntax
    /// node between them to hang an identity on.
    /// </summary>
    public int First, Second, Third;

    /// <summary>15.5.6.1 hazard — the same shape with an initializer on each declarator, so
    /// two of the three members also have distinct initializer expressions.</summary>
    public int Alpha = 1, Beta = 2, Gamma;

    /// <summary>15.5.6.3 — an instance field initializer, which runs per instance and may
    /// read constants and static members but has no `this` available.</summary>
    public int WithInitializer = ConstLimit + 1;

    /// <summary>15.5.6.2 — a static field initializer, which runs once for the class.</summary>
    public static int StaticWithInitializer = ConstLimit * 2;

    /// <summary>15.5.6.2 — a static initializer reading another class's static readonly
    /// field, so the order the two classes are initialized in is observable.</summary>
    public static int StaticFromOther = ClsStaticClassReferences.CopiedLimit;

    /// <summary>15.5.6.1 — an initializer that is an object creation, so the initializer
    /// carries a reference to a constructor as well as to a type.</summary>
    public ClsPlain OwnPlain = new ClsPlain();

    /// <summary>15.5.6.1 — an initializer that is an array creation with an element list.</summary>
    public int[] Row = { 1, 2, 3 };

    /// <summary>15.5.6.1 — an initializer that is an enumeration member access.</summary>
    public ClsGrade GradeField = ClsGrade.Mid;

    /// <summary>15.5.6.1 — an initializer over a nullable reference element type.</summary>
    public string?[] Names = new string?[2];
}

/// <summary>
/// 15.5.2 hazard — the static and instance fields of a *generic* class. `Shared` exists once
/// per closed type and `Own` once per instance of one: one declaration, and as many storage
/// locations as there are type arguments in the program.
/// </summary>
/// <typeparam name="TItem">The argument that multiplies the static field.</typeparam>
public class ClsFieldsOfGeneric<TItem>
{
    /// <summary>15.5.2 — one per closed constructed type.</summary>
    public static TItem? Shared;

    /// <summary>15.5.2 — one per instance.</summary>
    public TItem? Own;

    /// <summary>15.5.3.1 — a readonly field of the type parameter's type.</summary>
    public readonly TItem? Frozen;

    /// <summary>15.5.1 — a field whose type is an array of the type parameter.</summary>
    public TItem[]? Row;
}

/// <summary>
/// 15.5.3.3 — versioning. Reading a constant copies its value into the reference; reading a
/// static readonly field goes to the field. The two reads below are written the same way and
/// mean different things, which is the clause's point and is invisible in the source.
/// </summary>
public static class ClsVersioningReads
{
    /// <summary>15.5.3.3 — a read of a constant, which the compiler folds.</summary>
    public static readonly int FromConst = ClsFieldModifiers.ConstLimit;

    /// <summary>15.5.3.3 — a read of a static readonly field, which stays a field access.</summary>
    public static readonly int FromStaticReadonly = ClsFieldModifiers.StaticReadonlyLimit;

    /// <summary>15.5.3.2 — a read of the reference-typed stand-in constant.</summary>
    public static readonly ClsPlain SharedDefaultCopy = ClsFieldModifiers.SharedDefault;
}
