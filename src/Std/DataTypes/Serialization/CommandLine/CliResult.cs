using System.Collections;
using System.Collections.Generic;

namespace Elk.Std.DataTypes.Serialization.CommandLine;

public class CliResult : IEnumerable<KeyValuePair<string, object?>>
{
    private readonly Dictionary<string, object?> _values;

    public CliResult(Dictionary<string, object?> values)
    {
        _values = values;
    }

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
        => _values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

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