using System.Collections.Generic;
using System.Linq;
using Elk.Interpreting.Exceptions;
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

            return (
                key: key.GetHashCode(),
                value: (key, value)
            );
        });

        return new(keyValuePairs.ToDictionary(x => x.key, x => x.value));
    }

    /// <returns>The keys in the given dictionary.</returns>
    [ElkFunction("keys")]
    public static RuntimeGenerator Keys(RuntimeDictionary dictionary)
        => new(dictionary.Entries.Values.Select(x => x.Item1));

    /// <returns>The values in the given dictionary.</returns>
    [ElkFunction("values")]
    public static RuntimeGenerator Values(RuntimeDictionary dictionary)
        => new(dictionary.Entries.Values.Select(x => x.Item2));
}