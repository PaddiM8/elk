using System;
using Shel.Attributes;
using Shel.Interpreting;

namespace Shel.Std;

static class Maths
{
    [ShellFunction("abs")]
    public static IRuntimeValue Abs(IRuntimeValue x)
        => x switch
        {
            RuntimeInteger integer => new RuntimeInteger(Math.Abs(integer.Value)),
            _ => new RuntimeFloat(Math.Abs(x.As<RuntimeFloat>().Value)),
        };

    [ShellFunction("ceil")]
    public static RuntimeFloat Ceil(IRuntimeValue x)
        => new(Math.Ceiling(x.As<RuntimeFloat>().Value));

    [ShellFunction("floor")]
    public static RuntimeFloat Floor(IRuntimeValue x)
        => new(Math.Floor(x.As<RuntimeFloat>().Value));

    [ShellFunction("sqrt")]
    public static RuntimeFloat Sqrt(IRuntimeValue x)
        => x is RuntimeInteger integer
            ? new(Math.Sqrt(integer.Value))
            : new(Math.Sqrt(x.As<RuntimeFloat>().Value));

    [ShellFunction("max")]
    public static IRuntimeValue Max(IRuntimeValue x, IRuntimeValue y)
    {
        if (x is RuntimeInteger xInt && y is RuntimeInteger yInt)
            return new RuntimeInteger(Math.Max(xInt.Value, yInt.Value));
        
        return new RuntimeFloat(Math.Max(x.As<RuntimeFloat>().Value, y.As<RuntimeFloat>().Value));
    }

    [ShellFunction("min")]
    public static IRuntimeValue Min(IRuntimeValue x, IRuntimeValue y)
    {
        if (x is RuntimeInteger xInt && y is RuntimeInteger yInt)
            return new RuntimeInteger(Math.Min(xInt.Value, yInt.Value));
        
        return new RuntimeFloat(Math.Min(x.As<RuntimeFloat>().Value, y.As<RuntimeFloat>().Value));
    }
}