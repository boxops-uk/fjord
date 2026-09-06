using System;

namespace Surface.Expressions.Operators.Anonymous;

/// <summary>An attribute the lambda signatures of 12.21.2 can carry.</summary>
[AttributeUsage(AttributeTargets.All)]
public sealed class OpLambdaMarkerAttribute : Attribute
{
}

/// <summary>A delegate with a by-reference parameter, for a lambda that declares one.</summary>
public delegate void OpRefMutator(ref int value);

/// <summary>A delegate with an output parameter.</summary>
public delegate bool OpOutProducer(out int value);

/// <summary>A delegate with a read-only by-reference parameter.</summary>
public delegate int OpInReader(in int value);

/// <summary>A delegate with a C# 12 <c>ref readonly</c> parameter.</summary>
public delegate int OpRefReadonlyReader(ref readonly int value);

/// <summary>A delegate with a parameter array, for a lambda that declares <c>params</c>.</summary>
public delegate int OpParamsCounter(params int[] values);

/// <summary>A delegate with a default argument, for a lambda that declares one.</summary>
public delegate int OpDefaultedScale(int factor = 2);

/// <summary>A delegate that returns by reference, so a lambda body can be a ref expression.</summary>
public delegate ref int OpRefSelector(int[] items, int index);
