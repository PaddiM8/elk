using System.Linq;
using Shel.Attributes;
using Shel.Interpreting;

namespace Shel.Std;

static class Iteration
{
    [ShellFunction("all")]
    public static RuntimeBoolean All(RuntimeList x)
        => RuntimeBoolean.From(x.Values.All(x => x.As<RuntimeBoolean>().Value));

    [ShellFunction("any")]
    public static RuntimeBoolean Any(RuntimeList x)
        => RuntimeBoolean.From(x.Values.Any(x => x.As<RuntimeBoolean>().Value));

    [ShellFunction("join")]
    public static RuntimeString Join(RuntimeList x, RuntimeString? separator = null)
        => new(string.Join(separator?.Value ?? "", x.Values.Select(x => x.As<RuntimeString>())));
}