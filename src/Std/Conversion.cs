#region

using Elk.Std.Attributes;
using Elk.Std.DataTypes;

#endregion

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

namespace Elk.Std;

static class Conversion
{
    /// <param name="value">Value that should be cast</param>
    [ElkFunction("bool", Reachability.Everywhere)]
    public static RuntimeBoolean ToBool(IRuntimeValue value)
        => value.As<RuntimeBoolean>();

    /// <param name="message">Error message</param>
    /// <returns>An Error with the provided message.</returns>
    [ElkFunction("error", Reachability.Everywhere)]
    public static RuntimeError ToError(RuntimeString message)
        => new(message.Value);

    /// <param name="value">Value that should be cast</param>
    [ElkFunction("float", Reachability.Everywhere)]
    public static RuntimeFloat ToFloat(IRuntimeValue value)
        => value.As<RuntimeFloat>();

    /// <param name="value">Value that should be cast</param>
    [ElkFunction("int", Reachability.Everywhere)]
    public static RuntimeInteger ToInt(IRuntimeValue value)
        => value.As<RuntimeInteger>();

    /// <param name="value">Value that should be cast</param>
    [ElkFunction("list", Reachability.Everywhere)]
    public static RuntimeList ToList(IRuntimeValue value)
        => value.As<RuntimeList>();

    /// <param name="value">Value that should be cast</param>
    [ElkFunction("str", Reachability.Everywhere)]
    public static RuntimeString ToString(IRuntimeValue value)
        => value.As<RuntimeString>();

    /// <param name="value">Value that should be cast</param>
    [ElkFunction("type", Reachability.Everywhere)]
    public static RuntimeType ToType(IRuntimeValue value)
        => new(value.GetType());

    /// <returns>The message stored in the given error.</returns>
    [ElkFunction("message", Reachability.Everywhere)]
    public static RuntimeString Message(RuntimeError err)
        => new(err.Value);
}