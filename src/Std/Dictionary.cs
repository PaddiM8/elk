using System.Collections.Generic;
using System.Linq;
using Elk.Exceptions;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;

namespace Elk.Std;

[ElkModule("dict")]
public static class Dictionary
{
    /// <param name="values">An Iterable of key-value-pairs</param>
    /// <returns>A new Dictionary based on the given values.</returns>
    /// <example>
    /// [["a", 1], ["b", 2]] | dict::create
    /// #=>
    /// # {
    /// #   "a": 1,
    /// #   "b": 2,
    /// # }
    /// </example>
    [ElkFunction("create")]
    public static RuntimeDictionary Create(IEnumerable<RuntimeObject> values)
    {
        var keyValuePairs = values.Select(x =>
        {
            if (x is not IEnumerable<RuntimeObject> keyValuePair)
                throw new RuntimeCastException(x.GetType(), "Iterable");

            var key = keyValuePair.FirstOrDefault() ?? RuntimeNil.Value;
            var value = keyValuePair.ElementAtOrDefault(1) ?? RuntimeNil.Value;

            return (key, value);
        });

        return new RuntimeDictionary(keyValuePairs.ToDictionary(x => x.key, x => x.value));
    }

    /// <param name="values">An Iterable of key-value-pairs</param>
    /// <returns>A new Dictionary based on the given values, where duplicate keys are merged and their values are combined into a list.</returns>
    /// <example>
    /// [["a", 1], ["a", 2], ["b", 3]] | dict::create
    /// #=>
    /// # {
    /// #   "a": [1, 2],
    /// #   "b": [3],
    /// # }
    /// </example>
    [ElkFunction("createLookup")]
    public static RuntimeDictionary CreateLookup(IEnumerable<RuntimeObject> values)
    {
        var entries = new Dictionary<RuntimeObject, RuntimeObject>();
        foreach (var givenValue in values)
        {
            if (givenValue is not IEnumerable<RuntimeObject> keyValuePair)
                throw new RuntimeCastException(givenValue.GetType(), "Iterable");

            var key = keyValuePair.FirstOrDefault() ?? RuntimeNil.Value;
            var value = keyValuePair.ElementAtOrDefault(1) ?? RuntimeNil.Value;
            if (entries.TryGetValue(key, out var existing))
            {
                ((RuntimeList)existing).Values.Add(value);
            }
            else
            {
                entries[key] = new RuntimeList([value]);
            }
        }

        return new RuntimeDictionary(entries);
    }

    /// <returns>The keys in the given dictionary.</returns>
    [ElkFunction("keys")]
    public static RuntimeGenerator Keys(RuntimeDictionary dictionary)
        => new(dictionary.Entries.Keys);

    /// <returns>The values in the given dictionary.</returns>
    [ElkFunction("values")]
    public static RuntimeGenerator Values(RuntimeDictionary dictionary)
        => new(dictionary.Entries.Values);
}