using System;
using Shel.Attributes;
using Shel.Interpreting;

namespace Shel.Std;

static class String
{
    [ShellFunction("startsWith")]
    public static RuntimeBoolean StartsWith(RuntimeString input, RuntimeString start)
        => RuntimeBoolean.From(input.Value.StartsWith(start.Value));

    [ShellFunction("endsWith")]
    public static RuntimeBoolean EndsWith(RuntimeString input, RuntimeString ending)
        => RuntimeBoolean.From(input.Value.EndsWith(ending.Value));

    [ShellFunction("trim")]
    public static RuntimeString Trim(RuntimeString input)
        => new(input.Value.Trim());
}