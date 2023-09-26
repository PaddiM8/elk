using System.Collections.Generic;

namespace Elk.Std.DataTypes.Serialization.CommandLine;

public class CommandLineResult
{
    private readonly Dictionary<string, object?> _values;

    public CommandLineResult(Dictionary<string, object?> values)
    {
        _values = values;
    }

    public bool Contains(string identifier)
        => _values.ContainsKey(identifier);

    public string? GetString(string identifier)
    {
        _values.TryGetValue(identifier, out var result);

        return result as string;
    }

    public List<string>? GetList(string identifier)
    {
        _values.TryGetValue(identifier, out var result);

        return result as List<string>;
    }
}