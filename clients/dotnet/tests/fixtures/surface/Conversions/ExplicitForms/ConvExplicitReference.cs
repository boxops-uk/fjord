using System;
using System.Collections;
using System.Collections.Generic;
using Surface.Conversions.ImplicitForms;

namespace Surface.Conversions.ExplicitForms;

/// <summary>The outer layer. Its <c>Peel</c> is the one a base-typed reference names.</summary>
public class ConvHusk
{
    /// <summary>Takes the layer off.</summary>
    public string Peel() => "husk";
}

/// <summary>
/// What is inside.
///
/// Clause 10.3.5, deliberate hazard: <see cref="Peel"/> hides <see cref="ConvHusk.Peel"/>
/// with <c>new</c> rather than overriding it, so the corpus holds two members with the same
/// name and the same empty parameter list, one per class, and an explicit reference
/// conversion at the call site is what decides which one a reference means.
/// </summary>
public sealed class ConvKernel : ConvHusk
{
    /// <summary>Takes the layer off, and is a different member from the base's.</summary>
    public new string Peel() => "kernel";
}

/// <summary>
/// Clause 10.3.5 — the explicit reference conversions. Every one is checked at run time, and
/// the clause also owns <c>as</c> and the type-testing forms of <c>is</c>, which are the same
/// conversion asked as a question rather than asserted.
/// </summary>
public static class ConvExplicitReference
{
    /// <summary>Clause 10.3.5 — <c>object</c> down to a class.</summary>
    public static ConvLeaf ObjectToLeaf(object value) => (ConvLeaf)value;

    /// <summary>Clause 10.3.5 — base class down to derived class.</summary>
    public static ConvLeaf NodeToLeaf(ConvNode node) => (ConvLeaf)node;

    /// <summary>Clause 10.3.5 — <c>object</c> down to an interface.</summary>
    public static IConvLabelled ObjectToInterface(object value) => (IConvLabelled)value;

    /// <summary>Clause 10.3.5 — interface to a class that may implement it.</summary>
    public static ConvLeaf InterfaceToLeaf(IConvLabelled labelled) => (ConvLeaf)labelled;

    /// <summary>Clause 10.3.5 — interface to unrelated interface, which the clause permits.</summary>
    public static IConvProducer<string> LabelledToProducer(IConvLabelled labelled) =>
        (IConvProducer<string>)labelled;

    /// <summary>Clause 10.3.5 — array to array, with an element downcast.</summary>
    public static string[] ObjectsToStrings(object[] values) => (string[])values;

    /// <summary>Clause 10.3.5 — <see cref="Array"/> down to an array type.</summary>
    public static int[] ArrayToInts(Array values) => (int[])values;

    /// <summary>Clause 10.3.5 — <see cref="Delegate"/> down to a delegate type.</summary>
    public static Func<int> DelegateToFunc(Delegate callback) => (Func<int>)callback;

    /// <summary>Clause 10.3.5 — the non-generic sequence down to the generic one.</summary>
    public static IEnumerable<int> SequenceToInts(IEnumerable values) => (IEnumerable<int>)values;

    /// <summary>Clause 10.3.5 — the variance conversion, run backwards with a cast.</summary>
    public static IConvProducer<string> NarrowProducer(IConvProducer<object> producer) =>
        (IConvProducer<string>)producer;

    /// <summary>Clause 10.3.5 — the same conversion as a question, via <c>as</c>.</summary>
    public static ConvLeaf? TryLeaf(object value) => value as ConvLeaf;

    /// <summary>Clause 10.3.5 — via <c>as</c> to an interface.</summary>
    public static IConvLabelled? TryLabelled(object value) => value as IConvLabelled;

    /// <summary>Clause 10.3.5 — via <c>is</c>, which answers without converting.</summary>
    public static bool IsLeaf(object value) => value is ConvLeaf;

    /// <summary>Clause 10.3.5 — via a <c>is</c> pattern, which answers and converts.</summary>
    public static string LabelIfLeaf(object value) => value is ConvLeaf leaf ? leaf.Label : "?";

    /// <summary>Clause 10.3.5, hazard — the base's <c>Peel</c>, reached through a cast.</summary>
    public static string PeelAsHusk(ConvKernel kernel) => ((ConvHusk)kernel).Peel();

    /// <summary>Clause 10.3.5, hazard — the derived one, reached with no cast at all.</summary>
    public static string PeelAsKernel(ConvKernel kernel) => kernel.Peel();
}
