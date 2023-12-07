using System.Collections.Generic;
using Elk.Interpreting.Exceptions;

namespace Elk.Std.DataTypes;

static class Extensions
{
    public static List<T> GetRange<T>(this List<T> items, RuntimeRange range)
    {
        var (start, length) = ResolveRange(range, items.Count);

        try
        {
            return items.GetRange(start, length);
        }
        catch
        {
            throw new RuntimeItemNotFoundException($"{range.From}..{range.To}");
        }
    }

    public static T GetAt<T>(this IList<T> items, RuntimeInteger index)
    {
        var indexValue = (int)index.As<RuntimeInteger>().Value;
        if (indexValue < 0)
            indexValue = items.Count + indexValue;

        try
        {
            return items[indexValue];
        }
        catch
        {
           throw new RuntimeItemNotFoundException(index.ToString());
        }
    }

    public static void RemoveAt<T>(this IList<T> items, RuntimeInteger index)
    {
        var indexValue = (int)index.As<RuntimeInteger>().Value;
        if (indexValue < 0)
            indexValue = items.Count + indexValue;

        try
        {
            items.RemoveAt(indexValue);
        }
        catch
        {
           throw new RuntimeItemNotFoundException(index.ToString());
        }
    }

    public static void RemoveRange<T>(this List<T> items, RuntimeRange range)
    {
        var (start, length) = ResolveRange(range, items.Count);

        try
        {
            items.RemoveRange(start, length);
        }
        catch
        {
            throw new RuntimeItemNotFoundException($"{range.From}..{range.To}");
        }
    }

    public static string Substring(this string value, RuntimeRange range)
    {
        var (start, length) = ResolveRange(range, value.Length);

        try
        {
            return value.Substring(start, length);
        }
        catch
        {
            throw new RuntimeItemNotFoundException($"{range.From}..{range.To}");
        }
    }

    private static (int start, int length) ResolveRange(RuntimeRange range, int containerLength)
    {
        var from = range.From is < 0
            ? containerLength + range.From.Value
            : range.From ?? 0;

        var to = range.To is < 0
            ? containerLength + range.To.Value
            : range.To ?? containerLength;

        return (
            (int)from,
            (int)(to - from)
        );
    }

    public static int OrdinalCompare(this IEnumerable<RuntimeObject> self, IEnumerable<RuntimeObject> other)
    {
        foreach (var (a, b) in self.ZipLongest(other))
        {
            if (a == null)
                return -1;
            if (b == null)
                return 1;

            var order = a.CompareTo(b);
            if (order != 0)
                return order;
        }

        return 0;
    }
}