using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Elk.Std.DataTypes.Serialization.CommandLine;

public class CliResult : IEnumerable<KeyValuePair<string, object?>>
{
    public IEnumerable<int> ArgumentIndices { get; }

    private readonly Dictionary<string, object?> _values;

    public CliResult(Dictionary<string, object?> values, IEnumerable<int> argumentIndices)
    {
        ArgumentIndices = argumentIndices;
        _values = values;
    }

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
        => _values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public RuntimeDictionary ToRuntimeDictionary()
    {
        var runtimeValues = _values.Select(pair =>
        {
            var key = new RuntimeString(pair.Key);
            RuntimeObject value = pair.Value switch
            {
                IEnumerable<string> list => new RuntimeList(
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
        => _values.ContainsKey(identifier);

    public string? GetString(string identifier)
    {
        _values.TryGetValue(identifier, out var result);

        return result as string;
    }

    public string GetRequiredString(string identifier)
    {
        _values.TryGetValue(identifier, out var result);

        return (string)result!;
    }

    public List<string>? GetList(string identifier)
    {
        _values.TryGetValue(identifier, out var result);

        return result as List<string>;
    }
}