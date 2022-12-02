#region

using System.Collections.Generic;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;

#endregion

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

namespace Elk.Std;

[ElkModule("conversion")]
static class Conversion
{
    /// <param name="value">Value that should be cast</param>
    [ElkFunction("bool", Reachability.Everywhere)]
    public static RuntimeBoolean ToBool(RuntimeObject value)
        => value.As<RuntimeBoolean>();

    /// <param name="value">Value that should be cast</param>
    [ElkFunction("dict", Reachability.Everywhere)]
    public static RuntimeDictionary ToDictionary(RuntimeObject? value = null)
        => value?.As<RuntimeDictionary>() ?? new RuntimeDictionary(new());

    /// <param name="value">Value that should be cast</param>
    [ElkFunction("float", Reachability.Everywhere)]
    public static RuntimeFloat ToFloat(RuntimeObject value)
        => value.As<RuntimeFloat>();

    /// <param name="value">Value that should be cast</param>
    [ElkFunction("int", Reachability.Everywhere)]
    public static RuntimeInteger ToInt(RuntimeObject value)
        => value.As<RuntimeInteger>();

    /// <param name="value">Value that should be cast</param>
    [ElkFunction("list", Reachability.Everywhere)]
    public static RuntimeList ToList(RuntimeObject? value = null)
        => value?.As<RuntimeList>() ?? new RuntimeList(new List<RuntimeObject>());

    /// <param name="value">Value that should be cast</param>
    [ElkFunction("regex", Reachability.Everywhere)]
    public static RuntimeRegex ToRegex(RuntimeObject value)
        => value.As<RuntimeRegex>();

    /// <param name="value">Value that should be cast</param>
    [ElkFunction("set", Reachability.Everywhere)]
    public static RuntimeSet ToSet(RuntimeObject? value = null)
        => value?.As<RuntimeSet>() ?? new RuntimeSet(new());

    /// <param name="value">Value that should be cast</param>
    [ElkFunction("str", Reachability.Everywhere)]
    public static RuntimeString ToString(RuntimeObject value)
        => value.As<RuntimeString>();

    /// <param name="value">Value that should be cast</param>
    [ElkFunction("type", Reachability.Everywhere)]
    public static RuntimeType ToType(RuntimeObject value)
        => new(value.GetType());

    /// <returns>The message stored in the given error.</returns>
    [ElkFunction("message", Reachability.Everywhere)]
    public static RuntimeString Message(RuntimeError err)
        => new(err.Value);
}