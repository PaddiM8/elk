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
        => new(input.Value.Split('\n').Select(x => new RuntimeString(x)));

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
    public static RuntimeList Split(RuntimeString input, RuntimeString delimiter)
        => new(input.Value.Split(delimiter.Value).Select(x => new RuntimeString(x)));

    [ShellFunction("upper")]
    public static RuntimeString Upper(RuntimeString input)
        => new(input.Value.ToUpper());
}