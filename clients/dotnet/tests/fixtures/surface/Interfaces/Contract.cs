// Clause 19 — interfaces — and 19.1, which says what one is: a contract that declares members
// it does not implement, which classes and structs implement and other interfaces inherit.
//
// Every type declared in this project is named `Iface…`, because twenty-two projects are
// indexed in one run and a simple name has to have one owner across all of them.

namespace Surface.Interfaces;

/// <summary>
/// 19.1 — the contract itself. Neither member has an implementation here, and both are
/// implicitly public and abstract.
/// </summary>
public interface IfaceContract
{
    /// <summary>19.1 — a property the implementer must supply.</summary>
    string Label { get; }

    /// <summary>19.1 — and a method.</summary>
    int Weigh(int units);
}

/// <summary>
/// 19 hazard — a class implementing the contract. This is one of four implementations of one
/// interface declaration in this file: an index must record four distinct base-type edges whose
/// target is the single <see cref="IfaceContract"/> declaration.
/// </summary>
public sealed class IfaceContractClass : IfaceContract
{
    /// <summary>19.1 — the class member the interface property maps onto.</summary>
    public string Label => "class";

    /// <summary>19.1 — and the one its method maps onto.</summary>
    public int Weigh(int units) => units;
}

/// <summary>19 hazard — a struct implements the same contract; the second edge to it.</summary>
public struct IfaceContractStruct : IfaceContract
{
    /// <summary>19.1 — a struct implementing an interface member is not virtual.</summary>
    public string Label => "struct";

    public int Weigh(int units) => units * 2;
}

/// <summary>19 hazard — a record class implements it; the third edge.</summary>
public sealed record IfaceContractRecord(int Units) : IfaceContract
{
    /// <summary>19.1 — a record's members implement interface members like any other class's.</summary>
    public string Label => "record";

    public int Weigh(int units) => units * Units;
}

/// <summary>19 hazard — a record struct implements it; the fourth edge.</summary>
public readonly record struct IfaceContractRecordStruct(int Units) : IfaceContract
{
    public string Label => "record struct";

    public int Weigh(int units) => units + Units;
}

/// <summary>
/// 19.1 — the interface used as a type: a variable, a parameter and a return, each of an
/// interface type whose value is an instance of one of the four implementations.
/// </summary>
public static class IfaceContractUse
{
    /// <summary>19.1 — an interface-typed local holding each implementation in turn.</summary>
    public static int WeighAll(int units)
    {
        IfaceContract fromClass = new IfaceContractClass();
        IfaceContract fromStruct = new IfaceContractStruct();
        IfaceContract fromRecord = new IfaceContractRecord(3);
        IfaceContract fromRecordStruct = new IfaceContractRecordStruct(4);

        return fromClass.Weigh(units)
            + fromStruct.Weigh(units)
            + fromRecord.Weigh(units)
            + fromRecordStruct.Weigh(units);
    }

    /// <summary>19.1 — an interface as a parameter type, so the call site binds to the member
    /// declared in the interface and not to any of the four class members.</summary>
    public static string LabelOf(IfaceContract contract) => contract.Label;

    /// <summary>19.1 — an interface as a return type.</summary>
    public static IfaceContract Heaviest(IfaceContract left, IfaceContract right) =>
        left.Weigh(1) >= right.Weigh(1) ? left : right;

    /// <summary>19.1 — an interface type in a type test, which is what makes a contract
    /// observable at run time.</summary>
    public static bool IsContract(object value) => value is IfaceContract;
}
