// Clause 9.7 — reference variables and returns, and the ref safe contexts that bound them.
// A ref local is an alias for another variable, and every rule in 9.7.2 is about how far
// that alias may travel. The syntax an index can see is small — `ref`, `ref readonly`,
// `scoped`, `= ref` — and the four combinations of ref-ness and readonly-ness on a ref
// field are the finest distinction in the clause.

namespace Surface.Types;

/// <summary>
/// 9.7.1 hazard — ref locals and ref returns. <c>cursor</c> below is one declaration whose
/// referent changes: the <c>= ref</c> reassignment is not an assignment to the variable it
/// aliases but a rebinding of the alias itself.
/// </summary>
public static class VarRefLocalsAndReturns
{
    private static int _slot = 1;

    private static readonly int[] _buffer = [1, 2, 3];

    /// <summary>9.7.1 — a ref local, and a write through it that the referent sees.</summary>
    public static int RefLocal()
    {
        ref int alias = ref _slot;
        alias = 5;
        return _slot;
    }

    /// <summary>9.7.1 — a ref readonly local, which may be read through and not written.</summary>
    public static int RefReadonlyLocal()
    {
        ref readonly int alias = ref _slot;
        return alias;
    }

    /// <summary>
    /// 9.7.1 hazard — one ref local, two referents. The first statement declares and binds,
    /// the second rebinds, and the third writes through whichever binding is current.
    /// </summary>
    public static int Reseat()
    {
        ref int cursor = ref _buffer[0];
        cursor = ref _buffer[1];
        cursor = 9;
        return _buffer[0] + _buffer[1];
    }

    /// <summary>9.7.1 — a ref return of a static variable, whose ref safe context is the
    /// caller-context and so may escape.</summary>
    public static ref int SlotRef() => ref _slot;

    /// <summary>9.7.1 — a ref readonly return of the same variable.</summary>
    public static ref readonly int SlotReadonlyRef() => ref _slot;

    /// <summary>9.7.1 — a ref return of an array element (9.2.4).</summary>
    public static ref int ElementRef(int index) => ref _buffer[index];

    /// <summary>9.7.1 — a ref return with a block body, so the `ref` appears on the return
    /// statement as well as on the signature.</summary>
    public static ref int LargestRef()
    {
        if (_buffer[0] > _buffer[1])
        {
            return ref _buffer[0];
        }

        return ref _buffer[1];
    }

    /// <summary>
    /// 9.7.2.6 hazard — invoking a ref-returning method. The invocation is itself a variable
    /// reference (9.5), so it may stand on the left of an assignment with no local between.
    /// </summary>
    public static int WriteThroughInvocation()
    {
        SlotRef() = 7;
        return _slot;
    }

    /// <summary>9.7.2.6 hazard — binding a ref local to an invocation's result, whose ref
    /// safe context is the narrowest of the arguments'.</summary>
    public static int BindInvocationResult()
    {
        ref int alias = ref ElementRef(2);
        alias += 1;
        return _buffer[2];
    }

    /// <summary>9.7.2.2 — a ref local bound to a local variable. Its ref safe context is
    /// this block, so returning it is what the clause forbids and nothing here does.</summary>
    public static int RefToALocal()
    {
        int local = 1;
        ref int alias = ref local;
        alias = 2;
        return local;
    }

    /// <summary>9.7.2.5 — the ref conditional operator, whose result is a variable reference
    /// whose ref safe context is the narrower of its two arms'.</summary>
    public static int RefConditional(bool flag)
    {
        int left = 1;
        int right = 2;
        ref int chosen = ref flag ? ref left : ref right;
        chosen = 9;
        return left + right;
    }

    /// <summary>9.7.2.7 hazard — a value passed to an `in` parameter. There is no variable to
    /// alias, so the compiler creates an unnamed one whose ref safe context is the
    /// caller-context; no declaration in the source names it.</summary>
    public static double MeasureValues() =>
        VarInputParameters.Measure(new TyPoint2D(1, 2)) + VarInputParameters.Negate(42);
}

/// <summary>
/// 9.7.2.3 hazard — parameter ref safe contexts. A plain <c>ref</c> parameter has the
/// caller-context and may be returned; a <c>scoped ref</c> parameter has the local context
/// and may not. The two differ by one keyword and in nothing else an index can see.
/// </summary>
public static class VarParameterRefSafety
{
    /// <summary>9.7.2.3 hazard — a ref parameter returned by reference, which is legal
    /// because its ref safe context is the caller's.</summary>
    public static ref int Escaping(ref int slot) => ref slot;

    /// <summary>9.7.2.3 hazard — the same parameter marked `scoped`, which may be written
    /// through and not returned.</summary>
    public static void NotEscaping(scoped ref int slot) => slot = 1;

    /// <summary>9.7.2.3 — a `scoped in` parameter, and a `scoped ref readonly` one.</summary>
    public static double ReadOnlyScoped(scoped in TyPoint2D point) => point.X;

    public static double StrictlyScoped(scoped ref readonly TyPoint2D point) => point.Y;

    /// <summary>9.7.2.3 — `scoped` on a value parameter of a ref struct type, which narrows
    /// the ref safe context of the references it carries rather than of the parameter.</summary>
    public static int ScopedRefStruct(scoped VarRefSlot slot) => slot.Slot;

    /// <summary>9.7.2.3 — an `out` parameter, whose ref safe context is the caller-context by
    /// definition, so a ref struct may carry a reference out through it.</summary>
    public static void OutRefStruct(ref int backing, out VarRefSlot slot) =>
        slot = new VarRefSlot(ref backing);

    /// <summary>9.7.2.3 — a `scoped` local, which narrows a ref struct local by hand.</summary>
    public static int ScopedLocal()
    {
        int backing = 3;
        scoped VarRefSlot slot = new(ref backing);
        slot.Slot = 4;
        return backing;
    }
}

/// <summary>
/// 9.7.2.4 — field ref safe contexts. A <c>ref</c> field may appear only in a ref struct,
/// and the four combinations below are four different declarations: whether the alias can be
/// rebound, and whether the referent can be written.
/// </summary>
public ref struct VarRefSlot
{
    /// <summary>9.7.2.4 — a ref field: rebindable alias, writable referent.</summary>
    public ref int Slot;

    /// <summary>9.7.2.4 — a readonly ref field: the alias is fixed at construction, the
    /// referent is still writable.</summary>
    public readonly ref int FixedAlias;

    /// <summary>9.7.2.4 — a ref readonly field: the alias may be rebound, the referent may
    /// only be read.</summary>
    public ref readonly int ReadOnlyReferent;

    /// <summary>9.7.2.4 — and both at once, which is the narrowest of the four.</summary>
    public readonly ref readonly int FixedAndReadOnly;

    /// <summary>
    /// 9.7.2.8 hazard — a ref struct constructor taking a <c>ref</c> parameter. The ref safe
    /// context of the constructed value is the narrowest of its arguments', so what this
    /// constructor returns is bounded by what the caller passed in.
    /// </summary>
    public VarRefSlot(ref int backing)
    {
        Slot = ref backing;
        FixedAlias = ref backing;
        ReadOnlyReferent = ref backing;
        FixedAndReadOnly = ref backing;
    }

    /// <summary>9.7.2.4 — reading through each of the four fields.</summary>
    public readonly int SumOfAliases() =>
        Slot + FixedAlias + ReadOnlyReferent + FixedAndReadOnly;

    /// <summary>9.7.2.4 — writing through the two whose referent is writable.</summary>
    public void Write(int value)
    {
        Slot = value;
        FixedAlias = value;
    }

    /// <summary>
    /// 9.7.2.4 — rebinding the two fields whose alias is not readonly. The referent has to
    /// be a variable of caller-context: a <c>ref</c> parameter of this method would be
    /// return-only, and ref-assigning one into a field of <c>this</c> is the rule 9.7.2.3
    /// exists to forbid, so the referent here is a static variable instead.
    /// </summary>
    public void RebindToFallback()
    {
        Slot = ref VarRefStorage.Fallback;
        ReadOnlyReferent = ref VarRefStorage.Fallback;
    }
}

/// <summary>
/// 9.7.2.4 — a variable whose ref safe context is the caller-context, which is what a ref
/// field may be rebound to outside a constructor.
/// </summary>
public static class VarRefStorage
{
    /// <summary>9.7.2.4 — the static variable the ref fields above fall back to.</summary>
    public static int Fallback;
}

/// <summary>
/// 9.7.2.8 hazard — constructor invocations that carry references out. Both methods build
/// the same ref struct; only the ref safe context of the argument decides whether the result
/// may leave the method.
/// </summary>
public static class VarConstructorRefSafety
{
    private static int _shared = 1;

    /// <summary>9.7.2.8 — constructed from a static variable, whose ref safe context is the
    /// caller-context, so the value may be returned.</summary>
    public static VarRefSlot FromStatic() => new(ref _shared);

    /// <summary>9.7.2.8 hazard — constructed from a local, so the value is confined to this
    /// block; it is used here and not returned.</summary>
    public static int FromLocal()
    {
        int local = 2;
        VarRefSlot slot = new(ref local);
        slot.Write(5);
        return local;
    }

    /// <summary>9.7.2.8 — an object creation expression whose argument is a ref parameter,
    /// which is the case the narrowing rule exists for.</summary>
    public static VarRefSlot FromParameter(ref int backing) => new(ref backing);
}

/// <summary>
/// 9.7.2.6 hazard — <c>this</c> in a struct is a scoped ref, so a member cannot return a
/// reference to a field. <c>UnscopedRef</c> is the attribute that says otherwise, and it is
/// the only difference between the two methods below.
/// </summary>
public struct VarUnscopedRef
{
    private int _value;

    private readonly int _fixed;

    public VarUnscopedRef(int value)
    {
        _value = value;
        _fixed = value;
    }

    /// <summary>9.7.2.6 hazard — a ref return of a field, legal only with the attribute.</summary>
    [System.Diagnostics.CodeAnalysis.UnscopedRef]
    public ref int ValueRef() => ref _value;

    /// <summary>9.7.2.6 — the readonly counterpart, likewise unscoped.</summary>
    [System.Diagnostics.CodeAnalysis.UnscopedRef]
    public readonly ref readonly int FixedRef() => ref _fixed;

    /// <summary>9.7.2.6 — a member that returns the field's value rather than a reference to
    /// it, which needs no attribute at all.</summary>
    public readonly int Value() => _value;

    /// <summary>9.7.2.6 — writing through the escaped reference, from outside the struct.</summary>
    public static int WriteThroughEscaped()
    {
        VarUnscopedRef holder = new(1);
        holder.ValueRef() = 8;
        return holder.Value();
    }
}
