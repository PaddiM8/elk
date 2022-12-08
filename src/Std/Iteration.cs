#region

using System;
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
    /// <returns>Whether or not all the values in the list evaluate to true.</returns>
    [ElkFunction("all")]
    public static RuntimeBoolean All(IEnumerable<RuntimeObject> items)
        => RuntimeBoolean.From(items.All(x => x.As<RuntimeBoolean>().IsTrue));

    /// <returns>Whether or not all the values in the list evaluate to true and the list is non-empty.</returns>
    [ElkFunction("allAndAny")]
    public static RuntimeBoolean AllANdAny(IEnumerable<RuntimeObject> items)
        => RuntimeBoolean.From(items.Any() && items.All(x => x.As<RuntimeBoolean>().IsTrue));

    /// <returns>Whether or not one of the values evaluates to true.</returns>
    [ElkFunction("any")]
    public static RuntimeBoolean Any(IEnumerable<RuntimeObject> items)
        => RuntimeBoolean.From(items.Any(x => x.As<RuntimeBoolean>().IsTrue));

    /// <param name="items">The items to split into chunks</param>
    /// <param name="size">The maximum size of each chunk</param>
    /// <returns>A list of chunks where each chunk is a list of items of the given size.</returns>
    [ElkFunction("chunks")]
    public static RuntimeList Chunks(IEnumerable<RuntimeObject> items, RuntimeInteger size)
    {
        if (size.Value == 0)
            return new RuntimeList(new List<RuntimeObject>());

        var chunks = new List<RuntimeList>
        {
            new(new List<RuntimeObject>()),
        };

        foreach (var item in items)
        {
            if (chunks.Last().Values.Count < size.Value)
            {
                chunks.Last().Values.Add(item);
            }
            else
            {
                chunks.Add(new(new List<RuntimeObject> { item }));
            }
        }

        return new RuntimeList(chunks);
    }

    /// <param name="items">The items to count</param>
    /// <param name="closure">A condition for which items should be counted</param>
    /// <returns>The amount of items that meet the condition.</returns>
    [ElkFunction("count")]
    public static RuntimeInteger Count(IEnumerable<RuntimeObject> items, Func<RuntimeObject, RuntimeObject> closure)
        => new(items.Count(x => closure(x).As<RuntimeBoolean>().IsTrue));

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

    /// <param name="items"></param>
    /// <param name="closure"></param>
    /// <returns>The first item for which the closure evaluates to true.</returns>
    [ElkFunction("find")]
    public static RuntimeObject Find(IEnumerable<RuntimeObject> items, Func<RuntimeObject, RuntimeObject> closure)
        => items.FirstOrDefault(x => closure(x).As<RuntimeBoolean>().IsTrue) ?? RuntimeNil.Value;

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
            throw new RuntimeCastException(input.GetType(), "Indexable");

        return indexable[new RuntimeInteger(indexable.Count - 1)];
    }

    /// <param name="items">A list of values that will be stringified.</param>
    /// <param name="separator">Character sequence that should be put between each value.</param>
    /// <returns>A new string of all the list values separated by the specified separator string.</returns>
    [ElkFunction("join", Reachability.Everywhere)]
    public static RuntimeString Join(IEnumerable<RuntimeObject> items, RuntimeString? separator = null)
        => new(string.Join(separator?.Value ?? "", items.Select(x => x.As<RuntimeString>())));

    [ElkFunction("reduce")]
    public static RuntimeObject Reduce(IEnumerable<RuntimeObject> items, Func<RuntimeObject, RuntimeObject, RuntimeObject> closure)
        => items.Aggregate(closure);

    [ElkFunction("reverse")]
    public static RuntimeList Reverse(IEnumerable<RuntimeObject> items)
        => new(items.Reverse().ToList());

    /// <param name="items">All items</param>
    /// <param name="count">The amount of items to skip from the left</param>
    /// <returns>A new list without the first n items.</returns>
    [ElkFunction("skip")]
    public static RuntimeList Skip(IEnumerable<RuntimeObject> items, RuntimeInteger count)
        => new(items.Skip((int)count.Value).ToList());

    /// <param name="items">All items</param>
    /// <param name="closure"></param>
    /// <returns></returns>
    [ElkFunction("skipWhile")]
    public static RuntimeList SkipWhile(IEnumerable<RuntimeObject> items, Func<RuntimeObject, RuntimeObject> closure)
        => new(items.SkipWhile(x => closure(x).As<RuntimeBoolean>().IsTrue).ToList());

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

    /// <param name="items"></param>
    /// <param name="closure"></param>
    /// <returns>A list of values where the closure has been called on each value.</returns>
    /// <example>[1, 2, 3] | select => x: x + 1 #=> [2, 3, 4]</example>
    [ElkFunction("select", Reachability.Everywhere)]
    public static RuntimeList Select(IEnumerable<RuntimeObject> items, Func<RuntimeObject, RuntimeObject> closure)
        => new(items.Select(closure));

    /// <param name="items"></param>
    /// <param name="closure"></param>
    /// <returns>A list of flattenedflattened  values where the closure has been called on each value.</returns>
    /// <example>["abc", "def"] | select => x: x  #=> ["a", "b", "c", "d", "e", "f"]</example>
    [ElkFunction("selectFlat", Reachability.Everywhere)]
    public static RuntimeList SelectFlat(IEnumerable<RuntimeObject> items, Func<RuntimeObject, RuntimeObject> closure)
    {
        var result = new List<RuntimeObject>();
        foreach (var item in items)
        {
            if (item is not IEnumerable<RuntimeObject> enumerable)
                throw new RuntimeCastException(item.GetType(), "Iterable");

            result.AddRange(enumerable.Select(closure));
        }

        return new(result);
    }

    /// <param name="items">All items</param>
    /// <param name="count">The amount of items to take from the left</param>
    /// <returns>A new list with the specified amount of items.</returns>
    [ElkFunction("take")]
    public static RuntimeList Take(IEnumerable<RuntimeObject> items, RuntimeInteger count)
        => new(items.Take((int)count.Value).ToList());

    /// <param name="items">All items</param>
    /// <param name="closure"></param>
    /// <returns></returns>
    [ElkFunction("takeWhile")]
    public static RuntimeList TakeWhile(IEnumerable<RuntimeObject> items, Func<RuntimeObject, RuntimeObject> closure)
        => new(items.TakeWhile(x => closure(x).As<RuntimeBoolean>().IsTrue).ToList());

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

    /// <param name="items"></param>
    /// <param name="closure"></param>
    /// <returns>A list of values containing only those who evaluated to true in the closure.</returns>
    /// <example>[1, 2, 3] | select => x: x + 1 #=> [2, 3, 4]</example>
    [ElkFunction("where", Reachability.Everywhere)]
    public static RuntimeList Where(IEnumerable<RuntimeObject> items, Func<RuntimeObject, RuntimeObject> closure)
        => new(items.Where(x => closure(x).As<RuntimeBoolean>().IsTrue));

    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns>A list containing pairs of values.</returns>
    /// <example>[1, 2, 3] | iter::zip([4, 5, 6]) #=> [(1, 4), (2, 5), (3, 6)]</example>
    [ElkFunction("zip")]
    public static RuntimeList Zip(IEnumerable<RuntimeObject> a, IEnumerable<RuntimeObject> b)
        => new(a.Zip(b).Select(x => new RuntimeTuple(new[] { x.First, x.Second })));
}