using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Elk.Std.DataTypes;

namespace Elk.Std.Serialization.CommandLine;

public class CliResult(Dictionary<string, object?> values, IEnumerable<int> argumentIndices)
    : IEnumerable<KeyValuePair<string, object?>>
{
    public IEnumerable<int> ArgumentIndices { get; } = argumentIndices;

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
        => values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public RuntimeDictionary ToRuntimeDictionary()
    {
        var runtimeValues = values.Select(pair =>
        {
            var key = new RuntimeString(pair.Key);
            RuntimeObject value = pair.Value switch
            {
                IEnumerable<string> list => new RuntimeGenerator(
                    list.Select(x => new RuntimeString(x))
                ),
                null => RuntimeNil.Value,
                _ => new RuntimeString(pair.Value.ToString() ?? ""),
            };

            return new KeyValuePair<int, (RuntimeObject, RuntimeObject)>(
                key.GetHashCode(),
                (key, value)
            );
        });

        return new RuntimeDictionary(
            new Dictionary<int, (RuntimeObject, RuntimeObject)>(runtimeValues)
        );
    }

    public bool Contains(string identifier)
        => values.ContainsKey(identifier);

    public string? GetString(string identifier)
    {
        values.TryGetValue(identifier, out var result);

        return result as string;
    }

    public string GetRequiredString(string identifier)
    {
        values.TryGetValue(identifier, out var result);

        return (string)result!;
    }

    public IEnumerable<string>? GetList(string identifier)
    {
        values.TryGetValue(identifier, out var result);

        return result as List<string>;
    }
}