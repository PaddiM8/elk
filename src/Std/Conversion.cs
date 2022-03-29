using Shel.Interpreting;

namespace Shel.Std;

static class Conversion
{
    [ShellFunction("string")]
    public static RuntimeString ToString(IRuntimeValue value)
        => value.As<RuntimeString>();
}