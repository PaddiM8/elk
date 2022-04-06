using System.Linq;
using Shel.Attributes;
using Shel.Interpreting;

namespace Shel.Std;

static class String
{
    [ShellFunction("endsWith")]
    public static RuntimeBoolean EndsWith(RuntimeString input, RuntimeString ending)
        => RuntimeBoolean.From(input.Value.EndsWith(ending.Value));

    [ShellFunction("lines")]
    public static RuntimeList Lines(RuntimeString input)
        => new(input.Value.Split('\n').Select(x => new RuntimeString(x)));

    [ShellFunction("startsWith")]
    public static RuntimeBoolean StartsWith(RuntimeString input, RuntimeString start)
        => RuntimeBoolean.From(input.Value.StartsWith(start.Value));

    [ShellFunction("trim")]
    public static RuntimeString Trim(RuntimeString input)
        => new(input.Value.Trim());
}