using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Surface.Modern.Attributes;

/// <summary>C# 10 — Allow AsyncMethodBuilder attribute on methods.</summary>
public static class AsyncBuilders
{
    /// <summary>
    /// C# 10 — <c>[AsyncMethodBuilder]</c> on an individual async method. Before C# 10 the
    /// builder was a property of the *return type*, so an override for one method had no
    /// spelling; here the attribute sits on the method and names a builder type the return
    /// type would not have chosen.
    /// </summary>
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    public static async ValueTask PooledAsync()
    {
        await Task.Yield();
    }

    /// <summary>The generic builder, named unbound, on a method returning a value.</summary>
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    public static async ValueTask<int> PooledCountAsync(int count)
    {
        await Task.Yield();

        return count;
    }

    /// <summary>The same shape with no attribute, so the annotated pair is a contrast.</summary>
    public static async ValueTask<int> PlainCountAsync(int count)
    {
        await Task.Yield();

        return count;
    }
}
