// Clause 23.3 (attribute specification), every target but the two global ones.
//
// The clause's list of attribute-target identifiers is `field`, `event`, `method`, `param`,
// `property`, `return`, `type`, `typevar`, `assembly` and `module`. Eight of them are here,
// each written explicitly at least once even where it is the default, because the explicit
// and implicit spellings are the pair worth separating: `[AttrMark("x")]` on a field and
// `[field: AttrMark("x")]` on the same field are the same application with two source
// texts.
//
// Three targets attach to something the source does not declare, and they are the hazard:
//
//   * `return:` attaches to the return value, which has no declaration and no name. An
//     index with no node for it either drops the application or files it on the method —
//     and then the method appears to carry two applications of an AllowMultiple=false
//     attribute class, which is a shape the compiler would have refused.
//   * `field:` on an auto-property or on a field-like event attaches to a *compiler
//     generated* backing field whose metadata name is `<Name>k__BackingField`. There is no
//     such identifier in this file.
//   * `param:` on a set accessor attaches to the implicit `value` parameter, which is
//     likewise not declared anywhere here.
//
// `AttrRecordTarget` is the sharpest case a reader would predict: one positional parameter
// of a record produces a parameter, a property and a backing field, and the three sections
// below annotate all three from one place in the source. An index keyed on (declaring
// symbol, attribute class) mints one string for the three of them.
//
// Two things the built assembly does here that reading the clause does not predict, both
// found by inspecting metadata rather than by reasoning:
//
//   * **A field-like event's backing field carries the event's own name.** `Changed` below
//     produces `event Changed` *and* `private EventHandler Changed` in one type — the
//     backing field is not name-mangled the way an auto-property's `<Name>k__BackingField`
//     is. So one source line declares two members with one name in one type, and an
//     identity string of (type, member name) is one string for an event and a field. This
//     is the closest thing in this project to a refused write that is not one of the five
//     quarantined shapes, and it is reached without writing anything unusual.
//   * **`[method: ...]` on a field-like event applies to both accessors.** The single
//     `[AttrRepeatable(3)]` section below is present on `add_Changed` and on
//     `remove_Changed`: one application in the source, two in the assembly, with no
//     syntax distinguishing them.

using System;
using Surface.Attributes.Classes;

namespace Surface.Attributes.Specification;

/// <summary>23.3: the <c>type</c> and <c>typevar</c> targets, written explicitly.</summary>
/// <typeparam name="TItem">Annotated through the <c>typevar</c> target.</typeparam>
[type: AttrMark("the type, through the explicit target")]
[AttrRepeatable(1)]
public class AttrEveryTarget<[typevar: AttrMark("the type parameter")] TItem>
    where TItem : notnull
{
    /// <summary>23.3: the <c>field</c> target on a field, where it is also the default.</summary>
    [field: AttrDetail("the field, through the explicit target")]
    [AttrDetail("the same field, through the default target")]
    public int Counted;

    private TItem? _item;

    /// <summary>23.3: the <c>method</c> target on a constructor.</summary>
    [method: AttrMark("the constructor")]
    public AttrEveryTarget()
    {
    }

    /// <summary>
    /// 23.3: the <c>event</c>, <c>field</c> and <c>method</c> targets on one field-like
    /// event. The <c>field</c> target lands on a backing field that is also called
    /// <c>Changed</c>, and the <c>method</c> target lands on <em>both</em> accessors.
    /// </summary>
    [event: AttrMark("the event itself")]
    [field: AttrRepeatable(2)]
    [method: AttrRepeatable(3)]
    public event EventHandler? Changed;

    /// <summary>
    /// 23.3: the <c>property</c> and <c>field</c> targets on one auto-property, which is
    /// two declarations from one member and only one of them has a source name.
    /// </summary>
    [property: AttrMark("the property")]
    [field: AttrRepeatable(4)]
    public string Name { get; set; } = "unnamed";

    /// <summary>
    /// 23.3: the <c>param</c> and <c>return</c> targets where they are not on a method —
    /// the setter's implicit <c>value</c> parameter and the getter's return value.
    /// </summary>
    public int Doubled
    {
        [return: AttrDetail("the getter's return value")]
        get => Counted * 2;

        [param: AttrDetail("the setter's value parameter")]
        set => Counted = value / 2;
    }

    /// <summary>
    /// 23.3: <c>method</c>, <c>param</c> and <c>return</c> on one method. The parameter
    /// carries both spellings, because <c>param</c> is the only legal target there.
    /// </summary>
    /// <param name="index">Annotated twice, explicitly and by default.</param>
    /// <param name="label">Annotated by the default target only.</param>
    [method: AttrMark("the method")]
    [return: AttrRepeatable(5)]
    public string Describe(
        [param: AttrDetail("explicit param target")] [AttrDetail("default param target")] int index,
        [AttrRepeatable(6)] string label) =>
        $"{label}#{index}:{_item}";

    /// <summary>23.3: the one indexer this type declares, with a parameter attribute.</summary>
    /// <param name="slot">Which slot.</param>
    public int this[[AttrDetail("the indexer's parameter")] int slot] => Counted + slot;

    /// <summary>23.3: <c>return</c> on an operator, which is a method with no name.</summary>
    [return: AttrDetail("an operator's return value")]
    public static AttrEveryTarget<TItem> operator +(
        AttrEveryTarget<TItem> left,
        [AttrDetail("an operator's parameter")] AttrEveryTarget<TItem> right) =>
        new() { Counted = left.Counted + right.Counted };

    /// <summary>Raises the event, so the declaration above is referenced.</summary>
    public void Raise() => Changed?.Invoke(this, EventArgs.Empty);

    /// <summary>Stores the item, so the field is referenced.</summary>
    public void Hold(TItem item) => _item = item;
}

/// <summary>
/// 23.3: a delegate declaration, which carries the <c>type</c>, <c>return</c> and
/// <c>param</c> targets — and, uniquely, the <c>method</c> target on something that has no
/// method body anywhere in the source.
/// </summary>
/// <param name="posting">A parameter of a delegate type.</param>
/// <returns>Whatever the target returns.</returns>
[type: AttrMark("the delegate type")]
[return: AttrDetail("the delegate's return value")]
public delegate bool AttrTargetHandler([AttrDetail("the delegate's parameter")] string posting);

/// <summary>
/// 23.3: an accessor and an explicit interface implementation, which are the two members
/// whose metadata name is not the name in the source.
/// </summary>
public sealed class AttrAccessorTargets : IDisposable
{
    /// <summary>23.3: attributes on both accessors of an explicit property.</summary>
    public bool Open
    {
        [method: AttrDetail("get_Open")]
        get;

        [method: AttrDetail("set_Open")]
        set;
    }

    /// <summary>23.3: on an interface method's implementation.</summary>
    [method: AttrMark("Dispose")]
    public void Dispose() => Open = false;
}

/// <summary>
/// 23.3: the record positional parameter, where one source name declares three things and
/// the three targets separate them.
/// </summary>
/// <param name="Ledger">A parameter, a property and a backing field at once.</param>
public sealed record AttrRecordTarget(
    [param: AttrDetail("the primary constructor's parameter")]
    [property: AttrDetail("the generated property")]
    [field: AttrDetail("the generated backing field")]
    string Ledger)
{
    /// <summary>23.3: the <c>method</c> target on a primary constructor is written on the
    /// type declaration, so the section and the symbol are in different places.</summary>
    public string Describe() => Ledger;
}

/// <summary>23.3: an enum, whose members take the <c>field</c> target.</summary>
public enum AttrTargetedEnum
{
    /// <summary>Annotated through the default target, which is <c>field</c>.</summary>
    [AttrMark("an enum member")]
    First = 1,

    /// <summary>And through the explicit one.</summary>
    [field: AttrDetail("an enum member, explicitly")]
    Second = 2,
}

/// <summary>23.3: an interface and a struct, so the <c>type</c> target is not only a class.</summary>
[AttrMark("an interface")]
public interface IAttrTargeted
{
    /// <summary>An interface method, annotated.</summary>
    [AttrDetail("an interface method")]
    [return: AttrDetail("an interface method's return value")]
    int Weigh([AttrDetail("an interface method's parameter")] string of);
}

/// <summary>23.3: the <c>type</c> target on a struct, and an implementation.</summary>
[type: AttrMark("a struct")]
public readonly struct AttrTargetedStruct : IAttrTargeted
{
    /// <summary>Implements the interface method, attributes and all.</summary>
    [AttrDetail("an implementation")]
    public int Weigh(string of) => of.Length;
}
