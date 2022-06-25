#region

using System.Collections.Generic;
using System.Linq;
using Elk.Interpreting.Exceptions;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;

#endregion

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

namespace Elk.Std;

[ElkModule("iter")]
static class Iteration
{
    /// <summary>
    /// Adds the given value to the container.
    /// </summary>
    /// <param name="container" types="List, Dictionary"></param>
    /// <param name="value1">List: Value to add<br />Dictionary: Key</param>
    /// <param name="value2">Dictionary: Value to add</param>
    /// <returns>The same container.</returns>
    /// <example>
    /// list | add(x)
    /// dict | add("name", "John")
    /// </example>
    [ElkFunction("add", Reachability.Everywhere)]
    public static IRuntimeValue Add(IRuntimeValue container, IRuntimeValue value1, IRuntimeValue? value2 = null)
    {
        if (container is RuntimeList list)
        {
            list.Values.Add(value1);
        }
        else if (container is RuntimeDictionary dict)
        {
            if (value2 == null)
                throw new RuntimeWrongNumberOfArgumentsException(3, 2);

            dict.Entries.Add(value1.GetHashCode(), (value1, value2));
        }
        else
        {
            throw new RuntimeException("Can only use function 'add' on lists and dictionaries");
        }

        return container;
    }

    /// <returns>Whether or not all the values in the list evaluate to true.</returns>
    [ElkFunction("all")]
    public static RuntimeBoolean All(RuntimeList list)
        => RuntimeBoolean.From(list.Values.All(x => x.As<RuntimeBoolean>().Value));

    /// <returns>Whether or not one of the values in the list evaluates to true.</returns>
    [ElkFunction("any")]
    public static RuntimeBoolean Any(RuntimeList list)
        => RuntimeBoolean.From(list.Values.Any(x => x.As<RuntimeBoolean>().Value));

    /// <summary>
    /// Inserts a value at the specified index in a list.
    /// </summary>
    /// <param name="list">List to act on</param>
    /// <param name="index">Index the item should be placed at</param>
    /// <param name="value">Value to insert</param>
    /// <returns>The same list.</returns>
    [ElkFunction("insert", Reachability.Everywhere)]
    public static RuntimeList Insert(RuntimeList list, RuntimeInteger index, IRuntimeValue value)
    {
        list.Values.Insert((int)index.Value, value);

        return list;
    }

    /// <param name="list">A list of values that will be stringified</param>
    /// <param name="separator">Character sequence that should be put between each value</param>
    /// <returns>A new string of all the list values separated by the specified separator string.</returns>
    [ElkFunction("join", Reachability.Everywhere)]
    public static RuntimeString Join(RuntimeList list, RuntimeString? separator = null)
        => new(string.Join(separator?.Value ?? "", list.Values.Select(x => x.As<RuntimeString>())));

    /// <param name="container" types="Tuple, List, Dictionary"></param>
    /// <returns>The amount of items in the container.</returns>
    [ElkFunction("len", Reachability.Everywhere)]
    public static RuntimeInteger Length(IRuntimeValue container)
        => container switch
        {
            RuntimeTuple tuple => new(tuple.Values.Count),
            RuntimeList list => new(list.Values.Count),
            RuntimeDictionary dict => new(dict.Entries.Count),
            _ => new(container.As<RuntimeString>().Value.Length),
        };

    /// <summary>
    /// Removes the item at the given index.
    /// </summary>
    /// <param name="container" types="List, Dictionary"></param>
    /// <param name="index">Index of the item to remove</param>
    /// <returns>The same container.</returns>
    [ElkFunction("remove", Reachability.Everywhere)]
    public static IRuntimeValue Remove(IRuntimeValue container, IRuntimeValue index)
    {
        if (container is RuntimeList list)
        {
            list.Values.RemoveAt((int)index.As<RuntimeInteger>().Value);
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

    /// <summary>Changes the step size of the given range. The step size determines how much the range value should increase by after each iteration.</summary>
    /// <param name="range">Range to modify</param>
    /// <param name="step">Step size</param>
    /// <returns>The same range.</returns>
    /// <example>
    /// # 0, 2, 4, 6, 8, 10
    /// for i in 0..10 | stepBy(2): echo(i)
    /// </example>
    [ElkFunction("stepBy", Reachability.Everywhere)]
    public static RuntimeRange StepBy(RuntimeRange range, RuntimeInteger step)
    {
        range.Increment = (int)step.Value;

        return range;
    }

    /// <param name="values" types="Iterable"></param>
    /// <returns>A list containing a tuple for each item in the original container.
    /// The first item of the tuple is the item from the original container, while the second item is the index of that item.</returns>
    /// <example>for item, i in values: echo("{i}: {item}")</example>
    [ElkFunction("withIndex", Reachability.Everywhere)]
    public static RuntimeList WithIndex(IRuntimeValue values)
    {
        var items = values as IEnumerable<IRuntimeValue> ?? values.As<RuntimeList>().Values;

        return new(items.Select((x, i) => new RuntimeTuple(new[] { x, new RuntimeInteger(i) })));
    }
}