#region

using System;
using System.Collections.Generic;
using System.Linq;
using Elk.Exceptions;
using Elk.Parsing;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;
using Elk.Std.Table;

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

    /// <returns>Whether or not the closure evaluates to true for all of the items.</returns>
    [ElkFunction("allOf")]
    public static RuntimeBoolean AllOf(IEnumerable<RuntimeObject> items, Func<RuntimeObject, RuntimeObject> closure)
        => RuntimeBoolean.From(items.All(x => closure(x).As<RuntimeBoolean>().IsTrue));

    /// <returns>Whether or not all the values in the list evaluate to true and the list is non-empty.</returns>
    [ElkFunction("allAndAny")]
    public static RuntimeBoolean AllANdAny(IEnumerable<RuntimeObject> items)
        => RuntimeBoolean.From(items.Any() && items.All(x => x.As<RuntimeBoolean>().IsTrue));

    /// <returns>Whether or not one of the values evaluates to true.</returns>
    [ElkFunction("any")]
    public static RuntimeBoolean Any(IEnumerable<RuntimeObject> items)
        => RuntimeBoolean.From(items.Any(x => x.As<RuntimeBoolean>().IsTrue));

    /// <returns>Whether or not the closure evaluates to true for any of the items.</returns>
    [ElkFunction("anyOf")]
    public static RuntimeBoolean AnyOf(IEnumerable<RuntimeObject> items, Func<RuntimeObject, RuntimeObject> closure)
        => RuntimeBoolean.From(items.Any(x => closure(x).As<RuntimeBoolean>().IsTrue));

    /// <returns>A generator for the given item append to the given Iterable.</returns>
    [ElkFunction("append")]
    public static RuntimeGenerator Append(IEnumerable<RuntimeObject> items, RuntimeObject item)
        => new(items.Append(item));

    /// <summary>
    /// Equivalent to x[y] but returns nil if the item does not exist.
    /// </summary>
    /// <returns>The item at the given index.</returns>
    [ElkFunction("at")]
    public static RuntimeObject At(IEnumerable<RuntimeObject> items, RuntimeObject index, RuntimeObject? fallback = null)
    {
        if (items is not IIndexable<RuntimeObject> indexable)
            return items.ElementAtOrDefault((int)index.As<RuntimeInteger>().Value) ?? RuntimeNil.Value;

        try
        {
            return indexable[index];
        }
        catch (RuntimeItemNotFoundException)
        {
            return fallback ?? RuntimeNil.Value;
        }
    }

    /// <param name="items">The items to split into chunks</param>
    /// <param name="size">The maximum size of each chunk</param>
    /// <returns>A list of chunks where each chunk is a list of items of the given size.</returns>
    [ElkFunction("chunks")]
    public static RuntimeGenerator Chunks(IEnumerable<RuntimeObject> items, RuntimeInteger size)
        => new(items.Chunk((int)size.Value).Select(x => new RuntimeTuple(x)));

    /// <summary>
    /// Some standard library functions return lazily evaluated Iterables. This function
    /// forces an Iterable's items to be evaluated right away.
    /// </summary>
    /// <param name="items">The Iterable to collect.</param>
    [ElkFunction("collect")]
    public static RuntimeList Collect(IEnumerable<RuntimeObject> items)
        => new(items.ToList());

    /// <param name="first">The first Iterable.</param>
    /// <param name="second">The second Iterable.</param>
    /// <returns>A generator for the items of both the the given Iterables.</returns>
    [ElkFunction("concat")]
    public static RuntimeGenerator Concat(IEnumerable<RuntimeObject> first, IEnumerable<RuntimeObject> second)
        => new(first.Concat(second));

    /// <param name="items">The items to count</param>
    /// <param name="closure">A condition for which items should be counted</param>
    /// <returns>The amount of items that meet the condition.</returns>
    [ElkFunction("count")]
    public static RuntimeInteger Count(IEnumerable<RuntimeObject> items, Func<RuntimeObject, RuntimeObject> closure)
        => new(items.Count(x => closure(x).As<RuntimeBoolean>().IsTrue));

    /// <returns>Whether or not x contains y</returns>
    [ElkFunction("contains")]
    public static RuntimeBoolean Contains(RuntimeObject container, RuntimeObject value)
    {
        var contains = container switch
        {
            RuntimeList list => list.Values
                .Find(x => x.Operation(OperationKind.EqualsEquals, value).As<RuntimeBoolean>().IsTrue) != null,
            RuntimeRange range => range.Contains(value.As<RuntimeInteger>().Value),
            RuntimeSet set => set.Entries.Contains(value),
            RuntimeDictionary dict => dict.Entries.ContainsKey(value),
            RuntimeString str => str.Value.Contains(value.As<RuntimeString>().Value),
            _ => throw new RuntimeInvalidOperationException("in", container.GetType()),
        };

        return RuntimeBoolean.From(contains);
    }

    /// <returns>A list of the distinct items in the given Iterable.</returns>
    [ElkFunction("distinct")]
    public static RuntimeGenerator Distinct(IEnumerable<RuntimeObject> items)
        => new(items.Distinct());

    /// <returns>A list of the distinct items in the given Iterable where the closure determines the keys.</returns>
    [ElkFunction("distinctBy")]
    public static RuntimeGenerator DistinctBy(IEnumerable<RuntimeObject> items, Func<RuntimeObject, RuntimeObject> closure)
        => new(items.DistinctBy(closure));

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

    /// <returns>
    /// The set difference of the given Iterables,
    /// i.e. the items in the first Iterable that don't appear in the second one.
    /// </returns>
    [ElkFunction("except")]
    public static RuntimeGenerator Except(IEnumerable<RuntimeObject> first, IEnumerable<RuntimeObject> second)
        => new(first.Except(second));

    /// <returns>
    /// The set difference of the given Iterables,
    /// i.e. the items in the first Iterable that don't appear in the second one.
    /// The closure is used to determine the keys.
    /// </returns>
    [ElkFunction("exceptBy")]
    public static RuntimeGenerator ExceptBy(
        IEnumerable<RuntimeObject> first,
        IEnumerable<RuntimeObject> second,
        Func<RuntimeObject, RuntimeObject> closure)
        => new(first.ExceptBy(second, closure));

    /// <param name="items"></param>
    /// <param name="closure"></param>
    /// <returns>The first item for which the closure evaluates to true.</returns>
    [ElkFunction("find")]
    public static RuntimeObject Find(IEnumerable<RuntimeObject> items, Func<RuntimeObject, RuntimeObject> closure)
        => items.FirstOrDefault(x => closure(x).As<RuntimeBoolean>().IsTrue) ?? RuntimeNil.Value;

    /// <param name="items"></param>
    /// <param name="closure"></param>
    /// <returns>The index of the first item for which the closure evaluates to true. Returns -1 if no item was found.</returns>
    [ElkFunction("findIndex")]
    public static RuntimeObject FindIndex(IEnumerable<RuntimeObject> items, Func<RuntimeObject, RuntimeObject> closure)
    {
        var index = items
            .Select((x, i) => (closure(x).As<RuntimeBoolean>().IsTrue, i))
            .FirstOrDefault(x => x.Item1, (true, -1))
            .Item2;

        return new RuntimeInteger(index);
    }

    /// <summary>
    /// Throws an error if the Iterable is empty.
    /// </summary>
    /// <param name="items"></param>
    /// <returns>The first element of the given iterable object.</returns>
    [ElkFunction("first")]
    public static RuntimeObject First(IEnumerable<RuntimeObject> items)
        => items.FirstOrDefault()
           ?? throw new RuntimeStdException("Can not get the first item of an empty Iterable.");

    /// <param name="items"></param>
    /// <returns>The first element of the given iterable object, or nil if the Iterable is empty</returns>
    [ElkFunction("firstOrNil")]
    public static RuntimeObject FirstOrNil(IEnumerable<RuntimeObject> items)
        => items.FirstOrDefault() ?? RuntimeNil.Value;

    /// <summary>
    /// Throws an error if the Iterable is empty.
    /// </summary>
    /// <returns>The first element of the given iterable object where the closure returns true.</returns>
    [ElkFunction("firstOf")]
    public static RuntimeObject FirstOf(IEnumerable<RuntimeObject> items, Func<RuntimeObject, RuntimeObject> closure)
        => items.FirstOrDefault(x => closure(x).As<RuntimeBoolean>().IsTrue)
           ?? throw new RuntimeStdException("An item matching the condition was not found");

    /// <returns>
    /// The first element of the given iterable object where the closure returns true,
    /// or nil if the Iterable is empty
    /// </returns>
    [ElkFunction("firstOfOrNil")]
    public static RuntimeObject FirstOfOrNil(IEnumerable<RuntimeObject> items,
        Func<RuntimeObject, RuntimeObject> closure)
        => items.FirstOrDefault(x => closure(x).As<RuntimeBoolean>().IsTrue)
           ?? RuntimeNil.Value;

    /// <param name="items"></param>
    /// <param name="closure"></param>
    /// <returns>A list of flattened values where the closure has been called on each value.</returns>
    /// <example>["abc", "def"] | flatMap => x: x  #=> ["a", "b", "c", "d", "e", "f"]</example>
    [ElkFunction("flatMap", Reachability.Everywhere)]
    public static RuntimeGenerator FlatMap(
        IEnumerable<RuntimeObject> items,
        Func<RuntimeObject, RuntimeObject> closure)
    {
        return new(
            items
                .Select(x =>
                    x as IEnumerable<RuntimeObject>
                        ?? throw new RuntimeCastException(x.GetType(), "Iterable")
                )
                .Select(x => x.Select(closure))
                .SelectMany(x => x)
        );
    }

    [ElkFunction("flatten")]
    public static RuntimeGenerator Flatten(IEnumerable<RuntimeObject> items)
        => new(
            items
                .Select(x =>
                    x as IEnumerable<RuntimeObject>
                        ?? throw new RuntimeCastException(x.GetType(), "Iterable")
                )
                .SelectMany(x => x)
        );

    /// <param name="items">The Iterable to look in</param>
    /// <param name="target">The value to search for</param>
    /// <param name="startIndex">Which index to start at</param>
    /// <returns>The index of the first item with the given value, or -1 if no match was found.</returns>
    [ElkFunction("indexOf")]
    public static RuntimeInteger IndexOf(
        IEnumerable<RuntimeObject> items,
        RuntimeObject target,
        RuntimeInteger? startIndex = null)
    {
        var i = (int?)startIndex?.Value ?? 0;
        foreach (var item in items.Skip(i))
        {
            if (item.Equals(target))
                return new RuntimeInteger(i);

            i++;
        }

        return new RuntimeInteger(-1);
    }

    /// <param name="items">The Iterable to look in</param>
    /// <param name="targets">The values to search for</param>
    /// <param name="startIndex">Which index to start at</param>
    /// <returns>
    /// The index of the first item that is equal to one of the given values, or -1 if no match was found.
    /// </returns>
    [ElkFunction("indexOfAny")]
    public static RuntimeInteger IndexOfAny(
        IEnumerable<RuntimeObject> items,
        IEnumerable<RuntimeObject> targets,
        RuntimeInteger? startIndex = null)
    {
        var i = (int?)startIndex?.Value ?? 0;
        foreach (var item in items.Skip(i))
        {
            if (targets.Any(x => item.Equals(x)))
                return new RuntimeInteger(i);

            i++;
        }

        return new RuntimeInteger(-1);
    }

    /// <returns>The intersect of the given Iterables.</returns>
    [ElkFunction("intersect")]
    public static RuntimeGenerator Intersect(IEnumerable<RuntimeObject> first, IEnumerable<RuntimeObject> second)
        => new(first.Intersect(second));

    /// <returns>The intersect of the given Iterables where the closure determines the keys.</returns>
    [ElkFunction("intersectBy")]
    public static RuntimeGenerator IntersectBy(
        IEnumerable<RuntimeObject> first,
        IEnumerable<RuntimeObject> second,
        Func<RuntimeObject, RuntimeObject> closure)
        => new(first.IntersectBy(second, closure));

    /// <param name="input"></param>
    /// <returns>The last element of the given indexable object.</returns>
    [ElkFunction("last")]
    public static RuntimeObject Last(RuntimeObject input)
    {
        if (input is not IIndexable<RuntimeObject> indexable)
            throw new RuntimeCastException(input.GetType(), "Indexable");

        return indexable[new RuntimeInteger(indexable.Count - 1)];
    }

    /// <param name="items">The Iterable to look in</param>
    /// <param name="target">The value to search for</param>
    /// <param name="startIndex">Which index to start at</param>
    /// <returns>The index of the last item with the given value, or -1 if no match was found.</returns>
    [ElkFunction("lastIndexOf")]
    public static RuntimeInteger LastIndexOf(
        IEnumerable<RuntimeObject> items,
        RuntimeObject target,
        RuntimeInteger? startIndex = null)
    {
        var maxIndex = items.Count() - 1;
        var i = (int?)startIndex?.Value ?? maxIndex;
        if (i > maxIndex)
            return new RuntimeInteger(-1);

        foreach (var item in items.Reverse().Skip(maxIndex - i))
        {
            if (item.Equals(target))
                return new RuntimeInteger(i);

            i--;
        }

        return new RuntimeInteger(-1);
    }

    /// <param name="items">The Iterable to look in</param>
    /// <param name="targets">The values to search for</param>
    /// <param name="startIndex">Which index to start at</param>
    /// <returns>
    /// The index of the last item that is equal to one of the values, or -1 if no match was found.
    /// </returns>
    [ElkFunction("lastIndexOfAny")]
    public static RuntimeInteger LastIndexOfAny(
        IEnumerable<RuntimeObject> items,
        IEnumerable<RuntimeObject> targets,
        RuntimeInteger? startIndex = null)
    {
        var maxIndex = items.Count() - 1;
        var i = (int?)startIndex?.Value ?? maxIndex;
        if (i > maxIndex)
            return new RuntimeInteger(-1);

        foreach (var item in items.Reverse().Skip(maxIndex - i))
        {
            if (targets.Any(x => item.Equals(x)))
                return new RuntimeInteger(i);

            i--;
        }

        return new RuntimeInteger(-1);
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
            RuntimeTable table => new(table.Rows.Count),
            RuntimePipe pipe => new(pipe.Count),
            RuntimeNil nil => throw new RuntimeCastException(nil.GetType(), "Iterable"),
            IEnumerable<RuntimeObject> enumerable => new(enumerable.Count()),
            _ => new(container.As<RuntimeString>().Value.Length),
        };

    /// <param name="items"></param>
    /// <param name="closure"></param>
    /// <returns>A list of values where the closure has been called on each value.</returns>
    /// <example>[1, 2, 3] | map => x: x + 1 #=> [2, 3, 4]</example>
    [ElkFunction("map", Reachability.Everywhere)]
    public static RuntimeGenerator Map(IEnumerable<RuntimeObject> items, Func<RuntimeObject, RuntimeObject> closure)
        => new(items.Select(closure));

    /// <returns>The lowest value in the Iterable.</returns>
    /// <example>[1, 2, 3] | max #=> [2, 3, 4]</example>
    [ElkFunction("max")]
    public static RuntimeObject Max(IEnumerable<RuntimeObject> items)
        => items.Max() ?? RuntimeNil.Value;

    /// <returns>The lowest value in the Iterable with the closure applied.</returns>
    [ElkFunction("maxOf")]
    public static RuntimeObject MaxOf(IEnumerable<RuntimeObject> items, Func<RuntimeObject, RuntimeObject> closure)
        => items.Max(closure) ?? RuntimeNil.Value;

    /// <returns>The lowest value in the Iterable.</returns>
    [ElkFunction("min")]
    public static RuntimeObject Min(IEnumerable<RuntimeObject> items)
        => items.Min() ?? RuntimeNil.Value;

    /// <returns>The lowest value in the Iterable with the closure applied.</returns>
    [ElkFunction("minOf")]
    public static RuntimeObject MinOf(IEnumerable<RuntimeObject> items, Func<RuntimeObject, RuntimeObject> closure)
        => items.Min(closure) ?? RuntimeNil.Value;

    /// <param name="items">A list of values that will be stringified</param>
    /// <param name="separator">Character sequence that should be put between each value</param>
    /// <returns>A new string of all the list values separated by the specified separator string.</returns>
    [ElkFunction("join", Reachability.Everywhere)]
    public static RuntimeString Join(IEnumerable<RuntimeObject> items, RuntimeString? separator = null)
        => new(string.Join(separator?.Value ?? "", items.Select(x => x.As<RuntimeString>())));

    /// <returns>The cartesian product of an Iterable.</returns>
    [ElkFunction("product")]
    public static RuntimeGenerator Product(IEnumerable<RuntimeObject> items, RuntimeInteger? repeat = null)
    {
        if (repeat == null)
        {
            var iterableItems = items.Select(x =>
                x as IEnumerable<RuntimeObject>
                    ?? throw new RuntimeCastException(x.GetType(), "Iterable")
            );

            return new(GetCartesianProduct(iterableItems));
        }

        var repeatedItems = Enumerable.Repeat(items, (int)repeat.Value);

        return new(GetCartesianProduct(repeatedItems));
    }

    private static IEnumerable<RuntimeObject> GetCartesianProduct(IEnumerable<IEnumerable<RuntimeObject>> iterables)
    {
        var result1 = new List<IEnumerable<RuntimeObject>> { new List<RuntimeObject>() };
        var result2 = new List<IEnumerable<RuntimeObject>>();
        foreach (var iterable in iterables)
        {
            foreach (var x in result1)
                result2.AddRange(iterable.Select(y => x.Append(y)));

            result1 = [..result2];
            result2 = [];
        }

        foreach (var r in result1)
            yield return new RuntimeGenerator(r);
    }

    /// <returns>A generator for the given item prepended to the given Iterable.</returns>
    [ElkFunction("prepend")]
    public static RuntimeGenerator Prepend(IEnumerable<RuntimeObject> items, RuntimeObject item)
        => new(items.Prepend(item));

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
            set.Entries.Add(value1);
        }
        else if (container is RuntimeDictionary dict)
        {
            if (value2 == null)
                throw new RuntimeWrongNumberOfArgumentsException("push", 3, 2);

            dict.Entries.Add(value1, value2);
        }
        else if (container is RuntimeTable table)
        {
            var row = value1 as RuntimeTableRow
                ?? new RuntimeTableRow(table, value1.As<RuntimeList>());
            table.Rows.Add(row);
        }
        else
        {
            throw new RuntimeException("Can only use function 'push' on mutable containers");
        }

        return container;
    }

    /// <summary>
    /// Adds all the given items to the container, one by one.
    /// </summary>
    /// <returns></returns>
    [ElkFunction("pushAll", Reachability.Everywhere)]
    public static RuntimeList PushAll(
        RuntimeList container,
        IEnumerable<RuntimeObject> values)
    {
        foreach (var value in values)
            container.Values.Add(value);

        return container;
    }

    /// <param name="input">An indexable object.</param>
    /// <param name="startIndex"></param>
    /// <param name="endIndex"></param>
    /// <returns>
    /// The elements between the specified indices.
    /// </returns>
    [ElkFunction("range")]
    public static RuntimeObject Range(IIndexable<RuntimeObject> input, RuntimeInteger startIndex, RuntimeInteger endIndex)
        => input[new RuntimeRange((int)startIndex.Value, (int)endIndex.Value)];

    [ElkFunction("reduce")]
    public static RuntimeObject Reduce(IEnumerable<RuntimeObject> items, Func<RuntimeObject, RuntimeObject, RuntimeObject> closure)
        => items.Aggregate(closure);

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
            if (index is RuntimeRange range)
            {
                list.Values.RemoveRange(range);

                return container;
            }

            list.Values.RemoveAt(index.As<RuntimeInteger>());
        }
        else if (container is RuntimeSet set)
        {
            set.Entries.Remove(index);
        }
        else if (container is RuntimeDictionary dict)
        {
            dict.Entries.Remove(index);
        }
        else if (container is RuntimeTable table)
        {
            if (index is RuntimeRange range)
            {
                table.Rows.RemoveRange(range);

                return container;
            }

            table.Rows.RemoveAt(index.As<RuntimeInteger>());
        }
        else
        {
            throw new RuntimeException("Can only use function 'remove' on mutable containers");
        }

        return container;
    }

    /// <param name="item">The object to repeat</param>
    /// <param name="n">The amount of times it should be repeated</param>
    /// <returns>A list containing n instances of the given item.</returns>
    [ElkFunction("repeat")]
    public static RuntimeGenerator Repeat(RuntimeObject item, RuntimeInteger? n = null)
        => new(
            n == null
                ? DataTypes.Extensions.RepeatIndefinitely(item)
                : Enumerable.Repeat(item, (int)n.Value)
        );

    [ElkFunction("reverse")]
    public static RuntimeGenerator Reverse(IEnumerable<RuntimeObject> items)
        => new(items.Reverse());

    [ElkFunction("reverseMut")]
    public static void Reverse(RuntimeList items)
    {
        items.Values.Reverse();
    }

    /// <param name="items">All items</param>
    /// <param name="count">The amount of items to skip from the left</param>
    /// <returns>A generator for the first n items.</returns>
    [ElkFunction("skip")]
    public static RuntimeGenerator Skip(IEnumerable<RuntimeObject> items, RuntimeInteger count)
        => new(items.Skip((int)count.Value).ToList());

    /// <param name="items">All items</param>
    /// <param name="closure"></param>
    /// <returns></returns>
    [ElkFunction("skipWhile")]
    public static RuntimeGenerator SkipWhile(IEnumerable<RuntimeObject> items, Func<RuntimeObject, RuntimeObject> closure)
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

    /// <param name="items">All items</param>
    /// <param name="count">The amount of items to take from the left</param>
    /// <returns>A generator for the specified amount of items.</returns>
    [ElkFunction("take")]
    public static RuntimeGenerator Take(IEnumerable<RuntimeObject> items, RuntimeInteger count)
        => new(items.Take((int)count.Value).ToList());

    /// <param name="items">All items</param>
    /// <param name="closure"></param>
    [ElkFunction("takeWhile")]
    public static RuntimeGenerator TakeWhile(IEnumerable<RuntimeObject> items, Func<RuntimeObject, RuntimeObject> closure)
        => new(items.TakeWhile(x => closure(x).As<RuntimeBoolean>().IsTrue).ToList());

    /// <param name="first">The first Iterable</param>
    /// <param name="second">The second Iterable</param>
    /// <returns>A generator for the items from the given Iterables combined, excluding duplicates.</returns>
    [ElkFunction("union")]
    public static RuntimeGenerator Union(IEnumerable<RuntimeObject> first, IEnumerable<RuntimeObject> second)
        => new(first.Union(second));

    /// <param name="first">The first Iterable</param>
    /// <param name="second">The second Iterable</param>
    /// <param name="closure"></param>
    /// <returns>
    /// A generator for the items from the given Iterables combined, excluding duplicates.
    /// The closure selects the key of each item.
    /// </returns>
    [ElkFunction("unionBy")]
    public static RuntimeGenerator UnionBy(IEnumerable<RuntimeObject> first, IEnumerable<RuntimeObject> second, Func<RuntimeObject, RuntimeObject> closure)
        => new(first.UnionBy(second, closure));

    /// <param name="values">The values to slide over</param>
    /// <param name="size">The size of the window</param>
    /// <returns>A generator for a sliding window over the given values.</returns>
    /// <example>
    /// [1, 2, 3, 4, 5] | iter::window(2) #=> [[1, 2], [2, 3], [3, 4], [4, 5], [5, nil]]
    /// </example>
    [ElkFunction("window")]
    public static RuntimeGenerator Window(IEnumerable<RuntimeObject> values, RuntimeInteger size)
        => new(Window(values, size.Value));

    private static IEnumerable<RuntimeObject> Window(IEnumerable<RuntimeObject> values, long size)
    {
        var buffer = new Queue<RuntimeObject>();
        foreach (var value in values)
        {
            buffer.Enqueue(value);
            if (buffer.Count == size)
            {
                yield return new RuntimeList(buffer.ToList());
                buffer.Dequeue();
            }
        }

        if (buffer.Count == size)
        {
            yield return new RuntimeList(buffer.ToList());
            buffer.Clear();
        }
    }

    /// <param name="values" types="Iterable"></param>
    /// <returns>
    /// A list containing a tuple for each item in the original container.
    /// The first item of the tuple is the item from the original container,
    /// while the second item is the index of that item.
    /// </returns>
    /// <example>for item, i in values: echo("{i}: {item}")</example>
    [ElkFunction("withIndex", Reachability.Everywhere)]
    public static RuntimeGenerator WithIndex(RuntimeObject values)
    {
        var items = values as IEnumerable<RuntimeObject> ?? values.As<RuntimeList>().Values;

        return new(items.Select((x, i) => new RuntimeTuple([x, new RuntimeInteger(i)])));
    }

    /// <param name="items"></param>
    /// <param name="closure"></param>
    /// <returns>A list of values containing only those who evaluated to true in the closure.</returns>
    /// <example>[1, 2, 3] | select => x: x + 1 #=> [2, 3, 4]</example>
    [ElkFunction("where", Reachability.Everywhere)]
    public static RuntimeGenerator Where(IEnumerable<RuntimeObject> items, Func<RuntimeObject, RuntimeObject> closure)
        => new(items.Where(x => closure(x).As<RuntimeBoolean>().IsTrue));

    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns>A list containing pairs of values.</returns>
    /// <example>[1, 2, 3] | iter::zip([4, 5, 6]) #=> [(1, 4), (2, 5), (3, 6)]</example>
    [ElkFunction("zip")]
    public static RuntimeGenerator Zip(IEnumerable<RuntimeObject> a, IEnumerable<RuntimeObject> b)
        => new(a.Zip(b).Select(x => new RuntimeTuple([x.First, x.Second])));

    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns>A list containing pairs of values.</returns>
    /// <example>[1, 2, 3, 4] | iter::zipLongest([4, 5, 6]) #=> [(1, 4), (2, 5), (3, 6), (4, 0)]</example>
    [ElkFunction("zipLongest")]
    public static RuntimeGenerator ZipLongest(IEnumerable<RuntimeObject> a, IEnumerable<RuntimeObject> b)
    {
        var result = a
            .ZipLongest(b)
            .Select(x =>
                new RuntimeTuple([
                    x.Item1 ?? RuntimeNil.Value,
                    x.Item2 ?? RuntimeNil.Value
                ])
        );

        return new RuntimeGenerator(result);
    }
}