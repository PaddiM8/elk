using System;
using Elk.Attributes;
using Elk.Interpreting;
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

namespace Elk.Std;

[ElkModule("math")]
static class Maths
{
    [ElkFunction("abs", Reachability.Everywhere)]
    public static IRuntimeValue Abs(IRuntimeValue x)
        => x switch
        {
            RuntimeInteger integer => new RuntimeInteger(Math.Abs(integer.Value)),
            _ => new RuntimeFloat(Math.Abs(x.As<RuntimeFloat>().Value)),
        };

    [ElkFunction("ceil", Reachability.Everywhere)]
    public static RuntimeFloat Ceil(IRuntimeValue x)
        => new(Math.Ceiling(x.As<RuntimeFloat>().Value));

    [ElkFunction("floor", Reachability.Everywhere)]
    public static RuntimeFloat Floor(IRuntimeValue x)
        => new(Math.Floor(x.As<RuntimeFloat>().Value));

    [ElkFunction("sqrt", Reachability.Everywhere)]
    public static RuntimeFloat Sqrt(IRuntimeValue x)
        => x is RuntimeInteger integer
            ? new(Math.Sqrt(integer.Value))
            : new(Math.Sqrt(x.As<RuntimeFloat>().Value));

    [ElkFunction("max", Reachability.Everywhere)]
    public static IRuntimeValue Max(IRuntimeValue x, IRuntimeValue y)
    {
        if (x is RuntimeInteger xInt && y is RuntimeInteger yInt)
            return new RuntimeInteger(Math.Max(xInt.Value, yInt.Value));
        
        return new RuntimeFloat(Math.Max(x.As<RuntimeFloat>().Value, y.As<RuntimeFloat>().Value));
    }

    [ElkFunction("min", Reachability.Everywhere)]
    public static IRuntimeValue Min(IRuntimeValue x, IRuntimeValue y)
    {
        if (x is RuntimeInteger xInt && y is RuntimeInteger yInt)
            return new RuntimeInteger(Math.Min(xInt.Value, yInt.Value));
        
        return new RuntimeFloat(Math.Min(x.As<RuntimeFloat>().Value, y.As<RuntimeFloat>().Value));
    }
}