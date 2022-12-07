using System.Collections.Generic;
using System.Linq;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;

namespace Elk.Std;

[ElkModule("regex")]
public class Regex
{
    /// <param name="pattern"></param>
    /// <param name="value"></param>
    /// <returns>The values of the groups of the first match.</returns>
    [ElkFunction("groups")]
    public static RuntimeList Groups(RuntimeRegex pattern, RuntimeString value)
    {
        var result = pattern.Value
            .Matches(value.Value)
            .FirstOrDefault()?
            .Groups
            .Values
            .Select(x => new RuntimeString(x.Value));

        return result == null
            ? new RuntimeList(new List<RuntimeObject>())
            : new RuntimeList(result);
    }

    /// <param name="pattern"></param>
    /// <param name="value"></param>
    /// <returns>Whether a match of the pattern is found anywhere in the given value.</returns>
    [ElkFunction("isMatch")]
    public static RuntimeBoolean IsMatch(RuntimeRegex pattern, RuntimeString value)
        => RuntimeBoolean.From(pattern.Value.IsMatch(value.Value));

    /// <param name="pattern"></param>
    /// <param name="value"></param>
    /// <returns>The first occurrence of the given pattern in the given value.</returns>
    [ElkFunction("find")]
    public static RuntimeString Find(RuntimeRegex pattern, RuntimeString value)
        => new(pattern.Value.Match(value.Value).Value);

    /// <param name="pattern"></param>
    /// <param name="value"></param>
    /// <returns>All the occurrences of the given pattern in the given value.</returns>
    [ElkFunction("findAll")]
    public static RuntimeList FindAll(RuntimeRegex pattern, RuntimeString value)
        => new(pattern.Value.Matches(value.Value).Select(x => new RuntimeString(x.Value)));
}