using System.Collections.Generic;
using System.Linq;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;

namespace Elk.Std;

[ElkModule("set")]
public static class Set
{
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns>The union of the two given sets.</returns>
    [ElkFunction("union")]
    public static RuntimeSet Union(RuntimeSet a, RuntimeSet b)
    {
        var dict = new Dictionary<int, RuntimeObject>();
        foreach (var item in a)
            dict.TryAdd(item.GetHashCode(), item);
        foreach (var item in b)
            dict.TryAdd(item.GetHashCode(), item);

        return new RuntimeSet(dict);
    }

    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns>The intersect of the two given sets.</returns>
    [ElkFunction("intersect")]
    public static RuntimeSet Intersect(RuntimeSet a, RuntimeSet b)
    {
        return new(
            a
                .Where(item => b.Entries.ContainsKey(item.GetHashCode()))
                .ToDictionary(item => item.GetHashCode())
        );
    }
}