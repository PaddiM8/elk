using System;
using System.Linq;
using System.Text.RegularExpressions;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;

namespace Elk.Std;

[ElkModule("re")]
public class Regex
{
    /// <param name="pattern"></param>
    /// <returns>A new Regex where dots match newlines.</returns>
    [ElkFunction("dotAll")]
    public static RuntimeRegex DotAll(RuntimeRegex pattern)
        => new(
            new System.Text.RegularExpressions.Regex(
                pattern.Value.ToString(),
                pattern.Value.Options | RegexOptions.Singleline
            )
        );

    /// <param name="pattern"></param>
    /// <param name="value"></param>
    /// <returns>The values of the groups of the first match.</returns>
    [ElkFunction("groups")]
    public static RuntimeList Groups(RuntimeString value, RuntimeRegex pattern)
    {
        var result = pattern.Value
            .Matches(value.Value)
            .FirstOrDefault()?
            .Groups
            .Values
            .Select<Group, RuntimeObject>(x => new RuntimeString(x.Value));

        return result == null
            ? new RuntimeList([])
            : new RuntimeList(result.ToList());
    }

    /// <param name="pattern"></param>
    /// <param name="value"></param>
    /// <returns>Whether a match of the pattern is found anywhere in the given value.</returns>
    [ElkFunction("isMatch")]
    public static RuntimeBoolean IsMatch(RuntimeString value, RuntimeRegex pattern)
        => RuntimeBoolean.From(pattern.Value.IsMatch(value.Value));

    /// <param name="pattern"></param>
    /// <param name="value"></param>
    /// <returns>The first occurrence of the given pattern in the given value.</returns>
    [ElkFunction("find")]
    public static RuntimeString Find(RuntimeString value, RuntimeRegex pattern)
        => new(pattern.Value.Match(value.Value).Value);

    /// <param name="pattern"></param>
    /// <param name="value"></param>
    /// <returns>The first occurrence of the given pattern in the given value.</returns>
    [ElkFunction("findLast")]
    public static RuntimeString FindLast(RuntimeString value, RuntimeRegex pattern)
        => new(
            System.Text.RegularExpressions.Regex.Match(
                value.Value,
                pattern.ToString(),
                RegexOptions.RightToLeft
            ).Value
        );

    /// <param name="pattern"></param>
    /// <param name="value"></param>
    /// <returns>All the occurrences of the given pattern in the given value.</returns>
    [ElkFunction("findAll")]
    public static RuntimeGenerator FindAll(RuntimeString value, RuntimeRegex pattern)
        => new(pattern.Value.Matches(value.Value).Select(x => new RuntimeString(x.Value)));

    /// <param name="value">The entire string</param>
    /// <param name="replacement">The replacement</param>
    /// <param name="pattern">The pattern to replace</param>
    [ElkFunction("replace")]
    public static RuntimeString Replace(RuntimeString value, RuntimeString replacement, RuntimeRegex pattern)
        => new(pattern.Value.Replace(value.Value, replacement.Value));

    /// <param name="value">The entire string</param>
    /// <param name="pattern">The pattern to replace</param>
    /// <param name="closure"></param>
    [ElkFunction("replaceWithClosure")]
    public static RuntimeString ReplaceWithClosure(
        RuntimeString value,
        RuntimeRegex pattern,
        Func<RuntimeObject, RuntimeObject> closure)
    {
        var result = pattern.Value.Replace(
            value.Value,
            match =>
            {
                var runtimeMatch = new RuntimeDictionary
                {
                    ["value"] = new RuntimeString(match.Value),
                    ["groups"] = new RuntimeList(
                        match.Groups.Values
                            .Select<Group, RuntimeObject>(x => new RuntimeString(x.Value))
                            .ToList()
                    )
                };

                return closure.Invoke(runtimeMatch).As<RuntimeString>().Value;
            });

        return new RuntimeString(result);
    }
}