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
    /// <summary>
    /// Appends all the items in one list to another list.
    /// </summary>
    /// <param name="a">The list to extend</param>
    /// <param name="b">The list to take items from</param>
    /// <returns>A reference to list `a`.</returns>
    public static RuntimeList Extend(RuntimeList a, RuntimeList b)
    {
        a.Values.AddRange(b.Values);
        
        return a;
    }

    /// <summary>
    /// Pushes the given value to the container.
    /// </summary>
    /// <param name="container" types="List, Dictionary"></param>
    /// <param name="value1">List: Value to push<br />Set: Element<br />Dictionary: Key</param>
    /// <param name="value2">Dictionary: Value to push</param>
    /// <returns>The same container.</returns>
    /// <example>
    /// list | push(x)
    /// dict | push("name", "John")
    /// </example>
    [ElkFunction("push", Reachability.Everywhere)]
    public static RuntimeObject Push(
        RuntimeObject container,
        RuntimeObject value1,
        RuntimeObject? value2 = null)
    {
        if (container is RuntimeList list)
        {
            list.Values.Add(value1);
        }
        else if (container is RuntimeSet set)
        {
            set.Entries.Add(value1.GetHashCode(), value1);
        }
        else if (container is RuntimeDictionary dict)
        {
            if (value2 == null)
                throw new RuntimeWrongNumberOfArgumentsException(3, 2);

            dict.Entries.Add(value1.GetHashCode(), (value1, value2));
        }
        else
        {
            throw new RuntimeException("Can only use function 'push' on lists and dictionaries");
        }

        return container;
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

    /// <param name="items"></param>
    /// <returns>The first element of the given iterable object.</returns>
    [ElkFunction("first", Reachability.Everywhere)]
    public static RuntimeObject First(IEnumerable<RuntimeObject> items)
        => items.FirstOrDefault() ?? RuntimeNil.Value;

    /// <param name="input"></param>
    /// <returns>The last element of the given indexable object.</returns>
    [ElkFunction("last", Reachability.Everywhere)]
    public static RuntimeObject Last(RuntimeObject input)
    {
        if (input is not IIndexable<RuntimeObject> indexable)
            throw new RuntimeCastException(input.GetType(), "indexable");

        return indexable[new RuntimeInteger(indexable.Count - 1)];
    }

    /// <param name="container" types="Tuple, List, Dictionary"></param>
    /// <returns>The amount of items in the container.</returns>
    [ElkFunction("len", Reachability.Everywhere)]
    public static RuntimeInteger Length(RuntimeObject container)
        => container switch
        {
            RuntimeTuple tuple => new(tuple.Values.Count),
            RuntimeList list => new(list.Values.Count),
            RuntimeSet set => new(set.Entries.Count),
            RuntimeDictionary dict => new(dict.Entries.Count),
            _ => new(container.As<RuntimeString>().Value.Length),
        };

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
    /// Removes the item at the given index.
    /// </summary>
    /// <param name="container" types="List, Dictionary"></param>
    /// <param name="index">Index of the item to remove</param>
    /// <returns>The same container.</returns>
    [ElkFunction("remove", Reachability.Everywhere)]
    public static RuntimeObject Remove(RuntimeObject container, RuntimeObject index)
    {
        if (container is RuntimeList list)
        {
            list.Values.RemoveAt((int)index.As<RuntimeInteger>().Value);
        }
        else if (container is RuntimeSet set)
        {
            set.Entries.Remove(index.GetHashCode());
        }
        else if (container is RuntimeDictionary dict)
        {
            dict.Entries.Remove(index.GetHashCode());
        }
        else
        {
            throw new RuntimeException("Can only use function 'remove' on lists and dictionaries");
        }

        return container;
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
    public static RuntimeObject Row(IIndexable<RuntimeObject> input, RuntimeInteger index)
    {
        if (input is RuntimeString str)
        {
            var line = str.Value.ToLines().ElementAtOrDefault((int)index.Value);

            return line == null
                ? RuntimeNil.Value
                : new RuntimeString(line);
        }

        return input[index];
    }

    /// <summary>
    /// Gets the item at the specified index or
    /// the line at the specified index.
    /// </summary>
    /// <param name="input">An indexable object.</param>
    /// <param name="startIndex"></param>
    /// <param name="endIndex"></param>
    /// <returns>
    /// Given a string: The lines between the specified indices.<br />
    /// Given any other indexable object: the elements between the specified indices.
    /// </returns>
    [ElkFunction("rows", Reachability.Everywhere)]
    public static RuntimeObject Rows(IIndexable<RuntimeObject> input, RuntimeInteger startIndex, RuntimeInteger endIndex)
    {
        if (input is RuntimeString str)
        {
            var lines = str.Value.ToLines();
            if (lines.Length == 0 || startIndex.Value < 0 || endIndex.Value >= lines.Length)
                return RuntimeNil.Value;

            var range = lines[(int)startIndex.Value..(int)endIndex.Value];

            return new RuntimeList(range.Select(x => new RuntimeString(x)));
        }

        return input[new RuntimeRange((int)startIndex.Value, (int)endIndex.Value)];
    }

    /// <param name="items"></param>
    /// <param name="closure"></param>
    /// <returns>A list of values where the closure has been called on each value.</returns>
    /// <example>[1, 2, 3] | select => x: x + 1 #=> [2, 3, 4]</example>
    [ElkFunction("select", Reachability.Everywhere)]
    public static RuntimeList Select(IEnumerable<RuntimeObject> items, Func<RuntimeObject, RuntimeObject> closure)
        => new(items.Select(closure));

    /// <param name="items"></param>
    /// <param name="closure"></param>
    /// <returns>A list of values containing only those who evaluated to true in the closure.</returns>
    /// <example>[1, 2, 3] | select => x: x + 1 #=> [2, 3, 4]</example>
    [ElkFunction("where", Reachability.Everywhere)]
    public static RuntimeList Where(IEnumerable<RuntimeObject> items, Func<RuntimeObject, RuntimeObject> closure)
        => new(items.Where(x => closure(x).As<RuntimeBoolean>().IsTrue));
}