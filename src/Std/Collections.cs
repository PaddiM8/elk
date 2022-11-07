using System;
using System.Collections.Generic;
using System.Linq;
using Elk.Interpreting.Exceptions;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;

namespace Elk.Std;

[ElkModule("collections")]
public class Collections
{
    /// <param name="input"></param>
    /// <returns>The first element of the given indexable object.</returns>
    [ElkFunction("first", Reachability.Everywhere)]
    public static RuntimeObject First(RuntimeObject input)
    {
        if (input is not IIndexable<RuntimeObject> indexable)
            throw new RuntimeCastException(input.GetType(), "indexable");

        return indexable[new RuntimeInteger(0)];
    }

    /// <param name="input"></param>
    /// <returns>The last element of the given indexable object.</returns>
    [ElkFunction("last", Reachability.Everywhere)]
    public static RuntimeObject Last(RuntimeObject input)
    {
        if (input is not IIndexable<RuntimeObject> indexable)
            throw new RuntimeCastException(input.GetType(), "indexable");

        return indexable[new RuntimeInteger(indexable.Count)];
    }

    /// <summary>
    /// Gets the item at the specified index or
    /// the line at the specified index.
    /// </summary>
    /// <param name="input">An indexable object.</param>
    /// <param name="index"></param>
    /// <returns>
    /// Given a string: The line at the specified index.<br />
    /// Given any other indexable object: the element at the specified index.
    /// </returns>
    [ElkFunction("row", Reachability.Everywhere)]
    public static RuntimeObject Row(RuntimeObject input, RuntimeInteger index)
    {
        if (input is RuntimeString str)
        {
            var line = str.Value.ToLines().ElementAtOrDefault((int)index.Value);

            return line == null
                ? RuntimeNil.Value
                : new RuntimeString(line);
        }

        if (input is IIndexable<RuntimeObject> indexable)
            return indexable[index];

        throw new RuntimeCastException(input.GetType(), "indexable");
    }

    /// <param name="items"></param>
    /// <param name="closure"></param>
    /// <returns>A list of values where the closure has been called on each value.</returns>
    /// <example>[1, 2, 3] | select => x: x + 1 #=> [2, 3, 4]</example>
    [ElkFunction("select", Reachability.Everywhere)]
    public static RuntimeList Select(IEnumerable<RuntimeObject> items, Func<RuntimeObject, RuntimeObject> closure)
        => new(items.Select(closure));
}