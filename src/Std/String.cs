using System;
using System.Linq;
using Elk.Attributes;
using Elk.Interpreting;
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

namespace Elk.Std;

[ElkModule("str")]
static class String
{
    [ElkFunction("endsWith")]
    public static RuntimeBoolean EndsWith(RuntimeString input, RuntimeString ending)
        => RuntimeBoolean.From(input.Value.EndsWith(ending.Value));

    [ElkFunction("isDigit")]
    public static RuntimeBoolean IsDigit(RuntimeString str)
        => RuntimeBoolean.From(str.Value.Length == 1 && char.IsDigit(str.Value[0]));

    [ElkFunction("lines", Reachability.Everywhere)]
    public static RuntimeList Lines(RuntimeString input)
    {
        var lines = input.Value.Split(System.Environment.NewLine).ToList();
        if (lines.LastOrDefault() == "")
            lines.RemoveAt(lines.Count - 1);

        return new(lines.Select(x => new RuntimeString(x)));
    }

    [ElkFunction("lower")]
    public static RuntimeString Lower(RuntimeString input)
        => new(input.Value.ToLower());

    [ElkFunction("startsWith")]
    public static RuntimeBoolean StartsWith(RuntimeString input, RuntimeString start)
        => RuntimeBoolean.From(input.Value.StartsWith(start.Value));

    [ElkFunction("trim")]
    public static RuntimeString Trim(RuntimeString input)
        => new(input.Value.Trim());

    [ElkFunction("split", Reachability.Everywhere)]
    public static RuntimeList Split(RuntimeString input, RuntimeString? delimiter = null)
        => delimiter == null
            ? new(input.Select(x => x))
            : new(input.Value.Split(delimiter.Value).Select(x => new RuntimeString(x)));

    [ElkFunction("upper")]
    public static RuntimeString Upper(RuntimeString input)
        => new(input.Value.ToUpper());
}