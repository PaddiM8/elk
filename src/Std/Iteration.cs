#region

using System;
using System.Collections.Generic;
using System.Linq;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;

#endregion

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

namespace Elk.Std;

[ElkModule("iter")]
static class Iteration
{
    /// <returns>Whether or not all the values in the list evaluate to true.</returns>
    [ElkFunction("all")]
    public static RuntimeBoolean All(IEnumerable<RuntimeObject> items)
        => RuntimeBoolean.From(items.All(x => x.As<RuntimeBoolean>().IsTrue));

    /// <returns>Whether or not one of the values in the list evaluates to true.</returns>
    [ElkFunction("any")]
    public static RuntimeBoolean Any(IEnumerable<RuntimeObject> items)
        => RuntimeBoolean.From(items.Any(x => x.As<RuntimeBoolean>().IsTrue));

    /// <summary>
    /// Invokes the given closure on each item in the given container.
    /// </summary>
    /// <param name="items">Container to iterate over.</param>
    /// <param name="closure">Closure to invoke on every individual item.</param>
    [ElkFunction("each", Reachability.Everywhere)]
    public static void Each(IEnumerable<RuntimeObject> items, Action<RuntimeObject> closure)
    {
        foreach (var item in items)
            closure(item);
    }

    /// <param name="items">A list of values that will be stringified.</param>
    /// <param name="separator">Character sequence that should be put between each value.</param>
    /// <returns>A new string of all the list values separated by the specified separator string.</returns>
    [ElkFunction("join", Reachability.Everywhere)]
    public static RuntimeString Join(IEnumerable<RuntimeObject> items, RuntimeString? separator = null)
        => new(string.Join(separator?.Value ?? "", items.Select(x => x.As<RuntimeString>())));

    /// <param name="items">All items</param>
    /// <param name="count">The amount of items to skip from the left</param>
    /// <returns>A new list without the first n items.</returns>
    [ElkFunction("skip")]
    public static RuntimeList Skip(IEnumerable<RuntimeObject> items, RuntimeInteger count)
        => new(items.Skip((int)count.Value).ToList());

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

    /// <param name="items">All items</param>
    /// <param name="count">The amount of items to take from the left</param>
    /// <returns>A new list with the specified amount of items.</returns>
    [ElkFunction("take")]
    public static RuntimeList Take(IEnumerable<RuntimeObject> items, RuntimeInteger count)
        => new(items.Take((int)count.Value).ToList());

    /// <param name="values" types="Iterable"></param>
    /// <returns>A list containing a tuple for each item in the original container.
    /// The first item of the tuple is the item from the original container, while the second item is the index of that item.</returns>
    /// <example>for item, i in values: echo("{i}: {item}")</example>
    [ElkFunction("withIndex", Reachability.Everywhere)]
    public static RuntimeList WithIndex(RuntimeObject values)
    {
        var items = values as IEnumerable<RuntimeObject> ?? values.As<RuntimeList>().Values;

        return new(items.Select((x, i) => new RuntimeTuple(new[] { x, new RuntimeInteger(i) })));
    }

    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns>A list containing pairs of values.</returns>
    /// <example>[1, 2, 3] | iter::zip([4, 5, 6]) #=> [(1, 4), (2, 5), (3, 6)]</example>
    [ElkFunction("zip")]
    public static RuntimeList Zip(IEnumerable<RuntimeObject> a, IEnumerable<RuntimeObject> b)
        => new(a.Zip(b).Select(x => new RuntimeTuple(new[] { x.First, x.Second })));
}