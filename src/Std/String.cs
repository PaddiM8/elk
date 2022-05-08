using System;
using System.Linq;
using Elk.Attributes;
using Elk.Interpreting;
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

namespace Elk.Std;

static class String
{
    [ShellFunction("endsWith")]
    public static RuntimeBoolean EndsWith(RuntimeString input, RuntimeString ending)
        => RuntimeBoolean.From(input.Value.EndsWith(ending.Value));

    [ShellFunction("lines")]
    public static RuntimeList Lines(RuntimeString input)
    {
        var lines = input.Value.Split(System.Environment.NewLine).ToList();
        if (lines.LastOrDefault() == "")
            lines.RemoveAt(lines.Count - 1);

        return new(lines.Select(x => new RuntimeString(x)));
    }

    [ShellFunction("lower")]
    public static RuntimeString Lower(RuntimeString input)
        => new(input.Value.ToLower());

    [ShellFunction("startsWith")]
    public static RuntimeBoolean StartsWith(RuntimeString input, RuntimeString start)
        => RuntimeBoolean.From(input.Value.StartsWith(start.Value));

    [ShellFunction("trim")]
    public static RuntimeString Trim(RuntimeString input)
        => new(input.Value.Trim());

    [ShellFunction("split")]
    public static RuntimeList Split(RuntimeString input, RuntimeString? delimiter = null)
        => delimiter == null
            ? new(input.Select(x => x))
            : new(input.Value.Split(delimiter.Value).Select(x => new RuntimeString(x)));

    [ShellFunction("upper")]
    public static RuntimeString Upper(RuntimeString input)
        => new(input.Value.ToUpper());
}