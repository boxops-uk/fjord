using System;
using System.Collections.Generic;

namespace Surface.Expressions.Primary;

/// <summary>
/// A receiver graph that can be null at every level, so a null-conditional chain has somewhere
/// to short-circuit. Declared here, used both in this file and in <c>PxCrossFileUses.cs</c>.
/// </summary>
public sealed class PxMaybe
{
    private readonly List<int> _slots = [1, 2, 3];

    /// <summary>A nullable reference to another link, for <c>a?.Next?.Next</c>.</summary>
    public PxMaybe? Next { get; init; }

    /// <summary>A non-nullable member reached through a conditional receiver.</summary>
    public int Seed { get; init; } = 1;

    /// <summary>A settable member, for the post-standard null-conditional assignment.</summary>
    public int Adjusted { get; set; }

    /// <summary>A nullable value-typed member, so a chain can hit <c>Nullable&lt;T&gt;</c> mid-way.</summary>
    public PxPoint? Point { get; init; }

    /// <summary>A delegate-typed member, for a null-conditional invocation.</summary>
    public PxTransform? Transform { get; init; }

    /// <summary>An event, whose backing delegate is the classic null-conditional invocation target.</summary>
    public event PxNotice? Notified;

    /// <summary>The one indexer, for a null-conditional element access.</summary>
    public int this[int slot] => _slots[slot];

    /// <summary>A method, for a null-conditional invocation.</summary>
    public int Measure() => _slots.Count;

    /// <summary>12.8.11 — the idiomatic null-conditional invocation of an event.</summary>
    public void Raise(string message) => Notified?.Invoke(message);

    /// <summary>
    /// Same-file null-conditional uses of every member above: member access (12.8.8),
    /// invocation (12.8.11) and element access (12.8.13).
    /// </summary>
    public string UsedHere()
    {
        var link = new PxMaybe { Next = this, Point = new PxPoint(1, 2), Transform = value => value };

        var member = link?.Seed;                 // 12.8.8
        var chained = link?.Next?.Seed;          // 12.8.8, twice
        var invoked = link?.Measure();           // 12.8.11
        var delegated = link?.Transform?.Invoke(2); // 12.8.11 through a delegate
        var element = link?[0];                  // 12.8.13
        this.Raise("same file");

        return $"{member} {chained} {invoked} {delegated} {element}";
    }
}

/// <summary>
/// 12.8.8, 12.8.11 and 12.8.13 — the null-conditional forms. The receiver of every one of these
/// is nullable, and the member each binds to is declared on a type that is *not* the receiver's
/// type whenever the receiver is a nullable value type.
/// </summary>
public static class PxNullConditional
{
    /// <summary>
    /// 12.8.8 — null-conditional member access: on a reference, on a nullable value type (where
    /// the member is on the underlying type, not on <c>Nullable&lt;T&gt;</c>), and chained.
    /// </summary>
    public static string MemberAccess(PxMaybe? link, int? count, PxPoint? point)
    {
        var onReference = link?.Seed;
        var chained = link?.Next?.Next?.Seed;
        var mixed = link?.Next!.Seed;                 // conditional, then null-forgiving (12.8.9.1)
        var onNullableValue = count?.ToString();      // binds to int.ToString, not Nullable<int>'s
        var onNullableStruct = point?.X;              // binds to PxPoint.X through the lifting
        var toNullable = point?.Weight;               // a property whose value becomes int?
        var coalesced = link?.Seed ?? -1;
        var deepThenPlain = link?.Next.Seed;          // only the first access is conditional

        return $"{onReference} {chained} {mixed} {onNullableValue} {onNullableStruct} {toNullable} {coalesced} {deepThenPlain}";
    }

    /// <summary>
    /// 12.8.11 — null-conditional invocation: of a method, of a delegate-valued member, of an
    /// event, and of a member on a nullable value type.
    /// </summary>
    public static string Invocation(PxMaybe? link, PxTransform? transform, int? count)
    {
        var method = link?.Measure();
        var throughDelegate = transform?.Invoke(1);
        var throughDelegateShorthand = link?.Transform?.Invoke(2);
        var onNullableValue = count?.GetHashCode();
        var chainedCall = link?.Next?.Measure();
        var conditionalThenPlain = link?.Measure().ToString();

        link?.Raise("conditional");

        return $"{method} {throughDelegate} {throughDelegateShorthand} {onNullableValue} {chainedCall} {conditionalThenPlain}";
    }

    /// <summary>
    /// 12.8.13 — null-conditional element access: on an array, on a string, on a declared
    /// indexer, and on a constructed generic indexer.
    /// </summary>
    public static string ElementAccess(int[]? array, string? text, PxMaybe? link, Dictionary<string, int>? lookup)
    {
        var fromArray = array?[0];
        var fromString = text?[0];
        var fromIndexer = link?[1];
        var fromGeneric = lookup?["a"];
        var chained = link?.Next?[2];
        var thenMember = array?[0].ToString();
        var fromEnd = array?[^1];

        return $"{fromArray} {fromString} {fromIndexer} {fromGeneric} {chained} {thenMember} {fromEnd}";
    }

    /// <summary>
    /// 12.8.9.1 — the null-forgiving operator. It binds nothing of its own; what it changes is
    /// the nullable analysis of the operand, and the member access under it binds as usual.
    /// </summary>
    public static int NullForgiving(PxMaybe? link)
    {
        var forgiven = link!.Seed;
        var forgivenCall = link!.Measure();
        var forgivenElement = link![0];
        var forgivenChain = link!.Next!.Seed;
        return forgiven + forgivenCall + forgivenElement + forgivenChain;
    }

    /// <summary>
    /// 12.8.8 in its post-standard extension: a null-conditional *assignment*, where the whole
    /// assignment is skipped when the receiver is null.
    /// </summary>
    public static int ConditionalAssignment(PxMaybe? link)
    {
        link?.Adjusted = 5;
        link?.Adjusted += 1;
        return link?.Adjusted ?? 0;
    }

    /// <summary>
    /// A null-conditional chain whose intermediate type differs at every link, which is where a
    /// receiver-typed index most often points at the wrong declaration.
    /// </summary>
    public static string ChangingReceiverTypes(PxMaybe? link)
    {
        var throughPoint = link?.Point?.Measure();          // PxMaybe -> PxPoint? -> PxPoint
        var throughString = link?.Seed.ToString()?.Length;  // int -> string -> int
        var throughBox = new PxBox<PxMaybe?>(link).Value?.Seed;
        return $"{throughPoint} {throughString} {throughBox}";
    }
}
