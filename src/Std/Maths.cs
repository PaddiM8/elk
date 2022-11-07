#region

using System;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;

#endregion

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

namespace Elk.Std;

[ElkModule("math")]
static class Maths
{
    /// <param name="x" types="Integer, Float"></param>
    /// <returns>The input number made positive.</returns>
    [ElkFunction("abs", Reachability.Everywhere)]
    public static RuntimeObject Abs(RuntimeObject x)
        => x switch
        {
            RuntimeInteger integer => new RuntimeInteger(Math.Abs(integer.Value)),
            _ => new RuntimeFloat(Math.Abs(x.As<RuntimeFloat>().Value)),
        };

    /// <param name="x" types="Integer, Float"></param>
    /// <returns>The input number rounded up.</returns>
    [ElkFunction("ceil", Reachability.Everywhere)]
    public static RuntimeFloat Ceil(RuntimeObject x)
        => new(Math.Ceiling(x.As<RuntimeFloat>().Value));

    /// <param name="x" types="Integer, Float"></param>
    /// <returns>The input number rounded down.</returns>
    [ElkFunction("floor", Reachability.Everywhere)]
    public static RuntimeFloat Floor(RuntimeObject x)
        => new(Math.Floor(x.As<RuntimeFloat>().Value));

    /// <param name="x" types="Integer, Float"></param>
    /// <returns>The square root of the input number</returns>
    [ElkFunction("sqrt", Reachability.Everywhere)]
    public static RuntimeFloat Sqrt(RuntimeObject x)
        => x is RuntimeInteger integer
            ? new(Math.Sqrt(integer.Value))
            : new(Math.Sqrt(x.As<RuntimeFloat>().Value));

    /// <param name="x" types="Integer, Float"></param>
    /// <param name="y" types="Integer, Float"></param>
    /// <returns>The highest of the two input numbers.</returns>
    [ElkFunction("max", Reachability.Everywhere)]
    public static RuntimeObject Max(RuntimeObject x, RuntimeObject y)
    {
        if (x is RuntimeInteger xInt && y is RuntimeInteger yInt)
            return new RuntimeInteger(Math.Max(xInt.Value, yInt.Value));
        
        return new RuntimeFloat(Math.Max(x.As<RuntimeFloat>().Value, y.As<RuntimeFloat>().Value));
    }

    /// <param name="x" types="Integer, Float"></param>
    /// <param name="y" types="Integer, Float"></param>
    /// <returns>The lowest of the two input numbers.</returns>
    [ElkFunction("min", Reachability.Everywhere)]
    public static RuntimeObject Min(RuntimeObject x, RuntimeObject y)
    {
        if (x is RuntimeInteger xInt && y is RuntimeInteger yInt)
            return new RuntimeInteger(Math.Min(xInt.Value, yInt.Value));
        
        return new RuntimeFloat(Math.Min(x.As<RuntimeFloat>().Value, y.As<RuntimeFloat>().Value));
    }
}