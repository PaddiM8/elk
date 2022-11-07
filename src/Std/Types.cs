#region

using System;
using System.Collections.Generic;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;

#endregion

namespace Elk.Std;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

[ElkModule("types")]
public class Types
{
    /// <returns>Whether or not the given value is of a specific type.</returns>
    [ElkFunction("isType", Reachability.Everywhere)]
    public static RuntimeBoolean IsType(RuntimeObject value, RuntimeType type)
        => RuntimeBoolean.From(value.GetType() == type.Type);

    /// <summary>
    /// Helper function used to create independent closures.
    /// </summary>
    /// <param name="args">Closure arguments</param>
    /// <param name="closure"></param>
    /// <returns>The result of the closure.</returns>
    [ElkFunction("Fn", Reachability.Everywhere)]
    public static RuntimeObject Fn(
        [ElkVariadic] IEnumerable<RuntimeObject> args,
        Func<IEnumerable<RuntimeObject>, RuntimeObject> closure)
    {
        return closure(args);
    }
}