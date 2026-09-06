// Clause 24 (Unsafe code), 24.1 (General) and 24.2 (Unsafe contexts).
//
// Clause 24.2 says an unsafe context is the textual extent of a declaration carrying the
// `unsafe` modifier, or of an `unsafe` block — and its grammar adds `unsafe` to every
// member modifier list there is. This file writes the modifier in each of those places
// once, so that a reader can see the whole of 24.2 in one screen, and so that a query can
// ask what the index says about an unsafe declaration. The answer is: nothing.
// `CodeMarkup.Modifiers` builds its word list from accessibility, `IsStatic`, `IsAbstract`,
// `IsVirtual`, `IsOverride` and `IsSealed`; there is no arm for `unsafe`, and no field of
// `csharp.Method` or `csharp.Field` holds it either. So every declaration below is written
// exactly once, under a symbol indistinguishable from a safe one's.

using System;

namespace Surface.Unsafe;

/// <summary>
/// Clause 24.2 — <c>unsafe</c> on a class declaration. The whole textual extent of the
/// declaration is an unsafe context, so no member below needs the modifier of its own.
/// </summary>
public unsafe class UnsUnsafeClass
{
    /// <summary>Clause 24.2 — a field of pointer type, legal because the class is unsafe.</summary>
    private readonly int* _origin;

    /// <summary>Clause 24.2 — an instance constructor inside an unsafe context.</summary>
    public UnsUnsafeClass(int* origin) => _origin = origin;

    /// <summary>Clause 24.2 — a static constructor inside an unsafe context.</summary>
    static UnsUnsafeClass() => Size = sizeof(int*);

    /// <summary>Clause 24.2 — a finalizer inside an unsafe context.</summary>
    ~UnsUnsafeClass() => GC.KeepAlive(this);

    /// <summary>Clause 24.6.9 — the size of a pointer, read once into a static.</summary>
    public static int Size { get; }

    /// <summary>Clause 24.2 — a property whose type is a pointer type.</summary>
    public int* Origin => _origin;

    /// <summary>Clause 24.6.2 — indirection through a pointer-typed field.</summary>
    public int Read() => *_origin;
}

/// <summary>Clause 24.2 — <c>unsafe</c> on a struct declaration.</summary>
public unsafe struct UnsUnsafeStruct
{
    /// <summary>A <c>void*</c> field: clause 24.3's pointer to an unknown type.</summary>
    public void* Handle;

    /// <summary>Clause 24.5.1 — the implicit conversion from any pointer type to <c>void*</c>.</summary>
    public void Take(int* cells) => Handle = cells;
}

/// <summary>Clause 24.2 — <c>unsafe</c> on an interface declaration, whose members may be pointer-typed.</summary>
public unsafe interface IUnsUnsafeInterface
{
    /// <summary>A pointer-typed interface property.</summary>
    int* Head { get; }

    /// <summary>A pointer-typed interface method parameter.</summary>
    int Peek(int* at);
}

/// <summary>Clause 24.2 — <c>unsafe</c> on a delegate declaration.</summary>
/// <remarks>
/// A delegate, not a function pointer: this one is an ordinary named type and is keyed like
/// any other, which is the contrast <c>UnsFunctionPointers</c> is written against.
/// </remarks>
public unsafe delegate int UnsPointerCallback(int* argument);

/// <summary>
/// Clause 24.2 — a type that is <i>not</i> unsafe, whose unsafe surface is one modifier or
/// one block at a time. The modifier is per-declaration, so this is the shape that shows a
/// safe and an unsafe member living in one type.
/// </summary>
public class UnsSafeHost : IUnsUnsafeInterface
{
    private int _cell = 3;

    /// <summary>Clause 24.2 — <c>unsafe</c> on a field-like event.</summary>
    public unsafe event EventHandler Read;

    /// <summary>Clause 24.2 — <c>unsafe</c> on an instance method.</summary>
    public unsafe int UnsafeMethod(int value)
    {
        int* at = &value;
        return *at;
    }

    /// <summary>Clause 24.2 — <c>unsafe</c> on a static method.</summary>
    public static unsafe int StaticUnsafeMethod(int value) => *(&value);

    /// <summary>Clause 24.2 — <c>unsafe</c> on a property, whose accessor body is the context.</summary>
    public unsafe int UnsafeProperty
    {
        get
        {
            int copy = _cell;
            return *(&copy);
        }
    }

    /// <summary>Clause 24.2 — the implementation of a pointer-typed interface member.</summary>
    public unsafe int* Head => null;

    /// <summary>Clause 24.2 — an interface method whose parameter is a pointer type.</summary>
    public unsafe int Peek(int* at) => at is null ? 0 : *at;

    /// <summary>
    /// Clause 24.2 — an <c>unsafe</c> block: a statement, in a method with no modifier of
    /// its own, whose body is an unsafe context and whose signature is not.
    /// </summary>
    public int BlockOnly(int value)
    {
        unsafe
        {
            int* at = &value;
            return *at;
        }
    }

    /// <summary>Raises <see cref="Read"/>, so the event has a use as well as a declaration.</summary>
    public void Raise() => Read?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// Clause 24.2 — <c>unsafe</c> on an operator and on an indexer.
/// </summary>
/// <remarks>
/// One indexer, deliberately: a property descriptor is a name plus <c>.</c> and Roslyn's
/// name for every C# indexer is the literal <c>this[]</c>, so a second one in this type
/// would mint the identity string the first one already holds.
/// </remarks>
public class UnsOperatorHost
{
    private readonly int[] _cells = [2, 3, 5, 7];

    /// <summary>Clause 24.2 — an <c>unsafe</c> indexer, reading through a fixed pointer.</summary>
    public unsafe int this[int index]
    {
        get
        {
            fixed (int* at = _cells)
            {
                return at[index];
            }
        }
    }

    /// <summary>Clause 24.2 — an <c>unsafe</c> operator declaration.</summary>
    public static unsafe UnsOperatorHost operator +(UnsOperatorHost left, int right)
    {
        int* offset = &right;
        return left[*offset] > 0 ? left : left;
    }
}
