using Shel.Attributes;
using Shel.Interpreting;

namespace Shel.Std;

static class Conversion
{
    [ShellFunction("bool")]
    public static RuntimeBoolean ToBool(IRuntimeValue value)
        => value.As<RuntimeBoolean>();

    [ShellFunction("int")]
    public static RuntimeInteger ToInt(IRuntimeValue value)
        => value.As<RuntimeInteger>();

    [ShellFunction("float")]
    public static RuntimeFloat ToFloat(IRuntimeValue value)
        => value.As<RuntimeFloat>();

    [ShellFunction("list")]
    public static RuntimeList ToList(IRuntimeValue value)
        => value.As<RuntimeList>();

    [ShellFunction("string")]
    public static RuntimeString ToString(IRuntimeValue value)
        => value.As<RuntimeString>();
}