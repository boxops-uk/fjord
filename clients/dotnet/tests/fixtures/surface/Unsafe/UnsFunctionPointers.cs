// Clause 24.3's post-standard extension — function pointer types (`delegate*<...>`), in
// every position a member can put one, and in every calling convention the syntax admits.
//
// **This is the one type kind in clause 24 that cannot be keyed at all.**
// `csharp.FunctionPointerType` names a `signature : Method`, a `csharp.Method`'s key leads
// with a `containingType`, and Roslyn gives a function pointer's signature symbol no
// containing type, no containing namespace and no containing symbol — so
// `CsharpEntities.Type` has no fact to point at, returns null, and counts the drop in
// `Inexpressible`. The union is in the key of `Method`, `Field`, `Parameter` and `Local`,
// so a *member* typed as one is dropped whole rather than recorded under a fabricated
// type. Every declaration in this file whose signature mentions a function pointer is
// therefore a declaration the index does not hold, and the count of them is the only
// evidence they were here — which is exactly why they are here.

using System.Runtime.InteropServices;

namespace Surface.Unsafe;

/// <summary>
/// Clause 24.3 — function pointer types as a field, a parameter, a return type, a local, a
/// property type and an array element type.
/// </summary>
public unsafe class UnsFunctionPointers
{
    /// <summary>Clause 24.3 — a function-pointer-typed <b>field</b>: managed calling convention.</summary>
    public delegate*<int, int> Doubler;

    /// <summary>Clause 24.3 — a field whose function pointer takes nothing and returns nothing.</summary>
    public delegate*<void> Nothing;

    /// <summary>Clause 24.3 — two parameters, so the arity is not the reason for anything.</summary>
    public delegate*<int, int, int> Adder;

    /// <summary>Clause 24.3 — the <c>unmanaged</c> calling convention, unqualified.</summary>
    public delegate* unmanaged<int, int> UnmanagedDoubler;

    /// <summary>Clause 24.3 — an explicit <c>Cdecl</c> calling convention.</summary>
    public delegate* unmanaged[Cdecl]<int, int> CdeclDoubler;

    /// <summary>Clause 24.3 — an explicit <c>Stdcall</c> calling convention, returning <c>void</c>.</summary>
    public delegate* unmanaged[Stdcall]<int, void> StdcallSink;

    /// <summary>Clause 24.3 — two convention identifiers in one list.</summary>
    public delegate* unmanaged[Cdecl, SuppressGCTransition]<int, int> SuppressedDoubler;

    /// <summary>Clause 24.3 — a function pointer whose parameter is a function pointer.</summary>
    public delegate*<delegate*<int, int>, int> HigherOrder;

    /// <summary>Clause 24.3 — a function pointer whose parameter is an ordinary pointer.</summary>
    public delegate*<int*, int> OverPointer;

    /// <summary>Clause 24.3 — a <c>ref</c> parameter in a function pointer signature.</summary>
    public delegate*<ref int, void> ByReference;

    /// <summary>Clause 24.3 — an <c>out</c> parameter, and a <c>bool</c> return.</summary>
    public delegate*<int, out int, bool> TryForm;

    /// <summary>
    /// Clause 24.3 — an <b>array</b> of function pointers: the array type is managed and
    /// keyable in itself, but its element type is not, so
    /// <c>CsharpEntities.Type</c>'s array arm returns null on the element and the field is
    /// dropped like the scalar ones above.
    /// </summary>
    public delegate*<int, int>[] Table;

    /// <summary>Clause 24.3 — a function-pointer-typed <b>property</b>.</summary>
    public delegate*<int, int> Selected => &Twice;

    /// <summary>An ordinary static method: the target every <c>&amp;</c> below names.</summary>
    public static int Twice(int value) => value * 2;

    /// <summary>A second target, so that a function pointer variable has something to be reassigned to.</summary>
    public static int Halve(int value) => value / 2;

    /// <summary>Clause 24.3 — a function-pointer-typed <b>return type</b>.</summary>
    public static delegate*<int, int> Chosen(bool half) => half ? &Halve : &Twice;

    /// <summary>Clause 24.3 — a function-pointer-typed <b>parameter</b>, and an invocation through it.</summary>
    /// <remarks>
    /// The parameter cannot be keyed, and <c>CsharpEntities.Ordered</c> increments its index
    /// whether or not the entity came back — so the surviving <c>csharp.MethodParameter</c>
    /// edge for <c>value</c> carries index 1 with no index 0 beside it.
    /// </remarks>
    public static int Apply(delegate*<int, int> function, int value) => function(value);

    /// <summary>Clause 24.3 — a function-pointer-typed <b>local</b>, reassigned and then invoked.</summary>
    public static int Local(int value)
    {
        delegate*<int, int> function = &Twice;
        function = &Halve;
        return function(value);
    }

    /// <summary>Clause 24.6.5 — the address of a method, which is what makes a function pointer value.</summary>
    public static delegate*<int, int> AddressOfMethod() => &Twice;

    /// <summary>Clause 24.6.9 — <c>sizeof</c> a function pointer type, which is unmanaged.</summary>
    public static int SizeOfFunctionPointer() => sizeof(delegate*<int, int>);

    /// <summary>Clause 24.5.1 — the explicit conversion from a function pointer to <c>void*</c> and back.</summary>
    public static delegate*<int, int> RoundTrip(delegate*<int, int> function)
    {
        void* erased = function;
        return (delegate*<int, int>)erased;
    }

    /// <summary>An entry point for native callers, whose address is the unmanaged form's value.</summary>
    [UnmanagedCallersOnly]
    public static int Native(int value) => value + 1;

    /// <summary>Clause 24.3 — the address of an <see cref="UnmanagedCallersOnlyAttribute"/> method.</summary>
    public static delegate* unmanaged<int, int> NativePointer() => &Native;
}
