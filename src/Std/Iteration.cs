using System.Collections.Generic;
using System.Linq;
using Elk.Attributes;
using Elk.Interpreting;
using Elk.Interpreting.Exceptions;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

namespace Elk.Std;

static class Iteration
{
    [ShellFunction("add")]
    public static IRuntimeValue Add(IRuntimeValue x, IRuntimeValue value1, IRuntimeValue? value2 = null)
    {
        if (x is RuntimeList list)
        {
            list.Values.Add(value1);
        }
        else if (x is RuntimeDictionary dict)
        {
            if (value2 == null)
                throw new RuntimeWrongNumberOfArgumentsException(3, 2);

            dict.Entries.Add(value1.GetHashCode(), (value1, value2));
        }
        else
        {
            throw new RuntimeException("Can only use function 'add' on lists and dictionaries");
        }

        return x;
    }

    [ShellFunction("all")]
    public static RuntimeBoolean All(RuntimeList list)
        => RuntimeBoolean.From(list.Values.All(x => x.As<RuntimeBoolean>().Value));

    [ShellFunction("any")]
    public static RuntimeBoolean Any(RuntimeList list)
        => RuntimeBoolean.From(list.Values.Any(x => x.As<RuntimeBoolean>().Value));

    [ShellFunction("insert")]
    public static RuntimeList Insert(RuntimeList list, RuntimeInteger index, IRuntimeValue value)
    {
        list.Values.Insert(index.Value, value);

        return list;
    }

    [ShellFunction("join")]
    public static RuntimeString Join(RuntimeList list, RuntimeString? separator = null)
        => new(string.Join(separator?.Value ?? "", list.Values.Select(x => x.As<RuntimeString>())));

    [ShellFunction("len")]
    public static RuntimeInteger Length(IRuntimeValue x)
        => x switch
        {
            RuntimeTuple tuple => new(tuple.Values.Count),
            RuntimeList list => new(list.Values.Count),
            RuntimeDictionary dict => new(dict.Entries.Count),
            _ => new(x.As<RuntimeString>().Value.Length),
        };

    [ShellFunction("remove")]
    public static IRuntimeValue Remove(IRuntimeValue x, IRuntimeValue index)
    {
        if (x is RuntimeList list)
        {
            list.Values.RemoveAt(index.As<RuntimeInteger>().Value);
        }
        else if (x is RuntimeDictionary dict)
        {
            dict.Entries.Remove(index.GetHashCode());
        }
        else
        {
            throw new RuntimeException("Can only use function 'remove' on lists and dictionaries");
        }

        return x;
    }

    [ShellFunction("stepBy")]
    public static RuntimeRange StepBy(RuntimeRange x, RuntimeInteger step)
    {
        x.Increment = step.Value;

        return x;
    }

    [ShellFunction("withIndex")]
    public static RuntimeList WithIndex(IRuntimeValue values)
    {
        var items = values is IEnumerable<IRuntimeValue> enumerable
            ? enumerable
            : values.As<RuntimeList>().Values;

        return new(items.Select((x, i) => new RuntimeTuple(new[] { x, new RuntimeInteger(i) })));
    }
}