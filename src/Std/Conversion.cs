using System.Linq;
using Elk.Attributes;
using Elk.Interpreting;
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

namespace Elk.Std;

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

    [ShellFunction("str")]
    public static RuntimeString ToString(IRuntimeValue value)
        => value.As<RuntimeString>();

    [ShellFunction("isDigit")]
    public static RuntimeBoolean IsDigit(RuntimeString str)
        => RuntimeBoolean.From(str.Value.Length == 1 && char.IsDigit(str.Value[0]));
}