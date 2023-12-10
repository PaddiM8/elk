#region

using System;
using System.Collections.Generic;
using System.Linq;
using Elk.Interpreting.Exceptions;
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

    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns>The greatest common denominator.</returns>
    [ElkFunction("gcd")]
    public static RuntimeInteger Gcd(RuntimeInteger a, RuntimeInteger b)
    {
        var x = a.Value;
        var y = b.Value;
        while (y != 0)
        {
            var temp = y;
            y = x % y;
            x = temp;
        }
        return new(x);
    }

    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns>The lowest common multiple.</returns>
    [ElkFunction("lcm")]
    public static RuntimeInteger Lcm(RuntimeInteger a, RuntimeInteger b)
    {
        var x = a.Value;
        var y = b.Value;
        while (y != 0)
        {
            var temp = y;
            y = x % y;
            x = temp;
        }

        if (x == 0)
            throw new RuntimeException("Not defined");

        return new(a.Value * b.Value / x);
    }

    [ElkFunction("log")]
    public static RuntimeFloat Log(RuntimeFloat a, RuntimeFloat newBase)
        => new(Math.Log(a.Value, newBase.Value));

    [ElkFunction("log2")]
    public static RuntimeFloat Log2(RuntimeFloat value)
        => new(Math.Log2(value.Value));

    [ElkFunction("log10")]
    public static RuntimeFloat Log10(RuntimeFloat value)
        => new(Math.Log10(value.Value));

    /// <param name="x" types="Integer, Float"></param>
    /// <param name="y" types="Integer, Float"></param>
    /// <returns>The highest of the two input numbers.</returns>
    [ElkFunction("max", Reachability.Everywhere)]
    public static RuntimeObject Max(RuntimeObject x, RuntimeObject? y = null)
    {
        if (y == null)
        {
            if (x is not IEnumerable<RuntimeObject> items)
                throw new RuntimeCastException(x.GetType(), "Iterable");

            return items.Max() ?? RuntimeNil.Value;
        }

        return x.CompareTo(y) == 1
            ? x
            : y;
    }

    /// <param name="x" types="Integer, Float"></param>
    /// <param name="y" types="Integer, Float"></param>
    /// <returns>The lowest of the two input numbers.</returns>
    [ElkFunction("min", Reachability.Everywhere)]
    public static RuntimeObject Min(RuntimeObject x, RuntimeObject? y = null)
    {
        if (y == null)
        {
            if (x is not IEnumerable<RuntimeObject> items)
                throw new RuntimeCastException(x.GetType(), "Iterable");

            return items.Min() ?? RuntimeNil.Value;
        }

        return x.CompareTo(y) == -1
            ? x
            : y;
    }

    /// <returns>The lowest value in the Iterable with the closure applied.</returns>
    [ElkFunction("minOf", Reachability.Everywhere)]
    public static RuntimeObject MinOf(IEnumerable<RuntimeObject> items, Func<RuntimeObject, RuntimeObject> closure)
        => items.Min(closure) ?? RuntimeNil.Value;

    /// <param name="items">Items to sum</param>
    /// <returns>An Integer of Float of the sum of the given values.</returns>
    [ElkFunction("sum")]
    public static RuntimeObject Sum(IEnumerable<RuntimeObject> items)
        => items.FirstOrDefault() is RuntimeFloat
            ? new RuntimeFloat(items.Sum(x => x.As<RuntimeFloat>().Value))
            : new RuntimeInteger(items.Sum(x => x.As<RuntimeInteger>().Value));

    /// <param name="x" types="Integer, Float"></param>
    /// <returns>The square root of the input number</returns>
    [ElkFunction("sqrt", Reachability.Everywhere)]
    public static RuntimeFloat Sqrt(RuntimeObject x)
        => x is RuntimeInteger integer
            ? new(Math.Sqrt(integer.Value))
            : new(Math.Sqrt(x.As<RuntimeFloat>().Value));
}