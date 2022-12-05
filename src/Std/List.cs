using System.Linq;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;

namespace Elk.Std;

[ElkModule("list")]
public class List
{
    /// <summary>
    /// Appends all the items in one list to another list.
    /// </summary>
    /// <param name="a">The list to extend</param>
    /// <param name="b">The list to take items from</param>
    /// <returns>A reference to list `a`.</returns>
    [ElkFunction("extend")]
    public static RuntimeList Extend(RuntimeList a, RuntimeList b)
    {
        a.Values.AddRange(b.Values);

        return a;
    }

    /// <summary>
    /// Inserts a value at the specified index in a list.
    /// </summary>
    /// <param name="list">List to act on</param>
    /// <param name="index">Index the item should be placed at</param>
    /// <param name="value">Value to insert</param>
    /// <returns>The same list.</returns>
    [ElkFunction("insert", Reachability.Everywhere)]
    public static RuntimeList Insert(RuntimeList list, RuntimeInteger index, RuntimeObject value)
    {
        list.Values.Insert((int)index.Value, value);

        return list;
    }

    [ElkFunction("pop", Reachability.Everywhere)]
    public static RuntimeObject Pop(RuntimeList list, RuntimeInteger? index = null)
    {
        int i = (int?)index?.Value ?? list.Count - 1;
        var value = list.Values.ElementAtOrDefault(i);
        if (value != null)
            list.Values.RemoveAt(i);

        return value ?? RuntimeNil.Value;
    }

    /// <summary>
    /// Removes the items within the given range.
    /// </summary>
    /// <param name="list"></param>
    /// <param name="range"></param>
    /// <returns>The same container.</returns>
    [ElkFunction("removeRange", Reachability.Everywhere)]
    public static RuntimeObject RemoveRange(RuntimeList list, RuntimeRange range)
    {
        int from = range.From ?? 0;
        int to = range.To ?? list.Count;
        list.Values.RemoveRange(from, to - from);

        return list;
    }
}