// Clause 23.6 (attributes for interoperation).
//
// The clause names the attributes that describe how a managed declaration maps onto an
// unmanaged one: StructLayout and FieldOffset for layout, DllImport for a platform
// invocation, MarshalAs, In and Out for the marshalling of each value, and the COM set —
// ComImport, Guid, InterfaceType — for a type whose implementation is not in this
// assembly at all.
//
// Two hazards, both about declarations with no body.
//
//   * A `DllImport` method is `static extern`: a declaration whose implementation is a
//     string in an attribute argument. `"kernel32.dll"` and `"GetTickCount64"` name an
//     entry point that no assembly in this corpus contains, and `EntryPoint` lets the
//     managed name differ from the unmanaged one — so `Ticks` and `TicksRenamed` are two
//     declarations of one imported function, which is a many-to-one an index has nothing
//     to key on. Nothing here is ever called at run time; the declarations are the point.
//   * A `ComImport` interface's members are likewise bodiless, and its identity is a GUID
//     rather than a name. `IAttrComLedger` has a `Guid` and a slot order that is part of
//     its contract, so its two methods are ordered declarations whose ordinal matters —
//     the same property a partial member's two halves have, and the reason the ordinal is
//     worth asserting here where it is safe.
//
// `AttrOverlaid` is the explicit-layout case: three fields at two offsets, so two of them
// are the same four bytes. Two declarations of one storage location, which is as close as
// C# comes to a union.

using System;
using System.Runtime.InteropServices;

namespace Surface.Attributes.Reserved;

/// <summary>
/// 23.6: sequential layout with both named parameters, which is the form that makes a
/// struct safe to hand to unmanaged code.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
public struct AttrPostingRecord
{
    /// <summary>The posting's ordinal.</summary>
    public int Ordinal;

    /// <summary>
    /// 23.6: a fixed-length string field, whose length lives in an attribute argument
    /// rather than in the type.
    /// </summary>
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string Label;

    /// <summary>23.6: a one-byte boolean, which is not what a managed bool is.</summary>
    [MarshalAs(UnmanagedType.U1)]
    public bool Posted;

    /// <summary>
    /// 23.6: a fixed-size array field, with both the element count and the element's
    /// unmanaged type in attribute arguments.
    /// </summary>
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4, ArraySubType = UnmanagedType.I4)]
    public int[] Weights;
}

/// <summary>
/// 23.6: explicit layout. Two of the three fields below are the same storage, and the
/// only statement of that is a pair of equal <c>FieldOffset</c> arguments.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 8)]
public struct AttrOverlaid
{
    /// <summary>The first four bytes, as an integer.</summary>
    [FieldOffset(0)]
    public int AsInt;

    /// <summary>The same four bytes, as a float.</summary>
    [FieldOffset(0)]
    public float AsSingle;

    /// <summary>The second four bytes, which overlap nothing.</summary>
    [FieldOffset(4)]
    public int Tag;
}

/// <summary>
/// 23.6: the platform invocations. Every method here is a declaration whose body is
/// somewhere else, named by a string.
/// </summary>
internal static class AttrNativeMethods
{
    /// <summary>23.6: the minimal form — a library name and a matching managed name.</summary>
    /// <returns>Milliseconds since the system started.</returns>
    [DllImport("kernel32.dll")]
    internal static extern ulong GetTickCount64();

    /// <summary>
    /// 23.6: <c>EntryPoint</c>, so the managed name and the unmanaged one differ. This is
    /// the same imported function as above, declared a second time.
    /// </summary>
    /// <returns>Milliseconds since the system started.</returns>
    [DllImport("kernel32.dll", EntryPoint = "GetTickCount64")]
    internal static extern ulong TicksRenamed();

    /// <summary>
    /// 23.6: every named parameter the clause mentions, plus <c>In</c>, <c>Out</c> and a
    /// marshalled return value.
    /// </summary>
    /// <param name="source">Marshalled in only.</param>
    /// <param name="destination">Marshalled out only.</param>
    /// <param name="length">How much to copy.</param>
    /// <returns>Whether it worked.</returns>
    [DllImport(
        "kernel32.dll",
        EntryPoint = "CopyFileW",
        CharSet = CharSet.Unicode,
        SetLastError = true,
        ExactSpelling = true,
        CallingConvention = CallingConvention.Winapi,
        BestFitMapping = false,
        ThrowOnUnmappableChar = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CopyOne(
        [In] [MarshalAs(UnmanagedType.LPWStr)] string source,
        [Out] [MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder destination,
        [In] [Out] ref int length);

    /// <summary>
    /// 23.6: an import in a nested type, so the type a P/Invoke hangs off is not always
    /// the one a reader expects.
    /// </summary>
    internal static class Nested
    {
        /// <summary>23.6: the same library, imported from a second type.</summary>
        /// <returns>The current thread's identifier.</returns>
        [DllImport("kernel32.dll")]
        internal static extern uint GetCurrentThreadId();
    }
}

/// <summary>
/// 23.6: a COM interface. Its identity is the GUID, its members are ordered slots, and its
/// implementation is in no assembly here.
/// </summary>
[ComImport]
[Guid("6B29FC40-CA47-1067-B31D-00DD010662DA")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IAttrComLedger
{
    /// <summary>23.6: slot three, after IUnknown's three.</summary>
    /// <param name="posting">What to post.</param>
    void Post([MarshalAs(UnmanagedType.LPWStr)] string posting);

    /// <summary>23.6: slot four. The order of these two declarations is the contract.</summary>
    /// <returns>How many postings there are.</returns>
    [return: MarshalAs(UnmanagedType.I4)]
    int Count();
}

/// <summary>
/// 23.6: a dispatch interface, so the two <c>InterfaceType</c> arguments both appear.
/// </summary>
[ComImport]
[Guid("6B29FC40-CA47-1067-B31D-00DD010662DB")]
[InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
public interface IAttrComReport
{
    /// <summary>The one member.</summary>
    /// <returns>The report.</returns>
    [return: MarshalAs(UnmanagedType.BStr)]
    string Render();
}

/// <summary>
/// 23.6: a delegate whose calling convention is stated for unmanaged callers, which is the
/// one attribute in the clause that applies to a delegate type.
/// </summary>
/// <param name="ordinal">Which posting.</param>
/// <returns>Whether to continue.</returns>
[UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
public delegate bool AttrNativeCallback(int ordinal);

/// <summary>
/// 23.6: the managed side, so every interop declaration above is referenced from
/// somewhere that compiles.
/// </summary>
public static class AttrInteropUse
{
    /// <summary>
    /// Builds the layout structs and names the imports and the COM interfaces, without
    /// calling anything unmanaged.
    /// </summary>
    /// <returns>A description of what was built.</returns>
    public static string Describes()
    {
        AttrPostingRecord record = new()
        {
            Ordinal = 1,
            Label = "a posting",
            Posted = true,
            Weights = [1, 2, 3, 4],
        };

        AttrOverlaid overlaid = new() { AsInt = 0x3F800000, Tag = 7 };

        AttrNativeCallback callback = static ordinal => ordinal > 0;

        return string.Join(
            ',',
            record.Label,
            record.Weights.Length,
            overlaid.AsSingle,
            overlaid.Tag,
            callback(1),
            Marshal.SizeOf<AttrPostingRecord>(),
            typeof(IAttrComLedger).GUID,
            typeof(IAttrComReport).Name,
            nameof(AttrNativeMethods.GetTickCount64),
            nameof(AttrNativeMethods.TicksRenamed),
            nameof(AttrNativeMethods.CopyOne),
            nameof(AttrNativeMethods.Nested.GetCurrentThreadId));
    }
}
