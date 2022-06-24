#region

using Elk.Std.Attributes;
using Elk.Std.DataTypes;

#endregion

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

namespace Elk.Std;

static class Conversion
{
    [ElkFunction("bool", Reachability.Everywhere)]
    public static RuntimeBoolean ToBool(IRuntimeValue value)
        => value.As<RuntimeBoolean>();

    [ElkFunction("error", Reachability.Everywhere)]
    public static RuntimeError ToError(RuntimeString message)
        => new(message.Value);

    [ElkFunction("float", Reachability.Everywhere)]
    public static RuntimeFloat ToFloat(IRuntimeValue value)
        => value.As<RuntimeFloat>();

    [ElkFunction("int", Reachability.Everywhere)]
    public static RuntimeInteger ToInt(IRuntimeValue value)
        => value.As<RuntimeInteger>();

    [ElkFunction("list", Reachability.Everywhere)]
    public static RuntimeList ToList(IRuntimeValue value)
        => value.As<RuntimeList>();

    [ElkFunction("str", Reachability.Everywhere)]
    public static RuntimeString ToString(IRuntimeValue value)
        => value.As<RuntimeString>();

    [ElkFunction("type", Reachability.Everywhere)]
    public static RuntimeType ToType(IRuntimeValue value)
        => new(value.GetType());

    [ElkFunction("message", Reachability.Everywhere)]
    public static RuntimeString Message(RuntimeError err)
        => new(err.Value);
}