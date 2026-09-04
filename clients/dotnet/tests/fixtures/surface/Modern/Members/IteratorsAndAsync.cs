using System.Threading.Tasks;

namespace Surface.Modern.Members;

/// <summary>
/// C# 13 — <c>ref</c> and <c>unsafe</c> in iterators and async methods. An iterator and an
/// async method are both rewritten into a state machine, and a <c>ref</c> local or a
/// <c>ref struct</c> local cannot be a field of one — so before C# 13 they were banned
/// outright. C# 13 allows them where they do not live across a <c>yield</c> or an
/// <c>await</c>, which makes the *position* of the local the thing that decides whether the
/// method compiles.
/// </summary>
public static class IteratorsAndAsync
{
    /// <summary>C# 13 — a <c>ref</c> local inside an iterator, scoped to end before the yield.</summary>
    public static IEnumerable<int> Doubled(int[] source)
    {
        for (var index = 0; index < source.Length; index++)
        {
            int doubled;

            {
                ref var slot = ref source[index];
                doubled = slot * 2;
            }

            yield return doubled;
        }
    }

    /// <summary>C# 13 — a <c>ref struct</c> local inside an iterator.</summary>
    public static IEnumerable<char> Initials(string[] words)
    {
        foreach (var word in words)
        {
            char initial;

            {
                ReadOnlySpan<char> span = word;
                initial = span.Length == 0 ? '?' : span[0];
            }

            yield return initial;
        }
    }

    /// <summary>C# 13 — an <c>unsafe</c> block inside an iterator.</summary>
    public static IEnumerable<int> Addresses(int[] source)
    {
        foreach (var value in source)
        {
            int size;

            unsafe
            {
                size = sizeof(int) + value;
            }

            yield return size;
        }
    }

    /// <summary>C# 13 — an <c>unsafe</c> block and a <c>fixed</c> statement inside an async method.</summary>
    public static async Task<int> SumAsync(int[] source)
    {
        await Task.Yield();

        var total = 0;

        unsafe
        {
            fixed (int* head = source)
            {
                for (var index = 0; index < source.Length; index++)
                {
                    total += head[index];
                }
            }
        }

        await Task.Yield();

        return total;
    }

    /// <summary>C# 13 — a <c>ref</c> local in an async method, on both sides of an await.</summary>
    public static async Task<int> ScaleAsync(int[] source)
    {
        int first;

        {
            ref var slot = ref source[0];
            first = slot;
        }

        await Task.Yield();

        {
            ref var slot = ref source[^1];
            first += slot;
        }

        return first;
    }

    /// <summary>C# 13 — an <c>unsafe</c> context in an async iterator, which is both at once.</summary>
    public static async IAsyncEnumerable<int> SizesAsync(int[] source)
    {
        foreach (var value in source)
        {
            await Task.Yield();

            int size;

            unsafe
            {
                size = sizeof(long) + value;
            }

            yield return size;
        }
    }
}
